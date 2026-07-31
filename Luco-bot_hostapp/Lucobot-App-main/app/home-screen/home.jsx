import { Ionicons } from "@expo/vector-icons";
import Constants from "expo-constants";
import { useCallback, useEffect, useRef, useState } from "react";
import {
  Alert,
  Animated,
  AppState,
  FlatList,
  Image,
  LayoutAnimation,
  Modal,
  Platform,
  Text,
  TouchableOpacity,
  TouchableWithoutFeedback,
  UIManager,
  View,
} from "react-native";
import { io } from "socket.io-client";
import home_styles from "./home_styling";

// Enable LayoutAnimation for Android
if (Platform.OS === 'android' && UIManager.setLayoutAnimationEnabledExperimental) {
  UIManager.setLayoutAnimationEnabledExperimental(true);
}

function getServerUrl() {
  const envUrl = process.env.EXPO_PUBLIC_SERVER_URL;
  if (envUrl && typeof envUrl === "string" && envUrl.trim().length > 0) return envUrl.trim();

  const hostUri =
    Constants.expoConfig?.hostUri ||
    Constants.manifest?.debuggerHost ||
    Constants.manifest2?.extra?.expoClient?.hostUri ||
    "";

  const host = String(hostUri).split(":")[0];
  if (host) return `http://${host}:3000`;

  return "http://localhost:3000";
}

const SERVER_URL = getServerUrl();
// Helper function to format date as "Month DD, YYYY"
const formatDate = (dateString) => {
  if (!dateString) return 'N/A';
  try {
    const date = new Date(dateString);
    const months = ['January', 'February', 'March', 'April', 'May', 'June', 
                    'July', 'August', 'September', 'October', 'November', 'December'];
    const month = months[date.getMonth()];
    const day = date.getDate();
    const year = date.getFullYear();
    return `${month} ${day}, ${year}`;
  } catch (e) {
    return dateString;
  }
};

// Helper function to format time as "XX:XX AM/PM"
const formatTime = (timeString) => {
  if (!timeString) return 'N/A';
  try {
    // Handle HH:MM:SS format
    const parts = timeString.split(':');
    let hours = parseInt(parts[0], 10);
    const minutes = parts[1] || '00';
    const ampm = hours >= 12 ? 'PM' : 'AM';
    hours = hours % 12;
    hours = hours === 0 ? 12 : hours;
    return `${hours}:${minutes} ${ampm}`;
  } catch (e) {
    return timeString;
  }
};

