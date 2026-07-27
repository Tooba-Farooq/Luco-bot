from fastapi import APIRouter, UploadFile, File, Form, HTTPException, Depends
from sqlalchemy.orm import Session
from app.database import get_db
from app.services.stt_service import transcribe_best_of_two
from app.services.llm_service import classify_intent, answer_query
from app.services.host_service import find_host
from app.services.detection_state import detection_state
from app.services.tts_service import generate_dynamic_audio
from app.models_db import Visitor, VisitLog
from app.models import RespondResponse
import tempfile
import os

router = APIRouter()

# TODO: if no host candidates come up employees directory emty it should go to anything else branch


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
                    heard_text=heard_text, detected_lang=detected_lang,
                    answer_text="Who would you like to meet?",
                    audio_key="ask_host_name"
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
            if detection_state.visitor_id is None:
                matched_visitor = (
                    db.query(Visitor)
                    .filter(Visitor.name == detection_state.recognized_name)
                    .order_by(Visitor.id.desc())
                    .first()
                )
                if matched_visitor:
                    detection_state.visitor_id = matched_visitor.id
                else:
                    new_visitor = Visitor(
                        name=detection_state.recognized_name,
                        face_embedding=None,
                        photo_path=None,
                    )
                    db.add(new_visitor)
                    db.commit()
                    db.refresh(new_visitor)
                    detection_state.visitor_id = new_visitor.id

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
    result = host_result["result"]

    if result == "ONE_MATCH":
        employee = host_result["employee"]
        detection_state.host_candidates = [employee]
        detection_state.state = "HOST_SELECTION"

        answer_text = f"Please confirm, Do you want to meet {employee['name']}?"
        audio_base64, _ = await generate_dynamic_audio(answer_text)

        return RespondResponse(
            session_id=session_id, state="HOST_SELECTION",
            heard_text=heard_text, detected_lang=detected_lang,
            host_candidates=[employee],
            answer_text=answer_text, audio_base64=audio_base64
        )

    if result == "MULTIPLE_MATCHES":
        candidates, audio_key = host_result["candidates"], "multiple_matches"
    elif host_result["candidates"]:  # no match, but directory fallback has names to offer
        candidates, audio_key = host_result["candidates"], "no_match_with_suggestions"
    else:  # no match, nothing to offer at all — empty list tells Unity not to render a picker
        candidates, audio_key = [], "no_match_no_suggestions"

    detection_state.host_candidates = candidates
    detection_state.state = "HOST_SELECTION"  # same state regardless of which text/candidates apply
    return RespondResponse(
        session_id=session_id, state="HOST_SELECTION",
        heard_text=heard_text, detected_lang=detected_lang,
        host_candidates=candidates,
        audio_key=audio_key
    )