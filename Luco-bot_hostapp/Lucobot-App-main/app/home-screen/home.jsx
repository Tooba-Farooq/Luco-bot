import { useEffect, useState, useCallback } from "react";
import {
  View,
  Text,
  Image,
  ActivityIndicator,
  TouchableOpacity,
  StyleSheet,
  ScrollView,
  RefreshControl,
} from "react-native";
import { LinearGradient } from "expo-linear-gradient";
import { BlurView } from "expo-blur";
import { getMe, getPendingAlerts, respondToAlert, logout as logoutClient } from "../api/client";

const WAIT_OPTIONS = [5, 10, 30, 60, 120];

function parseUTC(isoString) {
  if (!isoString) return null;
  const hasTZ = /Z$|[+-]\d{2}:\d{2}$/.test(isoString);
  return new Date(hasTZ ? isoString : isoString + "Z");
}

function minutesWaiting(arrivedAt) {
  const arrived = parseUTC(arrivedAt);
  if (!arrived) return 0;
  return Math.max(0, Math.floor((Date.now() - arrived.getTime()) / 60000));
}

function AlertCard({ alert, onRespond }) {
  const [waiting, setWaiting] = useState(alert.host_response === "wait");
  const [waitUntil, setWaitUntil] = useState(alert.wait_until ?? null);
  const [pickingWait, setPickingWait] = useState(false);
  const [sending, setSending] = useState(false);

  const handle = async (response, waitMinutes) => {
    setSending(true);
    try {
      const result = await respondToAlert(alert.session_id, response, waitMinutes);
      onRespond(alert.session_id, response, result);
      if (response === "wait") {
        setWaiting(true);
        setWaitUntil(result?.wait_until ?? null);
        setPickingWait(false);
      }
    } catch (err) {
      console.error("Failed to respond to alert:", err);
    } finally {
      setSending(false);
    }
  };

  const waitedMin = minutesWaiting(alert.arrived_at);

  return (
    <LinearGradient
      colors={["#1e293b", "#16233b"]}
      start={{ x: 0, y: 0 }}
      end={{ x: 1, y: 1 }}
      style={styles.alertCard}
    >
      <View style={styles.alertHeader}>
        {alert.visitor_photo_url ? (
          <Image
            source={{ uri: alert.visitor_photo_url, headers: { "ngrok-skip-browser-warning": "true" } }}
            style={styles.alertPhoto}
          />
        ) : (
          <View style={styles.alertPhotoPlaceholder}>
            <Text style={styles.alertPhotoInitial}>
              {alert.visitor_name?.charAt(0)?.toUpperCase() || "?"}
            </Text>
          </View>
        )}
        <View style={{ flex: 1 }}>
          <Text style={styles.alertVisitor} numberOfLines={1}>{alert.visitor_name}</Text>
          {alert.purpose ? (
            <Text style={styles.alertPurpose} numberOfLines={1}>{alert.purpose}</Text>
          ) : null}
        </View>
        <Text style={styles.waitedText}>{waitedMin}m</Text>
      </View>

      {waiting ? (
        <Text style={styles.waitingNote}>
          You asked them to wait
          {waitUntil ? ` — until ${parseUTC(waitUntil)?.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" })}` : ""}
        </Text>
      ) : pickingWait ? (
        <View style={styles.waitPicker}>
          <Text style={styles.waitPickerLabel}>Wait how long?</Text>
          <View style={styles.waitOptions}>
            {WAIT_OPTIONS.map((mins) => (
              <TouchableOpacity
                key={mins}
                style={styles.waitOptionBtn}
                disabled={sending}
                onPress={() => handle("wait", mins)}
              >
                <Text style={styles.waitOptionText}>{mins}m</Text>
              </TouchableOpacity>
            ))}
          </View>
          <TouchableOpacity style={styles.waitCancelBtn} onPress={() => setPickingWait(false)}>
            <Text style={styles.waitCancelText}>Cancel</Text>
          </TouchableOpacity>
        </View>
      ) : (
        <View style={styles.alertActions}>
          <TouchableOpacity
            style={[styles.actionBtnFull, styles.actionAvailable]}
            disabled={sending}
            onPress={() => handle("available")}
          >
            <Text style={styles.actionText}>Send In</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[styles.actionBtnFull, styles.actionWait]}
            disabled={sending}
            onPress={() => setPickingWait(true)}
          >
            <Text style={styles.actionTextMuted}>Ask to Wait</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[styles.actionBtnFull, styles.actionDecline]}
            disabled={sending}
            onPress={() => handle("not_available")}
          >
            <Text style={styles.actionTextDecline}>Not Available</Text>
          </TouchableOpacity>
        </View>
      )}
    </LinearGradient>
  );
}

