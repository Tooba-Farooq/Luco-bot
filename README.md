# Lucobot Backend

FastAPI backend for the reception robot — handles visitor detection, face recognition, and (soon) visitor/host interaction logic.

## Setup

1. Create and activate a virtual environment:

```bash
   python -m venv venv
   venv\Scripts\activate      # Windows
```

2. Install dependencies:

```bash
   pip install -r requirements.txt
```

3. Download required model files (not included in repo due to size) — run from project root:

```bash
   curl -o blaze_face_short_range.tflite https://storage.googleapis.com/mediapipe-models/face_detector/blaze_face_short_range/float16/1/blaze_face_short_range.tflite
   curl -o face_landmarker.task https://storage.googleapis.com/mediapipe-models/face_landmarker/face_landmarker/float16/latest/face_landmarker.task
```

4. Run the server (from project root):

```bash
   uvicorn app.main:app --reload
```

## API: `/detect`

**Method:** `POST`
**Content-Type:** `multipart/form-data`
**Field name:** `frame` (image file — JPEG/PNG)

Send a single camera frame. Call this endpoint repeatedly (every ~500ms–1s) while the tablet is in idle/detecting state — each call is independent, backend tracks state internally between calls.

### Response shape

```json
{
  "status": "idle" | "detecting" | "known" | "unknown",
  "face_forward": true | false,
  "forward_duration": 0.0,
  "visitor_name": "Ahmed" | null,
  "confidence": 0.87 | null,
  "session_id": "abc123" | null,
  "audio_base64": "<base64 mp3>" | null,
  "audio_key": "unknown_greeting" | null
}
```

### Status meanings — what Unity should do for each

| Status      | Meaning                                               | Suggested Unity behavior                                              |
| ----------- | ----------------------------------------------------- | --------------------------------------------------------------------- |
| `idle`      | No face detected / no one there                       | Idle animation, no audio, keep polling                                |
| `detecting` | Face detected, checking if forward-facing long enough | Idle animation, keep polling                                          |
| `known`     | Recognized visitor (3s+ forward-facing confirmed)     | Play greeting audio (`audio_base64`), **stop polling `/detect`**      |
| `unknown`   | Unrecognized visitor (3s+ forward-facing confirmed)   | Fetch + play greeting audio (`audio_key`), **stop polling `/detect`** |

### ⚠️ Stop polling once `known` or `unknown` fires

`known` and `unknown` are terminal states for this endpoint — they mean a person has been identified (or confirmed unidentifiable) and the interaction is moving into the greeting/name-capture flow. **Unity should stop calling `/detect` at this point** and move to whatever endpoint handles the next step (name capture, intent, etc. — not yet built). Continuing to poll `/detect` after this point will keep re-running detection and recognition unnecessarily and is not part of the intended flow.

Polling should only resume once the current visitor's interaction is fully done and the tablet returns to idle (e.g. after `VISIT_LOGGED` or a QR handoff).

### `session_id`

Present once status is `known` or `unknown`. Use this to guard against re-triggering the greeting if `/detect` is somehow called again before Unity has moved off this state — the same `session_id` should only produce one greeting playback.

### Greeting audio: two delivery paths

There are two different mechanisms depending on whether the greeting is a fixed phrase or contains visitor-specific data:

| Field          | When present      | What it is                                                                       | How to use it                                                            |
| -------------- | ----------------- | -------------------------------------------------------------------------------- | ------------------------------------------------------------------------ |
| `audio_base64` | `status: known`   | Base64-encoded MP3 bytes, inline in the response                                 | Decode and play directly — no extra request needed                       |
| `audio_key`    | `status: unknown` | A key identifying a **static, pre-generated** phrase (e.g. `"unknown_greeting"`) | Fetch the actual audio from `GET /audio/{key}` (see below), then play it |

**Why the split:** the "unknown visitor" greeting is the same fixed sentence every time, so it's generated once at server startup and served as a static file — this avoids re-running TTS generation on every visitor. The "known visitor" greeting contains the visitor's name, so it's different every time and can't be pre-cached — it's generated live and sent inline instead of requiring a second round-trip.

### API: `GET /audio/{key}`

Returns raw `audio/mpeg` bytes for a static, pre-cached greeting phrase (e.g. `GET /audio/unknown_greeting`).

**⚠️ Unity should cache this file locally after the first fetch.** The backend serves the same bytes for a given `key` every time — nothing about it changes between requests. Fetching it fresh on every `unknown` detection wastes bandwidth and adds unnecessary latency. Recommended approach: fetch once, store the resulting `AudioClip` in memory (or on disk) keyed by `audio_key`, and reuse it for all future `unknown` greetings without hitting this endpoint again. The response includes `Cache-Control` headers to support this if your HTTP client respects them.

