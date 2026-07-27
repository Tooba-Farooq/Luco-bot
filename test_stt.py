# from groq import Groq
# import os
# from dotenv import load_dotenv
# import sounddevice as sd
# from scipy.io.wavfile import write
# import time

# SAMPLE_RATE = 16000
# RECORD_SECONDS = 4
# OUTPUT_FILE = "test_recording.wav"
# MODEL = "whisper-large-v3-turbo"

# load_dotenv()
# GROQ_API_KEY = os.getenv("GROQ_API_KEY")
# if not GROQ_API_KEY:
#     raise RuntimeError("GROQ_API_KEY is not set. Add it to your environment before running test_stt.py.")

# client = Groq(api_key=GROQ_API_KEY)


# def record_audio():
#     print(f"Recording for {RECORD_SECONDS} seconds... speak now.")
#     audio = sd.rec(int(RECORD_SECONDS * SAMPLE_RATE), samplerate=SAMPLE_RATE, channels=1, dtype='int16')
#     sd.wait()
#     write(OUTPUT_FILE, SAMPLE_RATE, audio)
#     print("Recording saved.")


# def transcribe_best_of_two(audio_path: str) -> dict:
#     """
#     Runs transcription forced to English and forced to Urdu, picks whichever
#     Whisper was more confident about. Returns the winning text + which
#     language won + both raw results (useful for debugging/logging).
#     """
#     results = {}

#     for lang in ["en", "ur"]:
#         with open(audio_path, "rb") as f:
#             transcription = client.audio.transcriptions.create(
#                 file=f,
#                 model=MODEL,
#                 language=lang,
#                 response_format="verbose_json",
#                 temperature=0.0
#             )

#         segments = getattr(transcription, "segments", None) or []
#         if segments:
#             avg_logprob = sum(s["avg_logprob"] for s in segments) / len(segments)
#             no_speech_prob = sum(s["no_speech_prob"] for s in segments) / len(segments)
#         else:
#             avg_logprob = -999
#             no_speech_prob = 1.0

#         results[lang] = {
#             "text": transcription.text.strip(),
#             "avg_logprob": avg_logprob,
#             "no_speech_prob": no_speech_prob,
#             "score": avg_logprob - no_speech_prob  # higher = more confident
#         }

#     best_lang = max(results, key=lambda l: results[l]["score"])

#     return {
#         "text": results[best_lang]["text"],
#         "detected_lang": best_lang,
#         "en_result": results["en"],
#         "ur_result": results["ur"]
#     }


# if __name__ == "__main__":
#     while True:
#         input("\nPress Enter to record (Ctrl+C to quit)...")
#         record_audio()

#         result = transcribe_best_of_two(OUTPUT_FILE)

#         print(f"\n[Best of Two] \"{result['text']}\" (detected: {result['detected_lang']})")
#         print(f"[English-forced] \"{result['en_result']['text']}\"  ({result['en_result']['score']:.2f})")
#         print(f"[Urdu-forced]    \"{result['ur_result']['text']}\"  ({result['ur_result']['score']:.2f})")

from groq import Groq
import os
import numpy as np
from dotenv import load_dotenv
import sounddevice as sd
from scipy.io.wavfile import write

SAMPLE_RATE = 16000
OUTPUT_FILE = "test_recording.wav"
MODEL = "whisper-large-v3-turbo"

load_dotenv()
GROQ_API_KEY = os.getenv("GROQ_API_KEY")
if not GROQ_API_KEY:
    raise RuntimeError("GROQ_API_KEY is not set. Add it to your environment before running this script.")

client = Groq(api_key=GROQ_API_KEY)


def record_audio():
    """Records from the mic until the user presses Enter, however long that takes."""
    print("Recording... press Enter to stop.")

    frames = []

    def callback(indata, frame_count, time_info, status):
        if status:
            print(status)
        frames.append(indata.copy())

    stream = sd.InputStream(
        samplerate=SAMPLE_RATE, channels=1, dtype='int16', callback=callback
    )
    with stream:
        # Blocks here until Enter is pressed; callback keeps appending frames meanwhile.
        input()

    if not frames:
        print("No audio captured.")
        return False

    audio = np.concatenate(frames, axis=0)
    write(OUTPUT_FILE, SAMPLE_RATE, audio)
    duration = len(audio) / SAMPLE_RATE
    print(f"Recording saved ({duration:.1f}s).")
    return True


def transcribe_forced(audio_path: str, lang: str) -> dict:
    """Force a single language and return text + confidence score."""
    with open(audio_path, "rb") as f:
        transcription = client.audio.transcriptions.create(
            file=f,
            model=MODEL,
            language=lang,
            response_format="verbose_json",
            temperature=0.0
        )

    segments = getattr(transcription, "segments", None) or []
    if segments:
        avg_logprob = sum(s["avg_logprob"] for s in segments) / len(segments)
        no_speech_prob = sum(s["no_speech_prob"] for s in segments) / len(segments)
    else:
        avg_logprob = -999
        no_speech_prob = 1.0

    return {
        "text": transcription.text.strip(),
        "avg_logprob": avg_logprob,
        "no_speech_prob": no_speech_prob,
        "score": avg_logprob - no_speech_prob
    }


def transcribe_auto(audio_path: str) -> dict:
    """No language param at all — let Whisper auto-detect. Useful baseline."""
    with open(audio_path, "rb") as f:
        transcription = client.audio.transcriptions.create(
            file=f,
            model=MODEL,
            response_format="verbose_json",
            temperature=0.0
        )
    return {
        "text": transcription.text.strip(),
        "detected_language": getattr(transcription, "language", "unknown")
    }


if __name__ == "__main__":
    print("STT debug tool — compares forced-English vs forced-Urdu vs auto-detect")
    print("for the SAME recording, so you can see exactly what forcing a language does.\n")

    while True:
        is_name_input = input(
            "\nIs this recording simulating the NAME field? (y/n, or 'q' to quit): "
        ).strip().lower()
        if is_name_input == "q":
            break
        is_name = is_name_input == "y"

        input("Press Enter to START recording...")
        if not record_audio():
            continue

        en_result = transcribe_forced(OUTPUT_FILE, "en")
        ur_result = transcribe_forced(OUTPUT_FILE, "ur")
        auto_result = transcribe_auto(OUTPUT_FILE)

        print("\n--- Results for the same audio ---")
        print(f"[Forced English] \"{en_result['text']}\"  (score={en_result['score']:.2f})")
        print(f"[Forced Urdu]    \"{ur_result['text']}\"  (score={ur_result['score']:.2f})")
        print(f"[Auto-detect]    \"{auto_result['text']}\"  (detected_language={auto_result['detected_language']})")

        if is_name:
            print("\n--- Name-field diagnosis ---")
            if en_result["score"] < ur_result["score"] - 1.0:
                print(
                    "Forced-English confidence is notably LOWER than forced-Urdu.\n"
                    "This suggests the production code's `force_language='en'` is making Whisper\n"
                    "hallucinate/guess at English words for what was actually Urdu speech —\n"
                    "likely the cause of garbled name transcriptions like "
                    "\"You're going to go to the next one.\""
                )
            elif en_result["text"].strip() == "":
                print("Forced-English came back empty — same failure mode, different symptom.")
            else:
                print(
                    "Forced-English confidence looks comparable or better here.\n"
                    "If this particular take came out fine but others don't, the issue may be\n"
                    "intermittent (mic pickup, background noise, or specific names/sounds\n"
                    "that don't map cleanly to English phonetics) rather than the language force itself."
                )