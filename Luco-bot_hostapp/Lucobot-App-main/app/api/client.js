import * as SecureStore from 'expo-secure-store';

const BASE_URL = "https://snub-tactics-impatient.ngrok-free.dev";

const ACCESS_TOKEN_KEY = 'access_token';
const REFRESH_TOKEN_KEY = 'refresh_token';

async function saveTokens(accessToken, refreshToken) {
  await SecureStore.setItemAsync(ACCESS_TOKEN_KEY, accessToken);
  await SecureStore.setItemAsync(REFRESH_TOKEN_KEY, refreshToken);
}

async function getAccessToken() {
  return SecureStore.getItemAsync(ACCESS_TOKEN_KEY);
}

async function getRefreshToken() {
  return SecureStore.getItemAsync(REFRESH_TOKEN_KEY);
}

async function clearTokens() {
  await SecureStore.deleteItemAsync(ACCESS_TOKEN_KEY);
  await SecureStore.deleteItemAsync(REFRESH_TOKEN_KEY);
}

export async function login(employeeCode, password) {
  const body = new URLSearchParams();
  body.append('grant_type', 'password');
  body.append('username', employeeCode);
  body.append('password', password);

  const response = await fetch(`${BASE_URL}/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
    body: body.toString(),
  });

  if (!response.ok) {
  if (response.status === 403) {
    throw new Error('Account exists but is not activated yet. Check your email for the activation link.');
  }
  throw new Error('Invalid employee code or password');
}

  const data = await response.json();
  await saveTokens(data.access_token, data.refresh_token);
  return data;
}

export async function registerDevice(deviceToken, platform) {
  const accessToken = await getAccessToken();

  const response = await fetch(`${BASE_URL}/auth/register-device`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'Authorization': `Bearer ${accessToken}`,
    },
    body: JSON.stringify({ device_token: deviceToken, platform }),
  });

  if (!response.ok) {
    throw new Error('Failed to register device');
  }

  return response.json();
}

// Prevents concurrent refresh calls from racing each other and stepping
// on a rotated refresh token (see refreshAccessToken below).
let refreshInFlight = null;

export async function refreshAccessToken() {
  if (refreshInFlight) {
    return refreshInFlight;
  }

  refreshInFlight = (async () => {
    const refreshToken = await getRefreshToken();
    if (!refreshToken) {
      throw new Error('No refresh token available');
    }

    const response = await fetch(`${BASE_URL}/auth/refresh`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ refresh_token: refreshToken }),
    });

    if (!response.ok) {
      await clearTokens();
      throw new Error('Refresh failed, session expired');
    }

    const data = await response.json();
    await SecureStore.setItemAsync(ACCESS_TOKEN_KEY, data.access_token);

    // If the backend rotates refresh tokens, persist the new one too —
    // otherwise the next refresh attempt uses a stale/invalidated token
    // and silently logs the user out after the next expiry.
    if (data.refresh_token) {
      await SecureStore.setItemAsync(REFRESH_TOKEN_KEY, data.refresh_token);
    }

    return data.access_token;
  })();

  try {
    return await refreshInFlight;
  } finally {
    refreshInFlight = null;
  }
}

// getMe() defined below, after authFetch wrapper

export async function authFetch(url, options = {}) {
  let accessToken = await getAccessToken();

  let response = await fetch(url, {
    ...options,
    headers: {
      ...(options.headers || {}),
      'Authorization': `Bearer ${accessToken}`,
    },
  });

  if (response.status === 401) {
    // Access token expired — try refreshing once, then retry the original request
    accessToken = await refreshAccessToken();

    response = await fetch(url, {
      ...options,
      headers: {
        ...(options.headers || {}),
        'Authorization': `Bearer ${accessToken}`,
      },
    });
  }

  return response;
}

export async function getMe() {
  const response = await authFetch(`${BASE_URL}/auth/me`);

  if (!response.ok) {
    throw new Error('Failed to fetch profile');
  }

  return response.json();
}

export async function getPendingAlerts() {
  const response = await authFetch(`${BASE_URL}/host/pending-alerts`);

  if (!response.ok) {
    throw new Error('Failed to fetch pending alerts');
  }

  return response.json();
}

export async function getHostMessages() {
  const response = await authFetch(`${BASE_URL}/host/messages`);

  if (!response.ok) {
    throw new Error('Failed to fetch messages');
  }

  return response.json();
}

export async function respondToAlert(sessionId, response, waitMinutes) {
  const body = { session_id: sessionId, response };
  if (response === 'wait') {
    body.wait_minutes = waitMinutes;
  }

  const res = await authFetch(`${BASE_URL}/host/respond`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  });

  if (!res.ok) {
    const errData = await res.json().catch(() => ({}));
    throw new Error(errData.detail || 'Failed to respond to alert');
  }

  return res.json();
}

export async function logout() {
  await clearTokens();
}

export async function updateFloorRoom(floorRoom) {
  const res = await authFetch(`${BASE_URL}/host/profile/floor-room`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ floor_room: floorRoom }),
  });

  if (!res.ok) {
    const errData = await res.json().catch(() => ({}));
    throw new Error(errData.detail || 'Failed to update floor/room');
  }

  return res.json();
}

export async function getAlertHistory({ limit = 20, offset = 0 } = {}) {
  const response = await authFetch(`${BASE_URL}/host/alert-history?limit=${limit}&offset=${offset}`);

  if (!response.ok) {
    throw new Error('Failed to fetch alert history');
  }

  return response.json();
}