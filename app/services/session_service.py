from datetime import datetime, timedelta
from sqlalchemy.orm import Session as DBSession
from app.models_db import VisitSession
from app.services.detection_state import detection_state


def persist_at_handoff(db: DBSession) -> VisitSession:
    """
    Call this exactly once, at the moment detection_state.state becomes
    READY_FOR_HANDOFF. Copies the relevant fields out of the in-memory
    singleton into a durable DB row, so the host-alert flow and visitor
    webpage can keep working with this visitor even after the tablet
    resets for the next person.
    """
    session = VisitSession(
        session_id=detection_state.session_id,
        state="READY_FOR_HANDOFF",
        visitor_id=detection_state.visitor_id,
        visit_log_id=detection_state.visit_log_id,
        selected_host_id=detection_state.selected_host_id,
        purpose=detection_state.purpose,
        recognized_name=detection_state.recognized_name or detection_state.heard_name,
    )
    db.add(session)
    db.commit()
    db.refresh(session)
    return session


def get_session(db: DBSession, session_id: str) -> VisitSession | None:
    return db.query(VisitSession).filter(VisitSession.session_id == session_id).first()


def update_session(db: DBSession, session: VisitSession, **fields) -> VisitSession:
    for key, value in fields.items():
        setattr(session, key, value)
    session.last_active_at = datetime.utcnow()
    db.commit()
    db.refresh(session)
    return session


def close_session(db: DBSession, session: VisitSession):
    """Call at the true end of the host-alert flow (visitor let in, or
    interaction otherwise concluded) — not at persist_at_handoff time."""
    session.is_closed = True
    session.last_active_at = datetime.utcnow()
    db.commit()


def cleanup_stale_sessions(db: DBSession, max_age_minutes: int = 30):
    cutoff = datetime.utcnow() - timedelta(minutes=max_age_minutes)
    stale = (
        db.query(VisitSession)
        .filter((VisitSession.is_closed == True) | (VisitSession.last_active_at < cutoff))
        .all()
    )
    for session in stale:
        db.delete(session)
    db.commit()
    return len(stale)