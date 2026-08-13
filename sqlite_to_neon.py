import sqlite3
from app.database import SessionLocal
from app.models_db import Employee, Visitor, VisitLog, VisitSession

sqlite_conn = sqlite3.connect("lucobot.db")
sqlite_conn.row_factory = sqlite3.Row

neon_db = SessionLocal()  # this now points at Neon, since DATABASE_URL was updated

def migrate_table(table_name, model_class):
    rows = sqlite_conn.execute(f"SELECT * FROM {table_name}").fetchall()
    for row in rows:
        record = model_class(**dict(row))
        neon_db.merge(record)  # merge instead of add, so re-running this script is safe
    neon_db.commit()
    print(f"Migrated {len(rows)} rows from {table_name}")

migrate_table("employees", Employee)
migrate_table("visitors", Visitor)
migrate_table("visit_logs", VisitLog)
migrate_table("visit_sessions", VisitSession)

sqlite_conn.close()
neon_db.close()
print("Done.")