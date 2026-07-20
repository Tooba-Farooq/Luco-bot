import requests
import sounddevice as sd
from scipy.io.wavfile import write
import json
import base64
import tempfile
import os
import pygame

RESPOND_URL = "http://127.0.0.1:8000/session/respond"
AUDIO_URL = "http://127.0.0.1:8000/audio"
SAMPLE_RATE = 16000
RECORD_SECONDS = 4
OUTPUT_FILE = "test_respond.wav"

pygame.mixer.init()


def record_audio():
    print(f"Recording for {RECORD_SECONDS} seconds... speak now.")
    audio = sd.rec(int(RECORD_SECONDS * SAMPLE_RATE), samplerate=SAMPLE_RATE, channels=1, dtype='int16')
    sd.wait()
    write(OUTPUT_FILE, SAMPLE_RATE, audio)
    print("Recording saved.")


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


def play_response_audio(data: dict):
    if data.get("audio_base64"):
        play_audio_bytes(base64.b64decode(data["audio_base64"]))
    elif data.get("audio_key"):
        resp = requests.get(f"{AUDIO_URL}/{data['audio_key']}")
        if resp.status_code == 200:
            play_audio_bytes(resp.content)
        else:
            print(f"Failed to fetch static audio: {resp.status_code}")
    else:
        print("(no audio in this response)")


def send_respond(session_id: str):
    record_audio()
    with open(OUTPUT_FILE, "rb") as f:
        response = requests.post(
            RESPOND_URL,
            data={"session_id": session_id},
            files={"audio": ("audio.wav", f, "audio/wav")}
        )

    print(f"\n--- Status: {response.status_code} ---")
    if response.status_code != 200:
        print(response.text)
        return None

    data = response.json()
    print(json.dumps(data, indent=2, ensure_ascii=False))
    play_response_audio(data)
    return data


if __name__ == "__main__":
    session_id = input("Paste the session_id from your webcam script: ").strip()
    while True:
        input("\nPress Enter to record and respond (Ctrl+C to quit)...")
        send_respond(session_id)