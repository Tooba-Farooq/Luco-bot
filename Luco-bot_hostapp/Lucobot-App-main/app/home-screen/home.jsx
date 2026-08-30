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
import { getMe, respondToAlert, logout as logoutClient } from "../api/client";

const WAIT_OPTIONS = [5, 10, 30, 60, 120];
const WAIT_WARNING_MIN = 8;
const WAIT_URGENT_MIN = 20;

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

function getUrgency(minutes) {
  if (minutes >= WAIT_URGENT_MIN) return "urgent";
  if (minutes >= WAIT_WARNING_MIN) return "warning";
  return "normal";
}

// Sends the device's local wall-clock time with no timezone suffix, matching
// the backend's expectation that available_again_at is naive local time
// (it attaches LOCAL_TZ itself in host/respond.py before converting to UTC).
// Using toISOString() here would send UTC-with-Z, which the backend would
// misinterpret as already being local time — a several-hour error.
function toLocalNaiveISOString(d) {
  const pad = (n) => String(n).padStart(2, "0");
  return (
    `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}` +
    `T${pad(d.getHours())}:${pad(d.getMinutes())}:${pad(d.getSeconds())}`
  );
}

function laterToday() {
  const d = new Date();
  d.setHours(d.getHours() + 3);
  return toLocalNaiveISOString(d);
}
function tomorrowAt(hour) {
  const d = new Date();
  d.setDate(d.getDate() + 1);
  d.setHours(hour, 0, 0, 0);
  return toLocalNaiveISOString(d);
}
const RETURN_PRESETS = [
  { label: "Later today", iso: laterToday },
  { label: "Tomorrow morning", iso: () => tomorrowAt(9) },
  { label: "Tomorrow afternoon", iso: () => tomorrowAt(14) },
];

function AlertCard({ alert, onRespond }) {
  const [waiting, setWaiting] = useState(alert.host_response === "wait");
  const [waitUntil, setWaitUntil] = useState(alert.wait_until ?? null);
  const [pickingWait, setPickingWait] = useState(false);
  const [pickingReturn, setPickingReturn] = useState(false);
  const [sending, setSending] = useState(false);
  const [, setTick] = useState(0);

  useEffect(() => {
    const interval = setInterval(() => setTick((t) => t + 1), 30000);
    return () => clearInterval(interval);
  }, []);

  const handle = async (response, waitMinutes, availableAgainAt) => {
    setSending(true);
    try {
      const result = await respondToAlert(alert.session_id, response, waitMinutes, availableAgainAt);
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
      setPickingReturn(false);
    }
  };

  const waitedMin = minutesWaiting(alert.arrived_at);
  const urgency = getUrgency(waitedMin);
  const isNew = waitedMin < 1;

  return (
    <LinearGradient
      colors={["#1e293b", "#16233b"]}
      start={{ x: 0, y: 0 }}
      end={{ x: 1, y: 1 }}
      style={[styles.alertCard, urgency === "urgent" && styles.alertCardUrgent]}
    >
      {isNew && (
        <View style={styles.newBadge}>
          <Text style={styles.newBadgeText}>NEW</Text>
        </View>
      )}

      <View style={styles.alertHeader}>
        {alert.visitor_photo_url ? (
          <Image source={{ uri: alert.visitor_photo_url }} style={styles.visitorPhoto} />
        ) : (
          <View style={styles.visitorPhotoPlaceholder}>
            <Text style={styles.visitorPhotoInitial}>
              {alert.visitor_name?.[0]?.toUpperCase() || "?"}
            </Text>
          </View>
        )}
        <View style={{ flex: 1 }}>
          <Text style={styles.alertVisitor} numberOfLines={1}>{alert.visitor_name}</Text>
          {alert.purpose ? (
            <Text style={styles.alertPurpose} numberOfLines={1}>{alert.purpose}</Text>
          ) : null}
        </View>
        <Text
          style={[
            styles.waitedText,
            urgency === "warning" && styles.waitedTextWarning,
            urgency === "urgent" && styles.waitedTextUrgent,
          ]}
        >
          {waitedMin}m
        </Text>
      </View>

      {waiting && (
        <Text style={styles.waitingNote}>
          You asked them to wait
          {waitUntil ? ` — until ${parseUTC(waitUntil)?.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" })}` : ""}
        </Text>
      )}

      {pickingReturn ? (
        <View style={styles.waitPicker}>
          <Text style={styles.waitPickerLabel}>When can they come back?</Text>
          {RETURN_PRESETS.map((p) => (
            <TouchableOpacity
              key={p.label}
              style={styles.returnOptionBtn}
              disabled={sending}
              onPress={() => handle("not_available", undefined, p.iso())}
            >
              <Text style={styles.waitOptionText}>{p.label}</Text>
            </TouchableOpacity>
          ))}
          <TouchableOpacity
            style={styles.returnOptionBtn}
            disabled={sending}
            onPress={() => handle("not_available")}
          >
            <Text style={styles.waitOptionText}>No specific time</Text>
          </TouchableOpacity>
          <TouchableOpacity style={styles.waitCancelBtn} onPress={() => setPickingReturn(false)}>
            <Text style={styles.waitCancelText}>Cancel</Text>
          </TouchableOpacity>
        </View>
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
            <Text style={styles.actionTextMuted}>{waiting ? "Extend Wait" : "Ask to Wait"}</Text>
          </TouchableOpacity>
          <TouchableOpacity
            style={[styles.actionBtnFull, styles.actionDecline]}
            disabled={sending}
            onPress={() => setPickingReturn(true)}
          >
            <Text style={styles.actionTextDecline}>Not Available</Text>
          </TouchableOpacity>
        </View>
      )}
    </LinearGradient>
  );
}

