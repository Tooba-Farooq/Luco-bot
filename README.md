# Lucobot Frontend

Unity front-end for the Lucobot visitor-reception kiosk. Talks to a Python (FastAPI/uvicorn) backend over HTTPS (currently ngrok) for face detection, conversation flow, and photo capture, and qr code.

---

## Prerequisites

- Python 3.x with the backend's dependencies installed (`pip install -r requirements.txt` or equivalent, from the backend repo)
- Unity (version matching the project settings) for building/running the frontend
- Host PC and tablet/phone on the **same WiFi network**
- Android device with camera + mic permissions (for on-device testing)

---

## Running the Server (every time)

```bash
uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload
```

**Important:** must use `--host 0.0.0.0`, not just `--reload` — otherwise the tablet (a separate device) cannot reach the server; only the host PC can.

---

### Updating the address in app

No rebuild needed — use the in-app IP settings panel (gear icon):

- Type the new IP
- Saved automatically via `PlayerPrefs`
- Format: just the address, e.g. `abcd-asweds-cacccc.ngrok-free.dev` (port/protocol handled automatically)

---

## Frontend — What's Done

### Core Systems

| Component | Description |
|---|---|
| **Android build pipeline** | Permissions (camera, mic, internet), tested working on real phone |
| **DeviceCheck.cs** | Runtime permission requests, starts camera + mic, passes camera feed to detection service |
| **FaceExpressionController.cs** | Full face animation system — idle blink loop, talking animation |
| **FaceDetectionService.cs** | Polls backend `/detect` endpoint every ~0.7s with the camera feed |
| **VisitorDetectionHandler.cs** | Maps backend detection status → face reactions, drives flow into conversation automatically once a visitor is detected; greeting caption synced to audio playback start/end (shows `answer_text`, hides when greeting audio ends) |
| **In-app backend IP config** | Settings panel (gear icon), persists between sessions |

### Conversational Flow

- **Full voice loop (unknown-visitor path):** detect → greeting → purpose → host confirm → name confirm → photo capture → QR — tested end-to-end
- **Known-visitor path:** correctly skips photo capture, goes straight to QR

### Host Selection / Name Confirmation


- **Host candidate selection panel:** tap a candidate to select/highlight, then explicit Confirm / Cancel buttons
  - Confirm → calls `/session/confirm-host`
  - Cancel is local-only (no backend cancel endpoint exists — `/session/cancel-host-selection` is 501)
- **Name confirmation:** `TMP_InputField`-based panel, pre-filled with heard name, editable, single Submit → `/session/submit-name`

### Photo Capture

- Fully rebuilt to be backend-driven: polls `/session/photo-frame`, auto-fires `/session/capture-photo` when `ready_to_capture` is true, handles 409 retry
- Square boundary frame (4-bar UI construction) replacing old single-line boundary; color-swap logic unchanged

### Stability Fixes
*(freeze-bug pattern: fall back to listening instead of dying silently)*

- No/empty audio recorded → `OnRecordingFailed` event
- Failed `/session/respond` HTTP request → same event
- Unknown/unhandled state string → default case falls back to `StartListening()`
- `FALLBACK` state → simplified to just `StartListening()` (backend now auto-routes `FALLBACK` → `AWAITING_INTENT`)

- [ ] Re-test full conversational loop (unknown- and known-visitor paths) against new backend
- [ ] Re-test host selection, name confirmation, and photo capture flows for endpoint/contract changes
- [ ] Merge `FrontendA27072026` back once stable, or document as the new baseline