Returns `404` if the key doesn't match a known cached phrase.

Cache-Control is set to `public, max-age=31536000, immutable` — safe because each `audio_key` maps to a fixed phrase that never changes at runtime. If you ever edit a static phrase's wording in `tts_service.py`, use a new key (e.g. `unknown_greeting_v2`) rather than reusing the old one, since aggressively-cached clients won't know to drop stale copies otherwise.

### Polling guidance

- Send a frame roughly every 500ms–1s while camera is active and status is `idle` or `detecting`.
- No need to hold state on the Unity side — backend tracks the forward-facing timer internally between calls.
- Stop polling once `known` or `unknown` is received (see above).

## API: `/employees`

**Method:** `POST`
**Content-Type:** `multipart/form-data`

Registers a new employee, including their face embedding for recognition. This is an admin-facing endpoint (not called by Unity) — used to onboard employees so Lucobot can recognize them at the tablet.

### Request fields

| Field          | Type   | Required | Notes                                |
| -------------- | ------ | -------- | ------------------------------------ |
| `name`         | string | Yes      |                                      |
| `floor_room`   | string | No       | e.g. `"3rd Floor, Room 204"`         |
| `phone_number` | string | No       |                                      |
| `email`        | string | No       |                                      |
| `photo`        | file   | Yes      | Clear, front-facing photo (JPEG/PNG) |

### Response shape

```json
{
  "id": 12,
  "name": "Ahmed Khan",
  "floor_room": "3rd Floor, Room 204",
  "phone_number": "0300-1234567",
  "email": "ahmed@company.com",
  "photo_path": "employee_photos/a1b2c3d4.jpg",
  "embedding_created": true
}
```

### ⚠️ Important: check `embedding_created` on every response

The employee record is **always created**, even if a face embedding could not be generated from the uploaded photo (e.g. the photo has no clearly detectable face, poor lighting/angle, or the face is significantly covered/obscured). Registration will not fail or return an error in this case — `embedding_created` is your only signal.

- **`embedding_created: true`** → Face recognition will work for this employee at the tablet. Nothing further needed.
- **`embedding_created: false`** → **Show a warning to the admin submitting this form.** The employee record and photo were saved successfully, but Lucobot will **not** be able to visually recognize this person — they'll always be treated as an unrecognized visitor at the tablet (prompted for their name like anyone else) rather than greeted by name. Suggested warning copy for the frontend:

  > ⚠️ Employee saved, but no face could be detected in the uploaded photo. Ahmed Khan will not be automatically recognized by Lucobot — consider re-uploading a clearer, front-facing photo without face coverings to enable recognition.

  Don't block the admin from proceeding — this is a soft warning, not an error. The registration itself succeeded; only the recognition _capability_ is missing. If the admin wants recognition enabled, they'll need to re-submit with a different photo (there's currently no separate "update photo" endpoint — re-registering is the workaround until one exists).

### Why this design

Face recognition is treated as a bonus capability layered on top of a valid employee record, not a requirement for one — this keeps the system consistent with how it already handles unrecognized visitors elsewhere (gracefully falls back to manual identification rather than hard-failing).

## Troubleshooting

### `AttributeError: module 'cv2' has no attribute 'CascadeClassifier'`

This means a broken/incompatible opencv version got installed (seen with `opencv-python==5.0.0.93`, which is not a stable release). `requirements.txt` pins a known-good version, but if you hit this anyway (e.g. after a manual install or version bump), fix it with:

```bash
pip uninstall opencv-python opencv-python-headless opencv-contrib-python opencv-contrib-python-headless -y
pip install opencv-contrib-python==4.10.0.84
```

Only one opencv package should be installed at a time — having more than one installed simultaneously can cause partial/broken imports. Verify with:

```bash
pip list | findstr opencv    # Windows
pip list | grep opencv       # Mac/Linux
```

Should show exactly one line: `opencv-contrib-python 4.10.0.84`.

### `haarcascade_frontalface_default.xml` not found / opencv detector fails to load

Confirm the cascade file path resolves correctly:

```python
import cv2
print(cv2.data.haarcascades)
```

If this errors or points to an empty/missing folder, reinstalling opencv-contrib-python per the fix above should resolve it — the cascade files ship bundled with the package. If the folder exists but the specific file is missing, download it directly and place it in that exact folder:

```bash
curl -o haarcascade_frontalface_default.xml https://raw.githubusercontent.com/opencv/opencv/master/data/haarcascades/haarcascade_frontalface_default.xml
```
