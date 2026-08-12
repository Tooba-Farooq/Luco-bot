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
      setPendingAlerts(data.pending ?? []);
    } catch (err) {
      console.error('Failed to refresh pending alerts:', err);
    }
  }, []);

  // Restore session on launch instead of always dropping to Login.
  useEffect(() => {
    let cancelled = false;

    (async () => {
      const start = Date.now();
      let nextScreen = 'login';
      let userData = null;

      try {
        userData = await getMe();
        nextScreen = 'home';
      } catch {
        nextScreen = 'login';
      }

      const elapsed = Date.now() - start;
      const remaining = Math.max(0, 1200 - elapsed);

      setTimeout(async () => {
        if (cancelled) return;
        if (nextScreen === 'home') {
          setCurrentUser(userData);
          await refreshPendingAlerts();
        }
        setCurrentScreen(nextScreen);
      }, remaining);
    })();

    return () => { cancelled = true; };
  }, [refreshPendingAlerts]);

  useEffect(() => {
    notificationListener.current = Notifications.addNotificationReceivedListener((notification) => {
      console.log('Notification received in foreground:', notification.request.content.data);
      refreshPendingAlerts();
    });

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

  useEffect(() => {
    const subscription = AppState.addEventListener('change', (nextAppState) => {
      const wasBackgrounded = appState.current.match(/inactive|background/);
      const isNowActive = nextAppState === 'active';

      if (wasBackgrounded && isNowActive && currentUser && currentScreen !== 'splash' && currentScreen !== 'login') {
        refreshPendingAlerts();
      }

      appState.current = nextAppState;
    });

    return () => subscription.remove();
  }, [currentUser, currentScreen, refreshPendingAlerts]);

  const goToHome = useCallback((userData) => {
    setCurrentUser(userData);
    setCurrentScreen('home');
    refreshPendingAlerts();
  }, [refreshPendingAlerts]);

  const goToAlerts = useCallback((alerts) => {
    if (alerts) setPendingAlerts(alerts);
    setCurrentScreen('alerts');
  }, []);

  const goToMessages = useCallback(() => setCurrentScreen('messages'), []);

  const goToThread = useCallback((visitorId) => {
    setMessageThreadId(visitorId);
    setCurrentScreen('messageThread');
  }, []);

  const handleAlertResolved = useCallback((sessionId, response, result) => {
    if (response === 'wait') {
      setPendingAlerts((prev) =>
        prev.map((a) =>
          a.session_id === sessionId
            ? { ...a, host_response: 'wait', wait_until: result?.wait_until ?? null }
            : a
        )
      );
      return;
    }
    setPendingAlerts((prev) => prev.filter((a) => a.session_id !== sessionId));
  }, []);

  const logout = useCallback(() => {
    setCurrentUser(null);
    setPendingAlerts([]);
    setCurrentScreen('login');
  }, []);

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
            pending={pendingAlerts}
            onAlertResolved={handleAlertResolved}
            onRefresh={refreshPendingAlerts}
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
    paddingBottom: 24,
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