// Animated appointment card with Review button, long press for delete, and fade-out animation support
const AnimatedCard = ({ item, index, onReview, onLongPress, isRemoving }) => {
  const fadeAnim = useRef(new Animated.Value(0)).current;
  const translateY = useRef(new Animated.Value(20)).current;
  const scaleAnim = useRef(new Animated.Value(1)).current;

  useEffect(() => {
    // Entry animation
    Animated.parallel([
      Animated.timing(fadeAnim, {
        toValue: 1,
        duration: 400,
        delay: index * 100,
        useNativeDriver: true,
      }),
      Animated.timing(translateY, {
        toValue: 0,
        duration: 400,
        delay: index * 100,
        useNativeDriver: true,
      }),
    ]).start();
  }, []);

  // Handle removal animation - smooth fade out only (no slide)
  useEffect(() => {
    if (isRemoving) {
      Animated.parallel([
        Animated.timing(fadeAnim, {
          toValue: 0,
          duration: 400,
          useNativeDriver: true,
        }),
        Animated.timing(scaleAnim, {
          toValue: 0.95,
          duration: 400,
          useNativeDriver: true,
        }),
      ]).start();
    }
  }, [isRemoving]);

  const getStatusColor = (status) => {
    switch (status) {
      case "pending":
        return "#FFA500";
      case "approved":
        return "#00FF00";
      case "rejected":
        return "#FF0000";
      default:
        return "#00bcd4";
    }
  };

  const getStatusLabel = (status) => {
    switch (status) {
      case "pending":
        return "Pending Review";
      case "approved":
        return "Approved";
      case "rejected":
        return "Declined";
      default:
        return status.charAt(0).toUpperCase() + status.slice(1);
    }
  };

  // Check if visitor has an image
  const hasVisitorImage = item.visitor_image && item.visitor_image.length > 0;

  // FIX 8: Wrap in TouchableOpacity for long press on approved items
  const CardContent = (
    <Animated.View
      style={[
        home_styles.card,
        item.status === "approved" && home_styles.approvedCard,
        {
          opacity: fadeAnim,
          transform: [
            { translateY },
            { scale: scaleAnim },
          ],
        },
      ]}
    >
      <View style={home_styles.cardHeader}>
        <View style={home_styles.cardTitleRow}>
          {hasVisitorImage && (
            <View style={home_styles.cardImageIndicator}>
              <Ionicons name="camera" size={14} color="#00bcd4" />
            </View>
          )}
          <Text style={home_styles.cardTitle}>Appointment Request</Text>
        </View>
        <View
          style={[
            home_styles.statusBadge,
            { backgroundColor: getStatusColor(item.status) + '20', borderColor: getStatusColor(item.status) },
          ]}
        >
          <View style={[home_styles.statusDot, { backgroundColor: getStatusColor(item.status) }]} />
          <Text style={[home_styles.statusBadgeText, { color: getStatusColor(item.status) }]}>
            {getStatusLabel(item.status)}
          </Text>
        </View>
      </View>

      <View style={home_styles.cardBody}>
        <View style={home_styles.infoRow}>
          <Ionicons name="person-outline" size={16} color="#00bcd4" />
          <Text style={home_styles.cardText}>{item.student_name}</Text>
        </View>
        <View style={home_styles.infoRow}>
          <Ionicons name="document-text-outline" size={16} color="#00bcd4" />
          <Text style={home_styles.cardText} numberOfLines={1}>{item.purpose}</Text>
        </View>
      </View>

      {item.status === "pending" && (
        <TouchableOpacity
          style={home_styles.reviewButton}
          onPress={() => onReview(item)}
          activeOpacity={0.8}
        >
          <Text style={home_styles.reviewButtonText}>Review</Text>
          <Ionicons name="chevron-forward" size={18} color="#000000" />
        </TouchableOpacity>
      )}
    </Animated.View>
  );

  // FIX 8: Only enable long press for approved appointments
  if (item.status === "approved") {
    return (
      <TouchableOpacity
        onLongPress={() => onLongPress(item)}
        delayLongPress={500}
        activeOpacity={0.9}
      >
        {CardContent}
      </TouchableOpacity>
    );
  }

  return CardContent;
};

// Custom Success Toast Component
const SuccessToast = ({ visible, message, onHide }) => {
  const fadeAnim = useRef(new Animated.Value(0)).current;
  const scaleAnim = useRef(new Animated.Value(0.8)).current;

  useEffect(() => {
    if (visible) {
      // Fade in
      Animated.parallel([
        Animated.timing(fadeAnim, {
          toValue: 1,
          duration: 300,
          useNativeDriver: true,
        }),
        Animated.spring(scaleAnim, {
          toValue: 1,
          friction: 8,
          tension: 40,
          useNativeDriver: true,
        }),
      ]).start();

      // Auto hide after 2.5 seconds
      const timer = setTimeout(() => {
        Animated.parallel([
          Animated.timing(fadeAnim, {
            toValue: 0,
            duration: 300,
            useNativeDriver: true,
          }),
          Animated.timing(scaleAnim, {
            toValue: 0.8,
            duration: 300,
            useNativeDriver: true,
          }),
        ]).start(() => {
          onHide();
        });
      }, 2500);

      return () => clearTimeout(timer);
    }
  }, [visible]);

  if (!visible) return null;

  return (
    <Modal transparent visible={visible} animationType="none">
      <View style={home_styles.toastOverlay}>
        <Animated.View
          style={[
            home_styles.toastContainer,
            {
              opacity: fadeAnim,
              transform: [{ scale: scaleAnim }],
            },
          ]}
        >
          <Ionicons name="checkmark-circle" size={40} color="#00FF00" style={{ marginBottom: 12 }} />
          <Text style={home_styles.toastText}>{message}</Text>
        </Animated.View>
      </View>
    </Modal>
  );
};

