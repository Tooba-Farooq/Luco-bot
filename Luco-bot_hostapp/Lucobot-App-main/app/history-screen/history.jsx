import { useState, useCallback, useEffect } from "react";
import {
  View,
  Text,
  Image,
  FlatList,
  TouchableOpacity,
  StyleSheet,
  ActivityIndicator,
} from "react-native";
import { LinearGradient } from "expo-linear-gradient";
import { BlurView } from "expo-blur";
import { getAlertHistory } from "../api/client";

const LIMIT = 20;

function parseUTC(isoString) {
  if (!isoString) return null;
  const hasTZ = /Z$|[+-]\d{2}:\d{2}$/.test(isoString);
  return new Date(hasTZ ? isoString : isoString + "Z");
}

function fmtWhen(iso) {
  const d = parseUTC(iso);
  if (!d) return "";
  return d.toLocaleString([], { month: "short", day: "numeric", hour: "numeric", minute: "2-digit" });
}

function fmtTime(iso) {
  const d = parseUTC(iso);
  if (!d) return "";
  return d.toLocaleTimeString([], { hour: "numeric", minute: "2-digit" });
}

export default function HistoryScreen() {
  const [items, setItems] = useState([]);
  const [offset, setOffset] = useState(0);
  const [hasMore, setHasMore] = useState(false);
  const [loading, setLoading] = useState(true);
  const [loadingMore, setLoadingMore] = useState(false);
  const [error, setError] = useState(null);

  const loadFirstPage = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getAlertHistory({ limit: LIMIT, offset: 0 });
      setItems(data.history);
      setHasMore(data.has_more);
      setOffset(LIMIT);
    } catch (err) {
      console.error("Failed to load alert history:", err);
      setError("Couldn't load your alert history.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadFirstPage();
  }, [loadFirstPage]);

  const loadMore = async () => {
    setLoadingMore(true);
    try {
      const data = await getAlertHistory({ limit: LIMIT, offset });
      setItems((prev) => [...prev, ...data.history]);
      setHasMore(data.has_more);
      setOffset((prev) => prev + LIMIT);
    } catch (err) {
      console.error("Failed to load more history:", err);
    } finally {
      setLoadingMore(false);
    }
  };

  const renderItem = ({ item }) => (
    <LinearGradient
      colors={["#1e293b", "#16233b"]}
      start={{ x: 0, y: 0 }}
      end={{ x: 1, y: 1 }}
      style={styles.card}
    >
      {item.visitor_photo_url ? (
        <Image
          source={{ uri: item.visitor_photo_url, headers: { "ngrok-skip-browser-warning": "true" } }}
          style={styles.photo}
        />
      ) : (
        <View style={styles.photoPlaceholder}>
          <Text style={styles.photoInitial}>
            {item.visitor_name?.charAt(0)?.toUpperCase() || "?"}
          </Text>
        </View>
      )}

      <View style={styles.details}>
        <Text style={styles.name} numberOfLines={1}>{item.visitor_name}</Text>
        {item.purpose ? (
          <Text style={styles.purpose} numberOfLines={1}>{item.purpose}</Text>
        ) : null}
        <Text style={styles.arrived}>Arrived {fmtWhen(item.arrived_at)}</Text>
        {item.host_response === "not_available" && item.available_again_at ? (
          <Text style={styles.returnNote}>
            Asked to return at {fmtTime(item.available_again_at)}
          </Text>
        ) : null}
      </View>

      <View
        style={[
          styles.responseTag,
          item.host_response === "available" ? styles.tagAvailable : styles.tagUnavailable,
        ]}
      >
        <Text
          style={[
            styles.responseTagText,
            item.host_response === "available" ? styles.tagAvailableText : styles.tagUnavailableText,
          ]}
        >
          {item.host_response === "available" ? "Sent In" : "Not Available"}
        </Text>
      </View>
    </LinearGradient>
  );

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
        <TouchableOpacity style={styles.retryButton} onPress={loadFirstPage}>
          <Text style={styles.retryButtonText}>Retry</Text>
        </TouchableOpacity>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <BlurView intensity={40} tint="dark" style={styles.glassHeader}>
        <Text style={styles.header}>History</Text>
        <Text style={styles.headerSubtitle}>Resolved visitor alerts</Text>
      </BlurView>

      {items.length === 0 ? (
        <Text style={styles.empty}>No resolved alerts yet.</Text>
      ) : (
        <FlatList
          data={items}
          keyExtractor={(item) => item.session_id}
          renderItem={renderItem}
          contentContainerStyle={{ paddingBottom: 120 }}
          ListFooterComponent={
            hasMore ? (
              <TouchableOpacity style={styles.loadMoreButton} onPress={loadMore} disabled={loadingMore}>
                {loadingMore ? (
                  <ActivityIndicator size="small" color="#00bcd4" />
                ) : (
                  <Text style={styles.loadMoreText}>Load more</Text>
                )}
              </TouchableOpacity>
            ) : (
              <Text style={styles.endText}>That's the whole history.</Text>
            )
          }
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
  header: { fontSize: 22, color: "#fff", fontWeight: "600" },
  headerSubtitle: { fontSize: 13, color: "#64748b", marginTop: 2 },
  empty: { color: "#64748b", fontSize: 14, textAlign: "center", marginTop: 40 },
  card: {
    borderRadius: 16,
    padding: 16,
    marginBottom: 10,
    flexDirection: "row",
    alignItems: "flex-start",
    gap: 12,
    borderWidth: 1,
    borderColor: "rgba(148, 163, 184, 0.1)",
    shadowColor: "#000",
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.2,
    shadowRadius: 8,
    elevation: 4,
  },
  photo: { width: 44, height: 44, borderRadius: 22 },
  photoPlaceholder: {
    width: 44, height: 44, borderRadius: 22,
    backgroundColor: "#00bcd4", alignItems: "center", justifyContent: "center",
  },
  photoInitial: { fontSize: 16, color: "#fff", fontWeight: "600" },
  details: { flex: 1, minWidth: 0 },
  name: { fontSize: 15, color: "#fff", fontWeight: "600" },
  purpose: { fontSize: 13, color: "#94a3b8", marginTop: 2 },
  arrived: { fontSize: 11, color: "#64748b", marginTop: 4 },
  returnNote: { fontSize: 11, color: "#fbbf24", marginTop: 4 },
  responseTag: { borderRadius: 8, paddingVertical: 4, paddingHorizontal: 8 },
  tagAvailable: { backgroundColor: "rgba(22, 163, 74, 0.2)" },
  tagUnavailable: { backgroundColor: "rgba(220, 38, 38, 0.15)" },
  responseTagText: { fontSize: 11, fontWeight: "600" },
  tagAvailableText: { color: "#4ade80" },
  tagUnavailableText: { color: "#fca5a5" },
  loadMoreButton: {
    marginTop: 8, borderWidth: 1, borderColor: "#334155", borderRadius: 12,
    paddingVertical: 12, alignItems: "center",
  },
  loadMoreText: { color: "#00bcd4", fontWeight: "600", fontSize: 14 },
  endText: { color: "#64748b", fontSize: 12, textAlign: "center", marginTop: 12 },
  errorText: { color: "#f87171", textAlign: "center", marginBottom: 16 },
  retryButton: { paddingVertical: 10, paddingHorizontal: 24, borderRadius: 8, backgroundColor: "#00bcd4" },
  retryButtonText: { color: "#0f172a", fontWeight: "600" },
});