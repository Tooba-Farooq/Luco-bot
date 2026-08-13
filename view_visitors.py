from app.database import SessionLocal
from app.models_db import Visitor

db = SessionLocal()
visitors = db.query(Visitor).order_by(Visitor.id.desc()).all()

if not visitors:
    print("No visitors found.")
else:
    for v in visitors:
        embedding_status = "yes" if v.face_embedding else "no"
        print(f"[{v.id}] {v.name} | photo: {v.photo_path} | embedding: {embedding_status} | created: {v.created_at}")

db.close()

# from app.database import SessionLocal
# from app.models_db import Visitor

# db = SessionLocal()

# visitor_id = int(input("Enter visitor ID to update: ").strip())

# visitor = db.query(Visitor).filter(Visitor.id == visitor_id).first()

# if not visitor:
#     print(f"No visitor found with id={visitor_id}")
#     db.close()
#     exit()

# print(f"Current: [{visitor.id}] {visitor.name} | photo: {visitor.photo_path}")

# new_name = input("Enter new name: ").strip()

# if not new_name:
#     print("Name cannot be empty — cancelled.")
#     db.close()
#     exit()

# confirm = input(f"Rename '{visitor.name}' -> '{new_name}'? Type 'yes' to confirm: ").strip()

# if confirm.lower() == "yes":
#     visitor.name = new_name
#     db.commit()
#     print("Updated.")
# else:
#     print("Cancelled.")

# db.close()