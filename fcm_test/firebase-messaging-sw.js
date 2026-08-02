importScripts("https://www.gstatic.com/firebasejs/10.7.0/firebase-app-compat.js");
importScripts("https://www.gstatic.com/firebasejs/10.7.0/firebase-messaging-compat.js");

const firebaseConfig = {
    apiKey: "AIzaSyCllAjou5mbHgzzbfUtwYZkbv6grDsP2Do",
    authDomain: "luco-bot.firebaseapp.com",
    projectId: "luco-bot",
    storageBucket: "luco-bot.firebasestorage.app",
    messagingSenderId: "370724217740",
    appId: "1:370724217740:web:3cc08c32cd96b5160aeca9"
  };

firebase.initializeApp(
  firebaseConfig
);

const messaging = firebase.messaging();

messaging.onBackgroundMessage((payload) => {
    console.log("received", payload);
  self.registration.showNotification(payload.notification.title, {
    body: payload.notification.body,
    icon: payload.notification.image
  });
});