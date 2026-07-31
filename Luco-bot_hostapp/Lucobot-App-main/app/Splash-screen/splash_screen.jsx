import { useEffect, useState } from 'react';
import { Animated, Text, View } from 'react-native';
import Splash from './splash_styling';

export default function SplashScreen() {
  const [loadingText, setLoadingText] = useState('');
  const [currentTip, setCurrentTip] = useState(0);
  const fadeAnim = new Animated.Value(0);

  const loadingTips = [
    "Preparing your admin dashboard...",
    "Syncing with LucuBot systems...",
    "Checking for new appointments...",
    "Loading notification center...",
    "Almost ready to manage your schedule!",
    "Connecting to faculty network..."
  ];

  const funFacts = [
    "LucuBot can handle up to 50 simultaneous appointments",
    "Faculty admins save 2 hours daily with automated notifications",
    "Over 200 faculty members use LucuBot daily",
    "Appointment reminders have 95% open rate",
    "LucuBot means 'Playful Helper' in robot language!"
  ];

  useEffect(() => {
    let dotCount = 0;
    const textInterval = setInterval(() => {
      dotCount = (dotCount + 1) % 4;
      setLoadingText(`Loading${'.'.repeat(dotCount)}`);
    }, 500);

    const tipInterval = setInterval(() => {
      setCurrentTip(prev => (prev + 1) % funFacts.length);
      fadeAnim.setValue(0);
      Animated.timing(fadeAnim, {
        toValue: 1,
        duration: 1000,
        useNativeDriver: true,
      }).start();
    }, 3000);

    return () => {
      clearInterval(textInterval);
      clearInterval(tipInterval);
    };
  }, []);

  return (
    <View style={Splash.container}>
      <View style={Splash.logoContainer}>
        <Text style={Splash.lucotext}>👋 Luco</Text> 
        <Text style={Splash.bottext}>Bot</Text>
      </View>

      <View style={Splash.loadingSection}>
        <Text style={Splash.loadingText}>{loadingText}</Text>
        <View style={Splash.dotsContainer}>
          {[0, 1, 2].map((index) => (
            <Animated.View 
              key={index}
              style={[
                Splash.dot,
                {
                  opacity: fadeAnim.interpolate({
                    inputRange: [0, 0.5, 1],
                    outputRange: [0.3, 1, 0.3],
                  }),
                }
              ]}
            />
          ))}
        </View>
      </View>

      <Animated.View style={[Splash.tipContainer, { opacity: fadeAnim }]}>
        <Text style={Splash.tipTitle}>Did you know?</Text>
        <Text style={Splash.tipText}>{funFacts[currentTip]}</Text>
      </Animated.View>

      <View style={Splash.footer}>
        <Text style={Splash.footerText}>Admin Portal v1.0</Text>
        <Text style={Splash.footerText}>Connected to LucuBot Network</Text>
      </View>
    </View>
  );
}
