import cv2
import mediapipe as mp
from mediapipe.tasks import python
from mediapipe.tasks.python import vision as mp_vision
import numpy as np
import math

# --- MediaPipe setup ---
base_options = python.BaseOptions(model_asset_path='blaze_face_short_range.tflite')
detector_options = mp_vision.FaceDetectorOptions(base_options=base_options, min_detection_confidence=0.5)
mp_detector = mp_vision.FaceDetector.create_from_options(detector_options)

landmarker_base_options = python.BaseOptions(model_asset_path='face_landmarker.task')
landmarker_options = mp_vision.FaceLandmarkerOptions(
    base_options=landmarker_base_options,
    output_facial_transformation_matrixes=True,
    num_faces=1
)
face_landmarker = mp_vision.FaceLandmarker.create_from_options(landmarker_options)

# --- opencv setup ---
face_cascade = cv2.CascadeClassifier(cv2.data.haarcascades + "haarcascade_frontalface_default.xml")

MAX_YAW_DEGREES = 25
MIN_FACE_AREA_RATIO = 0.03


def mp_check_face_present(image):
    image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
    mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=image_rgb)
    result = mp_detector.detect(mp_image)
    if not result.detections:
        return False, None
    bbox = result.detections[0].bounding_box
    return True, (bbox.origin_x, bbox.origin_y, bbox.width, bbox.height)


def mp_check_face_forward(image, face_box):
    h, w, _ = image.shape
    box_x, box_y, box_w, box_h = face_box
    face_area_ratio = (box_w * box_h) / (w * h)
    if face_area_ratio < MIN_FACE_AREA_RATIO:
        return False

    image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
    mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=image_rgb)
    result = face_landmarker.detect(mp_image)
    if not result.facial_transformation_matrixes:
        return False

    matrix = result.facial_transformation_matrixes[0]
    yaw_rad = math.atan2(-matrix[0][2], matrix[2][2])
    yaw_deg = math.degrees(yaw_rad)
    return abs(yaw_deg) < MAX_YAW_DEGREES


def opencv_check_face_present(image):
    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    faces = face_cascade.detectMultiScale(gray, scaleFactor=1.1, minNeighbors=5, minSize=(60, 60))
    if len(faces) == 0:
        return False, None
    x, y, w, h = faces[0]
    return True, (int(x), int(y), int(w), int(h))


if __name__ == "__main__":
    test_images = [
        r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260706_12_23_22_Pro.jpg",
        r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260706_12_23_34_Pro.jpg",
        r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260706_12_23_44_Pro.jpg",
        r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260706_13_02_09_Pro.jpg",
        r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260706_13_05_17_Pro.jpg",
        r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260706_13_05_26_Pro.jpg",
        r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260707_10_06_12_Pro.jpg",
        r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260707_10_06_18_Pro.jpg",
        r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260707_10_06_24_Pro.jpg",
        r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260707_10_06_28_Pro.jpg",
        r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260707_10_07_25_Pro.jpg",
        r"C:\Users\tooba\Downloads\WhatsApp Image 2026-07-08 at 3.13.51 PM.jpeg",
    ]

    mp_passed = []

    print("=== STAGE 1: MediaPipe forward-facing filter ===")
    for img_path in test_images:
        image = cv2.imread(img_path)
        if image is None:
            print(f"{img_path} -> failed to load")
            continue

        found, box = mp_check_face_present(image)
        if not found:
            print(f"{img_path} -> MediaPipe: no face")
            continue

        is_forward = mp_check_face_forward(image, box)
        print(f"{img_path} -> MediaPipe: face found, forward={is_forward}")
        if is_forward:
            mp_passed.append(img_path)

    print(f"\n{len(mp_passed)}/{len(test_images)} images passed MediaPipe's forward-facing gate.\n")

    print("=== STAGE 2: opencv haarcascade on the SAME MediaPipe-passed images ===")
    opencv_hits = 0
    for img_path in mp_passed:
        image = cv2.imread(img_path)
        found, box = opencv_check_face_present(image)
        status = "FOUND" if found else "MISSED"
        print(f"{img_path} -> opencv: {status}")
        if found:
            opencv_hits += 1

    if mp_passed:
        rate = opencv_hits / len(mp_passed) * 100
        print(f"\nopencv hit rate on MediaPipe-passed images: {opencv_hits}/{len(mp_passed)} ({rate:.1f}%)")
    else:
        print("\nNo images passed MediaPipe's gate — can't test opencv's hit rate on 'good' frames.")