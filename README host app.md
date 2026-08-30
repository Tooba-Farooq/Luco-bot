# Lucobot — Host App Backend Reference

For whoever is building the host/employee-facing pieces. Two separate deliverables — read both sections.

---

## Deliverable 1: The mobile app

Employees log in with `employee_code` + password. No signup/activation screen in the app — see Deliverable 2 for that.

### Login

```
POST /auth/login
Content-Type: application/x-www-form-urlencoded

grant_type=password&username=EMP-07&password=<password>
```

Form-encoded (OAuth2 convention), not JSON. `username` = `employee_code`.

Response:

```json
{ "access_token": "<jwt>", "refresh_token": "<jwt>", "token_type": "bearer" }
```

- `access_token` — expires in 30 min. Send as `Authorization: Bearer <token>` on every request after login.
- `refresh_token` — expires in 30 days. Store securely (Keychain / Android Keystore).

### Register device (call right after login, and again if the push token ever changes)

```
POST /auth/register-device
Authorization: Bearer <access_token>
Content-Type: application/json

{ "device_token": "<FCM or APNs token>", "platform": "ios" | "android" }
```

One device per employee — registering a new token overwrites the old one. Nothing to revoke manually on device switch.

### Refresh an expired access token

```
POST /auth/refresh
Content-Type: application/json

{ "refresh_token": "<stored refresh token>" }
```

Returns a new `access_token`. Call on any `401`, then retry the original request.

### Who am I

```
GET /auth/me
Authorization: Bearer <access_token>
```

Returns `{ id, employee_code, name, photo_url, floor_room }`. `photo_url` is a full URL you can load directly into an image view; `null` if no photo was uploaded at registration. `floor_room` is a free-text string (e.g. `"3rd Floor, Room 12"`); `null` if not yet set.

### App screens

1. **Login** (first screen) → `/auth/login` → store tokens → `/auth/register-device`
2. **Home/idle** — where push alerts will land
3. _(Not built yet)_ Incoming visitor alert screen — Available Now / Notify Later / Not Available
4. _(Not built yet, optional)_ Visit history

### Getting test credentials

Two ways to get a working `employee_code` + password to log in with:

**Real flow (email is now automated):** ask the backend dev to create an employee record with your real email address — you'll receive an actual invite email with the activation link. Open it, set a password, you're done.

**Manual/Swagger flow (no email needed):**

1. `POST /employees` → response includes `employee_code` and `invite_token`. Copy the token.
2. `POST /auth/activate` with that `invite_token` + a chosen password.
3. `POST /auth/login` — note this is `application/x-www-form-urlencoded` (Swagger shows a form, not JSON, because of `OAuth2PasswordRequestForm`) — `username` = the `employee_code`, `password` = what you set.
4. Copy `access_token` → click Swagger's "Authorize" button → paste it → now `GET /auth/me` and `POST /auth/register-device` should work.

---

## Deliverable 2: The activation web page

**Not part of the app.** A standalone page, opened from an email link, on whatever device the employee checks email on. Build in plain HTML/JS or whatever's fastest — no framework required.

### What it does

1. Reads `token` from the URL query string. The domain/path is entirely up to you — wherever you build and host it (Netlify, Vercel, even localhost during dev). Example shape: `<wherever-you-host-it>/activate?token=abc123`
2. Shows a form: new password + confirm password
3. On submit, calls:

```
POST /auth/activate
Content-Type: application/json

{ "invite_token": "<token from URL>", "password": "<entered password>" }
```

4. **Success (200):**

```json
{ "detail": "Password set. You can now log in.", "employee_code": "EMP-07" }
```

Display the `employee_code` — tell the user it's their login ID for the app.

5. **Failure (400):**

```json
{ "detail": "Invalid or expired invite" }
```

Show as an error. Tokens expire after 7 days or after first use.

### Requirements

