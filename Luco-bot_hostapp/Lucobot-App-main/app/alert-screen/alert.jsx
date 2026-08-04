import { useState, useEffect } from "react";
import {
  View,
  Text,
  Image,
  FlatList,
  TouchableOpacity,
  StyleSheet,
  Modal,
  Alert,
  ActivityIndicator,
} from "react-native";
import { respondToAlert } from "../api/client";

function parseUTC(isoString) {
  if (!isoString) return null;
  const hasTZ = /Z$|[+-]\d{2}:\d{2}$/.test(isoString);
  return new Date(hasTZ ? isoString : isoString + "Z");
}

// Returns minutes waited, or 0 if just arrived / invalid
function minutesWaiting(arrivedAt) {
  const arrived = parseUTC(arrivedAt);
  if (!arrived) return 0;
  const diffMs = Date.now() - arrived.getTime();
  return Math.max(0, Math.floor(diffMs / 60000));
}

// Urgency thresholds — tweak these two numbers to change when colors escalate
const WAIT_WARNING_MIN = 8;   // amber from here
const WAIT_URGENT_MIN = 20;   // red from here

function getUrgency(minutes) {
  if (minutes >= WAIT_URGENT_MIN) return "urgent";
  if (minutes >= WAIT_WARNING_MIN) return "warning";
  return "normal";
}

const WAIT_OPTIONS = [5, 10, 30, 60, 120];

