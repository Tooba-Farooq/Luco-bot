import { useState, useCallback, useEffect } from "react";
import {
  View,
  Text,
  Image,
  FlatList,
  TouchableOpacity,
  StyleSheet,
  ActivityIndicator,
  RefreshControl,
} from "react-native";
import { LinearGradient } from "expo-linear-gradient";
import { BlurView } from "expo-blur";
import { getHostMessages } from "../api/client";

function parseUTC(isoString) {
  if (!isoString) return null;
  const hasTZ = /Z$|[+-]\d{2}:\d{2}$/.test(isoString);
  return new Date(hasTZ ? isoString : isoString + "Z");
}

export function fmtStamp(iso) {
  const d = parseUTC(iso);
  if (!d) return "";
  const sameDay = new Date().toDateString() === d.toDateString();
  return sameDay
    ? d.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" })
    : d.toLocaleString([], { month: "short", day: "numeric", hour: "numeric", minute: "2-digit" });
}

export function groupThreads(messages) {
  const map = new Map();
  for (const m of messages) {
    const t = map.get(m.visitor_id) ?? {
      visitor_id: m.visitor_id,
      visitor_name: m.visitor_name,
      visitor_photo_url: m.visitor_photo_url,
      messages: [],
    };
    t.messages.push(m);
    map.set(m.visitor_id, t);
  }
  const threads = [...map.values()];
  for (const t of threads) {
    t.messages.sort((a, b) => parseUTC(a.left_at) - parseUTC(b.left_at));
  }
  return threads.sort((a, b) => {
    const aLast = a.messages[a.messages.length - 1];
    const bLast = b.messages[b.messages.length - 1];
    return parseUTC(bLast.left_at) - parseUTC(aLast.left_at);
  });
}

const POLL_INTERVAL_MS = 15000;

export default function MessagesScreen({ goToThread }) {
  const [rawMessages, setRawMessages] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const data = await getHostMessages();
      setRawMessages(data.messages);
    } catch (err) {
      console.error("Failed to load messages:", err);
      setError("Couldn't load your messages.");
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    load();
    // Poll for new messages since there's no push channel for /host/messages.
    const interval = setInterval(load, POLL_INTERVAL_MS);
    return () => clearInterval(interval);
  }, [load]);

  const onRefresh = () => {
    setRefreshing(true);
    load();
  };

  const threads = groupThreads(rawMessages);

  const renderThread = ({ item: t }) => {
    const last = t.messages[t.messages.length - 1];
    return (
      <TouchableOpacity onPress={() => goToThread(t.visitor_id)}>
        <LinearGradient
          colors={["#1e293b", "#16233b"]}
          start={{ x: 0, y: 0 }}
          end={{ x: 1, y: 1 }}
          style={styles.card}
        >
          {t.visitor_photo_url ? (
            <Image
              source={{ uri: t.visitor_photo_url, headers: { "ngrok-skip-browser-warning": "true" } }}
              style={styles.photo}
            />
          ) : (
            <View style={styles.photoPlaceholder}>
              <Text style={styles.photoInitial}>{t.visitor_name?.charAt(0)?.toUpperCase() || "?"}</Text>
            </View>
          )}
          <View style={styles.middle}>
            <Text style={styles.name} numberOfLines={1}>{t.visitor_name}</Text>
            <Text style={styles.preview} numberOfLines={1}>{last.message_text}</Text>
          </View>
          <View style={styles.right}>
            <Text style={styles.stamp}>{fmtStamp(last.left_at)}</Text>
            <View style={styles.countBadge}>
              <Text style={styles.countBadgeText}>{t.messages.length}</Text>
            </View>
          </View>
        </LinearGradient>
      </TouchableOpacity>
    );
  };

  if (loading) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator size="large" color="#00bcd4" />
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <BlurView intensity={40} tint="dark" style={styles.glassHeader}>
        <View style={styles.headerRow}>
          <Text style={styles.header}>Messages</Text>
          <Text style={styles.headerSubtitle}>
            {threads.length} visitor{threads.length !== 1 ? "s" : ""}
          </Text>
        </View>
      </BlurView>

      {error ? (
        <Text style={styles.errorText}>{error}</Text>
      ) : threads.length === 0 ? (
        <Text style={styles.empty}>No visitor messages yet.</Text>
      ) : (
        <FlatList
          data={threads}
          keyExtractor={(t) => String(t.visitor_id)}
          renderItem={renderThread}
          contentContainerStyle={{ paddingBottom: 120 }}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor="#00bcd4" />}
        />
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: "#0f172a", padding: 16 },
  centered: { flex: 1, alignItems: "center", justifyContent: "center", backgroundColor: "#0f172a" },
  glassHeader: {
    borderRadius: 20,
    padding: 20,
    overflow: "hidden",
    borderWidth: 1,
    borderColor: "rgba(148, 163, 184, 0.15)",
    marginBottom: 16,
    marginTop: 8,
  },
  headerRow: {},
  header: { fontSize: 22, color: "#fff", fontWeight: "600" },
  headerSubtitle: { fontSize: 13, color: "#64748b", marginTop: 2 },
  empty: { color: "#64748b", fontSize: 14, textAlign: "center", marginTop: 40 },
  errorText: { color: "#f87171", textAlign: "center", marginTop: 40 },
  card: {
    borderRadius: 16,
    padding: 16,
    marginBottom: 10,
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
    borderWidth: 1,
    borderColor: "rgba(148, 163, 184, 0.1)",
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.2,
    shadowRadius: 8,
    elevation: 4,
  },
  photo: { width: 48, height: 48, borderRadius: 24 },
  photoPlaceholder: {
    width: 48, height: 48, borderRadius: 24,
    backgroundColor: "#00bcd4", alignItems: "center", justifyContent: "center",
  },
  photoInitial: { fontSize: 18, color: "#fff", fontWeight: "600" },
  middle: { flex: 1, minWidth: 0 },
  name: { fontSize: 15, color: "#fff", fontWeight: "600" },
  preview: { fontSize: 13, color: "#94a3b8", marginTop: 2 },
  right: { alignItems: "flex-end" },
  stamp: { fontSize: 11, color: "#64748b" },
  countBadge: {
    marginTop: 4, backgroundColor: "rgba(0, 188, 212, 0.15)",
    borderRadius: 10, paddingHorizontal: 7, paddingVertical: 2,
  },
  countBadgeText: { fontSize: 11, fontWeight: "600", color: "#00bcd4" },
});