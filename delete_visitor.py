from app.database import SessionLocal
from app.models_db import Visitor, VisitLog

db = SessionLocal()

mode = input("Delete [s]ingle visitor by ID, or [a]ll visitors? (s/a): ").strip().lower()

if mode == "a":
    all_visitors = db.query(Visitor).all()

    if not all_visitors:
        print("No visitors in the database — nothing to delete.")
        db.close()
        exit()

    all_logs = db.query(VisitLog).all()

    print(f"This will delete ALL {len(all_visitors)} visitor(s) and {len(all_logs)} associated VisitLog row(s):")
    for visitor in all_visitors:
        print(f"  - [{visitor.id}] {visitor.name} | photo: {visitor.photo_path}")

    confirm = input("Type 'DELETE ALL' to confirm (case-sensitive): ").strip()

    if confirm == "DELETE ALL":
        for log in all_logs:
            db.delete(log)
        for visitor in all_visitors:
            db.delete(visitor)
        db.commit()
        print(f"Deleted {len(all_visitors)} visitor(s) and {len(all_logs)} VisitLog row(s).")
    else:
        print("Cancelled — confirmation text did not match.")

elif mode == "s":
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

else:
    print("Invalid option — enter 's' or 'a'.")

db.close()