- Make the API base URL a config variable, not hardcoded — it'll change once we deploy.
- **Once built and hosted (anywhere — even localhost for now), send the backend dev the base URL** (e.g. `https://yoursite.com/activate`). That's the one thing they need from you — they append `?token=...` themselves when sending invite emails.
- ✅ **Done — already live**, hosted, sending real emails, and confirmed landing in inbox (not spam).

---

### Push notifications

Once registered, visitor alerts arrive via FCM automatically — no polling needed. Tested and working end to end.

Payload shape:

​`json
{
  "notification": { "title": "<visitor name> is here to see you", "body": "<purpose>" },
  "data": { "session_id": "<uuid>", "visitor_photo_url": "<url or empty string>" }
}
​`

**Tray notification is text-only by design** — no `image` field. Circular-cropped tray icons look wrong for visitor photos, so the photo isn't part of the OS-rendered notification at all. `visitor_photo_url` is sent only in `data`, for the app to load and display full-size once the visitor-response screen is shown. Treat an empty string as "no photo available."

Delivery behavior by app state:

- **Killed/backgrounded:** OS shows the (text-only) tray notification automatically. On tap, use `session_id` from the notification's data/intent extras to route to the visitor-response screen, then load `visitor_photo_url` there.
- **Foregrounded:** `onMessageReceived` fires immediately with the same payload — show your own in-app UI (banner/alert) rather than relying on a tray notification, since Android doesn't auto-display one while the app is in the foreground.

### Checking for missed alerts

Push is fire-and-forget — if the app was closed when it arrived and the tray notification was dismissed/missed, nothing tells the app it happened. Call this whenever the app opens or resumes, to catch anything unresolved regardless of whether a push was seen:

​`
GET /host/pending-alerts
Authorization: Bearer <access_token>
​`

Returns **all** unresolved visitor alerts for the logged-in host, not just the most recent — a host may have several waiting (e.g. after being in a meeting):

​`json
{
  "pending": [
    {
      "visitor_id": 123,
      "session_id": "...",
      "visitor_name": "...",
      "purpose": "...",
      "visitor_photo_url": "...",
      "arrived_at": "2026-08-02T10:15:00"
      "host_response": null,
      "wait_until": null
    }
  ]
}
​`

`pending` is `[]` if nothing's waiting. List is ordered oldest-first as a display hint (so the UI can naturally show "waiting longest" at top) — the host can act on any entry in any order, not necessarily top-to-bottom.
host_response is null for a brand-new alert, or "wait" if the host already deferred it. wait_until is null unless host_response is "wait", in which case it's an ISO timestamp.

### Message history

The host app can also load the message history endpoint:

```
GET /host/messages
Authorization: Bearer <access_token>
```

Response shape:

​`json
{
  "messages": [
    {
      "visitor_id": 123,
      "session_id": "...",
      "visitor_name": "Tooba Farooq",
      "visitor_photo_url": "...",
      "message_text": "...",
      "purpose": "...",
      "left_at": "2026-08-04T13:06:46.823468"
    }
  ]
}
​`

Use `visitor_id` as the stable grouping key when you want the frontend to show one chat thread per visitor across multiple visits. Keep `session_id` for visit-specific actions and traceability.

### Responding to an alert

​`
POST /host/respond
Authorization: Bearer <access_token>
Content-Type: application/json

{
"session_id": "...",
"response": "available" | "not_available" | "wait",
"wait_minutes": 20,
"available_again_at": "2026-08-05T15:00:00"
}
​`

- `response` maps to your three buttons: **Send In → `"available"`**, **Not Available → `"not_available"`**, **Wait → `"wait"`**.
- `wait_minutes` is **required only** when `response` is `"wait"` — omit it otherwise. Suggested picker values: 5 / 10 / 30 / 60 / 120 minutes, plus a custom numeric entry. Any positive integer is accepted.
- `available_again_at` is **optional**, only used when `response` is `"not_available"`. If the host wants to give the visitor a specific return date/time, send it as an ISO datetime; if omitted, the visitor just gets a generic "come back another time" message.
- A session belongs to whichever host it was routed to — responding to a `session_id` not assigned to the logged-in host returns `403`.

