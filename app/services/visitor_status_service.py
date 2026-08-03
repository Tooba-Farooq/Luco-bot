from app.models_db import Employee
from datetime import datetime, timezone
import math


def build_visitor_status(
    response: str,
    employee: Employee,
    wait_minutes: int | None = None,
    wait_until: datetime | None = None,
    available_again_at: datetime | None = None,
) -> dict:
    if response == "available":
        location = f" at {employee.floor_room}" if employee.floor_room else ""
        return {
            "visitor_state": "PROCEED_TO_HOST",
            "visitor_message": f"{employee.name} is waiting for you{location}. Please head over.",
        }

    if response == "wait":
        remaining = wait_minutes
        if wait_until:
            # SQLite strips tzinfo on round-trip — normalize to aware UTC before subtracting
            wu = wait_until if wait_until.tzinfo else wait_until.replace(tzinfo=timezone.utc)
            delta = wu - datetime.now(timezone.utc)
            remaining = max(0, math.ceil(delta.total_seconds() / 60))
        until_str = wait_until.strftime("%I:%M %p") if wait_until else None

        if until_str:
            message = f"{employee.name} is currently unavailable and has asked you to wait about {remaining} more minutes (until {until_str})."
        else:
            message = f"{employee.name} is currently unavailable and has asked you to wait about {remaining} minutes."

        return {
            "visitor_state": "HOST_ASKED_WAIT",
            "visitor_message": message,
        }

    # not_available
    if available_again_at:
        formatted = available_again_at.strftime("%A, %I:%M %p")
        message = f"{employee.name} is unavailable today. They'll be available again on {formatted}. You're welcome to leave a message in the meantime."
    else:
        message = f"{employee.name} is unavailable right now. You're welcome to leave a message, or come back another time."

    return {
        "visitor_state": "HOST_UNAVAILABLE",
        "visitor_message": message,
    }