export default function Home({ goToLogin, goToAlerts, goToMessages }) {
  const [employee, setEmployee] = useState(null);
  const [pending, setPending] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);

  const loadProfile = useCallback(async (isRefresh = false) => {
    if (isRefresh) setRefreshing(true);
    else setLoading(true);
    setError(null);
    try {
      const data = await getMe();
      setEmployee(data);

      const alertData = await getPendingAlerts();
      setPending(alertData.pending ?? []);
      // No auto-redirect to full-screen AlertScreen here —
      // pending alerts render inline below instead.
    } catch (err) {
      console.error("Failed to load profile:", err);
      if (err.message === "Refresh failed, session expired") {
        goToLogin();
        return;
      }
      setError("Could not load your profile. Pull to retry or log in again.");
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [goToLogin]);

  const onRefresh = () => loadProfile(true);

  useEffect(() => {
    loadProfile();
  }, [loadProfile]);

  const handleAlertResolved = (sessionId, response, result) => {
    if (response === "wait") {
      setPending((prev) =>
        prev.map((a) =>
          a.session_id === sessionId
            ? { ...a, host_response: "wait", wait_until: result?.wait_until ?? null }
            : a
        )
      );
    } else {
      setPending((prev) => prev.filter((a) => a.session_id !== sessionId));
    }
  };

  const handleLogout = async () => {
    await logoutClient();
    goToLogin();
  };

  if (loading) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator size="large" color="#00bcd4" />
      </View>
    );
  }

  if (error) {
    return (
      <View style={styles.centered}>
        <Text style={styles.errorText}>{error}</Text>
        <TouchableOpacity style={styles.retryButton} onPress={() => loadProfile()}>
          <Text style={styles.retryButtonText}>Retry</Text>
        </TouchableOpacity>
        <TouchableOpacity onPress={handleLogout}>
          <Text style={styles.logoutText}>Log out</Text>
        </TouchableOpacity>
      </View>
    );
  }

  return (
    <ScrollView
      style={styles.container}
      contentContainerStyle={styles.containerContent}
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor="#00bcd4" />}
    >
      <BlurView intensity={40} tint="dark" style={styles.glassHeader}>
        {employee?.photo_url ? (
          <Image
            source={{ uri: employee.photo_url, headers: { "ngrok-skip-browser-warning": "true" } }}
            style={styles.avatar}
          />
        ) : (
          <View style={styles.avatarPlaceholder}>
            <Text style={styles.avatarInitial}>
              {employee?.name?.charAt(0)?.toUpperCase() || "?"}
            </Text>
          </View>
        )}
        <Text style={styles.welcomeText}>Welcome, {employee?.name || "there"}</Text>
        <Text style={styles.subtitle}>{employee?.employee_code}</Text>
      </BlurView>

      {pending.length > 0 ? (
        <View style={styles.alertsSection}>
          <View style={styles.alertsSectionHeader}>
            <Text style={styles.sectionTitle}>
              {pending.length} pending alert{pending.length !== 1 ? "s" : ""}
            </Text>
            {pending.length > 1 && (
              <TouchableOpacity onPress={() => goToAlerts(pending)}>
                <Text style={styles.viewAllLink}>View all</Text>
              </TouchableOpacity>
            )}
          </View>
          {pending.map((a) => (
            <AlertCard key={a.session_id} alert={a} onRespond={handleAlertResolved} />
          ))}
        </View>
      ) : (
        <View style={styles.idleBox}>
          <Text style={styles.idleIcon}>👀</Text>
          <Text style={styles.idleText}>No active visitor alerts</Text>
          <Text style={styles.idleSubtext}>
            LucoBot will notify you the moment someone arrives
          </Text>
        </View>
      )}

    </ScrollView>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: "#0f172a" },
  containerContent: { alignItems: "center", padding: 24, paddingTop: 48, paddingBottom: 120 },
  centered: { flex: 1, alignItems: "center", justifyContent: "center", padding: 24, backgroundColor: "#0f172a" },

  glassHeader: {
    width: "100%",
    maxWidth: 400,
    borderRadius: 20,
    padding: 20,
    alignItems: "center",
    overflow: "hidden",
    borderWidth: 1,
    borderColor: "rgba(148, 163, 184, 0.15)",
    marginBottom: 8,
  },
  avatar: { width: 96, height: 96, borderRadius: 48, marginBottom: 16 },
  avatarPlaceholder: {
    width: 96, height: 96, borderRadius: 48,
    backgroundColor: "#00bcd4", alignItems: "center", justifyContent: "center",
    marginBottom: 16,
  },
  avatarInitial: { fontSize: 36, color: "#fff", fontWeight: "600" },
  welcomeText: { fontSize: 22, color: "#fff", fontWeight: "600" },
  subtitle: { fontSize: 14, color: "#94a3b8", marginTop: 4 },

  alertsSection: { width: "100%", maxWidth: 400, marginTop: 24 },
  alertsSectionHeader: {
    flexDirection: "row", justifyContent: "space-between", alignItems: "center", marginBottom: 8,
  },
  sectionTitle: { fontSize: 13, color: "#f59e0b", fontWeight: "600", textTransform: "uppercase" },
  viewAllLink: { fontSize: 12, color: "#00bcd4", fontWeight: "600" },

  alertCard: {
    borderRadius: 18,
    padding: 16,
    marginBottom: 12,
    borderWidth: 1,
    borderColor: "rgba(148, 163, 184, 0.1)",
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 6 },
    shadowOpacity: 0.25,
    shadowRadius: 10,
    elevation: 6,
  },
  alertHeader: { flexDirection: "row", alignItems: "center", gap: 10, marginBottom: 12 },
  alertPhoto: { width: 40, height: 40, borderRadius: 20 },
  alertPhotoPlaceholder: {
    width: 40, height: 40, borderRadius: 20,
    backgroundColor: "#00bcd4", alignItems: "center", justifyContent: "center",
  },
  alertPhotoInitial: { fontSize: 15, color: "#fff", fontWeight: "600" },
  alertVisitor: { fontSize: 15, color: "#fff", fontWeight: "600" },
  alertPurpose: { fontSize: 12, color: "#94a3b8", marginTop: 1 },
  waitedText: { fontSize: 11, color: "#64748b", fontWeight: "600" },

  waitingNote: { fontSize: 12, color: "#64748b", fontStyle: "italic" },
  waitPicker: { marginTop: 4 },
  waitPickerLabel: { fontSize: 12, color: "#94a3b8", marginBottom: 8 },
  waitOptions: { flexDirection: "row", flexWrap: "wrap", gap: 8 },
  waitOptionBtn: { paddingVertical: 8, paddingHorizontal: 14, borderRadius: 8, backgroundColor: "#334155" },
  waitOptionText: { fontSize: 13, fontWeight: "600", color: "#e2e8f0" },
  waitCancelBtn: { paddingVertical: 10, alignItems: "center" },
  waitCancelText: { fontSize: 13, color: "#64748b" },

  alertActions: { gap: 8, marginTop: 4 },
  actionBtnFull: { paddingVertical: 12, borderRadius: 10, alignItems: "center" },
  actionAvailable: { backgroundColor: "#16a34a" },
  actionWait: { backgroundColor: "#334155", borderWidth: 1, borderColor: "#475569" },
  actionDecline: { backgroundColor: "transparent", borderWidth: 1, borderColor: "#dc2626" },
  actionText: { fontSize: 14, fontWeight: "700", color: "#fff" },
  actionTextMuted: { fontSize: 14, fontWeight: "600", color: "#e2e8f0" },
  actionTextDecline: { fontSize: 14, fontWeight: "600", color: "#f87171" },

  idleBox: {
    marginTop: 24, padding: 24, borderRadius: 16, borderWidth: 1, borderColor: "#1e293b",
    alignItems: "center", maxWidth: 320, width: "100%",
  },
  idleIcon: { fontSize: 28, marginBottom: 8 },
  idleText: { color: "#64748b", fontSize: 14, fontWeight: "600" },
  idleSubtext: { color: "#475569", fontSize: 12, marginTop: 4, textAlign: "center" },

  messagesButton: {
    marginTop: 24, paddingVertical: 12, paddingHorizontal: 32, borderRadius: 10,
    backgroundColor: "#1e293b", borderWidth: 1, borderColor: "#334155",
  },
  messagesButtonText: { color: "#00bcd4", fontWeight: "600" },
  logoutButton: {
    marginTop: 16, paddingVertical: 12, paddingHorizontal: 32,
    borderRadius: 10, backgroundColor: "#dc2626",
  },
  logoutButtonText: { color: "#fff", fontWeight: "600" },
  errorText: { color: "#f87171", textAlign: "center", marginBottom: 16 },
  retryButton: {
    paddingVertical: 10, paddingHorizontal: 24, borderRadius: 8,
    backgroundColor: "#00bcd4", marginBottom: 16,
  },
  retryButtonText: { color: "#0f172a", fontWeight: "600" },
  logoutText: { color: "#94a3b8", textDecorationLine: "underline" },
});