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

Send a single camera frame. Call this endpoint repeatedly (every ~500ms–1s) while the tablet is active — each call is independent, backend tracks state internally between calls.

### Response shape

```json
{
  "status": "idle" | "detecting" | "known" | "unknown",
  "face_forward": true | false,
  "forward_duration": 0.0,
  "visitor_name": "Ahmed" | null,
  "confidence": 0.87 | null
}
```

### Status meanings — what Unity should do for each

| Status      | Meaning                                               | Suggested Unity behavior                   |
| ----------- | ----------------------------------------------------- | ------------------------------------------ |
| `idle`      | No face detected / no one there                       | Idle animation, no audio                   |
| `detecting` | Face detected, checking if forward-facing long enough | Idle animation                             |
| `known`     | Recognized visitor (3s+ forward-facing confirmed)     | Play greeting audio with `visitor_name`    |
| `unknown`   | Unrecognized visitor (3s+ forward-facing confirmed)   | Play hardcoded "How may I help you?" audio |

**For now**, since face recognition isn't wired up yet, `status` will only ever be `idle`, `detecting`, or `unknown` — never `known`. Treat `unknown` as your trigger to play the hardcoded greeting audio. This will start correctly returning `known` (with real names) once recognition is completed — the response shape won't change, so no rework needed on your end later.

### Polling guidance

- Send a frame roughly every 500ms–1s while camera is active.
- No need to hold state on the Unity side — backend tracks the forward-facing timer internally between calls.

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
