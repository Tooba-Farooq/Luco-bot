import os
from groq import Groq

GROQ_API_KEY = os.environ["GROQ_API_KEY"]  # never hardcode — rotate the leaked key in Groq's console
MODEL = "whisper-large-v3-turbo"

client = Groq(api_key=GROQ_API_KEY)


def _transcribe_forced(audio_path: str, lang: str) -> dict:
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
        "score": avg_logprob - no_speech_prob  # higher = more confident
    }


def transcribe_best_of_two(audio_path: str) -> dict:
    """
    Runs transcription forced to English and forced to Urdu, picks whichever
    Whisper was more confident about.
    """
    results = {lang: _transcribe_forced(audio_path, lang) for lang in ["en", "ur"]}
    best_lang = max(results, key=lambda l: results[l]["score"])

    return {
        "text": results[best_lang]["text"],
        "detected_lang": best_lang,
        "en_result": results["en"],
        "ur_result": results["ur"]
    }