# Lucobot Frontend

## Running the server (every time)
uvicorn app.main:app --host 0.0.0.0 --port 8000 --reload
Important: must use `--host 0.0.0.0`, not just `--reload` — otherwise the tablet (a separate device) cannot reach the server, only the host PC can.


## Finding your PC's IP (for the Unity app to connect to)
1. Open Command Prompt or PowerShell
2. Run: `ipconfig`
3. Look for IPv4 Address under your active WiFi adapter
4. No rebuild needed to update — in-app IP settings panel (gear icon), type the new IP, saved via PlayerPrefs. Format: just the IP, e.g. `192.168.1.42` (port/protocol handled automatically).

## Frontend — What's Done

### Core systems
* Android build pipeline: permissions (camera, mic, internet), tested working on real phone
* DeviceCheck.cs: runtime permission requests, starts camera + mic, passes camera feed to detection service
* FaceExpressionController.cs: full face animation system — idle blink loop, 6 expressions (Idle, Listening, Happy, Thinking, Apologetic, Success), talking animation driven by live audio amplitude
* FaceDetectionService.cs: polls backend `/detect` endpoint every ~0.7s with the camera feed
* VisitorDetectionHandler.cs: maps backend detection status → face reactions, drives flow into conversation automatically once a visitor is detected; greeting caption now synced to audio playback start/end (shows `answer_text`, hides when greeting audio ends)
* In-app backend IP config: settings panel (gear icon), persists between sessions
* AudioRecorder.cs: real mic recording, silence detection + 15s hard cap — tested and confirmed working in isolation
* Full conversational voice loop (unknown-visitor path): detect → greeting → purpose → host confirm → name confirm → photo capture → QR — tested end-to-end
* Known-visitor path: correctly skips photo capture, goes straight to QR

### Host selection / name confirmation (previously blocked, now built)
* Host candidate selection panel: tap a candidate to select/highlight, then explicit **Confirm** / **Cancel** buttons — Confirm calls `/session/confirm-host`, Cancel is local-only (no backend cancel endpoint exists — `/session/cancel-host-selection` is 501)
* Name confirmation: `TMP_InputField`-based panel, pre-filled with heard name, editable, single Submit → `/session/submit-name`

### Photo capture
* Fully rebuilt to be backend-driven: polls `/session/photo-frame`, auto-fires `/session/capture-photo` when `ready_to_capture` is true, handles 409 retry
* Square boundary frame (4-bar UI construction) replacing old single-line boundary; color-swap logic unchanged


### Stability fixes (freeze-bug pattern: fall back to listening instead of dying silently)
* No/empty audio recorded → `OnRecordingFailed` event
* Failed `/session/respond` HTTP request → same event
* Unknown/unhandled `state` string → `default` case falls back to `StartListening()`
* `FALLBACK` state → simplified to just `StartListening()` (backend now auto-routes `FALLBACK` → `AWAITING_INTENT`)

### Listening indicator UI
* Rebuilt from a single static wave-icon image into a full panel: pulsing mic icon, "I'm listening..." text box, animated waveform bars (decorative, not mic-reactive — chosen for lower risk given time constraints)

## Pending — Backend Update Incoming
Backend received a significant update as of [27-07-2026/Monday]. Frontend work paused here; all current work committed to `[Frontend]` before switching to `FrontendA27072026` branch to safely adapt to new backend changes without risking Previous version.
