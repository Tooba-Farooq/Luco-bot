import { useState, useCallback, useEffect } from "react";
import {
  View,
  Text,
  Image,
  TextInput,
  TouchableOpacity,
  StyleSheet,
  ActivityIndicator,
} from "react-native";
import { getMe, updateFloorRoom, logout as logoutClient } from "../api/client";

export default function AccountScreen({ goToLogin }) {
  const [employee, setEmployee] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState("");
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setError(null);
    try {
      const data = await getMe();
      setEmployee(data);
    } catch (err) {
      console.error("Failed to load profile:", err);
      setError("Couldn't load your profile.");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    load();
  }, [load]);

  const startEditing = () => {
    setValue(employee?.floor_room ?? "");
    setEditing(true);
  };

  const save = async () => {
    setSaving(true);
    try {
      const result = await updateFloorRoom(value);
      setEmployee((prev) => ({ ...prev, floor_room: result.floor_room }));
      setEditing(false);
    } catch (err) {
      console.error("Failed to save floor/room:", err);
      // Keep the field open on failure so nothing is lost
    } finally {
      setSaving(false);
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
        <TouchableOpacity style={styles.retryButton} onPress={load}>
          <Text style={styles.retryButtonText}>Retry</Text>
        </TouchableOpacity>
      </View>
    );
  }

  return (
    <View style={styles.container}>
      <View style={styles.profileCard}>
        {employee?.photo_url ? (
          <Image
            source={{
              uri: employee.photo_url,
              headers: { "ngrok-skip-browser-warning": "true" },
            }}
            style={styles.avatar}
          />
        ) : (
          <View style={styles.avatarPlaceholder}>
            <Text style={styles.avatarInitial}>
              {employee?.name?.charAt(0)?.toUpperCase() || "?"}
            </Text>
          </View>
        )}
        <Text style={styles.name}>{employee?.name}</Text>
        <Text style={styles.code}>{employee?.employee_code}</Text>
      </View>

      <View style={styles.locationCard}>
        <Text style={styles.label}>FLOOR / ROOM</Text>

        {editing ? (
          <View style={styles.editRow}>
            <TextInput
              autoFocus
              value={value}
              onChangeText={setValue}
              placeholder="e.g. 3rd Floor, Room 12"
              placeholderTextColor="#64748b"
              style={styles.input}
            />
            <TouchableOpacity
              style={[styles.saveButton, saving && styles.saveButtonDisabled]}
              onPress={save}
              disabled={saving}
            >
              {saving ? (
                <ActivityIndicator size="small" color="#0f172a" />
              ) : (
                <Text style={styles.saveButtonText}>Save</Text>
              )}
            </TouchableOpacity>
          </View>
        ) : (
          <TouchableOpacity style={styles.viewRow} onPress={startEditing}>
            <Text style={employee?.floor_room ? styles.value : styles.valuePlaceholder}>
              {employee?.floor_room ?? "Not set"}
            </Text>
            <Text style={styles.editLink}>Edit</Text>
          </TouchableOpacity>
        )}
      </View>

      <TouchableOpacity style={styles.logoutButton} onPress={handleLogout}>
        <Text style={styles.logoutButtonText}>Log Out</Text>
      </TouchableOpacity>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, backgroundColor: "#0f172a", padding: 16 },
  centered: { flex: 1, alignItems: "center", justifyContent: "center", backgroundColor: "#0f172a" },
  profileCard: {
    backgroundColor: "#1e293b",
    borderRadius: 16,
    borderWidth: 1,
    borderColor: "#334155",
    paddingVertical: 32,
    paddingHorizontal: 20,
    alignItems: "center",
    marginTop: 16,
  },
  avatar: { width: 96, height: 96, borderRadius: 48 },
  avatarPlaceholder: {
    width: 96,
    height: 96,
    borderRadius: 48,
    backgroundColor: "#00bcd4",
    alignItems: "center",
    justifyContent: "center",
  },
  avatarInitial: { fontSize: 36, color: "#fff", fontWeight: "600" },
  name: { fontSize: 20, color: "#fff", fontWeight: "600", marginTop: 16 },
  code: { fontSize: 14, color: "#94a3b8", marginTop: 2 },
  locationCard: {
    backgroundColor: "#1e293b",
    borderRadius: 16,
    borderWidth: 1,
    borderColor: "#334155",
    padding: 20,
    marginTop: 16,
  },
  label: { fontSize: 11, fontWeight: "700", color: "#64748b", letterSpacing: 0.5 },
  viewRow: {
    marginTop: 8,
    flexDirection: "row",
    alignItems: "center",
    justifyContent: "space-between",
    gap: 12,
  },
  value: { fontSize: 15, color: "#fff", flexShrink: 1 },
  valuePlaceholder: { fontSize: 15, color: "#64748b", flexShrink: 1 },
  editLink: { fontSize: 14, fontWeight: "600", color: "#00bcd4" },
  editRow: {
    marginTop: 12,
    flexDirection: "row",
    gap: 8,
  },
  input: {
    flex: 1,
    backgroundColor: "#0f172a",
    borderWidth: 1,
    borderColor: "#334155",
    borderRadius: 10,
    paddingHorizontal: 12,
    paddingVertical: 10,
    color: "#fff",
    fontSize: 14,
  },
  saveButton: {
    backgroundColor: "#00bcd4",
    borderRadius: 10,
    paddingHorizontal: 16,
    justifyContent: "center",
    alignItems: "center",
  },
  saveButtonDisabled: { opacity: 0.5 },
  saveButtonText: { color: "#0f172a", fontWeight: "700", fontSize: 14 },
  logoutButton: {
    marginTop: 32,
    backgroundColor: "rgba(220, 38, 38, 0.15)",
    borderWidth: 1,
    borderColor: "rgba(220, 38, 38, 0.4)",
    borderRadius: 12,
    paddingVertical: 14,
    alignItems: "center",
  },
  logoutButtonText: { color: "#dc2626", fontWeight: "700", fontSize: 14 },
  errorText: { color: "#f87171", textAlign: "center", marginBottom: 16 },
  retryButton: {
    paddingVertical: 10,
    paddingHorizontal: 24,
    borderRadius: 8,
    backgroundColor: "#00bcd4",
  },
  retryButtonText: { color: "#0f172a", fontWeight: "600" },
});