export default function Home({ goToLogin, goToMessages, pending, onAlertResolved, onRefresh }) {
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);

  // Still calling getMe() to validate the session on load (and trigger a
  // forced re-login if the refresh token is dead) — we just no longer
  // render the employee's photo/name here, since that's on the Account tab.
  const loadProfile = useCallback(async (isRefresh = false) => {
    if (isRefresh) setRefreshing(true);
    else setLoading(true);
    setError(null);
    try {
      await getMe();
      await onRefresh();
    } catch (err) {
      console.error("Failed to load profile:", err);
      if (err.message === "Refresh failed, session expired") {
        goToLogin();
        return;
      }
      setError("Could not load your alerts. Pull to retry or log in again.");
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [goToLogin, onRefresh]);

  const onScreenRefresh = () => loadProfile(true);

  useEffect(() => {
    loadProfile();
  }, [loadProfile]);

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
      refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onScreenRefresh} tintColor="#00bcd4" />}
    >
      {pending.length > 0 ? (
        <View style={styles.alertsSection}>
          <Text style={styles.sectionTitle}>
            {pending.length} pending alert{pending.length !== 1 ? "s" : ""}
          </Text>
          {pending.map((a) => (
            <AlertCard key={a.session_id} alert={a} onRespond={onAlertResolved} />
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

  alertsSection: { width: "100%", maxWidth: 400, marginTop: 8 },
  sectionTitle: {
    fontSize: 13, color: "#f59e0b", fontWeight: "600",
    textTransform: "uppercase", marginBottom: 8,
  },

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
  alertCardUrgent: { borderColor: "#dc2626" },
  newBadge: {
    position: "absolute",
    top: 12,
    right: 12,
    backgroundColor: "#00bcd4",
    borderRadius: 6,
    paddingVertical: 3,
    paddingHorizontal: 8,
    zIndex: 1,
  },
  newBadgeText: { color: "#0f172a", fontSize: 10, fontWeight: "700", letterSpacing: 0.5 },

  alertHeader: { flexDirection: "row", alignItems: "center", gap: 10, marginBottom: 12 },
  visitorPhoto: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: "#334155",
  },
  visitorPhotoPlaceholder: {
    width: 40,
    height: 40,
    borderRadius: 20,
    backgroundColor: "#334155",
    alignItems: "center",
    justifyContent: "center",
  },
  visitorPhotoInitial: {
    color: "#94a3b8",
    fontSize: 16,
    fontWeight: "700",
  },
  alertVisitor: { fontSize: 15, color: "#fff", fontWeight: "600" },
  alertPurpose: { fontSize: 12, color: "#94a3b8", marginTop: 1 },
  waitedText: { fontSize: 11, color: "#64748b", fontWeight: "600" },
  waitedTextWarning: { color: "#fbbf24" },
  waitedTextUrgent: { color: "#fca5a5" },

  waitingNote: { fontSize: 12, color: "#64748b", fontStyle: "italic", marginBottom: 8 },
  waitPicker: { marginTop: 4 },
  waitPickerLabel: { fontSize: 12, color: "#94a3b8", marginBottom: 8 },
  waitOptions: { flexDirection: "row", flexWrap: "wrap", gap: 8 },
  waitOptionBtn: { paddingVertical: 8, paddingHorizontal: 14, borderRadius: 8, backgroundColor: "#334155" },
  returnOptionBtn: {
    paddingVertical: 10, paddingHorizontal: 14, borderRadius: 8,
    backgroundColor: "#334155", marginBottom: 8,
  },
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

  errorText: { color: "#f87171", textAlign: "center", marginBottom: 16 },
  retryButton: {
    paddingVertical: 10, paddingHorizontal: 24, borderRadius: 8,
    backgroundColor: "#00bcd4", marginBottom: 16,
  },
  retryButtonText: { color: "#0f172a", fontWeight: "600" },
  logoutText: { color: "#94a3b8", textDecorationLine: "underline" },
});