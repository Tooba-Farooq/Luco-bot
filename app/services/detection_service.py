import cv2
import mediapipe as mp
from mediapipe.tasks import python
from mediapipe.tasks.python import vision
from mediapipe.tasks.python import vision as mp_vision
import numpy as np
import math
from deepface import DeepFace
from sqlalchemy.orm import Session
from app.models_db import Employee, Visitor


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

# --- Recognition config (validated: ArcFace + opencv, see benchmark results) ---
FACE_RECOGNITION_MODEL = "ArcFace"
FACE_DETECTOR_BACKEND = "opencv"
RECOGNITION_THRESHOLD = 0.68  # from your own testing — true matches sat well under this


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


def _cosine_distance(emb1, emb2):
    a, b = np.array(emb1), np.array(emb2)
    return 1 - (np.dot(a, b) / (np.linalg.norm(a) * np.linalg.norm(b)))


def run_face_recognition(image, db: Session):
    """
    image: decoded numpy array (BGR), already confirmed forward-facing by the poll loop.
    Generates one embedding for the incoming frame (ArcFace + opencv), then compares
    it against every stored embedding (employees + known visitors) via cosine distance.

    Returns (name: str | None, confidence: float | None).
    Returns (None, None) if:
      - opencv couldn't detect/crop a face in this frame (e.g. niqab, mask, bad angle
        at the exact recognition instant) — treated as unknown, not an error
      - no stored embedding is within threshold
    """
    try:
        result = DeepFace.represent(
            img_path=image,
            model_name=FACE_RECOGNITION_MODEL,
            detector_backend=FACE_DETECTOR_BACKEND,
            enforce_detection=True
        )
        incoming_embedding = result[0]["embedding"]
    except ValueError:
        # opencv couldn't find a face this pass — graceful fallback, not a crash
        return None, None
    except Exception as e:
        print(f"Recognition embedding failed unexpectedly: {e}")
        return None, None

    best_match_name = None
    best_dist = float("inf")

    known_people = (
        db.query(Employee).filter(Employee.face_embedding.isnot(None)).all()
        + db.query(Visitor).filter(Visitor.face_embedding.isnot(None)).all()
    )

    for person in known_people:
        dist = _cosine_distance(incoming_embedding, person.face_embedding)
        if dist < best_dist:
            best_dist = dist
            best_match_name = person.name

    if best_match_name and best_dist < RECOGNITION_THRESHOLD:
        confidence = 1 - best_dist
        return best_match_name, confidence

    return None, None