export default function AlertScreen({ alerts, onResolved, goToHome }) {
  const [waitModalFor, setWaitModalFor] = useState(null);
  const [submitting, setSubmitting] = useState(null);
  // Forces re-render every 30s so "waiting X min" badges update live
  const [, setTick] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => setTick((t) => t + 1), 30000);
    return () => clearInterval(interval);
  }, []);

  const handleRespond = async (sessionId, response, waitMinutes) => {
    setSubmitting(sessionId);
    try {
      await respondToAlert(sessionId, response, waitMinutes);
      onResolved(sessionId, response, waitMinutes);
    } catch (err) {
      console.error("Respond failed:", err);
      Alert.alert("Error", err.message || "Could not send response");
    } finally {
      setSubmitting(null);
      setWaitModalFor(null);
    }
  };

  const renderItem = ({ item }) => {
    const waitedMin = minutesWaiting(item.arrived_at);
    const urgency = getUrgency(waitedMin);
    const isNew = waitedMin < 1;

    return (
      <View
        style={[
          styles.card,
          urgency === "urgent" && styles.cardUrgent,
        ]}
      >
        {isNew && (
          <View style={styles.newBadge}>
            <Text style={styles.newBadgeText}>NEW</Text>
          </View>
        )}

        {item.visitor_photo_url ? (
          <Image source={{ uri: item.visitor_photo_url }} style={styles.photo} />
        ) : (
          <View style={styles.photoPlaceholder}>
            <Text style={styles.photoInitial}>
              {item.visitor_name?.charAt(0)?.toUpperCase() || "?"}
            </Text>
          </View>
        )}

        <Text style={styles.name}>{item.visitor_name}</Text>
        <Text style={styles.purpose}>{item.purpose}</Text>

        <View style={styles.metaRow}>
          <Text style={styles.arrived}>
            Arrived: {parseUTC(item.arrived_at)?.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
          </Text>
          <View
            style={[
              styles.waitPill,
              urgency === "warning" && styles.waitPillWarning,
              urgency === "urgent" && styles.waitPillUrgent,
            ]}
          >
            <Text
              style={[
                styles.waitPillText,
                urgency === "warning" && styles.waitPillTextWarning,
                urgency === "urgent" && styles.waitPillTextUrgent,
              ]}
            >
              waiting {waitedMin} min
            </Text>
          </View>
        </View>

        {item.host_response === "wait" && (
          <Text style={styles.waitBadge}>
            You said: wait until {parseUTC(item.wait_until)?.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" })}
          </Text>
        )}

        {submitting === item.session_id ? (
          <ActivityIndicator style={{ marginTop: 16 }} color="#00bcd4" />
        ) : (
          <View style={styles.buttonRow}>
            <TouchableOpacity
              style={[styles.button, styles.availableButton]}
              onPress={() => handleRespond(item.session_id, "available")}
            >
              <Text style={styles.buttonText}>Send In</Text>
            </TouchableOpacity>

            <TouchableOpacity
              style={[styles.button, styles.waitButton]}
              onPress={() => setWaitModalFor(item.session_id)}
            >
              <Text style={styles.buttonText}>Wait</Text>
            </TouchableOpacity>

            <TouchableOpacity
              style={[styles.button, styles.notAvailableButton]}
              onPress={() => handleRespond(item.session_id, "not_available")}
            >
              <Text style={styles.buttonText}>Not Available</Text>
            </TouchableOpacity>
          </View>
        )}
      </View>
    );
  };

  return (
    <View style={styles.container}>
      <View style={styles.headerRow}>
        <View>
          <Text style={styles.header}>Visitor Alerts</Text>
          <Text style={styles.headerSubtitle}>
            {alerts.length} waiting at reception
          </Text>
        </View>
      </View>

      {alerts.length === 0 ? (
        <Text style={styles.empty}>No pending alerts</Text>
      ) : (
        <FlatList
          data={alerts}
          keyExtractor={(item) => item.session_id}
          renderItem={renderItem}
          contentContainerStyle={{ paddingBottom: 24 }}
        />
      )}

      <TouchableOpacity style={styles.homeButton} onPress={goToHome}>
        <Text style={styles.homeButtonText}>Back to Home</Text>
      </TouchableOpacity>

      <Modal visible={!!waitModalFor} transparent animationType="fade">
        <View style={styles.modalOverlay}>
          <View style={styles.modalBox}>
            <Text style={styles.modalTitle}>Wait how long?</Text>
            {WAIT_OPTIONS.map((mins) => (
              <TouchableOpacity
                key={mins}
                style={styles.modalOption}
                onPress={() => handleRespond(waitModalFor, "wait", mins)}
              >
                <Text style={styles.modalOptionText}>{mins} minutes</Text>
              </TouchableOpacity>
            ))}
            <TouchableOpacity
              style={styles.modalCancel}
              onPress={() => setWaitModalFor(null)}
            >
              <Text style={styles.modalCancelText}>Cancel</Text>
            </TouchableOpacity>
          </View>
        </View>
      </Modal>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: "#0f172a", padding: 16 },
  headerRow: {
    flexDirection: "row",
    justifyContent: "space-between",
    alignItems: "flex-start",
    marginBottom: 16,
    marginTop: 8,
  },
  header: { fontSize: 22, color: "#fff", fontWeight: "600" },
  headerSubtitle: { fontSize: 13, color: "#64748b", marginTop: 2 },
  empty: { color: "#64748b", fontSize: 14, textAlign: "center", marginTop: 40 },
  card: {
    backgroundColor: "#1e293b",
    borderRadius: 12,
    padding: 16,
    marginBottom: 16,
    alignItems: "center",
    borderWidth: 1,
    borderColor: "transparent",
  },
  cardUrgent: {
    borderColor: "#dc2626",
  },
  newBadge: {
    position: "absolute",
    top: 12,
    right: 12,
    backgroundColor: "#00bcd4",
    borderRadius: 6,
    paddingVertical: 3,
    paddingHorizontal: 8,
  },
  newBadgeText: {
    color: "#0f172a",
    fontSize: 10,
    fontWeight: "700",
    letterSpacing: 0.5,
  },
  photo: { width: 72, height: 72, borderRadius: 36, marginBottom: 8 },
  photoPlaceholder: {
    width: 72, height: 72, borderRadius: 36,
    backgroundColor: "#00bcd4", alignItems: "center", justifyContent: "center", marginBottom: 8,
  },
  photoInitial: { fontSize: 28, color: "#fff", fontWeight: "600" },
  name: { fontSize: 18, color: "#fff", fontWeight: "600" },
  purpose: { fontSize: 14, color: "#94a3b8", marginTop: 4, textAlign: "center" },
  metaRow: {
    flexDirection: "row",
    alignItems: "center",
    marginTop: 8,
    gap: 8,
  },
  arrived: { fontSize: 12, color: "#64748b" },
  waitPill: {
    borderRadius: 6,
    paddingVertical: 2,
    paddingHorizontal: 8,
    backgroundColor: "#334155",
  },
  waitPillWarning: {
    backgroundColor: "#713f12",
  },
  waitPillUrgent: {
    backgroundColor: "#7f1d1d",
  },
  waitPillText: {
    fontSize: 11,
    color: "#94a3b8",
    fontWeight: "600",
  },
  waitPillTextWarning: {
    color: "#fbbf24",
  },
  waitPillTextUrgent: {
    color: "#fca5a5",
  },
  waitBadge: { fontSize: 12, color: "#fbbf24", marginTop: 8 },
  buttonRow: { flexDirection: "row", marginTop: 16, gap: 8 },
  button: { paddingVertical: 10, paddingHorizontal: 14, borderRadius: 8 },
  availableButton: { backgroundColor: "#16a34a" },
  waitButton: { backgroundColor: "#ca8a04" },
  notAvailableButton: { backgroundColor: "#dc2626" },
  buttonText: { color: "#fff", fontWeight: "600", fontSize: 13 },
  homeButton: { marginTop: 8, alignItems: "center", padding: 12 },
  homeButtonText: { color: "#94a3b8", textDecorationLine: "underline" },
  modalOverlay: { flex: 1, backgroundColor: "rgba(0,0,0,0.6)", justifyContent: "center", alignItems: "center" },
  modalBox: { backgroundColor: "#1e293b", borderRadius: 12, padding: 24, width: "80%" },
  modalTitle: { color: "#fff", fontSize: 16, fontWeight: "600", marginBottom: 16, textAlign: "center" },
  modalOption: { paddingVertical: 12, borderBottomWidth: 1, borderBottomColor: "#334155" },
  modalOptionText: { color: "#00bcd4", fontSize: 15, textAlign: "center" },
  modalCancel: { paddingVertical: 12, marginTop: 8 },
  modalCancelText: { color: "#64748b", textAlign: "center" },
});