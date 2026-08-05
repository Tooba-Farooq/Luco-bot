import { useEffect, useRef, useState } from 'react';
import * as Notifications from 'expo-notifications';
import { StyleSheet, View } from 'react-native';

import SplashScreen from './app/Splash-screen/splash_screen';
import Home from './app/home-screen/home';
import Login from './app/login/login';
import AlertScreen from './app/alert-screen/alert';
import MessagesScreen from './app/messages-screen/messages';

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

  useEffect(() => {
    // Show splash screen for 4 seconds
    const timer = setTimeout(() => {
      setCurrentScreen('login');
    }, 4000);

    return () => clearTimeout(timer);
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
  
  const goToHome = (userData) => {
    setCurrentUser(userData);
    setCurrentScreen('home');
  };

  const goToAlerts = (alerts) => {
    setPendingAlerts(alerts);
    setCurrentScreen('alerts');
  };
  const goToMessages = () => setCurrentScreen('messages');
  const handleAlertResolved = (sessionId) => {
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