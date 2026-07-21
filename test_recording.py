import base64
import os
import tempfile
import time

import cv2
import numpy as np
import pygame
import requests
import sounddevice as sd
from scipy.io.wavfile import write

BASE_URL = "http://127.0.0.1:8000"
RESPOND_URL = f"{BASE_URL}/session/respond"
CONFIRM_HOST_URL = f"{BASE_URL}/session/confirm-host"
SUBMIT_NAME_URL = f"{BASE_URL}/session/submit-name"
AUDIO_URL = f"{BASE_URL}/audio"

SAMPLE_RATE = 16000
SILENCE_THRESHOLD = 300
SILENCE_DURATION = 1.5
MAX_RECORD_SECONDS = 12
OUTPUT_FILE = "test_respond.wav"
PHOTO_OUTPUT_FILE = "test_visitor_photo.jpg"


def init_audio_player():
    try:
        pygame.mixer.init()
        return True
    except Exception as exc:
        print(f"Audio playback disabled: {exc}")
        return False


def play_audio_bytes(audio_bytes: bytes):
    with tempfile.NamedTemporaryFile(suffix=".mp3", delete=False) as tmp_file:
        tmp_file.write(audio_bytes)
        temp_path = tmp_file.name
    try:
        pygame.mixer.music.load(temp_path)
        pygame.mixer.music.play()
        while pygame.mixer.music.get_busy():
            pygame.time.Clock().tick(10)
        pygame.mixer.music.unload()
    finally:
        os.remove(temp_path)


def play_response_audio(data: dict):
    if data.get("audio_base64"):
        play_audio_bytes(base64.b64decode(data["audio_base64"]))
        return

    if data.get("audio_key"):
        response = requests.get(f"{AUDIO_URL}/{data['audio_key']}")
        if response.status_code == 200:
            play_audio_bytes(response.content)
        else:
            print(f"Failed to fetch static audio: {response.status_code} {response.text}")


def record_until_silence():
    print("Listening... (speak now)")
    chunk_duration = 0.1
    chunk_samples = int(SAMPLE_RATE * chunk_duration)
    recorded = []
    silence_time = 0.0
    speech_started = False
    start_time = time.time()

    

    stream = sd.InputStream(samplerate=SAMPLE_RATE, channels=1, dtype="int16")
    stream.start()

    try:
        while True:
            chunk, _ = stream.read(chunk_samples)
            recorded.append(chunk)
            volume = np.abs(chunk).mean()

            if volume > SILENCE_THRESHOLD:
                speech_started = True
                silence_time = 0.0
                print(f"  [speech] volume={volume:.0f}")
            elif speech_started:
                silence_time += chunk_duration
                print(f"  [quiet]  volume={volume:.0f}  silence_time={silence_time:.1f}s")

            if speech_started and silence_time >= SILENCE_DURATION:
                break
            if time.time() - start_time >= MAX_RECORD_SECONDS:
                print("(max recording time hit)")
                break
    finally:
        stream.stop()
        stream.close()

    audio = np.concatenate(recorded, axis=0)
    write(OUTPUT_FILE, SAMPLE_RATE, audio)
    print("Done listening.")


def send_respond(session_id: str):
    record_until_silence()
    with open(OUTPUT_FILE, "rb") as audio_file:
        response = requests.post(
            RESPOND_URL,
            data={"session_id": session_id},
            files={"audio": ("audio.wav", audio_file, "audio/wav")},
            timeout=120,
        )

    if response.status_code != 200:
        print(f"Error {response.status_code}: {response.text}")
        return None

    data = response.json()
    print(f"\nHeard: \"{data['heard_text']}\"  |  State: {data['state']}")
    if data.get("answer_text"):
        print(f"Robo: {data['answer_text']}")
    play_response_audio(data)
    return data


def send_confirm_host(session_id: str, employee_id: int):
    response = requests.post(
        CONFIRM_HOST_URL,
        json={"session_id": session_id, "employee_id": employee_id},
        timeout=30,
    )
    if response.status_code != 200:
        print(f"Error {response.status_code}: {response.text}")
        return None

    data = response.json()
    print(f"\nRobo: {data.get('answer_text')}")
    play_response_audio(data)
    return data


def send_submit_name(session_id: str, name: str):
    response = requests.post(
        SUBMIT_NAME_URL,
        json={"session_id": session_id, "name": name},
        timeout=30,
    )
    if response.status_code != 200:
        print(f"Error {response.status_code}: {response.text}")
        return None

    data = response.json()
    print(f"\nRobo: {data.get('answer_text')}")
    play_response_audio(data)
    return data


def handle_host_selection(data: dict, session_id: str):
    print("\nCandidates:")
    for candidate in data.get("host_candidates", []):
        print(f"  [{candidate['id']}] {candidate['name']} — {candidate.get('floor_room', 'N/A')}")

    while True:
        choice = input("Enter employee ID to confirm, or 'q' to stop: ").strip()
        if choice.lower() == "q":
            return None
        if not choice.isdigit():
            print("Please enter a numeric employee ID.")
            continue

        return send_confirm_host(session_id, int(choice))


def handle_name_confirmation(data: dict, session_id: str):
    heard_name = data.get("heard_text", "").strip()
    choice = input(f"Backend heard name [{heard_name}]. Press Enter to accept or type a correction: ").strip()
    name = choice if choice else heard_name
    return send_submit_name(session_id, name)


def capture_photo_to_file() -> str | None:
    cap = cv2.VideoCapture(0)
    if not cap.isOpened():
        print("Could not open webcam for photo capture.")
        return None

    print("Photo capture: press 'c' to capture, or 'q' to skip.")
    try:
        while True:
            ret, frame = cap.read()
            if not ret:
                print("Failed to grab webcam frame.")
                return None

            cv2.imshow("Photo Capture - c to capture, q to skip", frame)
            key = cv2.waitKey(1) & 0xFF
            if key == ord("c"):
                cv2.imwrite(PHOTO_OUTPUT_FILE, frame)
                print(f"Saved photo to {PHOTO_OUTPUT_FILE}")
                return PHOTO_OUTPUT_FILE
            if key == ord("q"):
                print("Skipped photo capture.")
                return None
    finally:
        cap.release()
        cv2.destroyAllWindows()


def simulate_conversation(session_id: str):
    """Mirror the Unity flow without /detect: keep speaking, play responses, and handle host selection."""
    while True:
        data = send_respond(session_id)
        if data is None:
            break

        state = data.get("state")

        if data.get("host_candidates"):
            data = handle_host_selection(data, session_id)
            if data is None:
                break
            state = data.get("state")

        if state == "AWAITING_PHOTO":
            capture_photo_to_file()
            print("Reached AWAITING_PHOTO. Backend photo submission is not implemented yet, so the simulation stops here.")
            break

        if state == "NAME_CONFIRMATION":
            data = handle_name_confirmation(data, session_id)
            if data is None:
                break
            state = data.get("state")

        if state == "READY_FOR_HANDOFF":
            print("Flow completed at READY_FOR_HANDOFF.")
            break

        if state in ("FALLBACK", "QUERY_ANSWERED"):
            cont = input("\n[Enter] to continue talking, or 'q' to stop: ").strip()
            if cont.lower() == "q":
                break


def main():
    init_audio_player()
    session_id = input("Paste the session_id from your detect run: ").strip()
    if not session_id:
        print("Session ID is required.")
        return

    print("Starting conversation simulation.")
    simulate_conversation(session_id)


if __name__ == "__main__":
    main()