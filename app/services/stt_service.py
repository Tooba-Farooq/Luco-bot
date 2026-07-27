from groq import AsyncGroq
import asyncio
import os
from app.services.llm_service import resolve_name

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


async def transcribe_best_of_two(audio_path: str, force_language: str | None = None) -> dict:
    """
    Runs transcription forced to English and forced to Urdu IN PARALLEL,
    picks whichever Whisper was more confident about.
    Pass force_language="en" (or "ur") to skip the race and use that language directly —
    useful for fields like names where you don't want phonetic Urdu transcription.
    Returns: {"text": str, "detected_lang": "en" | "ur"}
    """
    if force_language:
        result = await _transcribe_one(audio_path, force_language)
        return {
            "text": result["text"],
            "detected_lang": force_language
        }

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


async def transcribe_name(audio_path: str) -> dict:
    """
    Dedicated path for the AWAITING_NAME state. Runs English-forced and Urdu-forced
    transcription in parallel (like transcribe_best_of_two), then hands both results
    to an LLM to reconcile into one Roman-script name — since neither language alone
    is reliably correct on short name-only clips (see stt debugging session).
    Always returns a usable name string; never null, since the visitor can edit it.
    Returns: {"text": str, "detected_lang": "en"}  (detected_lang kept as "en" since
    output is always normalized to Roman script regardless of which STT pass fed it)
    """
    en_result, ur_result = await asyncio.gather(
        _transcribe_one(audio_path, "en"),
        _transcribe_one(audio_path, "ur")
    )

    name = await resolve_name(en_result["text"], ur_result["text"])

    return {
        "text": name,
        "detected_lang": "en"
    }