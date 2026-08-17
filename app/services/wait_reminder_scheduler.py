import logging
from datetime import datetime, timezone

from apscheduler.schedulers.asyncio import AsyncIOScheduler
from apscheduler.jobstores.base import JobLookupError

from app.database import SessionLocal
from app.models_db import Employee, VisitSession
from app.services.push_service import send_wait_reminder

logger = logging.getLogger("wait_reminder_scheduler")

scheduler = AsyncIOScheduler()


def _job_id(session_id: str) -> str:
    return f"wait_reminder_{session_id}"


async def _fire_wait_reminder(session_id: str):
    """Runs exactly once, at wait_until, for one session."""
    db = SessionLocal()
    try:
        session = db.query(VisitSession).filter(VisitSession.session_id == session_id).first()
        if session is None:
            return
        # Re-check state at fire time — host may have resolved it in the last few seconds
        if session.host_response != "wait" or session.is_closed or session.wait_reminder_sent_at is not None:
            return

        employee = db.query(Employee).filter(Employee.id == session.selected_host_id).first()
        if employee is None:
            logger.warning(f"[wait_reminder] No employee for session {session_id}")
            return

        sent = await send_wait_reminder(
            employee=employee,
            visitor_name=session.recognized_name or "Your visitor",
            wait_minutes=session.wait_minutes,
            session_id=session.session_id,
        )
        if sent:
            session.wait_reminder_sent_at = datetime.now(timezone.utc)
            db.commit()
    finally:
        db.close()


def schedule_wait_reminder(session_id: str, run_at: datetime):
    """Call from /respond when host picks 'wait'."""
    scheduler.add_job(
        _fire_wait_reminder,
        "date",
        run_date=run_at,
        args=[session_id],
        id=_job_id(session_id),
        replace_existing=True,
    )


def cancel_wait_reminder(session_id: str):
    """Call from /respond when host resolves early (available / not_available)."""
    try:
        scheduler.remove_job(_job_id(session_id))
    except JobLookupError:
        pass  # nothing was scheduled — fine


async def reconcile_pending_wait_reminders():
    db = SessionLocal()
    try:
        now = datetime.now(timezone.utc)
        pending = (
            db.query(VisitSession)
            .filter(
                VisitSession.host_response == "wait",
                VisitSession.wait_until.isnot(None),
                VisitSession.wait_reminder_sent_at.is_(None),
                VisitSession.is_closed.is_(False),
            )
            .all()
        )
        for session in pending:
            wait_until = session.wait_until
            if wait_until.tzinfo is None:
                wait_until = wait_until.replace(tzinfo=timezone.utc)  # DB stores naive UTC — reattach it

            run_at = wait_until if wait_until > now else now
            schedule_wait_reminder(session.session_id, run_at)
        if pending:
            logger.info(f"[wait_reminder] Reconciled {len(pending)} pending reminder(s) after restart")
    finally:
        db.close()

def start_scheduler():
    scheduler.start()
    logger.info("[wait_reminder_scheduler] Started")


def stop_scheduler():
    scheduler.shutdown(wait=False)