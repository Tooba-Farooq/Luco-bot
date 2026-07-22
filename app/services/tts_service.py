import edge_tts
import os
import base64

STATIC_AUDIO_DIR = "static_audio_cache"
os.makedirs(STATIC_AUDIO_DIR, exist_ok=True)

VOICE_EN = "en-US-JennyNeural"
VOICE_UR = "hi-IN-MadhurNeural"

# Fixed phrases — no visitor-specific data, generated once and cached forever
STATIC_PHRASES = {
    "unknown_greeting_v2": "Hi! How may I help you",
    "qr_prompt": "Please scan the QR code so we can continue on your phone.",
    "host_notified": "I've notified your host — please wait a moment.",
    "multiple_matches": "I found a few people matching that name — which one did you mean?",
    "no_match_with_suggestions": "Sorry, I couldn't find anyone by that name. Did you mean one of these?",
    "no_match_no_suggestions": "Sorry, I couldn't find anyone by that name in our directory.",
    "ask_purpose": "Please tell me the purpose of your meeting?",
    "ask_name": "And what's your name?",
    "ask_photo": "Perfect, let's get your photo — place your face in center of frame and look at the camera.",
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


async def generate_dynamic_audio(text: str, lang: str = "en") -> tuple[str, str]:
    """
    Generates TTS for arbitrary dynamic text (not cacheable — contains
    variable content like names). Returns (audio_base64, text).
    """
    voice = VOICE_EN if lang == "en" else VOICE_UR

    audio_bytes = b""
    communicate = edge_tts.Communicate(text, voice)
    async for chunk in communicate.stream():
        if chunk["type"] == "audio":
            audio_bytes += chunk["data"]

    audio_base64 = base64.b64encode(audio_bytes).decode("utf-8")
    return audio_base64, text