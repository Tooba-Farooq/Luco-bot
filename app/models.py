from pydantic import BaseModel
from typing import Optional

class DetectionResponse(BaseModel):
    status: str
    face_forward: bool = False
    forward_duration: float = 0.0
    visitor_name: Optional[str] = None
    confidence: Optional[float] = None
    audio_base64: Optional[str] = None  # dynamic audio (known-visitor greeting)
    audio_key: Optional[str] = None     # static audio (fetch via GET /audio/{key})
    session_id: Optional[str] = None
    answer_text: Optional[str] = None

class EmployeeCreateResponse(BaseModel):
    id: int
    name: str
    floor_room: Optional[str] = None
    phone_number: Optional[str] = None
    email: Optional[str] = None
    photo_path: Optional[str] = None
    embedding_created: bool

class RespondResponse(BaseModel):
    session_id: str
    state: str
    heard_text: str
    detected_lang: str
    answer_text: Optional[str] = None
    matched_host: Optional[dict] = None
    host_candidates: Optional[list] = None
    audio_base64: Optional[str] = None       # ← add if missing
    audio_key: Optional[str] = None 

    