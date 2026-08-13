
# Host App — README (Deliverables 1 & 3 + Push Notifications)

## What's done

### Auth (Deliverable 1 — core)
- **Login** — `POST /auth/login`, form-urlencoded (`grant_type=password&username=<employee_code>&password=<password>`)
- **Token storage** — `access_token` + `refresh_token` stored securely via `expo-secure-store` (Keychain/Keystore)
- **Device registration** — `POST /auth/register-device`, fires automatically right after login, using a real FCM device token
- **Auto-refresh on 401** — any authenticated request hitting a 401 calls `POST /auth/refresh`, updates the stored token, retries once. If the refresh token itself is dead, forces logout back to login screen
- **Profile fetch** — `GET /auth/me` on the home screen, shows name/code/photo
- **Logout** — clears tokens, returns to login. Reachable from the Account tab, and also from Home's error state if `getMe()`/`getPendingAlerts()` fail before the tab bar is usable
- **Error handling** — login distinguishes 401 (wrong credentials) from 403 (not activated yet)

### Navigation — now 4 tabs (changed from single-screen flow)
- **Home, Messages, History, Account** — persistent floating pill-style tab bar at the bottom, with an unread/pending badge on the Home icon
- **Alerts** and **Message Thread** are full-screen overlays outside the tab bar (state-based routing via `currentScreen` in `App.js`, not a router library)
- Pending alerts **no longer auto-redirect to a full-screen alert view on app open** — this was the original design (`GET /host/pending-alerts` → jump straight to alert screen) but has been deliberately changed: alerts now render inline as cards on the Home tab, so browsing another tab doesn't get interrupted by an alert arriving. The full-screen Alert screen is now reserved specifically for when the host taps a push notification — a deliberate action that warrants a focused screen — not for ambient in-app polling

### Visitor alerts (Deliverable 1 — the rest)
- **Home tab** — shows the employee profile header plus pending alert cards inline (photo, visitor name, purpose, live "waited X min" counter). Each card has three always-available actions: **Send In** (`available`), **Wait** (picker: 5/10/30/60/120 min → `POST /host/respond` with `wait_minutes`), **Not Available**. Choosing Wait shows a "waiting until X" status note above the buttons but does **not** hide or disable them — per the backend contract, a Wait response never auto-expires or auto-resolves, so the host must always be able to act again (including extending the wait) at any point afterward
- **Full-screen Alert screen** — same three actions, plus urgency styling (color-escalating "waiting X min" pill at 8min/20min thresholds) and a "NEW" badge for alerts under 1 minute old. Reached via "View all" from Home (when 2+ pending) or via tapping a push notification
- **Multiple alerts supported** — listed oldest-first, any one resolvable independently, matching the backend's ordering
- **Push notifications**:
  - Foreground → banner shown, pending alerts silently refreshed (badge/inline cards update, no forced redirect)
  - Background/killed → tapping the tray notification opens the app and routes directly to the full-screen alert screen (**not tested end-to-end yet**)
  - Requires the custom dev client build, since Expo Go doesn't support raw FCM tokens

### Messages 
- **Messages tab** — lists visitor conversation threads, grouped by `visitor_id` from `GET /host/messages`, sorted most-recent-first, with a message-count badge per thread
- **Message thread screen** — single visitor's full conversation history, reached by tapping a thread; back button returns to the Messages tab
- 
### History (new this session)
- **History tab** — resolved alerts (`available`/`not_available`) from `GET /host/alert-history`, paginated (`limit`/`offset`), newest-first, with a "Load more" footer button driven by `has_more`
- Shows visitor photo, name, purpose, arrival time, and a "Sent In"/"Not Available" tag; if `available_again_at` was set, shows "Asked to return at [time]"

### Account
- Exists as its own tab (`app/account-screen/account.jsx`), handles logout 

### Visual design pass 
- Dark theme (`#0f172a` background, `#00bcd4` accent) retained throughout
- **Floating pill-shaped tab bar** (translucent rounded background, shadow, active-tab highlight pill) replacing the earlier flush rectangular bar
- **Elevated gradient cards** (`expo-linear-gradient`) on alert cards, history rows, and message thread-list rows — chat bubbles inside an open thread deliberately kept flat/non-gradient for visual quietness
- **Glassmorphism headers** (`expo-blur`) on Home, History, Messages, and Message Thread
- **Full-width stacked action buttons** (Send In / Wait / Not Available) replacing the original side-by-side row layout, on both Home's inline cards and the full-screen Alert screen

