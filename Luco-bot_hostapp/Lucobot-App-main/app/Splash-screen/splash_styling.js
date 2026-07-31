import { StyleSheet, Dimensions } from 'react-native';

const { width, height } = Dimensions.get("window");

const Splash = StyleSheet.create({
  container: {
    flex: 1,
    backgroundColor: '#000000', // Black background
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: height * 0.08, // responsive vertical padding
  },

  logoContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    marginTop: height * 0.12, // responsive margin top
  },

  lucotext: {
    fontSize: width * 0.12, // responsive font size
    fontWeight: 'bold',
    color: '#ffffff', 
  },

  bottext: {
    fontSize: width * 0.12, // responsive font size
    fontWeight: 'bold',
    color: '#00bcd4', 
  },

  loadingSection: {
    alignItems: 'center',
    marginVertical: height * 0.05, // responsive vertical spacing
  },

  loadingText: {
    fontSize: width * 0.045,
    color: '#ffffff',
    marginBottom: height * 0.02,
    fontWeight: '500',
  },

  dotsContainer: {
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
  },

  dot: {
    width: width * 0.02,
    height: width * 0.02,
    borderRadius: (width * 0.02) / 2,
    backgroundColor: '#00bcd4',
    marginHorizontal: width * 0.01,
  },

  tipContainer: {
    backgroundColor: '#1a1a1a',
    padding: width * 0.05,
    borderRadius: 12,
    marginHorizontal: width * 0.08,
    borderWidth: 1,
    borderColor: '#00bcd4',
  },

  tipTitle: {
    fontSize: width * 0.04,
    fontWeight: 'bold',
    color: '#ffffff',
    marginBottom: height * 0.01,
    textAlign: 'center',
  },

  tipText: {
    fontSize: width * 0.035,
    color: '#00bcd4',
    textAlign: 'center',
    lineHeight: height * 0.03,
  },

  footer: {
    alignItems: 'center',
  },

  footerText: {
    fontSize: width * 0.03,
    color: '#666666',
    marginBottom: height * 0.005,
  },
});

export default Splash;
