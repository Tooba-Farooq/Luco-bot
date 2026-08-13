import json
from app.database import SessionLocal
from app.models_db import Visitor

db = SessionLocal()

visitors = db.query(Visitor).filter(Visitor.face_embedding.isnot(None)).all()
fixed = 0

for v in visitors:
    if isinstance(v.face_embedding, str):
        try:
            parsed = json.loads(v.face_embedding)
            if isinstance(parsed, list) and len(parsed) > 0:
                v.face_embedding = parsed
                fixed += 1
                print(f"Fixed visitor_id={v.id} name={v.name!r} — {len(parsed)} dims")
            else:
                print(f"visitor_id={v.id} name={v.name!r} — parsed but not a valid list, skipping")
        except (json.JSONDecodeError, TypeError) as e:
            print(f"visitor_id={v.id} name={v.name!r} — could not parse: {e}")

db.commit()
db.close()
print(f"\nDone. Fixed {fixed} visitor(s).")