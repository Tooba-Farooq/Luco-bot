import { StyleSheet, Dimensions, Platform } from 'react-native';

const { width, height } = Dimensions.get("window");

// Responsive scaling functions for cross-platform consistency
const scale = (size) => (width / 375) * size;
const verticalScale = (size) => (height / 812) * size;
const moderateScale = (size, factor = 0.5) => size + (scale(size) - size) * factor;

const styles = StyleSheet.create({
    keyboardAvoidingContainer: {
        flex: 1,
        backgroundColor: '#000000',
    },
    scrollContainer: {
        flexGrow: 1,
        justifyContent: 'center',
        paddingBottom: Platform.select({ ios: 0, android: 20 }),
    },
    container: {
        backgroundColor: '#000000',
        alignItems: 'center',
        justifyContent: 'center',
        paddingHorizontal: scale(20),
        paddingVertical: verticalScale(24),
    },
    robotContainer: {
        marginBottom: verticalScale(40),
    },
    robotHead: {
        width: scale(95),
        height: scale(95),
        backgroundColor: '#00bcd4',
        borderRadius: scale(95) / 2,
        alignItems: 'center',
        justifyContent: 'center',
        borderWidth: 3,
        borderColor: '#ffffff',
        shadowColor: '#00bcd4',
        shadowOffset: { width: 0, height: 0 },
        shadowOpacity: 0.5,
        shadowRadius: 10,
        elevation: 8,
    },
    eyesContainer: {
        flexDirection: 'row',
        justifyContent: 'space-between',
        width: '60%',
    },
    eye: {
        width: scale(18),
        backgroundColor: '#000000',
        borderRadius: scale(18) / 2,
    },
    welcomeText: {
        color: '#00bcd4',
        fontSize: moderateScale(22),
        fontWeight: 'bold',
        marginBottom: verticalScale(8),
        textAlign: 'center',
    },
    subtitle: {
        color: '#ffffff',
        fontSize: moderateScale(15),
        marginBottom: verticalScale(40),
        textAlign: 'center',
        opacity: 0.8,
    },
    tagline: {
        color: '#666',
        fontSize: moderateScale(12),
        textAlign: 'center',
        marginTop: verticalScale(-24),
        marginBottom: verticalScale(32),
        paddingHorizontal: scale(20),
        lineHeight: moderateScale(17),
    },
    formContainer: {
        width: '100%',
        maxWidth: 400,
        paddingHorizontal: scale(10),
    },
    input: {
        backgroundColor: '#1a1a1a',
        color: '#ffffff',
        paddingVertical: Platform.select({
            ios: verticalScale(16),
            android: verticalScale(14),
        }),
        paddingHorizontal: scale(16),
        borderRadius: 12,
        marginBottom: verticalScale(16),
        borderWidth: 2,
        borderColor: '#333',
        fontSize: moderateScale(15),
        shadowColor: '#000',
        shadowOffset: { width: 0, height: 2 },
        shadowOpacity: 0.25,
        shadowRadius: 3.84,
        elevation: 5,
    },
    // Password input container with eye toggle
    passwordContainer: {
        flexDirection: 'row',
        alignItems: 'center',
        backgroundColor: '#1a1a1a',
        borderRadius: 12,
        marginBottom: verticalScale(16),
        borderWidth: 2,
        borderColor: '#333',
        shadowColor: '#000',
        shadowOffset: { width: 0, height: 2 },
        shadowOpacity: 0.25,
        shadowRadius: 3.84,
        elevation: 5,
    },
    passwordInput: {
        flex: 1,
        color: '#ffffff',
        paddingVertical: Platform.select({
            ios: verticalScale(16),
            android: verticalScale(14),
        }),
        paddingHorizontal: scale(16),
        paddingRight: scale(50),
        fontSize: moderateScale(15),
    },
    eyeButton: {
        position: 'absolute',
        right: scale(12),
        height: '100%',
        justifyContent: 'center',
        alignItems: 'center',
        paddingHorizontal: scale(8),
    },
    loginButton: {
        backgroundColor: '#00bcd4',
        paddingVertical: Platform.select({
            ios: verticalScale(16),
            android: verticalScale(14),
        }),
        borderRadius: 12,
        alignItems: 'center',
        marginTop: verticalScale(12),
        shadowColor: '#00bcd4',
        shadowOffset: { width: 0, height: 4 },
        shadowOpacity: 0.3,
        shadowRadius: 4.65,
        elevation: 8,
    },
    loginButtonDisabled: {
        backgroundColor: '#666',
        opacity: 0.7,
    },
    loginButtonText: {
        color: '#000000',
        fontSize: moderateScale(16),
        fontWeight: 'bold',
        letterSpacing: 1,
    },
});

export default styles;
