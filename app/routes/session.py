from fastapi import APIRouter, UploadFile, File, Form, HTTPException, Depends
from sqlalchemy.orm import Session
from app.database import get_db
from app.services.stt_service import transcribe_best_of_two
from app.services.llm_service import classify_intent, answer_query
from app.services.host_service import find_host
from app.services.detection_state import detection_state
from app.services.tts_service import generate_dynamic_audio
from app.services.embedding_service import generate_face_embedding
from app.services.detection_service import check_face_present, check_face_forward, check_face_centered, _load_image
from app.models_db import Visitor, VisitLog, Employee
from app.models import (
    ConfirmHostResponse,
    PhotoFrameResponse,
    RespondResponse,
    RetryHostNameRequest,
    SelectHostRequest,
    SubmitNameRequest,
    SubmitNameResponse,
)
import tempfile
import os
import uuid
import cv2
import time



router = APIRouter()


@router.post("/session/respond", response_model=RespondResponse)
async def respond(
    session_id: str = Form(...),
    audio: UploadFile = File(...),
    db: Session = Depends(get_db)
):
    if detection_state.session_id != session_id:
        raise HTTPException(status_code=400, detail="Session ID mismatch or expired")

    # --- Step 1: save audio to a temp file ---
    audio_bytes = await audio.read()
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
        tmp.write(audio_bytes)
        tmp_path = tmp.name

    # current_state read BEFORE the STT call, so we know which transcription path to use
    current_state = detection_state.state

    try:
        if current_state == "AWAITING_NAME":
            # force English so names come back in Roman script, not Urdu
            stt_result = await transcribe_best_of_two(tmp_path, force_language="en")
        else:
            stt_result = await transcribe_best_of_two(tmp_path)
    finally:
        os.remove(tmp_path)

    heard_text = stt_result["text"]
    detected_lang = stt_result["detected_lang"]

    # --- Step 2: route based on current state ---

    if current_state == "AWAITING_INTENT":
        classification = await classify_intent(heard_text)

        if classification["intent"] == "MEET_SOMEONE":
            person_name = classification.get("person_name")

            if not person_name:
                detection_state.state = "AWAITING_HOST_NAME"
                return RespondResponse(
                    session_id=session_id, state="AWAITING_HOST_NAME",
                    heard_text=heard_text, detected_lang=detected_lang
                )

            host_result = find_host(person_name, db)
            return await _handle_host_result(host_result, session_id, heard_text, detected_lang)

        else:  # GENERAL_QUERY
            answer = await answer_query(heard_text)

            if answer == "NO_MATCH":
                detection_state.state = "ANYTHING_ELSE"  # CHANGED: backend now expects the follow-up
                return RespondResponse(
                    session_id=session_id, state="FALLBACK",  # unchanged — still tells frontend what happened
                    heard_text=heard_text, detected_lang=detected_lang,
                    answer_text="I'm not sure about that — Is there anything else I can help you with?"
                )
            else:
                detection_state.state = "ANYTHING_ELSE"  # CHANGED
                return RespondResponse(
                    session_id=session_id, state="QUERY_ANSWERED",
                    heard_text=heard_text, detected_lang=detected_lang,
                    answer_text=answer
                )
            
    elif current_state == "AWAITING_HOST_NAME":
        host_result = find_host(heard_text, db)
        return await _handle_host_result(host_result, session_id, heard_text, detected_lang)

    elif current_state == "AWAITING_PURPOSE":
        detection_state.purpose = heard_text

        if detection_state.recognized_name:
            # known visitor — find their existing Visitor/Employee record, or create
            # a lightweight VisitLog tied to them directly
            new_visit = VisitLog(
                visitor_id=detection_state.visitor_id,  # however you're tracking known-visitor identity
                host_employee_id=detection_state.selected_host_id,
                purpose=detection_state.purpose,
                status="in_progress"
            )
            db.add(new_visit)
            db.commit()
            db.refresh(new_visit)
            detection_state.visit_log_id = new_visit.id

            detection_state.state = "READY_FOR_HANDOFF"

            return RespondResponse(
                session_id=session_id, state="READY_FOR_HANDOFF",
                heard_text=heard_text, detected_lang=detected_lang,
                answer_text="Thanks — I'll let them know you're here.",
                audio_key="ready_for_handoff"
            )
        else:
            detection_state.state = "AWAITING_NAME"
            return RespondResponse(
                session_id=session_id, state="AWAITING_NAME",
                heard_text=heard_text, detected_lang=detected_lang,
                answer_text="And what's your name?",
                audio_key="ask_name"
            )

    elif current_state == "AWAITING_NAME":
        detection_state.heard_name = heard_text
        detection_state.detected_lang = detected_lang
        detection_state.state = "NAME_CONFIRMATION"
        return RespondResponse(
            session_id=session_id, state="NAME_CONFIRMATION",
            heard_text=heard_text, detected_lang=detected_lang,
            answer_text="Is this right? Edit if needed, then submit.")

    elif current_state == "ANYTHING_ELSE":
        classification = await classify_intent(heard_text)
        detection_state.state = "AWAITING_INTENT"
        return RespondResponse(
            session_id=session_id, state="AWAITING_INTENT",
            heard_text=heard_text, detected_lang=detected_lang
        )

    else:
        raise HTTPException(status_code=400, detail=f"Not expecting audio in state: {current_state}")


