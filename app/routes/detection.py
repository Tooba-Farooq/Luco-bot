from fastapi import APIRouter, UploadFile, File
from app.models import DetectionResponse
from app.services.detection_service import check_face_present, check_face_forward, run_face_recognition
from app.services.detection_state import detection_state
import time

router = APIRouter()

GRACE_PERIOD_SECONDS = 1.5  # how long a brief look-away is tolerated before resetting
FORWARD_DURATION_THRESHOLD = 3.0


@router.post("/detect", response_model=DetectionResponse)
async def detect(frame: UploadFile = File(...)):
    image_bytes = await frame.read()

    # STEP 1: cheap face detection check
    face_found, face_box = check_face_present(image_bytes)
    if not face_found:
        detection_state.reset()
        return DetectionResponse(status="idle")

    # STEP 2: forward-facing + distance check
    is_forward = check_face_forward(image_bytes, face_box)
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

    # STEP 3: only now run face recognition (expensive)
    name, confidence = run_face_recognition(image_bytes)
    if name:
        return DetectionResponse(
            status="known", visitor_name=name, confidence=confidence,
            face_forward=True, forward_duration=duration
        )
    else:
        return DetectionResponse(status="unknown", face_forward=True, forward_duration=duration)