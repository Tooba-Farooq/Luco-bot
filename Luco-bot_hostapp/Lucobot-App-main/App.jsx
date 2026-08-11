import { useEffect, useRef, useState } from 'react';
import * as Notifications from 'expo-notifications';
import { AppState, StyleSheet, View } from 'react-native';

import SplashScreen from './app/Splash-screen/splash_screen';
import Home from './app/home-screen/home';
import Login from './app/login/login';
import AlertScreen from './app/alert-screen/alert';
import MessagesScreen from './app/messages-screen/messages';
import { getMe } from './app/api/client';

// Show alerts as banners even while the app is in the foreground
Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowAlert: true,
    shouldPlaySound: true,
    shouldSetBadge: false,
  }),
});

export default function App() {
  const [currentScreen, setCurrentScreen] = useState('splash');
  const [currentUser, setCurrentUser] = useState(null);
  const [pendingAlerts, setPendingAlerts] = useState([]);
  const [refreshKey, setRefreshKey] = useState(0);
  const notificationListener = useRef();
  const responseListener = useRef();
  const appState = useRef(AppState.currentState);

  // Restore session on launch instead of always dropping to Login.
  // Covers cold start AND the JS process getting killed in the
  // background and relaunched — tokens live in SecureStore, so if
  // they're still valid we should skip straight to Home.
  useEffect(() => {
    let cancelled = false;

    (async () => {
      const start = Date.now();
      let nextScreen = 'login';
      let userData = null;

      try {
        userData = await getMe(); // authFetch auto-refreshes an expired access token
        nextScreen = 'home';
      } catch {
        nextScreen = 'login'; // no tokens, or refresh token also expired/invalid
      }

      const elapsed = Date.now() - start;
      const remaining = Math.max(0, 1200 - elapsed); // brief splash, not a fixed 4s

      setTimeout(() => {
        if (cancelled) return;
        if (nextScreen === 'home') setCurrentUser(userData);
        setCurrentScreen(nextScreen);
      }, remaining);
    })();

    return () => { cancelled = true; };
  }, []);

  useEffect(() => {
    // Foreground: app is open, show in-app handling (banner already shown by handler above)
    notificationListener.current = Notifications.addNotificationReceivedListener((notification) => {
      const data = notification.request.content.data;
      console.log('Notification received in foreground:', data);
      // Re-check pending alerts so the new one shows up if user is on home
      if (currentScreen === 'home' || currentScreen === 'alerts') {
        setRefreshKey((k) => k + 1);
        setCurrentScreen('home'); // triggers Home's loadProfile to re-check pending alerts
      }
    });

    // Background/killed: user tapped the tray notification
    responseListener.current = Notifications.addNotificationResponseReceivedListener((response) => {
      const data = response.notification.request.content.data;
      console.log('Notification tapped:', data);
      if (data?.session_id) {
        setCurrentScreen('home'); // Home will fetch pending-alerts and route to the alert screen
      }
    });

    return () => {
      notificationListener.current?.remove();
      responseListener.current?.remove();
    };
  }, [currentScreen]);

  // Per backend docs: check /host/pending-alerts "whenever the app opens
  // or resumes, to catch anything unresolved regardless of whether a
  // push was seen." Pushes are fire-and-forget, so a missed/dismissed
  // tray notification leaves the app with no other signal that
  // something's waiting — this listener is that signal.
  useEffect(() => {
    const subscription = AppState.addEventListener('change', (nextAppState) => {
      const wasBackgrounded = appState.current.match(/inactive|background/);
      const isNowActive = nextAppState === 'active';

      if (wasBackgrounded && isNowActive && currentUser && currentScreen !== 'splash' && currentScreen !== 'login') {
        setRefreshKey((k) => k + 1);
        setCurrentScreen('home'); // remounts Home, which re-checks pending-alerts and routes accordingly
      }

      appState.current = nextAppState;
    });

    return () => subscription.remove();
  }, [currentUser, currentScreen]);

  const goToHome = (userData) => {
    setCurrentUser(userData);
    setCurrentScreen('home');
  };

  const goToAlerts = (alerts) => {
    setPendingAlerts(alerts);
    setCurrentScreen('alerts');
  };
  const goToMessages = () => setCurrentScreen('messages');

  const handleAlertResolved = (sessionId, response, result) => {
    if (response === 'wait') {
      // Per backend docs: "Wait" does not remove the alert — the host
      // may still want to see/act on it if they finish early. Update
      // the item in place instead of filtering it out.
      setPendingAlerts((prev) =>
        prev.map((a) =>
          a.session_id === sessionId
            ? { ...a, host_response: 'wait', wait_until: result?.wait_until ?? null }
            : a
        )
      );
      return;
    }

    // "available" or "not_available" — alert is resolved, remove it.
    setPendingAlerts((prev) => prev.filter((a) => a.session_id !== sessionId));
  };

  const logout = () => {
    setCurrentUser(null);
    setCurrentScreen('login');
  };

  const renderScreen = () => {
    switch (currentScreen) {
      case 'splash':
        return <SplashScreen />;
      case 'login':
        return <Login goToHome={goToHome} />;
      case 'home':
        return <Home key={refreshKey} goToLogin={logout} goToAlerts={goToAlerts} goToMessages={goToMessages} />;
      case 'messages':
        return <MessagesScreen goToHome={() => setCurrentScreen('home')} />;
      case 'alerts':
        return (
          <AlertScreen
            alerts={pendingAlerts}
            onResolved={handleAlertResolved}
            goToHome={() => setCurrentScreen('home')}
            goToMessages={goToMessages}
          />
        );
      default:
        return <Login goToHome={goToHome} />;
    }
  };

  return (
    <View style={styles.container}>
      {renderScreen()}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
});