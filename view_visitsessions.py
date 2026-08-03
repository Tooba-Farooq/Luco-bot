# # check_sessions.py
# import sqlite3
# conn = sqlite3.connect("lucobot.db")
# rows = conn.execute("""
#     SELECT session_id, selected_host_id, host_response, is_closed, created_at
#     FROM visit_sessions
#     ORDER BY created_at DESC
# """).fetchall()
# for r in rows:
#     print(r)

# # check_me.py
# import sqlite3
# conn = sqlite3.connect("lucobot.db")
# rows = conn.execute("SELECT id, employee_code, name FROM employees").fetchall()
# for r in rows:
#     print(r)


# debug_pending.py
# debug_pending2.py
import sqlite3
conn = sqlite3.connect("lucobot.db")
rows = conn.execute("""
    SELECT session_id, selected_host_id, host_response, is_closed
    FROM visit_sessions
    WHERE selected_host_id = 2
""").fetchall()
print(f"Total for host 2: {len(rows)}")
for r in rows:
    print(r)