import firebase_admin
from firebase_admin import credentials, messaging
from app.models_db import Employee

cred = credentials.Certificate("app/luco-bot-bb3ff73ea10d.json")
firebase_admin.initialize_app(cred)


async def send_host_alert(employee: Employee, visitor_name: str, visitor_photo_url: str, purpose: str, session_id: str):
    if not employee.device_token:
        print(f"[push_service] No device_token for employee {employee.id} — cannot alert.")
        return False

    message = messaging.Message(
        notification=messaging.Notification(
            title=f"{visitor_name} is here to see you",
            body=purpose,
            # image intentionally omitted — tray notifications crop to a circle,
            # which looks wrong for visitor photos. Full photo is sent via `data`
            # instead, for the app to render properly once opened.
        ),
        data={
            "session_id": session_id,
            "visitor_photo_url": visitor_photo_url or "",
        },
        token=employee.device_token,
    )

    try:
        response = messaging.send(message)
        print(f"[push_service] Push sent: {response}")
        return True
    except Exception as e:
        print(f"[push_service] Failed to send push: {e}")
        return False