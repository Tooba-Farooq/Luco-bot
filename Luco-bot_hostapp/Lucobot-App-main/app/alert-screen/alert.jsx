import { useState } from "react";
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

// Backend may return naive UTC timestamps (no "Z" or offset suffix).
// JS Date() treats a naive string as local time, which caused times to
// appear ~5 hours off in Pakistan. This forces UTC interpretation when
// the string has no explicit timezone marker.
function parseUTC(isoString) {
  if (!isoString) return null;
  const hasTZ = /Z$|[+-]\d{2}:\d{2}$/.test(isoString);
  return new Date(hasTZ ? isoString : isoString + "Z");
}

const WAIT_OPTIONS = [5, 10, 30, 60, 120];

export default function AlertScreen({ alerts, onResolved, goToHome }) {
  const [waitModalFor, setWaitModalFor] = useState(null); // session_id or null
  const [submitting, setSubmitting] = useState(null); // session_id being submitted

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

  const renderItem = ({ item }) => (
    <View style={styles.card}>
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
      <Text style={styles.arrived}>
        Arrived: {parseUTC(item.arrived_at)?.toLocaleTimeString()}
      </Text>

      {item.host_response === "wait" && (
        <Text style={styles.waitBadge}>
          You said: wait until {parseUTC(item.wait_until)?.toLocaleTimeString()}
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

  return (
    <View style={styles.container}>
      <Text style={styles.header}>Visitor Alerts</Text>

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
  header: { fontSize: 22, color: "#fff", fontWeight: "600", marginBottom: 16, marginTop: 8 },
  empty: { color: "#64748b", fontSize: 14, textAlign: "center", marginTop: 40 },
  card: {
    backgroundColor: "#1e293b",
    borderRadius: 12,
    padding: 16,
    marginBottom: 16,
    alignItems: "center",
  },
  photo: { width: 72, height: 72, borderRadius: 36, marginBottom: 8 },
  photoPlaceholder: {
    width: 72, height: 72, borderRadius: 36,
    backgroundColor: "#00bcd4", alignItems: "center", justifyContent: "center", marginBottom: 8,
  },
  photoInitial: { fontSize: 28, color: "#fff", fontWeight: "600" },
  name: { fontSize: 18, color: "#fff", fontWeight: "600" },
  purpose: { fontSize: 14, color: "#94a3b8", marginTop: 4, textAlign: "center" },
  arrived: { fontSize: 12, color: "#64748b", marginTop: 4 },
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