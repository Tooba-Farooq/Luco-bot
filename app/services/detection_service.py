import cv2
import mediapipe as mp
from mediapipe.tasks import python
from mediapipe.tasks.python import vision
from mediapipe.tasks.python import vision as mp_vision
import numpy as np
import math
from sqlalchemy.orm import Session
from app.models_db import Employee, Visitor
from app.services.embedding_service import get_embedding_from_array


# --- Face presence detector (cheap, runs every frame) ---
base_options = python.BaseOptions(model_asset_path='blaze_face_short_range.tflite')
options = vision.FaceDetectorOptions(base_options=base_options, min_detection_confidence=0.5)
detector = vision.FaceDetector.create_from_options(options)

# --- Face landmarker (heavier, only runs once a face is found) ---
landmarker_base_options = python.BaseOptions(model_asset_path='face_landmarker.task')
landmarker_options = mp_vision.FaceLandmarkerOptions(
    base_options=landmarker_base_options,
    output_facial_transformation_matrixes=True,
    num_faces=1
)
face_landmarker = mp_vision.FaceLandmarker.create_from_options(landmarker_options)

# --- Recognition config (validated: InsightFace buffalo_l, see benchmark +
# calculate_distance.py results — clean 0.35 margin between lowest
# genuine similarity (0.690) and highest impostor similarity (0.340) on
# internal test set; 0.515 midpoint chosen for zero-false-accept priority) ---
RECOGNITION_THRESHOLD = 0.515  # NOTE: similarity now, not distance — higher = more
                               # similar. Comparison direction is opposite of the
                               # old DeepFace/ArcFace distance-based threshold.

# MIN_MARGIN / ambiguous-match rejection intentionally removed (was here in the
# DeepFace setup). Deliberate tradeoff: a known visitor that once got misclassified
# as unknown (duplicate Visitor row) would otherwise stay stuck failing the margin
# check against their own duplicate forever. RECOGNITION_THRESHOLD (0.515, validated
# with a clean 0.35 similarity gap between genuine/impostor pairs) is relied on as
# the sole gate now. Revisit if real-world false accepts between distinct people
# turn out to be non-negligible — see calculate_distance.py for how this
# was calibrated.


def _load_image(image_bytes_or_path):
    """Shared helper: load image from either a file path or raw bytes."""
    if isinstance(image_bytes_or_path, str):
        return cv2.imread(image_bytes_or_path)
    nparr = np.frombuffer(image_bytes_or_path, np.uint8)
    return cv2.imdecode(nparr, cv2.IMREAD_COLOR)


def check_face_present(image):
    """
    Returns (face_found: bool, face_box: (x, y, width, height) or None)
    """

    image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
    mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=image_rgb)
    result = detector.detect(mp_image)

    if not result.detections:
        return False, None

    detection = result.detections[0]
    bbox = detection.bounding_box
    return True, (bbox.origin_x, bbox.origin_y, bbox.width, bbox.height)


def check_face_forward(image, face_box, debug: bool = False) -> bool:
    """
    Returns True only if:
    1. Face is close enough (size gate — distance proxy for genuine intent)
    2. Face is facing the camera (yaw gate, via Face Landmarker)
    """

    h, w, _ = image.shape

    # --- GATE 1: face size (distance proxy) ---
    box_x, box_y, box_w, box_h = face_box
    face_area_ratio = (box_w * box_h) / (w * h)
    MIN_FACE_AREA_RATIO = 0.03  # placeholder — calibrate once tablet hardware is ready

    if face_area_ratio < MIN_FACE_AREA_RATIO:
        return False  # too far away — doesn't count as intent

    # --- GATE 2: yaw angle via Face Landmarker ---
    image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
    mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=image_rgb)
    result = face_landmarker.detect(mp_image)

    if not result.facial_transformation_matrixes:
        return False

    matrix = result.facial_transformation_matrixes[0]
    yaw_rad = math.atan2(-matrix[0][2], matrix[2][2])
    yaw_deg = math.degrees(yaw_rad)

    MAX_YAW_DEGREES = 25
    return abs(yaw_deg) < MAX_YAW_DEGREES


def _cosine_similarity(emb1, emb2):
    """InsightFace's native comparison metric — HIGHER means more similar,
    opposite direction from the old _cosine_distance (lower = more similar)."""
    a, b = np.array(emb1), np.array(emb2)
    return float(np.dot(a, b) / (np.linalg.norm(a) * np.linalg.norm(b)))


def _is_valid_embedding(embedding) -> bool:
    return (
        embedding is not None
        and isinstance(embedding, (list, tuple))
        and len(embedding) > 0
        and all(value is not None for value in embedding)
    )


def run_face_recognition(image, db: Session):
    """
    image: decoded numpy array (BGR), already confirmed forward-facing by the poll loop.
    Generates one embedding for the incoming frame (InsightFace buffalo_l), then
    compares it against every stored embedding (known visitors) via cosine similarity.

    Accepts a match if the best similarity clears RECOGNITION_THRESHOLD.
    (No ambiguous-margin check — see note above RECOGNITION_THRESHOLD for why.)

    Returns (visitor_id: int | None, name: str | None, confidence: float | None).
    Returns (None, None, None) if:
      - no face could be detected/embedded in this frame (e.g. niqab, mask, bad angle
        at the exact recognition instant) — treated as unknown, not an error
      - no stored embedding is within threshold
    """
    incoming_embedding = get_embedding_from_array(image)
    if incoming_embedding is None:
        print("No face detected by InsightFace this frame")
        return None, None, None

    known_people = [
        person
        for person in (
            db.query(Visitor).filter(Visitor.face_embedding.isnot(None)).all()
        )
        if _is_valid_embedding(person.face_embedding)
    ]

    best_person, best_sim = None, float("-inf")

    print(f"--- comparing against {len(known_people)} known visitor(s) ---")
    for person in known_people:
        sim = _cosine_similarity(incoming_embedding, person.face_embedding)
        print(f"  visitor_id={person.id} name={person.name!r} sim={sim:.4f}")
        if sim > best_sim:
            best_sim = sim
            best_person = person

    print(f"best={best_person.name if best_person else 'N/A'}, best_sim={best_sim if best_person else 'N/A'}")
    if best_person is None or best_sim < RECOGNITION_THRESHOLD:
        print("Rejected: no match above threshold")
        return None, None, None

    return best_person.id, best_person.name, best_sim


def check_face_centered(image, face_box, x_tolerance: float = 0.15, y_tolerance: float = 0.30) -> bool:
    """
    Returns True if the face's center is within tolerance (as a fraction
    of image width/height) of the image's center.
    Separate x/y tolerances: y is looser by default since occluded faces
    (e.g. niqab) can shift the detected box's vertical center even when
    the visitor is genuinely centered and looking forward.
    """
    h, w, _ = image.shape
    box_x, box_y, box_w, box_h = face_box
    face_center_x = box_x + box_w / 2
    face_center_y = box_y + box_h / 2

    x_offset = abs(face_center_x - w / 2) / w
    y_offset = abs(face_center_y - h / 2) / h

    return x_offset < x_tolerance and y_offset < y_tolerance