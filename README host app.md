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

### Test credentials (until email is wired up)

Ask the backend dev for an `employee_code` + password — they'll create and activate a test employee manually.

OR

Do it yourself

Test flow in Swagger
POST /employees → response now includes employee_code and invite_token. Copy the token.
POST /auth/activate with that invite_token + a chosen password.
POST /auth/login — note this is application/x-www-form-urlencoded (Swagger will show a form, not JSON, because of OAuth2PasswordRequestForm) — username = the employee_code, password = what you set.
Copy access_token → click Swagger's "Authorize" button → paste it → now GET /auth/me and POST /auth/register-device should work.

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

---

## Not built yet

- Push delivery of visitor alerts (device token is stored, nothing sends to it yet)
- Host-response endpoints (Available / Not available / Wait)
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