// New Appointment Toast (for real-time notifications)
const NewAppointmentToast = ({ visible, visitorName, onHide }) => {
  const slideAnim = useRef(new Animated.Value(-100)).current;
  const fadeAnim = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    if (visible) {
      // Slide in from top
      Animated.parallel([
        Animated.spring(slideAnim, {
          toValue: 0,
          friction: 8,
          tension: 40,
          useNativeDriver: true,
        }),
        Animated.timing(fadeAnim, {
          toValue: 1,
          duration: 300,
          useNativeDriver: true,
        }),
      ]).start();

      // Auto hide after 3 seconds
      const timer = setTimeout(() => {
        Animated.parallel([
          Animated.timing(slideAnim, {
            toValue: -100,
            duration: 300,
            useNativeDriver: true,
          }),
          Animated.timing(fadeAnim, {
            toValue: 0,
            duration: 300,
            useNativeDriver: true,
          }),
        ]).start(() => {
          onHide();
        });
      }, 3000);

      return () => clearTimeout(timer);
    }
  }, [visible]);

  if (!visible) return null;

  return (
    <Animated.View
      style={[
        home_styles.newAppointmentToast,
        {
          opacity: fadeAnim,
          transform: [{ translateY: slideAnim }],
        },
      ]}
    >
      <Ionicons name="notifications" size={24} color="#00bcd4" />
      <View style={home_styles.newAppointmentToastText}>
        <Text style={home_styles.newAppointmentTitle}>New Appointment</Text>
        <Text style={home_styles.newAppointmentSubtitle}>{visitorName} is waiting</Text>
      </View>
    </Animated.View>
  );
};

