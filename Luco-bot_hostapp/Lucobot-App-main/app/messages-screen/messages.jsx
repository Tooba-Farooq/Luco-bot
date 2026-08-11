import { useEffect, useState, useCallback } from "react";
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
import { getHostMessages } from "../api/client";

function parseUTC(isoString) {
  if (!isoString) return null;
  const hasTZ = /Z$|[+-]\d{2}:\d{2}$/.test(isoString);
  return new Date(hasTZ ? isoString : isoString + "Z");
}

function formatTime(isoString) {
  const d = parseUTC(isoString);
  if (!d) return "";
  return d.toLocaleString([], {
    month: "short",
    day: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function groupByVisitor(messages) {
  const map = new Map();
  for (const msg of messages) {
    if (!map.has(msg.visitor_id)) {
      map.set(msg.visitor_id, {
        visitor_id: msg.visitor_id,
        visitor_name: msg.visitor_name,
        visitor_photo_url: msg.visitor_photo_url,
        messages: [],
      });
    }
    map.get(msg.visitor_id).messages.push(msg);
  }
  const threads = Array.from(map.values()).map((t) => ({
    ...t,
    messages: t.messages.sort((a, b) => parseUTC(b.left_at) - parseUTC(a.left_at)),
  }));
  threads.sort((a, b) => parseUTC(b.messages[0].left_at) - parseUTC(a.messages[0].left_at));
  return threads;
}

export default function MessagesScreen({ goToHome }) {
  const [threads, setThreads] = useState([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);
  const [error, setError] = useState(null);

  const load = useCallback(async () => {
    setError(null);
    try {
      const data = await getHostMessages();
      setThreads(groupByVisitor(data.messages));
    } catch (err) {
      console.error("Failed to load messages:", err);
      setError("Could not load messages. Pull to retry.");
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const onRefresh = () => {
    setRefreshing(true);
    load();
  };

  const renderThread = ({ item }) => (
    <View style={styles.card}>
      <View style={styles.headerRow}>
      {item.visitor_photo_url ? (
  <Image
    source={{
      uri: item.visitor_photo_url,
      headers: { 'ngrok-skip-browser-warning': 'true' },
    }}
    style={styles.photo}
  />
) : (
  <View style={styles.photoPlaceholder}>
    <Text style={styles.photoInitial}>
      {item.visitor_name?.charAt(0)?.toUpperCase() || "?"}
    </Text>
  </View>
)}
        <View>
          <Text style={styles.name}>{item.visitor_name}</Text>
          <Text style={styles.count}>
            {item.messages.length} message{item.messages.length > 1 ? "s" : ""}
          </Text>
        </View>
      </View>

      {item.messages.map((msg) => (
        <View key={msg.session_id + msg.left_at} style={styles.messageRow}>
          {msg.purpose ? <Text style={styles.purpose}>{msg.purpose}</Text> : null}
          <Text style={styles.messageText}>{msg.message_text}</Text>
          <Text style={styles.timestamp}>{formatTime(msg.left_at)}</Text>
        </View>
      ))}
    </View>
  );

  if (loading) {
    return (
      <View style={styles.centered}>
        <ActivityIndicator size="large" color="#00bcd4" />
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <View style={styles.headerBar}>
        <Text style={styles.header}>Messages</Text>
        <Text style={styles.headerSubtitle}>
          {threads.length} visitor{threads.length !== 1 ? "s" : ""}
        </Text>
      </View>

      {error ? (
        <Text style={styles.errorText}>{error}</Text>
      ) : threads.length === 0 ? (
        <Text style={styles.empty}>No messages yet</Text>
      ) : (
        <FlatList
          data={threads}
          keyExtractor={(item) => String(item.visitor_id)}
          renderItem={renderThread}
          contentContainerStyle={{ paddingBottom: 24 }}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
        />
      )}

      <TouchableOpacity style={styles.homeButton} onPress={goToHome}>
        <Text style={styles.homeButtonText}>Back to Home</Text>
      </TouchableOpacity>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: "#0f172a", padding: 16 },
  centered: { flex: 1, alignItems: "center", justifyContent: "center", backgroundColor: "#0f172a" },
  headerBar: { marginBottom: 16, marginTop: 8 },
  header: { fontSize: 22, color: "#fff", fontWeight: "600" },
  headerSubtitle: { fontSize: 13, color: "#64748b", marginTop: 2 },
  empty: { color: "#64748b", fontSize: 14, textAlign: "center", marginTop: 40 },
  errorText: { color: "#f87171", textAlign: "center", marginTop: 40 },
  card: { backgroundColor: "#1e293b", borderRadius: 12, padding: 16, marginBottom: 16 },
  headerRow: { flexDirection: "row", alignItems: "center", marginBottom: 8 },
  photo: { width: 48, height: 48, borderRadius: 24, marginRight: 12 },
  photoPlaceholder: {
    width: 48, height: 48, borderRadius: 24,
    backgroundColor: "#00bcd4", alignItems: "center", justifyContent: "center", marginRight: 12,
  },
  photoInitial: { fontSize: 18, color: "#fff", fontWeight: "600" },
  name: { fontSize: 16, color: "#fff", fontWeight: "600" },
  count: { fontSize: 12, color: "#64748b" },
  messageRow: { borderTopWidth: 1, borderTopColor: "#334155", paddingTop: 8, marginTop: 8 },
  purpose: { fontSize: 11, color: "#64748b", marginBottom: 2 },
  messageText: { fontSize: 14, color: "#e2e8f0" },
  timestamp: { fontSize: 11, color: "#475569", marginTop: 4 },
  homeButton: { marginTop: 8, alignItems: "center", padding: 12 },
  homeButtonText: { color: "#94a3b8", textDecorationLine: "underline" },
});