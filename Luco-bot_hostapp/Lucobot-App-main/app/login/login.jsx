import { Ionicons } from '@expo/vector-icons';
import Constants from 'expo-constants';
import React, { useEffect, useRef, useState } from 'react';
import { login, registerDevice } from '../api/client';
import {
    Alert,
    Animated,
    Keyboard,
    KeyboardAvoidingView,
    Platform,
    ScrollView,
    Text,
    TextInput,
    TouchableOpacity,
    TouchableWithoutFeedback,
    View,
} from 'react-native';
import styles from './login_styling';


export default function Login({ goToHome }) {
    const [username, setUsername] = useState('');
    const [password, setPassword] = useState('');
    const [isLoading, setIsLoading] = useState(false);
    const [showPassword, setShowPassword] = useState(false);
    const [keyboardVisible, setKeyboardVisible] = useState(false);

    const eye1Anim = useRef(new Animated.Value(1)).current;
    const eye2Anim = useRef(new Animated.Value(1)).current;

    // Listen for keyboard events
    useEffect(() => {
        const keyboardDidShowListener = Keyboard.addListener(
            Platform.OS === 'ios' ? 'keyboardWillShow' : 'keyboardDidShow',
            () => setKeyboardVisible(true)
        );
        const keyboardDidHideListener = Keyboard.addListener(
            Platform.OS === 'ios' ? 'keyboardWillHide' : 'keyboardDidHide',
            () => setKeyboardVisible(false)
        );

        return () => {
            keyboardDidShowListener.remove();
            keyboardDidHideListener.remove();
        };
    }, []);

    const blinkEyes = () => {
        Animated.sequence([
            Animated.timing(eye1Anim, {
                toValue: 0.1,
                duration: 100,
                useNativeDriver: false,
            }),
            Animated.timing(eye2Anim, {
                toValue: 0.1,
                duration: 100,
                useNativeDriver: false,
            }),
            Animated.timing(eye1Anim, {
                toValue: 1,
                duration: 100,
                useNativeDriver: false,
            }),
            Animated.timing(eye2Anim, {
                toValue: 1,
                duration: 100,
                useNativeDriver: false,
            }),
        ]).start(() => {
            setTimeout(blinkEyes, Math.random() * 3000 + 1000);
        });
    };

    useEffect(() => {
        blinkEyes();
    }, []);

   const handleLogin = async () => {
    if (!username.trim() || !password.trim()) {
        Alert.alert('Error', 'Please enter both username and password');
        return;
    }

    setIsLoading(true);

    try {
        await login(username.trim(), password.trim());

        // Register device right after login, per spec.
        // Placeholder token for now — real push token comes later.
        await registerDevice('placeholder-device-token', Platform.OS);

        goToHome();
    } catch (error) {
        Alert.alert('Login Failed', error.message || 'Invalid credentials');
        console.error('Login error:', error);
    } finally {
        setIsLoading(false);
    }
};

    const togglePasswordVisibility = () => {
        setShowPassword(!showPassword);
    };

    const dismissKeyboard = () => {
        Keyboard.dismiss();
    };

    return (
        <KeyboardAvoidingView
            style={styles.keyboardAvoidingContainer}
            behavior={Platform.OS === 'ios' ? 'padding' : 'padding'}
            keyboardVerticalOffset={Platform.OS === 'ios' ? 0 : 0}
            enabled = {true}
        >
            <TouchableWithoutFeedback onPress={dismissKeyboard}>
                <ScrollView
                    contentContainerStyle={styles.scrollContainer}
                    keyboardShouldPersistTaps="handled"
                    showsVerticalScrollIndicator={false}
                    bounces={false}
                >
                    <View style={styles.container}>
                        {/* Robot head - smaller when keyboard is visible */}
                        {!keyboardVisible && (
                            <View style={styles.robotContainer}>
                                <View style={styles.robotHead}>
                                    <View style={styles.eyesContainer}>
                                        <Animated.View
                                            style={[
                                                styles.eye,
                                                {
                                                    height: eye1Anim.interpolate({
                                                        inputRange: [0.1, 1],
                                                        outputRange: [2, 20],
                                                    }),
                                                },
                                            ]}
                                        />
                                        <Animated.View
                                            style={[
                                                styles.eye,
                                                {
                                                    height: eye2Anim.interpolate({
                                                        inputRange: [0.1, 1],
                                                        outputRange: [2, 20],
                                                    }),
                                                },
                                            ]}
                                        />
                                    </View>
                                </View>
                            </View>
                        )}

                        <Text style={styles.welcomeText}>Welcome to LucoBot</Text>
                        <Text style={styles.subtitle}>Admin Portal - Faculty Login</Text>

                        <View style={styles.formContainer}>
                            <TextInput
                                placeholder="Employee ID / Email"
                                placeholderTextColor="#888"
                                style={styles.input}
                                value={username}
                                onChangeText={setUsername}
                                autoCapitalize="none"
                                keyboardType="email-address"
                                returnKeyType="next"
                            />
                            
                            {/* Password input with eye toggle */}
                            <View style={styles.passwordContainer}>
                                <TextInput
                                    placeholder="Password"
                                    placeholderTextColor="#888"
                                    secureTextEntry={!showPassword}
                                    style={styles.passwordInput}
                                    value={password}
                                    onChangeText={setPassword}
                                    returnKeyType="done"
                                    onSubmitEditing={handleLogin}
                                />
                                <TouchableOpacity
                                    style={styles.eyeButton}
                                    onPress={togglePasswordVisibility}
                                    hitSlop={{ top: 10, bottom: 10, left: 10, right: 10 }}
                                >
                                    <Ionicons
                                        name={showPassword ? 'eye-off-outline' : 'eye-outline'}
                                        size={24}
                                        color="#00bcd4"
                                    />
                                </TouchableOpacity>
                            </View>

                            <TouchableOpacity
                                style={[styles.loginButton, isLoading && styles.loginButtonDisabled]}
                                onPress={handleLogin}
                                disabled={isLoading}
                                activeOpacity={0.8}
                            >
                                <Text style={styles.loginButtonText}>
                                    {isLoading ? 'LOGGING IN...' : 'LOGIN'}
                                </Text>
                            </TouchableOpacity>
                        </View>
                    </View>
                </ScrollView>
            </TouchableWithoutFeedback>
        </KeyboardAvoidingView>
    );
}
