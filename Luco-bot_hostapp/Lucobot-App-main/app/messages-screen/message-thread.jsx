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
import { BlurView } from "expo-blur";
import { Ionicons } from "@expo/vector-icons";
import { getHostMessages } from "../api/client";
import { fmtStamp, groupThreads } from "./message";

const POLL_INTERVAL_MS = 15000;

export default function MessageThreadScreen({ visitorId, goBack }) {
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
      console.error("Failed to load conversation:", err);
      setError("Couldn't load this conversation.");
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

  const thread = groupThreads(rawMessages).find((t) => t.visitor_id === visitorId);

  const renderMessage = ({ item: m }) => (
    <View style={styles.bubble}>
      {m.purpose && m.purpose.toLowerCase() !== "general" ? (
        <View style={styles.purposeTag}>
          <Text style={styles.purposeTagText}>{m.purpose}</Text>
        </View>
      ) : null}
      <Text style={styles.messageText}>{m.message_text}</Text>
      <Text style={styles.timestamp}>{fmtStamp(m.left_at)}</Text>
    </View>
  );

  return (
    <View style={styles.container}>
      <BlurView intensity={50} tint="dark" style={styles.glassHeaderRow}>
        <TouchableOpacity style={styles.backButton} onPress={goBack}>
          <Ionicons name="chevron-back" size={20} color="#00bcd4" />
        </TouchableOpacity>

        {thread?.visitor_photo_url ? (
          <Image
            source={{ uri: thread.visitor_photo_url, headers: { "ngrok-skip-browser-warning": "true" } }}
            style={styles.avatar}
          />
        ) : (
          <View style={styles.avatarPlaceholder}>
            <Text style={styles.avatarInitial}>
              {(thread?.visitor_name ?? "?").charAt(0).toUpperCase()}
            </Text>
          </View>
        )}

        <View style={styles.headerText}>
          <Text style={styles.headerName} numberOfLines={1}>
            {thread?.visitor_name ?? "Conversation"}
          </Text>
          {thread ? (
            <Text style={styles.headerSubtitle}>
              {thread.messages.length} message{thread.messages.length === 1 ? "" : "s"}
            </Text>
          ) : null}
        </View>
      </BlurView>

      {loading ? (
        <View style={styles.centered}>
          <ActivityIndicator size="large" color="#00bcd4" />
        </View>
      ) : error ? (
        <View style={styles.centered}>
          <Text style={styles.errorText}>{error}</Text>
          <TouchableOpacity style={styles.retryButton} onPress={load}>
            <Text style={styles.retryButtonText}>Retry</Text>
          </TouchableOpacity>
        </View>
      ) : !thread ? (
        <View style={styles.centeredPad}>
          <Text style={styles.empty}>This conversation is no longer available.</Text>
        </View>
      ) : (
        <FlatList
          data={thread.messages}
          keyExtractor={(m) => m.session_id + m.left_at}
          renderItem={renderMessage}
          contentContainerStyle={{ padding: 16 }}
          refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor="#00bcd4" />}
        />
      )}
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: "#0f172a" },
  centered: { flex: 1, alignItems: "center", justifyContent: "center" },
  centeredPad: { padding: 24, alignItems: "center" },
  glassHeaderRow: {
    flexDirection: "row",
    alignItems: "center",
    gap: 12,
    paddingHorizontal: 16,
    paddingTop: 16,
    paddingBottom: 16,
    overflow: "hidden",
    borderBottomWidth: 1,
    borderBottomColor: "rgba(148, 163, 184, 0.15)",
  },
  backButton: { borderWidth: 1, borderColor: "#334155", borderRadius: 10, padding: 8 },
  avatar: { width: 36, height: 36, borderRadius: 18 },
  avatarPlaceholder: {
    width: 36, height: 36, borderRadius: 18,
    backgroundColor: "#00bcd4", alignItems: "center", justifyContent: "center",
  },
  avatarInitial: { fontSize: 14, color: "#fff", fontWeight: "600" },
  headerText: { flex: 1, minWidth: 0 },
  headerName: { fontSize: 16, color: "#fff", fontWeight: "600" },
  headerSubtitle: { fontSize: 12, color: "#64748b", marginTop: 1 },
  bubble: {
    maxWidth: "85%",
    backgroundColor: "#1e293b",
    borderWidth: 1,
    borderColor: "#334155",
    borderRadius: 16,
    borderBottomLeftRadius: 4,
    padding: 14,
    marginBottom: 12,
    alignSelf: "flex-start",
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 3 },
    shadowOpacity: 0.15,
    shadowRadius: 6,
    elevation: 3,
  },
  purposeTag: {
    alignSelf: "flex-start", backgroundColor: "#334155", borderRadius: 6,
    paddingHorizontal: 8, paddingVertical: 2, marginBottom: 8,
  },
  purposeTagText: { fontSize: 11, color: "#94a3b8", fontWeight: "500" },
  messageText: { fontSize: 14, color: "#e2e8f0" },
  timestamp: { fontSize: 11, color: "#64748b", marginTop: 8 },
  empty: { color: "#64748b", fontSize: 14, textAlign: "center" },
  errorText: { color: "#f87171", textAlign: "center", marginBottom: 16 },
  retryButton: { paddingVertical: 10, paddingHorizontal: 24, borderRadius: 8, backgroundColor: "#00bcd4" },
  retryButtonText: { color: "#0f172a", fontWeight: "600" },
});