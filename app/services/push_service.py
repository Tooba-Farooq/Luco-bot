import firebase_admin
from firebase_admin import credentials, messaging
from app.models_db import Employee

cred = credentials.Certificate("app/luco-bot-firebase-adminsdk-fbsvc-23a758c6d4.json")
firebase_admin.initialize_app(cred)


async def send_host_alert(employee: Employee, visitor_name: str, visitor_photo_url: str, purpose: str, session_id: str):
    if not employee.device_token:
        print(f"[push_service] No device_token for employee {employee.id} — cannot alert.")
        return False

    message = messaging.Message(
        notification=messaging.Notification(
            title=f"{visitor_name} is here to see you",
            body=purpose,
            image=visitor_photo_url,
        ),
        data={"session_id": session_id},
        token=employee.device_token,
    )

    try:
        response = messaging.send(message)
        print(f"[push_service] Push sent: {response}")
        return True
    except Exception as e:
        print(f"[push_service] Failed to send push: {e}")
        return False