# Lucobot Backend

FastAPI backend for the reception robot — handles visitor detection, face recognition, and visitor/host interaction logic.

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

**Note:** the transcript field is unified as `answer_text` for both cases, but the audio still differs: known visitors get a name-specific dynamic greeting, while unknown visitors get the shared static greeting. Name capture no longer happens at this stage; it happens later, after purpose is captured (see `/session/respond` below).

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

## API: `/session/respond`

**Method:** `POST`
**Content-Type:** `multipart/form-data`
**Fields:** `session_id` (string), `audio` (file — WAV recommended)

Send a recorded audio clip of the visitor speaking. This is the main conversational endpoint — the same one is used at every step of the conversation (intent, host name, purpose, name capture). **What it does with the audio depends entirely on the session's current internal state** — Unity doesn't need to know or track this, just always send audio to this same endpoint whenever a response is expected.

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
  "state": "AWAITING_PURPOSE" | "HOST_SELECTION" | "HOST_SUGGESTIONS" | "NAME_CONFIRMATION" | "QUERY_ANSWERED" | "FALLBACK" | ...,
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

### State reference — what Unity should show/do

| `state`             | Meaning                                                     | Unity behavior                                                          |
| ------------------- | ----------------------------------------------------------- | ----------------------------------------------------------------------- |
| `AWAITING_PURPOSE`  | Host matched, or purpose being asked                        | Play audio, then record next response                                   |
| `HOST_SELECTION`    | Multiple people matched the spoken name                     | Play audio, **display `host_candidates` as tappable buttons**           |
| `HOST_SUGGESTIONS`  | No good match — showing suggestions or full directory       | Play audio, **display `host_candidates` as tappable buttons**           |
| `AWAITING_NAME`     | Purpose captured, now asking visitor's name                 | Play audio, then record next response                                   |
| `NAME_CONFIRMATION` | Backend heard a name, needs visitor to confirm it's correct | Display `heard_text`, show Yes/No buttons (see `/session/confirm-name`) |
| `QUERY_ANSWERED`    | General question was answered from the knowledge prompt     | Play `answer_text`, then continue conversation                          |
| `FALLBACK`          | Question couldn't be answered                               | Play fallback audio                                                     |

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

### Knowledge base for general queries

There's no separate knowledge-base database table — office info is inlined directly as a short text block in the LLM prompt (see `llm_service.py`). This is intentional: the content is small and doesn't need a full data model. To update what Robo knows, edit the `KNOWLEDGE_TEXT` string directly in that file.

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
