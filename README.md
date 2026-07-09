## Running the server (every time)

**Important:** you must use `--host 0.0.0.0`, not just `--reload` — otherwise the tablet (a separate device) cannot reach the server, only your own PC can.


uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload


## Finding your PC's IP address (for Unity to connect to)

1. Open Command Prompt or PowerShell
2. Run:

   ipconfig

3. Look for **IPv4 Address** under your active WiFi adapter .
4. This is the IP the Unity app needs to point to — update it in `FaceDetectionService`'s `Base Url` field, formatted as:
http://<your-ip>:8000