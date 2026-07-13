from fastapi import APIRouter, HTTPException
from fastapi.responses import Response
from app.services.tts_service import get_static_audio_bytes

router = APIRouter()


@router.get("/audio/{key}")
def get_audio(key: str):
    try:
        audio_bytes = get_static_audio_bytes(key)
        return Response(content=audio_bytes, media_type="audio/mpeg")
    except FileNotFoundError:
        raise HTTPException(status_code=404, detail="Audio not found")