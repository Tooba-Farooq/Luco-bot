# Lucobot Backend

FastAPI backend for the reception robot — handles visitor detection, visitor face recognition, and visitor/host interaction logic.

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

4. Create a `.env` file at the project root with your Groq API key:
   GROQ_API_KEY=your_key_here

Get a key at [console.groq.com](https://console.groq.com). Used for speech-to-text (Whisper) and intent classification (Llama).

5. Run the server (from project root):

```bash
   uvicorn app.main:app --reload
```

6. Test via Swagger UI: `http://127.0.0.1:8000/docs`

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
  "answer_text": "Hi! How may I help you today?" | null,
  "audio_base64": "<base64 mp3>" | null,
  "audio_key": "unknown_greeting_v2" | null
}
```

### Status meanings — what Unity should do for each

| Status      | Meaning                                               | Suggested Unity behavior                                              |
| ----------- | ----------------------------------------------------- | --------------------------------------------------------------------- |
| `idle`      | No face detected / no one there                       | Idle animation, no audio, keep polling                                |
| `detecting` | Face detected, checking if forward-facing long enough | Idle animation, keep polling                                          |
| `known`     | Recognized visitor (3s+ forward-facing confirmed)     | Play greeting audio (`audio_base64`), **stop polling `/detect`**      |
| `unknown`   | Unrecognized visitor (3s+ forward-facing confirmed)   | Fetch + play greeting audio (`audio_key`), **stop polling `/detect`** |

**Note:** the transcript field is unified as `answer_text` for both cases, but the audio still differs: known visitors get a name-specific dynamic greeting, while unknown visitors get the shared static greeting. Name capture no longer happens at this stage; it happens later, after purpose is captured (see `/session/respond` below) — **and only for unknown visitors** (see the `AWAITING_PURPOSE` note below).

Recognition at this stage is against stored **visitors only**. Employee records are not part of the live `/detect` recognition path.

### ⚠️ Stop polling once `known` or `unknown` fires

`known` and `unknown` are terminal states for this endpoint — the interaction moves into the conversational flow (`/session/respond`) from here. **Unity should stop calling `/detect` at this point.** The backend also enforces this server-side — once a session starts, repeated `/detect` calls return the cached result instead of re-running recognition, but Unity should still stop polling on its end to avoid wasted requests.

Polling should only resume once the current visitor's interaction is fully done and the tablet returns to idle.

### `session_id`

Present once status is `known` or `unknown`. This same ID must be passed to every subsequent `/session/*` call for this visitor — it's how the backend knows which conversation a given request belongs to.

### Greeting audio: two delivery paths

| Field          | When present      | What it is                                       | How to use it                                      |
| -------------- | ----------------- | ------------------------------------------------ | -------------------------------------------------- |
| `audio_base64` | `status: known`   | Base64-encoded MP3 bytes, inline in the response | Decode and play directly — no extra request needed |
| `audio_key`    | `status: unknown` | A key identifying a static, pre-generated phrase | Fetch from `GET /audio/{key}`, then play it        |

**Why the split:** the known-visitor greeting contains the visitor's name, so it's generated live per-request. The unknown-visitor greeting is a fixed sentence, generated once at server startup and cached.

### API: `GET /audio/{key}`

Returns raw `audio/mpeg` bytes for a static, pre-cached phrase. **Unity should cache this locally after the first fetch** — `Cache-Control: public, max-age=31536000, immutable` is set to support this. Returns `404` if the key doesn't exist.

**Currently available static keys:**

| Key                         | Phrase                                                                        |
| --------------------------- | ----------------------------------------------------------------------------- |
| `unknown_greeting_v2`       | "Hi! How may I help you today?"                                               |
| `qr_prompt`                 | "Please scan the QR code so we can continue on your phone." _(not wired yet)_ |
| `host_notified`             | "I've notified your host — please wait a moment." _(not wired yet)_           |
| `multiple_matches`          | "I found a few people matching that name — which one did you mean?"           |
| `no_match_with_suggestions` | "Sorry, I couldn't find anyone by that name. Did you mean one of these?"      |
| `no_match_no_suggestions`   | "Sorry, I couldn't find anyone by that name in our directory."                |
| `ask_purpose`               | "Please tell me the purpose of your meeting?"                                 |
| `ask_name`                  | "And what's your name?"                                                       |
| `ready_for_handoff`         | "Thanks — I'll let them know you're here."                                    |
| `didnt_catch_that`           | "Sorry, I didn't quite catch that — could you say it again?"                 |

## API: `/session/respond`

**Method:** `POST`
**Content-Type:** `multipart/form-data`
**Fields:** `session_id` (string), `audio` (file — WAV recommended)

Send a recorded audio clip of the visitor speaking. This is the main conversational endpoint — the same one is used at every step of the conversation (intent, host name, purpose, and name capture). **What it does with the audio depends entirely on the session's current internal state** — Unity doesn't need to know or track this, just always send audio to this same endpoint whenever a response is expected.

When the session is waiting for the visitor's name, the backend now uses a dedicated name-resolution path instead of the generic transcription path. It transcribes the clip in English and Urdu, then normalizes the result into a Roman-script name before showing the confirmation screen.

### ⚠️ How to know when to stop recording and send the audio

The visitor doesn't announce when they're done talking — Unity has to detect this itself. Recommended approach:

1. Start recording as soon as the previous prompt's audio finishes playing.
2. Continuously monitor microphone input volume. Once volume drops below a silence threshold for **~1.5–2 seconds** after speech was detected, stop recording and send what's been captured.
3. **Always enforce a hard maximum recording length as a safety cap** — around 10–15 seconds — so a mic that fails to detect silence (background noise, technical glitch) doesn't record indefinitely. If the max is hit, stop and send whatever was recorded regardless.
4. Do not use a fixed recording duration with no silence detection (e.g. "always record exactly 4 seconds") — this either cuts visitors off mid-sentence or leaves awkward dead air if they finish early. Silence detection plus a max-length safety cap is the correct combination, not either alone.

This entire behavior is Unity-side — the backend has no way to know when someone has "stopped talking" other than receiving a complete audio file.

### Response shape

```json
{
  "session_id": "abc123",
  "state": "AWAITING_PURPOSE" | "HOST_SELECTION" | "AWAITING_NAME" | "NAME_CONFIRMATION" | "AWAITING_PHOTO" | "READY_FOR_HANDOFF" | "QUERY_ANSWERED" | "FALLBACK" | ...,
  "heard_text": "I want to meet Ahmed",
  "detected_lang": "en" | "ur",
  "answer_text": "The washroom is on the 2nd floor." | null,
  "matched_host": {"id": 3, "name": "Ahmed Khan", "floor_room": "3rd Floor"} | null,
  "host_candidates": [{"id": 3, "name": "..."}, ...] | null,
  "audio_base64": "<base64 mp3>" | null,
  "audio_key": "no_match_with_suggestions" | null
}
```

Same audio-delivery rule as `/detect`: if `audio_base64` is present, play it directly; if `audio_key` is present, fetch it from `GET /audio/{key}` first.

### ⚠️ `AWAITING_PURPOSE` branches based on known vs. unknown visitor

Once the visitor states their purpose, the backend checks whether the original `/detect` call recognized them:

- **Known visitor** → their name and photo are already on file. Session moves straight to `READY_FOR_HANDOFF`, skipping name/photo capture entirely.
- **Unknown visitor** → session moves to `AWAITING_NAME` to begin name + photo capture.

**Unity does not need to track or check which case applies** — just follow whatever `state` comes back in the response, same as everywhere else in this flow.

### State reference — what Unity should show/do

| `state`             | Meaning                                                                                                                                                                                                                        | Unity behavior                                                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| ------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `AWAITING_PURPOSE`  | Host confirmed, purpose now being asked                                                                                                                                                                                        | Play audio, then record next response                                                                                                                                                                                                                                                                                                                                                                                                                                                                      |
| `HOST_SELECTION`    | Host name processed — matched (1+ candidates) or unmatched with fallback suggestions/full directory (1+ candidates), or truly nothing to offer (0 candidates). `audio_key`/`answer_text` tells the visitor which case applies. | **Always show Confirm + Cancel buttons when `host_candidates` is non-empty.** If `host_candidates.length == 1`, show that single name with Confirm/Cancel — visitor taps Confirm directly. If `host_candidates.length > 1`, show all names as **tappable options** — visitor selects one, then taps Confirm (Confirm stays disabled/inactive until a selection is made). If `host_candidates` is empty, there's nothing to confirm — just play the audio and backend will return to anything else branch . |
| `AWAITING_NAME`     | Purpose captured, unknown visitor — now asking for their name                                                                                                                                                                  | Play audio, then record next response. This response is passed through the name-specific transcription flow before the backend asks for confirmation.                                                                                                                                                                                                                                                                                                                                                     |
| `NAME_CONFIRMATION` | Backend heard a name — visitor confirms or edits it                                                                                                                                                                            | Show name input **pre-filled with `heard_text`**, editable. Submit button calls `/session/submit-name` regardless of whether text was changed.                                                                                                                                                                                                                                                                                                                                                             |
| `AWAITING_PHOTO`    | Name submitted — now capturing the visitor's photo                                                                                                                                                                             | Open camera view. See `/session/photo-frame` and `/session/capture-photo` below.                                                                                                                                                                                                                                                                                                                                                                                                                           |
| `READY_FOR_HANDOFF` | Purpose (and name/photo, if unknown) fully captured                                                                                                                                                                            | _(Next steps — QR handoff / host alert — not yet built)_                                                                                                                                                                                                                                                                                                                                                                                                                                                   |
| `QUERY_ANSWERED`    | General question was answered from the knowledge prompt                                                                                                                                                                        | Play `answer_text`, then continue conversation                                                                                                                                                                                                                                                                                                                                                                                                                                                             |
| `FALLBACK`          | Question couldn't be answered                                                                                                                                                                                                  | Play fallback audio (`didnt_catch_that`)                                                                                                                                                                                                                                                                                                                                                                                                                                                                    |

### API: `/session/confirm-host`

**Method:** `POST` (JSON body)
**Fields:** `session_id`, `employee_id`

Called when the visitor confirms a host from `host_candidates` after `HOST_SELECTION`. Moves the session to `AWAITING_PURPOSE`.

**UI behavior:**

- **1 candidate** → backend asks the visitor to confirm the single matched name; tapping **Confirm** calls this endpoint with that candidate's ID.
- **2+ candidates** → backend shows all matches; visitor selects one, then taps **Confirm**, which calls this endpoint with the selected ID.
- Cancel is visible in both cases but **not yet wired** — see `/session/cancel-host-selection` below.

### API: `/session/cancel-host-selection`

**Method:** `POST` (JSON body)
**Field:** `session_id`

⚠️ **Not implemented — currently returns `501 Not Implemented`.** Placeholder for future Cancel-button behavior. Do not wire the Cancel button to expect real behavior from this yet.

### API: `/session/submit-name`

**Method:** `POST` (JSON body)
**Fields:** `session_id`, `name`

Called when the visitor submits their name during `NAME_CONFIRMATION`. **UI note:** the name input should be pre-filled with the heard name (`heard_text` from the response that set this state) and left editable — the visitor can submit as-is if correct, or edit it first if wrong, then submit either way. There is no separate "confirm vs. retype" branch; this single endpoint handles both cases identically, since the backend doesn't need to know whether the text was edited.

Moves the session to `AWAITING_PHOTO`.

### API: `/session/photo-frame`

**Method:** `POST`
**Content-Type:** `multipart/form-data`
**Fields:** `session_id`, `frame` (image file)

Poll this endpoint continuously (same cadence as `/detect`, roughly every 200–500ms) while `AWAITING_PHOTO` is active and the camera view is open. Backend checks each frame for face presence, forward-facing angle, and centering, and tracks how long the frame has stayed "good" continuously.

**Response shape:**

```json
{
  "face_found": true,
  "is_forward": true,
  "is_centered": true,
  "ready_to_capture": true
}
```

**Unity behavior:**

- Boundary is **red** whenever `face_found` is `false`, or `is_forward`/`is_centered` is `false`.
- Boundary turns **green** when `face_found`, `is_forward`, and `is_centered` are all `true`.
- **`ready_to_capture: true`** means the frame has stayed good continuously for the required hold duration (~1 second) — this is backend-tracked, not something Unity needs to time itself. The instant this flips `true`, call `/session/capture-photo` with the current frame.
- If the visitor moves out of position at any point, the hold timer resets server-side automatically — Unity doesn't need to manage this either, just keep polling and reading `ready_to_capture`.

### API: `/session/capture-photo`

**Method:** `POST`
**Content-Type:** `multipart/form-data`
**Fields:** `session_id`, `frame` (image file)

Called once `ready_to_capture` is `true` from `/session/photo-frame`. Backend re-verifies face quality server-side (even though the client already saw green — never trust client-side checks alone), runs a blur check, and if everything passes: saves the photo, generates a face embedding, creates the `Visitor` record, and creates the `VisitLog` record for this visit (see note below on `VisitLog` timing).

**On success** → moves to `READY_FOR_HANDOFF`, returns the next prompt.

**On failure** (blurry, face check failed server-side) → returns `409 Conflict` with a reason. Unity should show a brief retry message and resume polling `/session/photo-frame`.

### `VisitLog` is created at photo-capture time, not at the very end

This is intentional: the `VisitLog` row (and the `Visitor` row it references) is created as soon as the visitor's identity is captured — right after a successful `/session/capture-photo` for unknown visitors, or right after purpose capture for known visitors — with `status: "in_progress"`. This ensures there's a traceable record of the visitor entering the building even if the interaction is later abandoned (host never responds, visitor leaves) before reaching a final outcome. The row is then **updated**, not recreated, once the visit concludes (see `/session/close`, not yet built).

### Knowledge base for general queries

There's no separate knowledge-base database table — office info is inlined directly as a short text block in the LLM prompt (see `llm_service.py`). This is intentional: the content is small and doesn't need a full data model. To update what Robo knows, edit the `KNOWLEDGE_TEXT` string directly in that file.

## API: `/employees`

**Method:** `POST`
**Content-Type:** `multipart/form-data`

Registers a new employee and stores their profile details for admin/host-directory use. This is an admin-facing endpoint (not called by Unity) — **live tablet recognition does not use employee records anymore**; the `/detect` flow matches against stored visitors only.

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

- **`embedding_created: true`** → A face embedding was generated and stored with the employee record.
- **`embedding_created: false`** → **Show a warning to the admin submitting this form.** The employee record and photo were saved successfully, but no face embedding could be generated from the uploaded photo. Suggested warning copy for the frontend:

  > ⚠️ Employee saved, but no face could be detected in the uploaded photo. Consider re-uploading a clearer, front-facing photo without face coverings.

  Don't block the admin from proceeding — this is a soft warning, not an error. The registration itself succeeded; only the stored embedding is missing.

### Why this design

Face recognition is treated as a bonus capability layered on top of a valid visitor record, not an employee record — live recognition now resolves visitors only, and employee data stays separate for the admin/host flow.

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
