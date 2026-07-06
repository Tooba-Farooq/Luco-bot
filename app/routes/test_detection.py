import cv2
import mediapipe as mp
from mediapipe.tasks import python
from mediapipe.tasks.python import vision

# set up the detector once, reused across calls
base_options = python.BaseOptions(model_asset_path='blaze_face_short_range.tflite')
options = vision.FaceDetectorOptions(base_options=base_options, min_detection_confidence=0.5)
detector = vision.FaceDetector.create_from_options(options)


def check_face_present(image_bytes_or_path):
    """
    Takes image bytes (or a file path for testing) and returns:
    - face_found: bool
    - face_box: (x, y, width, height) in pixels, or None
    """
    if isinstance(image_bytes_or_path, str):
        image = cv2.imread(image_bytes_or_path)
    else:
        import numpy as np
        nparr = np.frombuffer(image_bytes_or_path, np.uint8)
        image = cv2.imdecode(nparr, cv2.IMREAD_COLOR)

    if image is None:
        return False, None

    image_rgb = cv2.cvtColor(image, cv2.COLOR_BGR2RGB)
    mp_image = mp.Image(image_format=mp.ImageFormat.SRGB, data=image_rgb)

    result = detector.detect(mp_image)

    if not result.detections:
        return False, None

    detection = result.detections[0]
    bbox = detection.bounding_box  # already in pixel values, not relative!

    return True, (bbox.origin_x, bbox.origin_y, bbox.width, bbox.height)
    

def visualize_detection(image_path):
    found, box = check_face_present(image_path)
    image = cv2.imread(image_path)

    if found:
        x, y, w, h = box
        cv2.rectangle(image, (x, y), (x + w, y + h), (0, 255, 0), 2)

    cv2.imshow("Detection Result", image)
    cv2.waitKey(0)
    cv2.destroyAllWindows()

if __name__ == "__main__":
    # test on a few images
    test_images = [
        r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260706_12_23_22_Pro.jpg",
        r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260706_12_23_34_Pro.jpg",
        r"C:\Users\tooba\Pictures\Camera Roll\WIN_20260706_12_23_44_Pro.jpg",
    ]

    for img_path in test_images:
        found, box = check_face_present(img_path)
        visualize_detection(img_path)
        print(f"{img_path} -> Face found: {found}, Box: {box}")