import qrcode
import base64
import io
import os

VISITOR_STATUS_BASE_URL = os.getenv("VISITOR_STATUS_BASE_URL", "https://lucobot-visit-status.netlify.app")


def generate_status_qr_base64(status_token: str) -> str:
    url = f"{VISITOR_STATUS_BASE_URL}/?token={status_token}"
    img = qrcode.make(url)
    buf = io.BytesIO()
    img.save(buf, format="PNG")
    return base64.b64encode(buf.getvalue()).decode("utf-8")