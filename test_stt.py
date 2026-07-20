from groq import Groq
import sounddevice as sd
from scipy.io.wavfile import write
import time

SAMPLE_RATE = 16000
RECORD_SECONDS = 4
OUTPUT_FILE = "test_recording.wav"
MODEL = "whisper-large-v3-turbo"

client = Groq(api_key=GROQ_API_KEY)


def record_audio():
    print(f"Recording for {RECORD_SECONDS} seconds... speak now.")
    audio = sd.rec(int(RECORD_SECONDS * SAMPLE_RATE), samplerate=SAMPLE_RATE, channels=1, dtype='int16')
    sd.wait()
    write(OUTPUT_FILE, SAMPLE_RATE, audio)
    print("Recording saved.")


def transcribe_best_of_two(audio_path: str) -> dict:
    """
    Runs transcription forced to English and forced to Urdu, picks whichever
    Whisper was more confident about. Returns the winning text + which
    language won + both raw results (useful for debugging/logging).
    """
    results = {}

    for lang in ["en", "ur"]:
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

        results[lang] = {
            "text": transcription.text.strip(),
            "avg_logprob": avg_logprob,
            "no_speech_prob": no_speech_prob,
            "score": avg_logprob - no_speech_prob  # higher = more confident
        }

    best_lang = max(results, key=lambda l: results[l]["score"])

    return {
        "text": results[best_lang]["text"],
        "detected_lang": best_lang,
        "en_result": results["en"],
        "ur_result": results["ur"]
    }


if __name__ == "__main__":
    while True:
        input("\nPress Enter to record (Ctrl+C to quit)...")
        record_audio()

        result = transcribe_best_of_two(OUTPUT_FILE)

        print(f"\n[Best of Two] \"{result['text']}\" (detected: {result['detected_lang']})")
        print(f"[English-forced] \"{result['en_result']['text']}\"  ({result['en_result']['score']:.2f})")
        print(f"[Urdu-forced]    \"{result['ur_result']['text']}\"  ({result['ur_result']['score']:.2f})")