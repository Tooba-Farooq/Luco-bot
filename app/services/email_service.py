import os
import sib_api_v3_sdk
from sib_api_v3_sdk.rest import ApiException

BREVO_API_KEY = os.getenv("BREVO_API_KEY")
SENDER_EMAIL = os.getenv("SENDER_EMAIL")  # must match your verified sender in Brevo
SENDER_NAME = os.getenv("SENDER_NAME", "Lucobot")
ACTIVATION_BASE_URL = os.getenv("ACTIVATION_BASE_URL")  # e.g. https://luco-bot-activation.netlify.app/

configuration = sib_api_v3_sdk.Configuration()
configuration.api_key['api-key'] = BREVO_API_KEY


def send_invite_email(to_email: str, employee_name: str, invite_token: str, employee_code: str) -> bool:
    if not to_email:
        print("[email_service] No email on file for this employee — skipping invite email.")
        return False

    activation_link = f"{ACTIVATION_BASE_URL}?token={invite_token}"

    api_instance = sib_api_v3_sdk.TransactionalEmailsApi(sib_api_v3_sdk.ApiClient(configuration))

    send_smtp_email = sib_api_v3_sdk.SendSmtpEmail(
        to=[{"email": to_email, "name": employee_name}],
        sender={"email": SENDER_EMAIL, "name": SENDER_NAME},
        subject="Activate your Lucobot account",
        html_content=f"""
            <p>Hi {employee_name},</p>
            <p>You've been added to Lucobot. Click below to set your password and activate your account:</p>
            <p><a href="{activation_link}">Activate your account</a></p>
            <p>Your employee ID (used to log in) is: <strong>{employee_code}</strong></p>
            <p>This link expires in 7 days.</p>
        """,
    )

    try:
        response = api_instance.send_transac_email(send_smtp_email)
        print(f"[email_service] Invite sent to {to_email}, message id: {response.message_id}")
        return True
    except ApiException as e:
        print(f"[email_service] Failed to send invite to {to_email}: {e}")
        return False