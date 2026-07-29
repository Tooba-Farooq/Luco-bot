# Lucobot — Host App Backend Reference

This document is for whoever is building the **host/employee-facing app** (the app staff use to log in, receive visitor alerts, and respond). It only covers auth and host-side endpoints. For the reception tablet / Unity frontend, see the main `README.md`.

---

## Who uses this app

Employees only. They do **not** self-register — an admin creates their record first (via the separate admin-facing `/employees` endpoint, not part of this app), then the employee activates their own account using a one-time invite token.

---

## Auth flow (what the app needs to implement)

### Important: activation is NOT part of this app

Activation (setting a password for the first time) happens on a **separate web page**, opened from a link in an email — not inside this app. The app should assume every employee who opens it already has a working `employee_code` + password. **Do not build an "activate account" screen in the app.**

For reference, activation works like this (you don't need to implement this, just know it exists):

```
POST /auth/activate   { "invite_token": "...", "password": "..." }
```

This is called by the web page, not the app.

**How to get test credentials during development** (until the email step is wired up): ask the backend dev for an `employee_code` + password directly — they'll create a test employee and activate it manually via Swagger. Build and test everything below against those.

### 1. Login

```
POST /auth/login
Content-Type: application/x-www-form-urlencoded

grant_type=password&username=EMP-07&password=<their password>
```

This follows the OAuth2 "password grant" shape (form-encoded, not JSON) — that's a FastAPI/OAuth2 convention, not something specific to this app. `username` = the `employee_code`.

Response:

```json
{
  "access_token": "<jwt>",
  "refresh_token": "<jwt>",
  "token_type": "bearer"
}
```

- `access_token` — short-lived (30 min). Send as `Authorization: Bearer <token>` on every authenticated request.
- `refresh_token` — long-lived (30 days). Store securely (Keychain / Android Keystore, not plain storage).

### 3. Refreshing an expired access token

```
POST /auth/refresh
Content-Type: application/json

{ "refresh_token": "<stored refresh token>" }
```

Returns a new `access_token`. Call this whenever a request fails with `401` due to expiry, then retry the original request.

### 4. Registering for push notifications

Call this right after login **and again any time the device's push token changes** (reinstall, new phone, OS-level token rotation):

```
POST /auth/register-device
Authorization: Bearer <access_token>
Content-Type: application/json

{
  "device_token": "<FCM or APNs token>",
  "platform": "ios" | "android"
}
```

Only one device token is stored per employee — registering a new one overwrites the old one. If an employee switches phones, they just log in on the new device and call this again; nothing needs to be manually revoked on the old device.

### 5. Who am I

```
GET /auth/me
Authorization: Bearer <access_token>
```

Returns `{ id, employee_code, name }`. Useful for a "logged in as \_\_\_" header/profile screen.

---

## Screens this implies

1. **Login** — `employee_code` + password → `POST /auth/login` → store tokens → `POST /auth/register-device`. This is the first screen the app shows. There is no activation screen in the app.
2. **Home / idle** — mostly empty state; this is where push notifications land.
3. **Incoming visitor alert** — _(endpoint not built yet — coming next)_ — will show visitor photo, name, purpose, with **Available Now / Notify Later / Not Available** actions.
4. **Optional: visit history** — _(endpoint not built yet)_ — past visits to this host.

---

## Not built yet — do not implement UI expecting these to work

- Push delivery of visitor alerts (device token is stored, but nothing sends to it yet)
- Host-response endpoints (`Available` / `Not available` / `Wait`)
- Visit history endpoint
- Password reset / "forgot password" flow
- Admin-side UI for creating employees (that's a separate internal tool, not part of this app)

This doc will be updated as those land — check back before building against anything not listed above.

---

## Errors you should handle

| Status                           | Meaning                                                           |
| -------------------------------- | ----------------------------------------------------------------- |
| 400 on `/activate`               | Invalid or expired invite token                                   |
| 401 on `/login`                  | Wrong `employee_code` or password                                 |
| 403 on `/login`                  | Account exists but not yet activated                              |
| 401 on any authenticated request | Access token expired or invalid → try `/auth/refresh`, then retry |
| 401 on `/refresh`                | Refresh token itself expired → force full re-login                |
