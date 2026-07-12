import cv2

YUNET_MODEL_PATH = "models/face_detection_yunet_2023mar.onnx"

face_detector = cv2.FaceDetectorYN.create(
    model=YUNET_MODEL_PATH,
    config="",
    input_size=(320, 320),
    score_threshold=0.7,
    nms_threshold=0.3,
    top_k=5000
)


def check_face_present(image_path):
    image = cv2.imread(image_path)
    if image is None:
        return False, None

    h, w = image.shape[:2]
    face_detector.setInputSize((w, h))

    _, faces = face_detector.detect(image)
    if faces is None or len(faces) == 0:
        return False, None

    x, y, fw, fh = faces[0][:4]
    return True, (int(x), int(y), int(fw), int(fh))


def show_with_box(image_path, box):
    """Draws the detected box and displays it. Closes on any keypress."""
    image = cv2.imread(image_path)
    if image is None or box is None:
        return

    x, y, w, h = box
    cv2.rectangle(image, (x, y), (x + w, y + h), (0, 255, 0), 3)

    max_dim = 900
    h_img, w_img = image.shape[:2]
    scale = min(1.0, max_dim / max(h_img, w_img))
    if scale < 1.0:
        image = cv2.resize(image, (int(w_img * scale), int(h_img * scale)))

    cv2.imshow("Detection Result - press any key for next", image)
    cv2.waitKey(0)
    cv2.destroyAllWindows()


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

    for img_path in test_images:
        found, box = check_face_present(img_path)
        if not found:
            print(f"{img_path} -> No face found")
            continue

        print(f"{img_path} -> box={box}")
        show_with_box(img_path, box)