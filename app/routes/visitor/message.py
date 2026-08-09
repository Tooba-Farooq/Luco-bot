from fastapi import APIRouter, UploadFile, File, Form, HTTPException, Depends
from sqlalchemy.orm import Session
from app.database import get_db
from app.models_db import VisitSession, VisitLog
from app.services.stt_service import transcribe_best_of_two
import tempfile
import os

router = APIRouter()


@router.post("/message")
async def record_message(
    status_token: str = Form(...),
    text: str | None = Form(None),
    audio: UploadFile | None = File(None),
    db: Session = Depends(get_db),
):
    # Resolve the session server-side via status_token — the public,
    # visitor-facing identifier — rather than trusting/exposing session_id
    # directly to the client.
    session = db.query(VisitSession).filter(VisitSession.status_token == status_token).first()
    if session is None:
        raise HTTPException(status_code=404, detail="Session not found")

    message_text = None
    if audio:
        audio_bytes = await audio.read()
        with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as tmp:
            tmp.write(audio_bytes)
            tmp_path = tmp.name
        try:
            stt_result = await transcribe_best_of_two(tmp_path)
        finally:
            os.remove(tmp_path)
        message_text = stt_result["text"]
    elif text:
        message_text = text.strip()

    visit_log = db.query(VisitLog).filter(VisitLog.id == session.visit_log_id).first()
    if visit_log:
        visit_log.status = "message_left" if message_text else "concluded_no_message"
        visit_log.message_text = message_text
        db.commit()

    return {"detail": "Message recorded", "message_text": message_text}