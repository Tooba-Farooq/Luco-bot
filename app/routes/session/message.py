from fastapi import APIRouter, UploadFile, File, Form, HTTPException, Depends
from sqlalchemy.orm import Session
from app.database import get_db
from app.models_db import VisitSession
from app.services.stt_service import transcribe_best_of_two
from app.services.connection_manager import manager
import tempfile
import os

router = APIRouter()


@router.post("/session/record-message")
async def record_message(
    session_id: str = Form(...),
    audio: UploadFile = File(...),
    db: Session = Depends(get_db),
):
    session = db.query(VisitSession).filter(VisitSession.session_id == session_id).first()
    if session is None:
        raise HTTPException(status_code=404, detail="Session not found")

    audio_bytes = await audio.read()
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
        tmp.write(audio_bytes)
        tmp_path = tmp.name

    try:
        stt_result = await transcribe_best_of_two(tmp_path)
    finally:
        os.remove(tmp_path)

    session.message_text = stt_result["text"]
    session.visitor_choice = "message"
    db.commit()

    if session.status_token:
        await manager.send_update(session.status_token, {
            "state": session.state,
            "visitor_choice": session.visitor_choice,
        })

    return {"detail": "Message recorded", "message_text": session.message_text}