### Visitor status page (Deliverable 3)
- `visit-status.html`, standalone, no framework, config-variable base URL, hosted on Netlify
- Reads `?token=`, opens `wss://<backend>/ws/status/{token}`
- Message shape is now documented (`state`, `visitor_message`, `host_response`, `wait_until`, `available_again_at`, `visitor_choice`) 
- **WebSocket reconnect fix applied** (from a prior session): the page previously required 2–3 manual refreshes to catch a status update, root-caused to mobile browsers throttling `setTimeout`-based reconnect in backgrounded tabs. Fixed by adding `visibilitychange`/`pageshow`/`online` listeners to force a reconnect check on foreground, plus a 20s client-side keepalive ping to reduce idle-timeout disconnects. **Not yet confirmed working on-device** — still needs the background-30-60s-then-check test
- **Not yet handled**: WebSocket close code `4404` (invalid/expired `status_token`) falls into the generic reconnect loop instead of showing an explicit "invalid link" state

## Key files
- `app/api/client.js` — all auth + alert + messages + history API calls: `login()`, `registerDevice()`, `refreshAccessToken()`, `getMe()`, `authFetch()`, `logout()`, `getPendingAlerts()`, `respondToAlert()`, `getHostMessages()`, `getAlertHistory()`, `updateFloorRoom()`
- `app/login/login.jsx` — wired to `client.js`, fetches a real FCM token via `expo-notifications` before registering the device
- `app/home-screen/home.jsx` — shows profile (glass header), inline pending alert cards (gradient, stacked buttons, live wait state)
- `app/alert-screen/alert.jsx` — full-screen Send In / Wait / Not Available screen, reached from push-tap or "View all"
- `app/messages-screen/messages.jsx` — thread list (verify this filename, see warning above)
- `app/messages-screen/message-thread.jsx` — single-thread conversation view
- `app/history-screen/history.jsx` — paginated resolved-alert history
- `app/account-screen/account.jsx` — profile/logout (not reviewed in detail this session)
- `App.jsx` — 4-tab routing, floating tab bar, `expo-notifications` foreground/background listeners, pending-alerts refresh logic
- `app.json` — `android.package`, `android.googleServicesFile`, `expo-notifications` plugin
- `google-services.json` — needed for FCM, registered under package `com.lucobot.admin`
- `visit-status.html` — Deliverable 3, standalone visitor status page, WebSocket reconnect fix applied

## Setup — dev client, not Expo Go
Real push (raw FCM tokens) doesn't work in Expo Go, and neither does `expo-linear-gradient`/`expo-blur` without a matching native build. One-time setup:
```
npm install
npx expo install expo-dev-client expo-notifications expo-linear-gradient expo-blur babel-preset-expo
npx expo prebuild --clean
eas build --profile development --platform android
```
Install the resulting `.apk` on your phone once. From then on, run:
```
npx expo start --dev-client
```
and open it from the installed dev client app, not Expo Go. **Any time a new native dependency is added**, repeat `npx expo prebuild --clean` and rebuild — a plain `npx expo install` alone will not link it into the existing binary.

## Running it
**1. Backend + tunnel** (both must stay running — mobile app and visitor status page both depend on this URL)
```
uvicorn app.main:app --reload
ngrok http 8000
```

**2. Point the app at it** — create/edit `.env` in the project root:
```
EXPO_PUBLIC_SERVER_URL=https://<current-ngrok-url>
```
Restart `npx expo start --dev-client` after any `.env` change — env vars aren't hot-reloaded.


## Known gotchas
- `localhost`/`127.0.0.1` never works from a phone — needs LAN IP (same Wi-Fi) or a tunnel
- Free-tier ngrok URLs change on every restart — `.env` and `visit-status.html`'s `API_BASE` both need updating when that happens
- Web mode isn't supported (`expo-secure-store` needs native Keychain/Keystore)
- Dev client / APK builds bake in the `.env` URL at build time — changing the URL later means rebuilding, not just editing `.env`
-native modules (`expo-linear-gradient`, `expo-blur`) also require a full rebuild (`prebuild --clean` + `run:android`/`eas build`) any time they're added or changed — a plain JS reload will crash with `IllegalViewOperationException`
- Backend needs the Firebase Admin SDK key (`luco-bot-firebase-adminsdk-...json`) placed at `<backend-repo>/app/` to start without crashing — same file as before