async def _handle_host_result(host_result: dict, session_id: str, heard_text: str, detected_lang: str) -> RespondResponse:
    if host_result["result"] == "ONE_MATCH":
        detection_state.host_candidates = [host_result["employee"]]
        detection_state.state = "HOST_SELECTION"
        answer_text = f"Please confirm, Do you want to meet {host_result['employee']['name']}?"
        audio_base64, _ = await generate_dynamic_audio(answer_text)
        return RespondResponse(
            session_id=session_id, state="HOST_SELECTION",
            heard_text=heard_text, detected_lang=detected_lang,
            host_candidates=[host_result["employee"]],
            answer_text=answer_text, audio_base64=audio_base64
        )

    elif host_result["result"] == "MULTIPLE_MATCHES":
        detection_state.host_candidates = host_result["candidates"]
        detection_state.state = "HOST_SELECTION"
        return RespondResponse(
            session_id=session_id, state="HOST_SELECTION",
            heard_text=heard_text, detected_lang=detected_lang,
            host_candidates=host_result["candidates"],
            audio_key="multiple_matches"
        )

    else:  # NO_MATCH
        detection_state.host_candidates = host_result["candidates"]
        detection_state.state = "HOST_SUGGESTIONS"
        if host_result["candidates"]:
            return RespondResponse(
                session_id=session_id, state="HOST_SUGGESTIONS",
                heard_text=heard_text, detected_lang=detected_lang,
                host_candidates=host_result["candidates"],
                audio_key="no_match_with_suggestions"
            )
        else:
            return RespondResponse(
                session_id=session_id, state="HOST_SUGGESTIONS",
                heard_text=heard_text, detected_lang=detected_lang,
                host_candidates=[],
                audio_key="no_match_no_suggestions"
            )


@router.post("/session/confirm-host", response_model=ConfirmHostResponse)
async def confirm_host(payload: SelectHostRequest, db: Session = Depends(get_db)):
    if detection_state.session_id != payload.session_id:
        raise HTTPException(status_code=400, detail="Session mismatch")

    employee = db.query(Employee).filter(Employee.id == payload.employee_id).first()
    if not employee:
        raise HTTPException(status_code=404, detail="Employee not found")

    detection_state.selected_host_id = employee.id
    detection_state.host_candidates = None
    detection_state.state = "AWAITING_PURPOSE"

    return ConfirmHostResponse(
        session_id=payload.session_id,
        state="AWAITING_PURPOSE",
        matched_host={"id": employee.id, "name": employee.name},
        answer_text="Please tell me the purpose of your meeting?",
        audio_key="ask_purpose",
    )


@router.post("/session/cancel-host-selection")
async def cancel_host_selection(payload: RetryHostNameRequest):
    if detection_state.session_id != payload.session_id:
        raise HTTPException(status_code=400, detail="Session mismatch")
    raise HTTPException(status_code=501, detail="Not implemented yet")


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

PHOTO_DIR = "visitor_photos"
os.makedirs(PHOTO_DIR, exist_ok=True)


PHOTO_STEADY_THRESHOLD = 1.0  # seconds the frame must stay green before ready-to-capture

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
    photo_path = os.path.join(PHOTO_DIR, f"{uuid.uuid4().hex}.jpg")
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

    new_visit = VisitLog(
        visitor_id=new_visitor.id,
        host_employee_id=detection_state.selected_host_id,
        purpose=detection_state.purpose,
        status="in_progress"
    )
    db.add(new_visit)
    db.commit()
    db.refresh(new_visit)

    detection_state.visitor_id = new_visitor.id
    detection_state.visit_log_id = new_visit.id
    detection_state.state = "READY_FOR_HANDOFF"

    return {
        "session_id": session_id,
        "state": "READY_FOR_HANDOFF",
        "answer_text": "Thanks — I'll let them know you're here.",
        "audio_key": "ready_for_handoff",
    }