export default function Home({ logout, currentUser }) {
  const [showDropdown, setShowDropdown] = useState(false);
  const [modalVisible, setModalVisible] = useState(false);
  const [selectedAppointment, setSelectedAppointment] = useState(null);
  const [appointments, setAppointments] = useState([]);
  const [loading, setLoading] = useState(true);
  const [removingId, setRemovingId] = useState(null);
  const [showSuccessToast, setShowSuccessToast] = useState(false);
  const [showNewAppointmentToast, setShowNewAppointmentToast] = useState(false);
  const [newVisitorName, setNewVisitorName] = useState("");
  // FIX 8: Delete modal for approved appointments
  const [deleteModalVisible, setDeleteModalVisible] = useState(false);
  const [appointmentToDelete, setAppointmentToDelete] = useState(null);
  
  const socketRef = useRef(null);

  // FIX 7: Initialize Socket.IO connection with AppState handling for reconnection
  useEffect(() => {
    if (currentUser?.employee_id) {
      const connectSocket = () => {
        // Disconnect existing socket if any
        if (socketRef.current) {
          socketRef.current.disconnect();
        }
        
        // Connect to socket server
        socketRef.current = io(SERVER_URL, {
          transports: ['websocket', 'polling'],
          reconnection: true,
          reconnectionAttempts: Infinity,
          reconnectionDelay: 1000,
          reconnectionDelayMax: 5000,
        });

        socketRef.current.on('connect', () => {
          console.log('Socket connected:', socketRef.current.id);
          // Join employee-specific room
          socketRef.current.emit('join_employee_room', currentUser.employee_id);
          // Refresh appointments on reconnect
          fetchAppointments();
        });

        socketRef.current.on('disconnect', () => {
          console.log('Socket disconnected');
        });

        // Listen for new appointments
        socketRef.current.on('new_appointment', (appointment) => {
          console.log('New appointment received:', appointment);
          
          // Show notification toast
          setNewVisitorName(appointment.student_name);
          setShowNewAppointmentToast(true);
          
          // Add to appointments list with animation (dedupe by id)
          LayoutAnimation.configureNext({
            duration: 300,
            create: { type: LayoutAnimation.Types.easeInEaseOut, property: LayoutAnimation.Properties.opacity },
            update: { type: LayoutAnimation.Types.easeInEaseOut },
          });
        
          setAppointments((prev) => {
            // Normalize ids as strings for safe comparison
            const incomingId = appointment && appointment.id != null ? String(appointment.id) : null;

            // If incoming appointment has an id, replace existing entry with same id
            if (incomingId) {
              const existsIndex = prev.findIndex((a) => a && a.id != null && String(a.id) === incomingId);
              let updated;
              if (existsIndex !== -1) {
                // Replace existing and move to front
                updated = [appointment, ...prev.filter((a, i) => i !== existsIndex)];
              } else {
                updated = [appointment, ...prev];
              }

              // Ensure unique by id
              const unique = [];
              const seen = new Set();
              for (const a of updated) {
                const key = a && a.id != null ? String(a.id) : JSON.stringify(a);
                if (!seen.has(key)) {
                  seen.add(key);
                  unique.push(a);
                }
              }
              return sortAndFilterAppointments(unique);
            }

            // Fallback: if no id provided, prepend and dedupe by stringified item
            const merged = [appointment, ...prev];
            const deduped = merged.filter((v, i, arr) => arr.findIndex(x => JSON.stringify(x) === JSON.stringify(v)) === i);
            return sortAndFilterAppointments(deduped);
          });
        });
      };

      // Initial connection
      connectSocket();

      // FIX 7: Handle app state changes (background -> foreground)
      const handleAppStateChange = (nextAppState) => {
        if (nextAppState === 'active') {
          console.log('App came to foreground, reconnecting socket...');
          // Reconnect socket and refresh data
          if (socketRef.current && !socketRef.current.connected) {
            socketRef.current.connect();
          }
          // Always refresh appointments when coming back to foreground
          fetchAppointments();
        }
      };

      const appStateSubscription = AppState.addEventListener('change', handleAppStateChange);

      return () => {
        if (socketRef.current) {
          socketRef.current.disconnect();
        }
        appStateSubscription?.remove();
      };
    }
  }, [currentUser?.employee_id]);

  useEffect(() => {
    if (!currentUser?.employee_id) return;
    fetchAppointments();
  }, [currentUser?.employee_id]);

  // Sort appointments: pending first, then approved
  // Filter out rejected appointments
  const sortAndFilterAppointments = useCallback((appointmentsList) => {
    return appointmentsList
      .filter((apt) => apt.status !== "rejected") // Remove declined appointments
      .sort((a, b) => {
        // Priority: pending (0) > approved (1)
        const getPriority = (status) => {
          switch (status) {
            case "pending":
              return 0;
            case "approved":
              return 1;
            default:
              return 2;
          }
        };
        return getPriority(a.status) - getPriority(b.status);
      });
  }, []);

  const fetchAppointments = async () => {
    try {
      setLoading(true);
      if (!currentUser?.employee_id) {
        setLoading(false);
        return;
      }

      const response = await fetch(
        `${SERVER_URL}/api/appointments/${currentUser.employee_id}`,
        { method: "GET", headers: { "Content-Type": "application/json" } }
      );
      const data = await response.json();
      if (response.ok) {
        // Ensure unique appointments by id
        const raw = data.appointments || [];
        const unique = [];
        const seen = new Set();
        for (const a of raw) {
          const key = a && a.id != null ? String(a.id) : JSON.stringify(a);
          if (!seen.has(key)) {
            seen.add(key);
            unique.push(a);
          }
        }
        const sortedAppointments = sortAndFilterAppointments(unique);
        setAppointments(sortedAppointments);
      } else {
        Alert.alert("Error", "Failed to fetch appointments");
      }
    } catch (error) {
      console.error("Fetch appointments error:", error);
      Alert.alert("Error", "Network error while fetching appointments");
    } finally {
      setLoading(false);
    }
  };

  const toggleDropdown = () => setShowDropdown(!showDropdown);

  const handleLogout = () => {
    setShowDropdown(false);
    if (socketRef.current) {
      socketRef.current.disconnect();
    }
    logout();
  };

  const openModal = (appointment) => {
    setSelectedAppointment(appointment);
    setModalVisible(true);
  };

  const closeModal = () => {
    setSelectedAppointment(null);
    setModalVisible(false);
  };

  const handleAppointmentAction = async (action) => {
    try {
      const appointmentId = selectedAppointment.id;
      
      // Close modal immediately for better UX
      closeModal();

      // If declining, trigger fade-out animation first (no notification)
      if (action === "reject") {
        setRemovingId(appointmentId);
        
        // Wait for animation to complete before removing from list
        setTimeout(() => {
          // Configure layout animation for smooth list adjustment
          LayoutAnimation.configureNext({
            duration: 300,
            create: { type: LayoutAnimation.Types.easeInEaseOut, property: LayoutAnimation.Properties.opacity },
            update: { type: LayoutAnimation.Types.easeInEaseOut },
            delete: { type: LayoutAnimation.Types.easeInEaseOut, property: LayoutAnimation.Properties.opacity },
          });

          // Remove from local state immediately
          setAppointments((prev) =>
            prev.filter((apt) => apt.id !== appointmentId)
          );
          setRemovingId(null);
        }, 450);
      }

      const response = await fetch(
        `${SERVER_URL}/api/appointments/${appointmentId}/${action}`,
        {
          method: "PUT",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ employee_id: currentUser?.employee_id }),
        }
      );
      const data = await response.json();
      
      if (response.ok) {
        // Only show success toast for approval action
        if (action === "approve") {
          setShowSuccessToast(true);
          
          LayoutAnimation.configureNext({
            duration: 300,
            create: { type: LayoutAnimation.Types.easeInEaseOut, property: LayoutAnimation.Properties.opacity },
            update: { type: LayoutAnimation.Types.easeInEaseOut },
          });
          
          setAppointments((prev) => {
            const updated = prev.map((apt) =>
              apt.id === appointmentId ? { ...apt, status: "approved" } : apt
            );
            return sortAndFilterAppointments(updated);
          });
        }
        // No notification for decline action
      } else {
        // If server action failed, refetch to restore correct state
        Alert.alert("Error", data.message || `Failed to ${action} appointment`);
        fetchAppointments();
      }
    } catch (error) {
      console.error(`${action} appointment error:`, error);
      Alert.alert("Error", "Network error. Please try again.");
      fetchAppointments();
    }
  };

  const handleAccept = () => handleAppointmentAction("approve");
  const handleDecline = () => handleAppointmentAction("reject");

  // FIX 8: Handle long press on approved appointments
  const handleLongPressApproved = (appointment) => {
    if (appointment.status === "approved") {
      setAppointmentToDelete(appointment);
      setDeleteModalVisible(true);
    }
  };

  // FIX 8: Handle delete appointment
  const handleDeleteAppointment = async () => {
    if (!appointmentToDelete) return;
    
    const appointmentId = appointmentToDelete.id;
    setDeleteModalVisible(false);
    
    // Trigger fade-out animation
    setRemovingId(appointmentId);
    
    // Wait for animation to complete
    setTimeout(async () => {
      try {
        const response = await fetch(
          `${SERVER_URL}/api/appointments/${appointmentId}`,
          {
            method: "DELETE",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ employee_id: currentUser?.employee_id }),
          }
        );
        
        if (response.ok) {
          // Configure layout animation for smooth list adjustment
          LayoutAnimation.configureNext({
            duration: 300,
            create: { type: LayoutAnimation.Types.easeInEaseOut, property: LayoutAnimation.Properties.opacity },
            update: { type: LayoutAnimation.Types.easeInEaseOut },
            delete: { type: LayoutAnimation.Types.easeInEaseOut, property: LayoutAnimation.Properties.opacity },
          });
          
          // Remove from local state
          setAppointments((prev) =>
            prev.filter((apt) => apt.id !== appointmentId)
          );
        } else {
          Alert.alert("Error", "Failed to delete appointment");
          fetchAppointments();
        }
      } catch (error) {
        console.error("Delete appointment error:", error);
        Alert.alert("Error", "Network error. Please try again.");
        fetchAppointments();
      } finally {
        setRemovingId(null);
        setAppointmentToDelete(null);
      }
    }, 450);
  };

  const closeDeleteModal = () => {
    setDeleteModalVisible(false);
    setAppointmentToDelete(null);
  };

  const renderEmptyState = () => (
    <View style={home_styles.emptyContainer}>
      <View style={home_styles.emptyCard}>
        <Ionicons name="calendar-outline" size={48} color="#00bcd4" style={{ marginBottom: 15 }} />
        <Text style={home_styles.emptyTitle}>No Appointments</Text>
        <Text style={home_styles.emptyText}>
          {loading
            ? "Loading appointments..."
            : "No appointment requests found! ☕\nYour schedule is clear."}
        </Text>
      </View>
    </View>
  );

  const renderItem = useCallback(
    ({ item, index }) => (
      <AnimatedCard
        item={item}
        index={index}
        onReview={openModal}
        onLongPress={handleLongPressApproved}
        isRemoving={removingId === item.id}
      />
    ),
    [removingId]
  );

  const keyExtractor = useCallback((item, index) => {
    if (item && item.id != null) return String(item.id);
    // Fallback to a stable key when id is missing
    const name = item?.student_name ?? '';
    const date = item?.preferred_date ?? '';
    const time = item?.preferred_time ?? '';
    return `tmp-${index}-${name}-${date}-${time}`;
  }, []);

  // Check if selected appointment has a visitor image
  const hasVisitorImage = selectedAppointment?.visitor_image && selectedAppointment.visitor_image.length > 0;

  return (
    <View style={[home_styles.container, { position: "relative" }]}>
      {/* New Appointment Toast */}
      <NewAppointmentToast
        visible={showNewAppointmentToast}
        visitorName={newVisitorName}
        onHide={() => setShowNewAppointmentToast(false)}
      />

      {/* Navbar */}
      <View style={home_styles.navbar}>
        <TouchableOpacity onPress={toggleDropdown} style={home_styles.settingsButton}>
          <Ionicons name="settings-outline" size={28} color="black" />
        </TouchableOpacity>

        <Text style={home_styles.title}>Admin</Text>

        <View style={home_styles.userInfo}>
          <Text style={home_styles.userName}>{currentUser?.name || "Faculty"}</Text>
        </View>
      </View>

      {/* Appointments */}
      <View style={home_styles.content}>
        {appointments.length === 0 ? (
          renderEmptyState()
        ) : (
          <FlatList
            data={appointments}
            renderItem={renderItem}
            keyExtractor={keyExtractor}
            contentContainerStyle={home_styles.listContainer}
            showsVerticalScrollIndicator={false}
            refreshing={loading}
            onRefresh={fetchAppointments}
            removeClippedSubviews={false}
          />
        )}
      </View>

      {/* Dropdown rendered last for touch responsiveness */}
      {showDropdown && (
        <View
          style={[
            home_styles.dropdown,
            { top: 100, right: 10, position: "absolute", zIndex: 9999, elevation: 99 },
          ]}
        >
          <TouchableOpacity
            style={home_styles.dropdownItem}
            onPress={() => {
              setShowDropdown(false);
              fetchAppointments();
            }}
          >
            <Ionicons name="refresh-outline" size={18} color="#000000" style={{ marginRight: 8 }} />
            <Text style={home_styles.dropdownText}>Refresh</Text>
          </TouchableOpacity>
          <TouchableOpacity style={home_styles.dropdownItem} onPress={handleLogout}>
            <Ionicons name="log-out-outline" size={18} color="#000000" style={{ marginRight: 8 }} />
            <Text style={home_styles.dropdownText}>Logout</Text>
          </TouchableOpacity>
        </View>
      )}

      {/* Review Modal */}
      <Modal visible={modalVisible} transparent animationType="fade" onRequestClose={closeModal}>
        <TouchableWithoutFeedback onPress={closeModal}>
          <View style={home_styles.modalOverlay}>
            <TouchableWithoutFeedback>
              <Animated.View style={home_styles.modalContainer}>
                <TouchableOpacity 
                  style={home_styles.closeButton} 
                  onPress={closeModal}
                  hitSlop={{ top: 20, bottom: 20, left: 20, right: 20 }}
                  activeOpacity={0.7}
                >
                  <Ionicons name="close" size={28} color="#00bcd4" />
                </TouchableOpacity>

                <Text style={home_styles.modalTitle}>Appointment Details</Text>

                {/* Visitor Image (if consented) */}
                {hasVisitorImage ? (
                  <View style={home_styles.visitorImageContainer}>
                    <Image
                      source={{ uri: `data:image/jpeg;base64,${selectedAppointment.visitor_image}` }}
                      style={home_styles.visitorImage}
                      resizeMode="cover"
                    />
                  </View>
                ) : (
                  <View style={home_styles.noImageContainer}>
                    <Ionicons name="person-circle-outline" size={60} color="#444" />
                    <Text style={home_styles.noImageText}>No photo available</Text>
                  </View>
                )}

                <View style={home_styles.modalContent}>
                  <View style={home_styles.modalRow}>
                    <View style={home_styles.modalIconContainer}>
                      <Ionicons name="person" size={20} color="#00bcd4" />
                    </View>
                    <View style={home_styles.modalTextContainer}>
                      <Text style={home_styles.modalLabel}>Visitor</Text>
                      <Text style={home_styles.modalValue}>
                        {selectedAppointment?.student_name}
                      </Text>
                    </View>
                  </View>

                  <View style={home_styles.modalRow}>
                    <View style={home_styles.modalIconContainer}>
                      <Ionicons name="calendar" size={20} color="#00bcd4" />
                    </View>
                    <View style={home_styles.modalTextContainer}>
                      <Text style={home_styles.modalLabel}>Date</Text>
                      <Text style={home_styles.modalValue}>
                        {formatDate(selectedAppointment?.preferred_date)}
                      </Text>
                    </View>
                  </View>

                  <View style={home_styles.modalRow}>
                    <View style={home_styles.modalIconContainer}>
                      <Ionicons name="time" size={20} color="#00bcd4" />
                    </View>
                    <View style={home_styles.modalTextContainer}>
                      <Text style={home_styles.modalLabel}>Time</Text>
                      <Text style={home_styles.modalValue}>
                        {formatTime(selectedAppointment?.preferred_time)}
                      </Text>
                    </View>
                  </View>

                  <View style={home_styles.modalRow}>
                    <View style={home_styles.modalIconContainer}>
                      <Ionicons name="document-text" size={20} color="#00bcd4" />
                    </View>
                    <View style={home_styles.modalTextContainer}>
                      <Text style={home_styles.modalLabel}>Purpose</Text>
                      <Text style={home_styles.modalValue}>
                        {selectedAppointment?.purpose}
                      </Text>
                    </View>
                  </View>
                </View>

                <View style={home_styles.actionContainer}>
                  <TouchableOpacity
                    style={home_styles.declineButton}
                    onPress={handleDecline}
                    activeOpacity={0.8}
                  >
                    <Ionicons name="close-circle-outline" size={20} color="#ffffff" />
                    <Text style={home_styles.declineButtonText}>Decline</Text>
                  </TouchableOpacity>
                  <TouchableOpacity
                    style={home_styles.acceptButton}
                    onPress={handleAccept}
                    activeOpacity={0.8}
                  >
                    <Ionicons name="checkmark-circle-outline" size={20} color="#000000" />
                    <Text style={home_styles.acceptButtonText}>Accept</Text>
                  </TouchableOpacity>
                </View>
              </Animated.View>
            </TouchableWithoutFeedback>
          </View>
        </TouchableWithoutFeedback>
      </Modal>

      {/* Custom Success Toast */}
      <SuccessToast
        visible={showSuccessToast}
        message="Appointment approved successfully!"
        onHide={() => setShowSuccessToast(false)}
      />

      {/* FIX 8: Delete Confirmation Modal for Approved Appointments */}
      <Modal visible={deleteModalVisible} transparent animationType="fade" onRequestClose={closeDeleteModal}>
        <TouchableWithoutFeedback onPress={closeDeleteModal}>
          <View style={home_styles.modalOverlay}>
            <TouchableWithoutFeedback>
              <View style={home_styles.deleteModalContainer}>
                <View style={home_styles.deleteModalHeader}>
                  <Ionicons name="trash-outline" size={28} color="#FF6B6B" />
                  <Text style={home_styles.deleteModalTitle}>Delete Appointment</Text>
                </View>
                
                <Text style={home_styles.deleteModalText}>
                  Are you sure you want to remove this approved appointment from your list?
                </Text>
                
                {appointmentToDelete && (
                  <View style={home_styles.deleteModalInfo}>
                    <Text style={home_styles.deleteModalInfoText}>
                      Visitor: {appointmentToDelete.student_name}
                    </Text>
                  </View>
                )}
                
                <View style={home_styles.deleteModalActions}>
                  <TouchableOpacity
                    style={home_styles.deleteModalCloseButton}
                    onPress={closeDeleteModal}
                    activeOpacity={0.8}
                  >
                    <Text style={home_styles.deleteModalCloseText}>Close</Text>
                  </TouchableOpacity>
                  <TouchableOpacity
                    style={home_styles.deleteModalDeleteButton}
                    onPress={handleDeleteAppointment}
                    activeOpacity={0.8}
                  >
                    <Ionicons name="trash" size={18} color="#ffffff" />
                    <Text style={home_styles.deleteModalDeleteText}>Delete</Text>
                  </TouchableOpacity>
                </View>
              </View>
            </TouchableWithoutFeedback>
          </View>
        </TouchableWithoutFeedback>
      </Modal>
    </View>
  );
}