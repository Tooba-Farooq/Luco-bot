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

Returns `{ id, employee_code, name, photo_url }`. `photo_url` is a full URL you can load directly into an image view; `null` if no photo was uploaded at registration.

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

Not yet available: a way to check for a missed/unresolved alert if the app was closed when the push arrived and got dismissed/missed. Will be added as a small "pending alert" endpoint — documented here once ready.

## Not built yet

- Host-response endpoints (Available / Not available / Wait) — in progress, not tested yet, will be documented here once ready
- "Pending alert" sync endpoint — for the case where the app was closed/killed when a push arrived and the notification wasn't seen; lets the app check for any unresolved visitor alert on open, independent of push. Not built yet, needed to fully match "catch up on missed alerts" behavior.
- Visit history endpoint
- Password reset flow
- Admin UI for creating employees (internal tool, separate from both deliverables above)

---

## Errors to handle

| Status                           | Meaning                                            |
| -------------------------------- | -------------------------------------------------- |
| 400 on `/activate`               | Invalid or expired invite token                    |
| 401 on `/login`                  | Wrong `employee_code` or password                  |
| 403 on `/login`                  | Account exists but not activated yet               |
| 401 on any authenticated request | Access token expired → call `/auth/refresh`, retry |
| 401 on `/refresh`                | Refresh token expired → force full re-login        |
