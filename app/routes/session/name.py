from fastapi import APIRouter, HTTPException
from app.services.detection_state import detection_state
from app.models import SubmitNameRequest, SubmitNameResponse

router = APIRouter()


@router.post("/session/submit-name", response_model=SubmitNameResponse)
async def submit_name(payload: SubmitNameRequest):
    if detection_state.session_id != payload.session_id:
        raise HTTPException(status_code=400, detail="Session mismatch")

    detection_state.heard_name = payload.name.strip()
    detection_state.state = "AWAITING_PHOTO"

    return SubmitNameResponse(
        session_id=payload.session_id,
        state="AWAITING_PHOTO",
        visitor_name=detection_state.heard_name,
        answer_text="Great, let's get your photo.",
        audio_key="ask_photo",
    )