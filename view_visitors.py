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