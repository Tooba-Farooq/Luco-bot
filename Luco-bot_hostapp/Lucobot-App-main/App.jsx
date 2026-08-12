import { useEffect, useRef, useState, useCallback } from 'react';
import * as Notifications from 'expo-notifications';
import { AppState, StyleSheet, View, TouchableOpacity, Text } from 'react-native';
import { Ionicons } from '@expo/vector-icons';

import SplashScreen from './app/Splash-screen/splash_screen';
import Home from './app/home-screen/home';
import Login from './app/login/login';
import AlertScreen from './app/alert-screen/alert';
import MessagesScreen from './app/messages-screen/message';
import MessageThreadScreen from './app/messages-screen/message-thread';
import HistoryScreen from './app/history-screen/history';
import AccountScreen from './app/account-screen/account';
import { getMe, getPendingAlerts } from './app/api/client';

// Show alerts as banners even while the app is in the foreground
Notifications.setNotificationHandler({
  handleNotification: async () => ({
    shouldShowAlert: true,
    shouldPlaySound: true,
    shouldSetBadge: false,
  }),
});

const TABS = [
  { key: 'home', label: 'Home', icon: 'home-outline', activeIcon: 'home' },
  { key: 'messages', label: 'Messages', icon: 'chatbubble-outline', activeIcon: 'chatbubble' },
  { key: 'history', label: 'History', icon: 'time-outline', activeIcon: 'time' },
  { key: 'account', label: 'Account', icon: 'person-outline', activeIcon: 'person' },
];

// Screens that own the whole viewport, no tab bar underneath them
const NO_TAB_BAR_SCREENS = ['splash', 'login', 'alerts', 'messageThread'];

export default function App() {
  const [currentScreen, setCurrentScreen] = useState('splash');
  const [currentUser, setCurrentUser] = useState(null);
  const [pendingAlerts, setPendingAlerts] = useState([]);
  const [refreshKey, setRefreshKey] = useState(0);
  const [messageThreadId, setMessageThreadId] = useState(null);
  const notificationListener = useRef();
  const responseListener = useRef();
  const appState = useRef(AppState.currentState);

  // Central place to re-sync the pending-alerts badge/inline cards without
  // forcing a screen change. Safe to call from any listener.
  const refreshPendingAlerts = useCallback(async () => {
  try {
    const data = await getPendingAlerts();
    setPendingAlerts(data.pending ?? []);   // was data.alerts ?? []
  } catch (err) {
    console.error('Failed to refresh pending alerts:', err);
  }
}, []);



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

      setTimeout(async () => {
        if (cancelled) return;
        if (nextScreen === 'home') {
          setCurrentUser(userData);
          await refreshPendingAlerts(); // badge is correct on first paint, not just after AppState change
        }
        setCurrentScreen(nextScreen);
      }, remaining);
    })();

    return () => { cancelled = true; };
  }, [refreshPendingAlerts]);

  useEffect(() => {
    // Foreground: app is open, show in-app handling (banner already shown by handler above).
    // Just refresh the badge/inline cards — don't force a redirect, since the
    // user may be mid-task on another tab.
    notificationListener.current = Notifications.addNotificationReceivedListener((notification) => {
      console.log('Notification received in foreground:', notification.request.content.data);
      refreshPendingAlerts();
    });

    // Background/killed: user tapped the tray notification — this is a deliberate
    // "deal with this now" action, so go full-screen to AlertScreen.
    responseListener.current = Notifications.addNotificationResponseReceivedListener(async (response) => {
      const data = response.notification.request.content.data;
      console.log('Notification tapped:', data);
      if (data?.session_id) {
        await refreshPendingAlerts();
        setCurrentScreen('alerts');
      }
    });

    return () => {
      notificationListener.current?.remove();
      responseListener.current?.remove();
    };
  }, [refreshPendingAlerts]);

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
        refreshPendingAlerts(); // updates badge/inline cards only — no forced tab switch
      }

      appState.current = nextAppState;
    });

    return () => subscription.remove();
  }, [currentUser, currentScreen, refreshPendingAlerts]);

  const goToHome = (userData) => {
    setCurrentUser(userData);
    setCurrentScreen('home');
    refreshPendingAlerts();
  };

  const goToAlerts = (alerts) => {
    if (alerts) setPendingAlerts(alerts);
    setCurrentScreen('alerts');
  };
  const goToMessages = () => setCurrentScreen('messages');

  const goToThread = (visitorId) => {
    setMessageThreadId(visitorId);
    setCurrentScreen('messageThread');
  };

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
    setPendingAlerts([]);
    setCurrentScreen('login');
  };

  const renderScreen = () => {
    switch (currentScreen) {
      case 'splash':
        return <SplashScreen />;
      case 'login':
        return <Login goToHome={goToHome} />;
      case 'home':
        return (
          <Home
            key={refreshKey}
            goToLogin={logout}
            goToAlerts={goToAlerts}
            goToMessages={goToMessages}
          />
        );
      case 'messages':
        return <MessagesScreen goToThread={goToThread} />;
      case 'messageThread':
        return (
          <MessageThreadScreen
            visitorId={messageThreadId}
            goBack={() => setCurrentScreen('messages')}
          />
        );
      case 'history':
        return <HistoryScreen />;
      case 'account':
        return <AccountScreen goToLogin={logout} />;
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

  const showTabBar = currentUser && !NO_TAB_BAR_SCREENS.includes(currentScreen);

  return (
    <View style={styles.container}>
      <View style={styles.content}>
        {renderScreen()}
      </View>

      {showTabBar && (
        <View style={styles.tabBar}>
          {TABS.map((tab) => {
            const active = currentScreen === tab.key;
            const showBadge = tab.key === 'home' && pendingAlerts.length > 0;
            return (
              <TouchableOpacity
                key={tab.key}
                style={styles.tabItem}
                onPress={() => setCurrentScreen(tab.key)}
              >
                <View>
                  <Ionicons
                    name={active ? tab.activeIcon : tab.icon}
                    size={22}
                    color={active ? '#00bcd4' : '#64748b'}
                  />
                  {showBadge && <View style={styles.tabBadge} />}
                </View>
                <Text style={[styles.tabLabel, active && styles.tabLabelActive]}>
                  {tab.label}
                </Text>
              </TouchableOpacity>
            );
          })}
        </View>
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#0f172a',
  },
  content: {
    flex: 1,
  },
  tabBar: {
    flexDirection: 'row',
    backgroundColor: '#1e293b',
    borderTopWidth: 1,
    borderTopColor: '#334155',
    paddingTop: 8,
    paddingBottom: 24, // safe-area padding for home indicator
  },
  tabItem: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: 2,
  },
  tabBadge: {
    position: 'absolute',
    top: -2,
    right: -6,
    width: 8,
    height: 8,
    borderRadius: 4,
    backgroundColor: '#f59e0b',
  },
  tabLabel: {
    fontSize: 11,
    color: '#64748b',
    fontWeight: '500',
  },
  tabLabelActive: {
    color: '#00bcd4',
    fontWeight: '600',
  },
});