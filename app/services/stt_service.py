from groq import AsyncGroq
import asyncio
import os

client = AsyncGroq(api_key=os.getenv("GROQ_API_KEY"))
MODEL = "whisper-large-v3-turbo"


async def _transcribe_one(audio_path: str, lang: str) -> dict:
    with open(audio_path, "rb") as f:
        transcription = await client.audio.transcriptions.create(
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
        "score": avg_logprob - no_speech_prob
    }


async def transcribe_best_of_two(audio_path: str) -> dict:
    """
    Runs transcription forced to English and forced to Urdu IN PARALLEL,
    picks whichever Whisper was more confident about.
    Returns: {"text": str, "detected_lang": "en" | "ur"}
    """
    en_result, ur_result = await asyncio.gather(
        _transcribe_one(audio_path, "en"),
        _transcribe_one(audio_path, "ur")
    )

    results = {"en": en_result, "ur": ur_result}
    best_lang = max(results, key=lambda l: results[l]["score"])

    return {
        "text": results[best_lang]["text"],
        "detected_lang": best_lang
    }