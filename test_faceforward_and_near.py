import cv2

PROTOTXT_PATH = "models/deploy.prototxt"
MODEL_PATH = "models/res10_300x300_ssd_iter_140000.caffemodel"
CONFIDENCE_THRESHOLD = 0.5

net = cv2.dnn.readNetFromCaffe(PROTOTXT_PATH, MODEL_PATH)


def check_face_present(image_path):
    image = cv2.imread(image_path)
    if image is None:
        return False, None

    h, w = image.shape[:2]
    blob = cv2.dnn.blobFromImage(
        cv2.resize(image, (300, 300)), 1.0, (300, 300), (104.0, 117.0, 123.0)
    )
    net.setInput(blob)
    detections = net.forward()

    best_conf = 0
    best_box = None
    for i in range(detections.shape[2]):
        confidence = detections[0, 0, i, 2]
        if confidence > CONFIDENCE_THRESHOLD and confidence > best_conf:
            box = detections[0, 0, i, 3:7] * [w, h, w, h]
            x, y, x1, y1 = box.astype("int")
            best_conf = confidence
            best_box = (int(x), int(y), int(x1 - x), int(y1 - y))

    if best_box is None:
        return False, None
    return True, best_box


def show_with_box(image_path, box):
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