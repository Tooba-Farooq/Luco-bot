import { useEffect, useState, useCallback } from "react";
import {
  View,
  Text,
  Image,
  ActivityIndicator,
  TouchableOpacity,
  StyleSheet,
} from "react-native";
import { getMe, logout as logoutClient } from "../api/client";

export default function Home({ goToLogin }) {
  const [employee, setEmployee] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

 const loadProfile = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await getMe();
      setEmployee(data);
    } catch (err) {
      console.error("Failed to load profile:", err);
      if (err.message === 'Refresh failed, session expired') {
        goToLogin();
        return;
      }
      setError("Could not load your profile. Pull to retry or log in again.");
    } finally {
      setLoading(false);
    }
  }, []);

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
        <TouchableOpacity style={styles.retryButton} onPress={loadProfile}>
          <Text style={styles.retryButtonText}>Retry</Text>
        </TouchableOpacity>
        <TouchableOpacity onPress={handleLogout}>
          <Text style={styles.logoutText}>Log out</Text>
        </TouchableOpacity>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      {employee?.photo_url ? (
        <Image source={{ uri: employee.photo_url }} style={styles.avatar} />
      ) : (
        <View style={styles.avatarPlaceholder}>
          <Text style={styles.avatarInitial}>
            {employee?.name?.charAt(0)?.toUpperCase() || "?"}
          </Text>
        </View>
      )}

      <Text style={styles.welcomeText}>Welcome, {employee?.name || "there"}</Text>
      <Text style={styles.subtitle}>{employee?.employee_code}</Text>

      <View style={styles.idleBox}>
        <Text style={styles.idleText}>No active visitor alerts</Text>
      </View>

      <TouchableOpacity style={styles.logoutButton} onPress={handleLogout}>
        <Text style={styles.logoutButtonText}>Log Out</Text>
      </TouchableOpacity>
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    flex: 1,
    alignItems: "center",
    justifyContent: "center",
    padding: 24,
    backgroundColor: "#0f172a",
  },
  centered: {
    flex: 1,
    alignItems: "center",
    justifyContent: "center",
    padding: 24,
    backgroundColor: "#0f172a",
  },
  avatar: {
    width: 96,
    height: 96,
    borderRadius: 48,
    marginBottom: 16,
  },
  avatarPlaceholder: {
    width: 96,
    height: 96,
    borderRadius: 48,
    backgroundColor: "#00bcd4",
    alignItems: "center",
    justifyContent: "center",
    marginBottom: 16,
  },
  avatarInitial: {
    fontSize: 36,
    color: "#fff",
    fontWeight: "600",
  },
  welcomeText: {
    fontSize: 22,
    color: "#fff",
    fontWeight: "600",
  },
  subtitle: {
    fontSize: 14,
    color: "#94a3b8",
    marginTop: 4,
  },
  idleBox: {
    marginTop: 40,
    padding: 20,
    borderRadius: 12,
    borderWidth: 1,
    borderColor: "#1e293b",
  },
  idleText: {
    color: "#64748b",
    fontSize: 14,
  },
  logoutButton: {
    marginTop: 48,
    paddingVertical: 12,
    paddingHorizontal: 32,
    borderRadius: 8,
    backgroundColor: "#dc2626",
  },
  logoutButtonText: {
    color: "#fff",
    fontWeight: "600",
  },
  errorText: {
    color: "#f87171",
    textAlign: "center",
    marginBottom: 16,
  },
  retryButton: {
    paddingVertical: 10,
    paddingHorizontal: 24,
    borderRadius: 8,
    backgroundColor: "#00bcd4",
    marginBottom: 16,
  },
  retryButtonText: {
    color: "#0f172a",
    fontWeight: "600",
  },
  logoutText: {
    color: "#94a3b8",
    textDecorationLine: "underline",
  },
});