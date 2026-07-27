from app.database import SessionLocal
from app.models_db import Visitor, VisitLog

db = SessionLocal()

visitor_id = int(input("Enter visitor ID to delete: ").strip())

visitor = db.query(Visitor).filter(Visitor.id == visitor_id).first()

if not visitor:
    print(f"No visitor found with id={visitor_id}")
    db.close()
    exit()

related_logs = db.query(VisitLog).filter(VisitLog.visitor_id == visitor_id).all()

print(f"Deleting: [{visitor.id}] {visitor.name} | photo: {visitor.photo_path}")
if related_logs:
    print(f"This visitor has {len(related_logs)} associated VisitLog row(s), which will also be deleted:")
    for log in related_logs:
        print(f"  - VisitLog id={log.id} purpose={log.purpose!r} status={log.status}")

confirm = input("Type 'yes' to confirm: ").strip()

if confirm.lower() == "yes":
    for log in related_logs:
        db.delete(log)
    db.delete(visitor)
    db.commit()
    print("Deleted.")
else:
    print("Cancelled.")

db.close()