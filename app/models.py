from pydantic import BaseModel
from typing import Optional

class DetectionResponse(BaseModel):
    status: str  # "idle" | "detecting" | "known" | "unknown"
    face_forward: bool = False
    forward_duration: float = 0.0  # seconds accumulated facing forward
    visitor_name: Optional[str] = None  # only set if status == "known"
    confidence: Optional[float] = None  # recognition confidence score