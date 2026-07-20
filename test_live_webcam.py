import cv2
import requests
import time
import base64
import tempfile
import os
import pygame

API_URL = "http://127.0.0.1:8000/detect"
AUDIO_URL = "http://127.0.0.1:8000/audio"
SEND_INTERVAL = 0.7

pygame.mixer.init()

cap = cv2.VideoCapture(0)

if not cap.isOpened():
    print("Could not open webcam")
    exit()

print("Starting live test. Press 'q' in the video window to quit.")

last_sent_time = 0
status_text = "waiting..."
active_session_id = None  # once set, stop polling /detect


def play_audio_bytes(audio_bytes: bytes):
    with tempfile.NamedTemporaryFile(suffix=".mp3", delete=False) as f:
        f.write(audio_bytes)
        temp_path = f.name
    try:
        pygame.mixer.music.load(temp_path)
        pygame.mixer.music.play()
        while pygame.mixer.music.get_busy():
            pygame.time.Clock().tick(10)
        pygame.mixer.music.unload()
    finally:
        os.remove(temp_path)


def handle_audio_response(data: dict):
    if data.get("audio_base64"):
        audio_bytes = base64.b64decode(data["audio_base64"])
        play_audio_bytes(audio_bytes)
    elif data.get("audio_key"):
        resp = requests.get(f"{AUDIO_URL}/{data['audio_key']}")
        if resp.status_code == 200:
            play_audio_bytes(resp.content)
        else:
            print(f"Failed to fetch static audio: {resp.status_code}")


while True:
    ret, frame = cap.read()
    if not ret:
        print("Failed to grab frame")
        break

    now = time.time()

    # STOP condition — once we have a session, don't call /detect anymore
    if active_session_id is None and now - last_sent_time >= SEND_INTERVAL:
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
                    if data.get("session_id"):
                        status_text += f" session={data['session_id'][:8]}"
                    if data.get("visitor_name"):
                        status_text += f" name={data['visitor_name']} conf={data.get('confidence', 0):.2f}"
                    print(status_text)

                    if data["status"] in ("known", "unknown"):
                        active_session_id = data["session_id"]
                        handle_audio_response(data)
                        print(f"\n>>> Session started: {active_session_id}")
                        print(">>> Polling stopped. Use this session_id with your /session/respond test script.\n")

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