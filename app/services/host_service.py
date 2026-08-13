from rapidfuzz import process, fuzz
from sqlalchemy.orm import Session
from app.models_db import Employee

# Tier thresholds — tune these independently without touching the matching logic below.
STRONGEST_THRESHOLD = 95
STRONG_THRESHOLD = 80
WEAK_THRESHOLD = 65
WEAKEST_THRESHOLD = 45


def find_host(name_spoken: str, db: Session) -> dict:
    employees = db.query(Employee).all()
    if not employees:
        return {"result": "NO_EMPLOYEES", "candidates": []}

    choices = [e.name for e in employees]
    matches = process.extract(name_spoken, choices, scorer=fuzz.WRatio, limit=5)

    def to_employee(match):
        name = match[0]
        emp = next(e for e in employees if e.name == name)
        return {"id": emp.id, "name": emp.name, "floor_room": emp.floor_room}

    # Single pass over the already-scored matches (≤5 items) — negligible cost regardless
    # of how many tiers we check, since process.extract already did the real work above.
    tiers = [
        [m for m in matches if m[1] > STRONGEST_THRESHOLD],
        [m for m in matches if STRONG_THRESHOLD < m[1] <= STRONGEST_THRESHOLD],
        [m for m in matches if WEAK_THRESHOLD < m[1] <= STRONG_THRESHOLD],
        [m for m in matches if WEAKEST_THRESHOLD < m[1] <= WEAK_THRESHOLD],
    ]

    for tier in tiers:
        if len(tier) == 1:
            return {"result": "ONE_MATCH", "employee": to_employee(tier[0])}
        elif len(tier) > 1:
            return {"result": "MULTIPLE_MATCHES", "candidates": [to_employee(m) for m in tier]}
        # empty tier -> fall through to the next, weaker tier

    # nothing matched even at the weakest tier — fall back to full directory
    return {"result": "NO_MATCH", "candidates": [to_employee((e.name, 0, 0)) for e in employees]}