Success response:

​`json
{
  "detail": "Response recorded",
  "host_response": "wait",
  "wait_until": "2026-08-03T15:40:00Z",
  "available_again_at": null,
  "visitor_state": "HOST_ASKED_WAIT",
  "visitor_message": "Tooba Farooq is currently unavailable and has asked you to wait about 20 more minutes (until 3:40 PM)."
}
​`

`visitor_message` is the exact human-readable sentence shown to the visitor on their status page — same wording, pushed live over the WebSocket the moment you respond (see Deliverable 3). `visitor_state` is a machine-readable flag if you want to key off it in the app UI too (`PROCEED_TO_HOST`, `HOST_ASKED_WAIT`, `HOST_UNAVAILABLE`).

Errors: `404` if `session_id` doesn't exist, `403` if it's not this host's session, `400` if `response` is invalid or `wait_minutes` is missing/non-positive when `response` is `"wait"`.

Note on "Wait": choosing Wait does **not** remove the alert from `/host/pending-alerts` — see above. It's meant for cases like "I'm in a meeting, ask them to wait 30 min," so the host can still see and act on it early if they finish sooner than the wait duration. Selecting "Wait" again on the same session (e.g. extending the wait) is supported — just call this endpoint again with a new `wait_minutes`.

---

### Alert history

Resolved alerts (host responded `"available"` or `"not_available"`) — not `"wait"` or unresponded ones, those stay in `/host/pending-alerts`.

​`
GET /host/alert-history?limit=20&offset=0
Authorization: Bearer <access_token>
​`

`limit` (default 20, max 100) and `offset` (default 0) are optional query params for pagination.

Response shape:

​`json
{
  "history": [
    {
      "visitor_id": 123,
      "session_id": "...",
      "visitor_name": "...",
      "visitor_photo_url": "...",
      "purpose": "...",
      "arrived_at": "2026-08-02T10:15:00",
      "host_response": "available",
      "available_again_at": null
    }
  ],
  "total": 87,
  "limit": 20,
  "offset": 0,
  "has_more": true
}
​`

Ordered newest-first (opposite of `/host/pending-alerts`, which is oldest-first). Use `has_more` to know whether to fetch the next page (`offset += limit`) — e.g. "load more" button or infinite scroll.

### Updating floor/room

Lets the logged-in host update their own floor/room — resolved from the token, no `employee_id` needed.

​`
PATCH /host/profile/floor-room
Authorization: Bearer <access_token>
Content-Type: application/json

{ "floor_room": "3rd Floor, Room 12" }
​`

Success response:

​`json
{ "detail": "Floor/room updated", "floor_room": "3rd Floor, Room 12" }
​`

Current value is also returned by `GET /auth/me` (see above) — no separate GET needed for display.

## Not built yet

- Password reset flow

---

## Deliverable 3: Visitor status page (QR code)

Same pattern as Deliverable 2 (the activation page) — a standalone page, no login, hosted anywhere (Netlify is fine, same as before).

1. Build a page that reads a `token` query param, e.g. `<wherever-you-host-it>/visit-status?token=abc123`
2. Once built and hosted, send the backend dev the base URL — same as you did for the activation page
3. Backend will append the visitor's `status_token` (generated per-visit, already stored on `VisitSession`) to that URL and render it as a QR code at the kiosk, so the visitor can scan it and check their own status from their phone
4. **✅ Built and tested end to end.** Connect via WebSocket to:

   ​`
wss://<backend-host>/ws/status/{status_token}
​`

   On connect, you'll immediately receive the current status (covers the case where the host already responded before the page loaded). After that, any host response pushes a new message instantly, no polling needed.

   Message shape:

   ​`json
{
  "state": "HOST_ASKED_WAIT",
  "visitor_message": "Tooba Farooq is currently unavailable and has asked you to wait about 18 more minutes (until 3:40 PM).",
  "host_response": "wait",
  "wait_until": "2026-08-03T15:40:00Z",
  "available_again_at": null,
  "visitor_choice": null
}
​`
   - Just display `visitor_message` directly — it's already a complete, human-readable sentence.
   - `state` is one of `PROCEED_TO_HOST`, `HOST_ASKED_WAIT`, `HOST_UNAVAILABLE` — use it if you want different visual treatment per state (e.g. a green "proceed" banner vs. a waiting spinner).
   - Socket closes with code `4404` if the `status_token` in the URL doesn't match any session — handle that as "invalid or expired link."

