import requests
import sounddevice as sd
import numpy as np
from scipy.io.wavfile import write
import json
import base64
import tempfile
import os
import pygame
import time

RESPOND_URL = "http://127.0.0.1:8000/session/respond"
SELECT_HOST_URL = "http://127.0.0.1:8000/session/select-host"
AUDIO_URL = "http://127.0.0.1:8000/audio"

SAMPLE_RATE = 16000
SILENCE_THRESHOLD = 300      # tune based on your mic — lower = more sensitive
SILENCE_DURATION = 1.5       # seconds of quiet before we consider speech "done"
MAX_RECORD_SECONDS = 12      # hard safety cap
OUTPUT_FILE = "test_respond.wav"

pygame.mixer.init()


def record_until_silence():
    print("Listening... (speak now)")
    chunk_duration = 0.1
    chunk_samples = int(SAMPLE_RATE * chunk_duration)
    recorded = []
    silence_time = 0.0
    speech_started = False
    start_time = time.time()

    stream = sd.InputStream(samplerate=SAMPLE_RATE, channels=1, dtype='int16')
    stream.start()

    while True:
        chunk, _ = stream.read(chunk_samples)
        recorded.append(chunk)
        volume = np.abs(chunk).mean()

        if volume > SILENCE_THRESHOLD:
            speech_started = True
            silence_time = 0.0
        elif speech_started:
            silence_time += chunk_duration

        if speech_started and silence_time >= SILENCE_DURATION:
            break
        if time.time() - start_time >= MAX_RECORD_SECONDS:
            print("(max recording time hit)")
            break

    stream.stop()
    stream.close()

    audio = np.concatenate(recorded, axis=0)
    write(OUTPUT_FILE, SAMPLE_RATE, audio)
    print("Done listening.")


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


def send_respond(session_id: str):
    record_until_silence()
    with open(OUTPUT_FILE, "rb") as f:
        response = requests.post(
            RESPOND_URL,
            data={"session_id": session_id},
            files={"audio": ("audio.wav", f, "audio/wav")}
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


def send_select_host(session_id: str, employee_id: int):
    response = requests.post(SELECT_HOST_URL, json={"session_id": session_id, "employee_id": employee_id})
    if response.status_code != 200:
        print(f"Error {response.status_code}: {response.text}")
        return None
    data = response.json()
    print(f"\nRobo: {data.get('answer_text')}")
    play_response_audio(data)
    return data


def simulate_conversation(session_id: str):
    """Keeps calling /session/respond automatically, only pausing for a tap
    when host_candidates need a UI selection — mirrors real Unity behavior."""
    while True:
        data = send_respond(session_id)
        if data is None:
            break

        if data.get("host_candidates"):
            print("\nCandidates:")
            for c in data["host_candidates"]:
                print(f"  [{c['id']}] {c['name']} — {c.get('floor_room', 'N/A')}")
            choice = input("Tap employee ID to confirm: ").strip()
            send_select_host(session_id, int(choice))

        if data["state"] in ("FALLBACK", "QUERY_ANSWERED", "AWAITING_PURPOSE"):
            cont = input("\n[Enter] to continue talking, or 'q' to stop: ").strip()
            if cont.lower() == "q":
                break


if __name__ == "__main__":
    session_id = input("Paste the session_id from your webcam script: ").strip()
    simulate_conversation(session_id)