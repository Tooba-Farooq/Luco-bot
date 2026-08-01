import * as SecureStore from 'expo-secure-store';

const BASE_URL = process.env.EXPO_PUBLIC_SERVER_URL;

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

export async function refreshAccessToken() {
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
  return data.access_token;
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

export async function logout() {
  await clearTokens();
}