---

### Leaving a message for the host

If the host is unavailable, the visitor can leave a message (typed or voice) from the status page.

​`
POST /message
Content-Type: multipart/form-data

status_token=<token from URL>
text=<typed message> # optional
audio=<audio file> # optional
​`

Send **either** `text` or `audio`, not both. Uses `status_token` (not `session_id`) — same public-safe identifier as the WebSocket connection, so the frontend never needs to know or send the internal `session_id`.

Success response:

​`json
{ "detail": "Message recorded", "message_text": "<transcribed or typed text>" }
​`

Errors: `404` if `status_token` doesn't match any session.

## Errors to handle

| Status                           | Meaning                                                                                 |
| -------------------------------- | --------------------------------------------------------------------------------------- |
| 400 on `/activate`               | Invalid or expired invite token                                                         |
| 401 on `/login`                  | Wrong `employee_code` or password                                                       |
| 403 on `/login`                  | Account exists but not activated yet                                                    |
| 401 on any authenticated request | Access token expired → call `/auth/refresh`, retry                                      |
| 401 on `/refresh`                | Refresh token expired → force full re-login                                             |
| 404 on `/host/respond`           | `session_id` doesn't exist                                                              |
| 403 on `/host/respond`           | Session is assigned to a different host                                                 |
| 400 on `/host/respond`           | Invalid `response` value, or missing/invalid `wait_minutes` when `response` is `"wait"` |
| 404 on `/message`                | `status_token` doesn't match any session                                                |

---

## Deliverable 4: Admin dashboard authentication

**Single admin, no signup.** There's exactly one admin account, credentials set on the backend side — no registration flow needed on your end.

### Login

```
POST /auth/admin-login
Content-Type: application/x-www-form-urlencoded

grant_type=password&username=<admin_username>&password=<admin_password>
```

Form-encoded (OAuth2 convention), same as employee login — not JSON.

Response:

```json
{ "access_token": "<jwt>", "token_type": "bearer" }
```

- No refresh token for admin — just the one access token.
- Token expires in 8 hours. On expiry, just show the login form again — no refresh flow to implement.
- Store the token however you're already storing state in the dashboard (memory/localStorage — your call).

### Using the token

Attach it to every request to the two protected endpoints below:

```
Authorization: Bearer <access_token>
```

### Protected endpoints

```
GET /employees
Authorization: Bearer <access_token>
```

Returns the employee list — same shape you're already using in the dashboard.

```
POST /employees
Authorization: Bearer <access_token>
Content-Type: multipart/form-data

name=<string>
floor_room=<string, optional>
phone_number=<string, optional>
email=<string, optional>
photo=<file>
```

Creates an employee — same as before, just now requires the header.

### What changes on your side

Replace whatever hardcoded/fake login check the dashboard currently does with an actual call to `/auth/admin-login`. On success, store the returned token and attach it as a `Bearer` header on the `/employees` GET and POST calls. If either of those calls comes back `401`, treat it as "not logged in" and route back to the login screen.

### Errors to handle (admin-specific)

| Status                            | Meaning                                                       |
| --------------------------------- | ------------------------------------------------------------- |
| 401 on `/auth/admin-login`        | Wrong admin username or password                              |
| 401 on `/employees` (GET or POST) | Missing, invalid, or expired admin token → send back to login |
