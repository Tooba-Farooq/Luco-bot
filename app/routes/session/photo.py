from fastapi import APIRouter, UploadFile, File, Form, HTTPException, Depends
from sqlalchemy.orm import Session
from app.database import get_db
from app.services.detection_state import detection_state
from app.services.detection_service import check_face_present, check_face_forward, check_face_centered, _load_image
from app.services.embedding_service import generate_face_embedding
from app.services.session_service import persist_at_handoff
from app.models_db import Visitor, VisitLog
from app.models import PhotoFrameResponse
import os
import uuid
import cv2
import time

router = APIRouter()

PHOTO_DIR = "visitor_photos"
os.makedirs(PHOTO_DIR, exist_ok=True)
PHOTO_STEADY_THRESHOLD = 2.0


@router.post("/session/photo-frame", response_model=PhotoFrameResponse)
async def photo_frame(session_id: str = Form(...), frame: UploadFile = File(...)):
    if detection_state.session_id != session_id:
        raise HTTPException(status_code=400, detail="Session mismatch")
    if detection_state.state != "AWAITING_PHOTO":
        raise HTTPException(status_code=400, detail=f"Not expecting photo frames in state: {detection_state.state}")

    image = _load_image(await frame.read())
    if image is None:
        detection_state.photo_steady_start_time = None
        return PhotoFrameResponse(face_found=False, is_forward=False, is_centered=False, ready_to_capture=False)

    face_found, face_box = check_face_present(image)
    if not face_found:
        detection_state.photo_steady_start_time = None
        return PhotoFrameResponse(face_found=False, is_forward=False, is_centered=False, ready_to_capture=False)

    is_forward = check_face_forward(image, face_box)
    is_centered = check_face_centered(image, face_box)
    is_good_frame = is_forward and is_centered

    now = time.time()
    if is_good_frame:
        if detection_state.photo_steady_start_time is None:
            detection_state.photo_steady_start_time = now
        steady_duration = now - detection_state.photo_steady_start_time
    else:
        detection_state.photo_steady_start_time = None
        steady_duration = 0.0

    ready_to_capture = steady_duration >= PHOTO_STEADY_THRESHOLD

    return PhotoFrameResponse(
        face_found=True, is_forward=is_forward, is_centered=is_centered,
        ready_to_capture=ready_to_capture
    )


@router.post("/session/capture-photo")
async def capture_photo(session_id: str = Form(...), frame: UploadFile = File(...), db: Session = Depends(get_db)):
    if detection_state.session_id != session_id:
        raise HTTPException(status_code=400, detail="Session mismatch")
    if detection_state.state != "AWAITING_PHOTO":
        raise HTTPException(status_code=400, detail=f"Not expecting a capture in state: {detection_state.state}")

    image = _load_image(await frame.read())
    if image is None:
        raise HTTPException(status_code=400, detail="Could not decode image")

    # re-verify quality server-side even though client already saw green
    face_found, face_box = check_face_present(image)
    if not face_found or not check_face_forward(image, face_box) or not check_face_centered(image, face_box):
        raise HTTPException(status_code=409, detail="Face not steady — retry capture")

    gray = cv2.cvtColor(image, cv2.COLOR_BGR2GRAY)
    blur_score = cv2.Laplacian(gray, cv2.CV_64F).var()
    print(f"Blur score: {blur_score:.1f}")  # temporary, remove once calibrated
    if blur_score < 20.0:
        raise HTTPException(status_code=409, detail="Image too blurry — retry capture")

    # save the raw frame to disk
    photo_path = os.path.join(PHOTO_DIR, f"{uuid.uuid4().hex}.jpg").replace("\\", "/")
    cv2.imwrite(photo_path, image)

    # CHANGED: embedding may legitimately fail for occluded faces (niqab, mask, etc.) —
    # same limitation as run_face_recognition's opencv backend hitting enforce_detection=True.
    # This is not an error condition; don't delete the photo or block capture on it.
    embedding = generate_face_embedding(photo_path)
    if embedding is None:
        print(f"No embedding extracted for {photo_path} (occluded face or detector miss) — saving visitor without one.")

    new_visitor = Visitor(
        name=detection_state.heard_name,
        face_embedding=embedding,  # may be None — that's fine, matches /detect's "unknown" tolerance
        photo_path=photo_path
    )
    db.add(new_visitor)
    db.commit()
    db.refresh(new_visitor)

    detection_state.visitor_id = new_visitor.id

    new_visit = VisitLog(
        visitor_id=new_visitor.id,
        host_employee_id=detection_state.selected_host_id,
        purpose=detection_state.purpose,
        status="in_progress"
    )
    db.add(new_visit)
    db.commit()
    db.refresh(new_visit)

    
    detection_state.visit_log_id = new_visit.id
    detection_state.state = "READY_FOR_HANDOFF"
    await persist_at_handoff(db)

    response = {
        "session_id": session_id,
        "state": "READY_FOR_HANDOFF",
        "answer_text": "Thanks — I'll let them know you're here.",
        "audio_key": "ready_for_handoff",
    }
    detection_state.reset()
    return response