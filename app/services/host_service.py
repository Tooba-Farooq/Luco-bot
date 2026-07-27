from rapidfuzz import process, fuzz
from sqlalchemy.orm import Session
from app.models_db import Employee


def find_host(name_spoken: str, db: Session) -> dict:
    employees = db.query(Employee).all()
    if not employees:
        return {"result": "NO_EMPLOYEES", "candidates": []}

    choices = [e.name for e in employees]
    matches = process.extract(name_spoken, choices, scorer=fuzz.WRatio, limit=5)

    strong_matches = [m for m in matches if m[1] > 85]
    weak_matches = [m for m in matches if m[1] > 60]

    def to_employee(match):
        name = match[0]
        emp = next(e for e in employees if e.name == name)
        return {"id": emp.id, "name": emp.name, "floor_room": emp.floor_room}

    if len(strong_matches) == 1:
        return {"result": "ONE_MATCH", "employee": to_employee(strong_matches[0])}
    elif len(strong_matches) > 1:
        return {"result": "MULTIPLE_MATCHES", "candidates": [to_employee(m) for m in strong_matches]}
    elif len(weak_matches) == 1:
            return {"result": "ONE_MATCH", "employee": to_employee(weak_matches[0])}
    elif len(weak_matches) > 1:
        # weak matches still get treated as a real selection scenario, not "no match"
        return {"result": "MULTIPLE_MATCHES", "candidates": [to_employee(m) for m in weak_matches]}
    else:
        # truly nothing matched, even weakly — fall back to full directory
        return {"result": "NO_MATCH", "candidates": [to_employee((e.name, 0, 0)) for e in employees]}