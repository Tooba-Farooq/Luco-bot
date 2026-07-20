from fastapi import APIRouter, UploadFile, File, Depends
from sqlalchemy.orm import Session
from app.database import get_db
from app.models import DetectionResponse
from app.services.detection_service import check_face_present, check_face_forward, run_face_recognition, _load_image
from app.services.detection_state import detection_state
from app.services.tts_service import generate_dynamic_audio
import time

router = APIRouter()

GRACE_PERIOD_SECONDS = 1.5  # how long a brief look-away is tolerated before resetting
FORWARD_DURATION_THRESHOLD = 3.0


@router.post("/detect", response_model=DetectionResponse)
async def detect(frame: UploadFile = File(...), db: Session = Depends(get_db)):
    image_bytes = await frame.read()
    image = _load_image(image_bytes)  # decode ONCE, here

    if image is None:
        detection_state.reset()
        return DetectionResponse(status="idle")

    face_found, face_box = check_face_present(image)
    if not face_found:
        detection_state.reset()
        return DetectionResponse(status="idle")

    is_forward = check_face_forward(image, face_box)
    now = time.time()

    if is_forward:
        if detection_state.forward_start_time is None:
            detection_state.forward_start_time = now
        detection_state.last_seen_forward_time = now
        duration = now - detection_state.forward_start_time
    else:
        within_grace = (
            detection_state.last_seen_forward_time is not None
            and (now - detection_state.last_seen_forward_time) < GRACE_PERIOD_SECONDS
        )
        if within_grace:
            duration = detection_state.last_seen_forward_time - detection_state.forward_start_time
        else:
            detection_state.reset()
            duration = 0.0

    if duration < FORWARD_DURATION_THRESHOLD:
        return DetectionResponse(status="detecting", face_forward=is_forward, forward_duration=duration)

    # STEP 3: only now run face recognition (expensive) — pass decoded image + db
    name, confidence = run_face_recognition(image, db)
    status = "known" if name else "unknown"
    session_id = detection_state.start_session()

    if status == "known":
        audio_base64 = await generate_dynamic_audio(f"Hi {name}! How may I help you?", lang="en")
        return DetectionResponse(
            status=status, session_id=session_id, visitor_name=name, confidence=confidence,
            face_forward=True, forward_duration=duration,
            audio_base64=audio_base64
        )
    else:
        return DetectionResponse(
            status=status, session_id=session_id,
            face_forward=True, forward_duration=duration,
            audio_key="unknown_greeting_v2"  # Unity fetches GET /audio/unknown_greeting_v2
        )