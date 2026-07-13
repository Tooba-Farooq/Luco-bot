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

class EmployeeCreateResponse(BaseModel):
    id: int
    name: str
    floor_room: Optional[str] = None
    phone_number: Optional[str] = None
    email: Optional[str] = None
    photo_path: Optional[str] = None
    embedding_created: bool

    