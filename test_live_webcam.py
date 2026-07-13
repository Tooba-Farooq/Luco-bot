import cv2
import requests
import time
import base64
import tempfile
import os
from playsound import playsound

API_URL = "http://127.0.0.1:8000/detect"
AUDIO_URL = "http://127.0.0.1:8000/audio"
SEND_INTERVAL = 0.7  # seconds between frames, matches your polling guidance

cap = cv2.VideoCapture(0)  # 0 = default webcam

if not cap.isOpened():
    print("Could not open webcam")
    exit()

print("Starting live test. Press 'q' in the video window to quit.")

last_sent_time = 0
status_text = "waiting..."
last_played_session_id = None  # guards against replaying the greeting every poll


def play_audio_bytes(audio_bytes: bytes):
    """Write mp3 bytes to a temp file and play it (blocking)."""
    with tempfile.NamedTemporaryFile(suffix=".mp3", delete=False) as f:
        f.write(audio_bytes)
        temp_path = f.name
    try:
        playsound(temp_path)
    finally:
        os.remove(temp_path)


def handle_audio_response(data: dict):
    global last_played_session_id

    session_id = data.get("session_id")
    if not session_id or session_id == last_played_session_id:
        return  # already played this session's greeting, or no session yet

    if data.get("audio_base64"):
        audio_bytes = base64.b64decode(data["audio_base64"])
        play_audio_bytes(audio_bytes)
        last_played_session_id = session_id

    elif data.get("audio_key"):
        resp = requests.get(f"{AUDIO_URL}/{data['audio_key']}")
        if resp.status_code == 200:
            play_audio_bytes(resp.content)
            last_played_session_id = session_id
        else:
            print(f"Failed to fetch static audio: {resp.status_code}")


while True:
    ret, frame = cap.read()
    if not ret:
        print("Failed to grab frame")
        break

    now = time.time()

    if now - last_sent_time >= SEND_INTERVAL:
        last_sent_time = now

        success, encoded_image = cv2.imencode('.jpg', frame)
        if success:
            image_bytes = encoded_image.tobytes()

            try:
                response = requests.post(
                    API_URL,
                    files={"frame": ("frame.jpg", image_bytes, "image/jpeg")}
                )
                if response.status_code != 200:
                    status_text = f"Backend error {response.status_code}: {response.text[:200]}"
                    print(status_text)
                else:
                    data = response.json()
                    status_text = (
                        f"status={data['status']} "
                        f"forward={data['face_forward']} "
                        f"duration={data['forward_duration']:.1f}s"
                    )
                    if data.get("visitor_name"):
                        status_text += f" name={data['visitor_name']} conf={data.get('confidence', 0):.2f}"
                    print(status_text)

                    handle_audio_response(data)

            except Exception as e:
                status_text = f"Error: {e}"
                print(status_text)

    cv2.putText(frame, status_text, (10, 30), cv2.FONT_HERSHEY_SIMPLEX,
                0.7, (0, 255, 0), 2)
    cv2.imshow("Live Detection Test", frame)

    if cv2.waitKey(1) & 0xFF == ord('q'):
        break

cap.release()
cv2.destroyAllWindows()