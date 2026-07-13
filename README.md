# Lucobot Frontend — Status Update

### Running the server (every time)
**Important:** must use `--host 0.0.0.0`, not just `--reload` — otherwise the tablet (a separate device) cannot reach the server, only the host PC can.
```bash
uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload
```

### Finding your PC's IP (for the Unity app to connect to)
1. Open Command Prompt or PowerShell
2. Run: `ipconfig`
3. Look for **IPv4 Address** under your active WiFi adapter
4. **No longer needs a Unity rebuild to update** — the app now has an in-app IP settings panel (tap the gear icon), type the new IP, and it's saved via PlayerPrefs. Format: just the IP, e.g. `192.168.1.42` (port/protocol handled automatically).

---

## Frontend — What's Done

### Core systems
- **Android build pipeline**: permissions (camera, mic, internet), tested working on real phone
- **DeviceCheck.cs**: runtime permission requests, starts camera + mic, passes camera feed to detection service
- **FaceExpressionController.cs**: full face animation system — idle blink loop, 6 expressions (Idle, Listening, Happy, Thinking, Apologetic, Success), talking animation driven by live audio amplitude
- **FaceDetectionService.cs**: polls backend `/detect` endpoint every ~0.7s with the camera feed
- **VisitorDetectionHandler.cs**: maps backend detection status → face reactions and drives the flow into `CollectName` automatically once an unrecognized visitor is detected
- **In-app backend IP config**: settings panel (gear icon) to type/save the backend's IP without needing to rebuild the app — persists between sessions

- Real camera capture (with retake) on the CapturePhoto screen
- Type/Voice choice on every input screen (voice currently stubbed — see below)
- Retry-count logic on host notification (2 attempts before showing Host Unavailable)
- Full loop tested end-to-end, including all 3 branches out of Host Unavailable
---

## What's Blocked on Backend (paused here, waiting)

- **Voice input** (all screens): currently shows "Listening..." then falls back to typing after 2 seconds — needs a `/transcribe` (or similar) speech-to-text endpoint
- **Host lookup / similar-names screens**: not built yet — needs a real host-directory API to look up against
- **Real host-notification logic**: `WaitingHostResponse` screen is fully built but currently always times out to "no response" — needs a real backend system to notify hosts and receive accept/unavailable/no-response back
