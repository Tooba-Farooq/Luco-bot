import cv2
import mediapipe as mp
from mediapipe.tasks import python
from mediapipe.tasks.python import vision
from mediapipe.tasks.python import vision as mp_vision
import numpy as np
import math

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


def _load_image(image_bytes_or_path):
    """Shared helper: load image from either a file path or raw bytes."""
    if isinstance(image_bytes_or_path, str):
        return cv2.imread(image_bytes_or_path)
    nparr = np.frombuffer(image_bytes_or_path, np.uint8)
    return cv2.imdecode(nparr, cv2.IMREAD_COLOR)


def check_face_present(image_bytes_or_path):
    """
    Returns (face_found: bool, face_box: (x, y, width, height) or None)
    """
    image = _load_image(image_bytes_or_path)
    if image is None:
        return False, None

    image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
    mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=image_rgb)
    result = detector.detect(mp_image)

    if not result.detections:
        return False, None

    detection = result.detections[0]
    bbox = detection.bounding_box
    return True, (bbox.origin_x, bbox.origin_y, bbox.width, bbox.height)


def check_face_forward(image_bytes_or_path, face_box) -> bool:
    """
    Returns True only if:
    1. Face is close enough (size gate — distance proxy for genuine intent)
    2. Face is facing the camera (yaw gate, via Face Landmarker)
    """
    image = _load_image(image_bytes_or_path)
    if image is None:
        return False

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


def run_face_recognition(image_bytes_or_path):
    """
    NOT BUILT YET — placeholder until face recognition is integrated.
    Returns (None, None) so status resolves to "unknown" for now.
    """
    return None, None