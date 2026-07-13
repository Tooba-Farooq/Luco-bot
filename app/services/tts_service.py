import edge_tts
import os
import base64

STATIC_AUDIO_DIR = "static_audio_cache"
os.makedirs(STATIC_AUDIO_DIR, exist_ok=True)

VOICE_EN = "en-US-AriaNeural"
VOICE_UR = "hi-IN-MadhurNeural"

# Fixed phrases — no visitor-specific data, generated once and cached forever
STATIC_PHRASES = {
    "unknown_greeting": "Hi! I don't think we've met — what's your name?",
}


async def build_static_audio_cache():
    """Run once at server startup — generates and caches all fixed phrases."""
    for key, text in STATIC_PHRASES.items():
        filepath = os.path.join(STATIC_AUDIO_DIR, f"{key}.mp3")
        if not os.path.exists(filepath):
            communicate = edge_tts.Communicate(text, VOICE_EN)
            await communicate.save(filepath)
            print(f"Cached: {key}")


def get_static_audio_bytes(key: str) -> bytes:
    filepath = os.path.join(STATIC_AUDIO_DIR, f"{key}.mp3")
    with open(filepath, "rb") as f:
        return f.read()


async def generate_known_greeting_audio(visitor_name: str, lang: str = "en") -> str:
    """
    Dynamic greeting — contains the visitor's real name, so it's generated
    live per-request, not cached. Returns (audio_base64, greeting_text).
    """
    voice = VOICE_EN if lang == "en" else VOICE_UR
    greeting_text = f"Hi {visitor_name}, how may I help you today?"

    audio_bytes = b""
    communicate = edge_tts.Communicate(greeting_text, voice)
    async for chunk in communicate.stream():
        if chunk["type"] == "audio":
            audio_bytes += chunk["data"]

    audio_base64 = base64.b64encode(audio_bytes).decode("utf-8")
    return audio_base64