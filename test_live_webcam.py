import cv2
import requests
import time

API_URL = "http://127.0.0.1:8000/detect"
SEND_INTERVAL = 0.7  # seconds between frames, matches your polling guidance

cap = cv2.VideoCapture(0)  # 0 = default webcam

if not cap.isOpened():
    print("Could not open webcam")
    exit()

print("Starting live test. Press 'q' in the video window to quit.")

last_sent_time = 0
status_text = "waiting..." 

while True:
    ret, frame = cap.read()
    if not ret:
        print("Failed to grab frame")
        break

    now = time.time()

    if now - last_sent_time >= SEND_INTERVAL:
        last_sent_time = now

        # encode frame as JPEG in memory (no need to save to disk)
        success, encoded_image = cv2.imencode('.jpg', frame)
        if success:
            image_bytes = encoded_image.tobytes()

            try:
                response = requests.post(
                    API_URL,
                    files={"frame": ("frame.jpg", image_bytes, "image/jpeg")}
                )
                data = response.json()
                status_text = (
                    f"status={data['status']} "
                    f"forward={data['face_forward']} "
                    f"duration={data['forward_duration']:.1f}s"
                )
                print(status_text)
            except Exception as e:
                status_text = f"Error: {e}"
                print(status_text)

    # overlay the latest status text on the video feed itself
    cv2.putText(frame, status_text, (10, 30), cv2.FONT_HERSHEY_SIMPLEX,
                0.7, (0, 255, 0), 2)
    cv2.imshow("Live Detection Test", frame)

    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

cap.release()
cv2.destroyAllWindows()