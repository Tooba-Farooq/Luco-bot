from fastapi import APIRouter, UploadFile, File, Form, HTTPException, Depends
from sqlalchemy.orm import Session
from app.database import get_db
from app.services.stt_service import transcribe_best_of_two
from app.services.llm_service import classify_intent, answer_query
from app.services.host_service import find_host
from app.services.detection_state import detection_state
from app.services.tts_service import generate_dynamic_audio
from app.models import RespondResponse
import tempfile
import os

router = APIRouter()


@router.post("/session/respond", response_model=RespondResponse)
async def respond(
    session_id: str = Form(...),
    audio: UploadFile = File(...),
    db: Session = Depends(get_db)
):
    if detection_state.session_id != session_id:
        raise HTTPException(status_code=400, detail="Session ID mismatch or expired")

    # --- Step 1: audio -> text (always happens first, regardless of state) ---
    audio_bytes = await audio.read()
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
        tmp.write(audio_bytes)
        tmp_path = tmp.name

    try:
        stt_result = await transcribe_best_of_two(tmp_path)
    finally:
        os.remove(tmp_path)

    heard_text = stt_result["text"]
    detected_lang = stt_result["detected_lang"]

    # --- Step 2: route based on current state ---
    current_state = detection_state.state

    if current_state == "AWAITING_INTENT":
        classification = await classify_intent(heard_text)

        if classification["intent"] == "MEET_SOMEONE":
            person_name = classification.get("person_name")

            if not person_name:
                # LLM understood they want to meet someone, but didn't catch a name
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
                detection_state.state = "FALLBACK"
                return RespondResponse(
                    session_id=session_id, state="FALLBACK",
                    heard_text=heard_text, detected_lang=detected_lang,
                    answer_text="I'm not sure about that — Is there anything else I can help you with?"
                )
            else:
                detection_state.state = "QUERY_ANSWERED"
                return RespondResponse(
                    session_id=session_id, state="QUERY_ANSWERED",
                    heard_text=heard_text, detected_lang=detected_lang,
                    answer_text=answer
                )

    elif current_state == "AWAITING_HOST_NAME":
        # visitor is now just saying the name directly, no intent classification needed
        host_result = find_host(heard_text, db)
        return await _handle_host_result(host_result, session_id, heard_text, detected_lang)

    elif current_state == "AWAITING_PURPOSE":
        detection_state.purpose = heard_text
        detection_state.state = "AWAITING_NAME"  # name/photo capture happens now, at the end
        return RespondResponse(
            session_id=session_id, state="AWAITING_NAME",
            heard_text=heard_text, detected_lang=detected_lang
        )

    elif current_state == "AWAITING_NAME":
        detection_state.heard_name = heard_text
        detection_state.detected_lang = detected_lang
        detection_state.state = "NAME_CONFIRMATION"
        return RespondResponse(
            session_id=session_id, state="NAME_CONFIRMATION",
            heard_text=heard_text, detected_lang=detected_lang
        )

    elif current_state == "ANYTHING_ELSE":
        # visitor answering "anything else?" after a query — reuse intent classifier
        classification = await classify_intent(heard_text)
        # simple yes/no-ish handling could go here; for now, loop back to intent
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
        

from pydantic import BaseModel
from app.models_db import Employee

class SelectHostRequest(BaseModel):
    session_id: str
    employee_id: int

class RetryHostNameRequest(BaseModel):
    session_id: str


@router.post("/session/confirm-host")
async def confirm_host(payload: SelectHostRequest, db: Session = Depends(get_db)):
    if detection_state.session_id != payload.session_id:
        raise HTTPException(status_code=400, detail="Session mismatch")

    employee = db.query(Employee).filter(Employee.id == payload.employee_id).first()
    if not employee:
        raise HTTPException(status_code=404, detail="Employee not found")

    detection_state.selected_host_id = employee.id
    detection_state.host_candidates = None
    detection_state.state = "AWAITING_PURPOSE"
    answer_text = f"Please tell me the purpose of your visit?"
    audio_base64, _ = await generate_dynamic_audio(answer_text)

    return {
        "session_id": payload.session_id, "state": "AWAITING_PURPOSE",
        "matched_host": {"id": employee.id, "name": employee.name},
        "answer_text": answer_text, "audio_base64": audio_base64
    }


@router.post("/session/cancel-host-selection")
async def cancel_host_selection(payload: RetryHostNameRequest):
    if detection_state.session_id != payload.session_id:
        raise HTTPException(status_code=400, detail="Session mismatch")
    # TODO: decide behavior — back to idle, or retry host name, or something else
    raise HTTPException(status_code=501, detail="Not implemented yet")