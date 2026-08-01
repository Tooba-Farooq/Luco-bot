# Host App — Deliverable 1 (Mobile Login/Auth) — README

## What's done

The mobile app (React Native / Expo) now implements the full auth flow from the backend spec:

- **Login** — `POST /auth/login`, form-urlencoded (`grant_type=password&username=<employee_code>&password=<password>`)
- **Token storage** — `access_token` + `refresh_token` stored securely via `expo-secure-store` (Keychain/Keystore), not plain storage
- **Device registration** — `POST /auth/register-device`, fired automatically right after login succeeds
- **Auto-refresh on 401** — any authenticated request that gets a `401` automatically calls `POST /auth/refresh`, updates the stored access token, and retries once. If the refresh token itself is dead, the app logs out and returns to the login screen automatically
- **Profile fetch** — `GET /auth/me` called on the home screen; shows employee name, code, and photo (or a placeholder initial if no photo)
- **Logout** — clears stored tokens, returns to login
- **Error handling** — login distinguishes `401` (wrong credentials) from `403` (account not yet activated) with different messages

## What's NOT done (blocked, not forgotten)

- Incoming visitor alert screen (Available Now / Notify Later / Not Available) — spec doc says the backend endpoints for this are "in progress, not tested yet"
- Visit history screen — optional, no endpoint provided yet
- Real push notifications — `register-device` currently sends a placeholder device token string, not a real FCM/APNs token

## Files changed

- `app/api/client.js` — **new file**. All auth/API logic lives here: `login()`, `registerDevice()`, `refreshAccessToken()`, `getMe()`, `authFetch()` (the auto-refresh wrapper), `logout()`
- `app/login/login.jsx` — `handleLogin` rewritten to use `client.js`; UI/styling untouched
- `app/home-screen/home.jsx` — rewritten from scratch. Old version was built against a different backend entirely (Socket.io + `/api/appointments/:id` for a different appointment-based flow) — that's all removed. New version just shows the logged-in employee's profile via `getMe()` plus a logout button
- `App.jsx` — one-line change: `<Home logout={logout} currentUser={currentUser} />` → `<Home goToLogin={logout} />`

## How to test it yourself

**1. Get the project running**
```powershell
npm install
```
If you hit a `babel-preset-expo` version mismatch warning, fix it with:
```powershell
npx expo install babel-preset-expo
```
(always use `npx expo install`, not plain `npm install`, for Expo-related packages — it picks the version matching our SDK)

**2. Point it at the backend**

Create/edit `.env` in the project root:
```
EXPO_PUBLIC_SERVER_URL=http://<backend-address>:<port>
```
- If backend is running on your own machine and you're testing on the same Wi-Fi via Expo Go → use your machine's LAN IP (`ipconfig` → IPv4 Address), not `localhost`
- If testing an APK build → needs a stable public URL (we used ngrok on this end — ask if you want the current tunnel URL, or set up your own with `ngrok http 8000`)

**3. Get test credentials**

Either ask the backend dev to create+activate an employee for your email, or do it yourself via Swagger:
1. `POST /employees` → copy `employee_code` + `invite_token`
2. `POST /auth/activate` with that token + a password you choose
3. Use that `employee_code` + password to log into the app

**4. Run it**
```powershell
npx expo start
```
Scan the QR with Expo Go on your phone (don't use `w` for web — `expo-secure-store` doesn't work in browsers, it's a native-only module).

**5. What to check**
- Login with your test credentials → no error, navigates to home
- Home screen shows a brief spinner, then your name + employee code
- Tap "Log Out" → returns to login screen
- (Optional/harder to test) Wait 30+ min or manually expire a token, then trigger any authenticated call — should silently refresh rather than kick you out; only if the *refresh* token is also dead should it force logout

## Known gotchas worth knowing about

- **`localhost` doesn't work on-device** — it means "this phone," not your PC. Always use LAN IP or a tunnel.
- **Web mode isn't supported for this app** — `expo-secure-store` needs native Keychain/Keystore, which doesn't exist in a browser.
- **APK builds bake in the `.env` URL at build time** — if the URL changes later (e.g. your IP changes, or an ngrok tunnel restarts with a new URL), the APK needs to be rebuilt, not just have `.env` edited.
