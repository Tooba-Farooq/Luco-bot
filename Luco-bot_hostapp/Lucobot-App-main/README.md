# Host App — README (Deliverables 1 & 3 + Push Notifications)

## What's done

### Auth (Deliverable 1 — core)
- **Login** — `POST /auth/login`, form-urlencoded (`grant_type=password&username=<employee_code>&password=<password>`)
- **Token storage** — `access_token` + `refresh_token` stored securely via `expo-secure-store` (Keychain/Keystore)
- **Device registration** — `POST /auth/register-device`, fires automatically right after login, now using a **real FCM device token** (not a placeholder anymore)
- **Auto-refresh on 401** — any authenticated request that hits a `401` calls `POST /auth/refresh`, updates the stored token, retries once. If the refresh token itself is dead, forces logout back to login screen
- **Profile fetch** — `GET /auth/me` on the home screen, shows name/code/photo
- **Logout** — clears tokens, returns to login
- **Error handling** — login distinguishes `401` (wrong credentials) from `403` (not activated yet)

### Visitor alerts (Deliverable 1 — the rest)
- **Pending alerts on app open** — `GET /host/pending-alerts` called on every home screen load; if anything's waiting, jumps straight to the alert screen instead of home
- **Alert screen** — shows visitor name, photo, purpose, arrival time. Three actions: **Send In** (`available`), **Wait** (picker: 5/10/30/60/120 min → `POST /host/respond` with `wait_minutes`), **Not Available**
- **Multiple alerts supported** — listed oldest-first, any one resolvable independently
- **Push notifications**:
  - Foreground → banner shown, then re-checks pending alerts
  - Background/killed → tapping the tray notification opens the app and routes to pending alerts *(not tested yet)*
  - Required switching from Expo Go to a **custom dev client build**, since Expo Go doesn't support raw FCM tokens

### Visitor status page (Deliverable 3 — new)
- `visit-status.html`, same pattern as the activation page — standalone, no framework, config-variable base URL
- Reads `?token=`, opens a WebSocket to `wss://<backend>/ws/status/{token}`
- Currently displays the raw incoming message — **needs a follow-up pass once the message shape is documented**
- Hosted on Netlify

## What's NOT done yet

- Visit history screen (no endpoint yet)
- Visitor status page's real message rendering (blocked on WebSocket payload shape being documented)
- **End-to-end push confirmation** — receiving side is wired, but not yet confirmed working with a real triggered alert. Need either a manual/Swagger way to trigger `send_host_alert`, or to test through the full kiosk flow together

## Key files

- `app/api/client.js` — all auth + alert API calls: `login()`, `registerDevice()`, `refreshAccessToken()`, `getMe()`, `authFetch()`, `logout()`, `getPendingAlerts()`, `respondToAlert()`
- `app/login/login.jsx` — wired to `client.js`; now also fetches a real FCM token via `expo-notifications` before registering the device
- `app/home-screen/home.jsx` — rewritten (old version was built against a different backend/appointments model). Shows profile, checks pending alerts, routes to alert screen if needed
- `app/alert-screen/alert.jsx` — **new**. Send In / Wait / Not Available screen
- `App.jsx` — screen routing + `expo-notifications` foreground/background listeners
- `app.json` — added `android.package`, `android.googleServicesFile`, `expo-notifications` plugin
- `google-services.json` — **new**, needed for FCM, registered under package `com.lucobot.admin`
- `visit-status.html` — **new**, separate from the mobile app, Deliverable 3

## Setup — required change: dev client, not Expo Go

Real push (raw FCM tokens) doesn't work in Expo Go. One-time setup:
```powershell
npm install
npx expo install expo-dev-client expo-notifications babel-preset-expo
eas build --profile development --platform android
```
Install the resulting `.apk` on your phone once. From then on, run:
```powershell
npx expo start --dev-client
```
and open it from the **installed dev client app**, not Expo Go.

## Running it

**1. Backend + tunnel** (both must stay running — mobile app and visitor status page both depend on this URL)
```powershell
uvicorn app.main:app --reload
```
```powershell
ngrok http 8000
```

**2. Point the app at it** — create/edit `.env` in the project root:
```
EXPO_PUBLIC_SERVER_URL=https://<current-ngrok-url>
```
Restart `npx expo start --dev-client` after any `.env` change — env vars aren't hot-reloaded.

**3. What to check**
- Login → home (or straight to alert screen if a real alert is pending)
- Home shows your name/code
- If an alert's pending: try Send In / Wait / Not Available, confirm it clears
- Background the app, trigger a test push if possible, confirm the tray notification appears and tapping it opens the alert screen
- Logout returns to login

## Known gotchas

- `localhost`/`127.0.0.1` never works from a phone — needs LAN IP (same Wi-Fi) or a tunnel
- Free-tier ngrok URLs change on every restart — `.env` **and** `visit-status.html`'s `API_BASE` both need updating when that happens
- Web mode isn't supported (`expo-secure-store` needs native Keychain/Keystore)
- Dev client / APK builds bake in the `.env` URL at build time — changing the URL later means rebuilding, not just editing `.env`
- Backend needs the Firebase Admin SDK key (`luco-bot-firebase-adminsdk-...json`) placed at `<backend-repo>/app/` to start without crashing — same file you already have