import requests

BASE_URL = "http://127.0.0.1:8000"
LOGIN_URL = f"{BASE_URL}/auth/login"
PENDING_URL = f"{BASE_URL}/host/pending-alerts"
RESPOND_URL = f"{BASE_URL}/host/respond"

access_token = None


def login():
    global access_token
    employee_code = input("Employee code (e.g. EMP-07): ").strip()
    password = input("Password: ").strip()

    response = requests.post(
        LOGIN_URL,
        data={"grant_type": "password", "username": employee_code, "password": password},
    )
    if response.status_code != 200:
        print(f"Login failed [{response.status_code}]: {response.text}")
        return False

    access_token = response.json()["access_token"]
    print("Logged in.\n")
    return True


def auth_headers():
    return {"Authorization": f"Bearer {access_token}"}


def fetch_pending():
    response = requests.get(PENDING_URL, headers=auth_headers())
    if response.status_code != 200:
        print(f"Error fetching pending alerts [{response.status_code}]: {response.text}")
        return []

    alerts = response.json().get("pending", [])
    if not alerts:
        print("No pending alerts.\n")
        return []

    print("\nPending alerts:")
    for i, alert in enumerate(alerts):
        print(f"  [{i}] {alert['visitor_name']} — {alert['purpose']} "
              f"(session={alert['session_id'][:8]}, host_response={alert['host_response']})")
    return alerts


def respond(session_id: str):
    print("\nResponse options: 1) Send in  2) Not available  3) Wait")
    choice = input("Choose 1/2/3: ").strip()

    payload = {"session_id": session_id}

    if choice == "1":
        payload["response"] = "available"

    elif choice == "2":
        payload["response"] = "not_available"
        has_time = input("Give a specific return date/time? (y/n): ").strip().lower()
        if has_time == "y":
            date_str = input("Enter as YYYY-MM-DD HH:MM (24h): ").strip()
            payload["available_again_at"] = date_str.replace(" ", "T")

    elif choice == "3":
        payload["response"] = "wait"
        minutes = input("Wait how many minutes?: ").strip()
        payload["wait_minutes"] = int(minutes)

    else:
        print("Invalid choice.")
        return

    response = requests.post(RESPOND_URL, json=payload, headers=auth_headers())
    if response.status_code != 200:
        print(f"Respond failed [{response.status_code}]: {response.text}")
        return

    data = response.json()
    print(f"\nRecorded. host_response={data['host_response']}")
    print(f"Visitor will see: \"{data.get('visitor_message')}\"\n")


def main():
    if not login():
        return

    while True:
        alerts = fetch_pending()
        if not alerts:
            cont = input("Press Enter to re-check, or 'q' to quit: ").strip()
            if cont.lower() == "q":
                break
            continue

        idx = input("Enter index to respond to, 'r' to refresh, or 'q' to quit: ").strip()
        if idx.lower() == "q":
            break
        if idx.lower() == "r":
            continue
        if not idx.isdigit() or int(idx) >= len(alerts):
            print("Invalid index.")
            continue

        respond(alerts[int(idx)]["session_id"])


if __name__ == "__main__":
    main()