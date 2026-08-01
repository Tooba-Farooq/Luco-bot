import { useEffect, useState } from 'react';
import { StyleSheet, View } from 'react-native';

import SplashScreen from './app/Splash-screen/splash_screen';
import Home from './app/home-screen/home';
import Login from './app/login/login';


export default function App() {
  const [currentScreen, setCurrentScreen] = useState('splash');
  const [currentUser, setCurrentUser] = useState(null);

  useEffect(() => {
    // Show splash screen for 4 seconds
    const timer = setTimeout(() => {
      setCurrentScreen('login');
    }, 4000);

    return () => clearTimeout(timer);
  }, []);

  const goToHome = (userData) => {
    setCurrentUser(userData);
    setCurrentScreen('home');
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
        return <Home goToLogin={logout}/>;
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