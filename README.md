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

| Status      | Meaning                                               | Suggested Unity behavior                           |
| ----------- | ----------------------------------------------------- | -------------------------------------------------- |
| `idle`      | No face detected / no one there                       | Idle animation, no audio                           |
| `detecting` | Face detected, checking if forward-facing long enough |Idle animation                                      |
| `known`     | Recognized visitor (3s+ forward-facing confirmed)     | Play greeting audio with `visitor_name`            |
| `unknown`   | Unrecognized visitor (3s+ forward-facing confirmed)   | Play hardcoded "How may I help you?" audio         |

**For now**, since face recognition isn't wired up yet, `status` will only ever be `idle`, `detecting`, or `unknown` — never `known`. Treat `unknown` as your trigger to play the hardcoded greeting audio. This will start correctly returning `known` (with real names) once recognition is completed — the response shape won't change, so no rework needed on your end later.

### Polling guidance

- Send a frame roughly every 500ms–1s while camera is active.
- No need to hold state on the Unity side — backend tracks the forward-facing timer internally between calls.


