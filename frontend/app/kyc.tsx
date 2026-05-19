import { useState } from 'react';
import { View, Text, TouchableOpacity, StyleSheet, Image, Alert, ActivityIndicator, ScrollView } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useRouter } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import * as ImagePicker from 'expo-image-picker';
import { useAuth } from '@/src/contexts/AuthContext';
import { api } from '@/src/lib/api';
import { colors, radii, spacing } from '@/src/constants/theme';

const TYPES = [
  { id: 'passport', label: 'Passport', icon: 'airplane' as const },
  { id: 'drivers_license', label: 'Driver\'s License', icon: 'car-sport' as const },
  { id: 'national_id', label: 'National ID', icon: 'card' as const },
];

export default function Kyc() {
  const router = useRouter();
  const { token, setUser, user } = useAuth();
  const [docType, setDocType] = useState<string | null>(null);
  const [photo, setPhoto] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  const pick = async () => {
    const perm = await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (!perm.granted) {
      Alert.alert('Permission needed', 'Allow photo access to upload your ID.');
      return;
    }
    const res = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ImagePicker.MediaTypeOptions.Images,
      base64: true,
      quality: 0.5,
    });
    if (!res.canceled && res.assets[0]?.base64) {
      setPhoto(`data:image/jpeg;base64,${res.assets[0].base64}`);
    }
  };

  const submit = async () => {
    if (!docType) { Alert.alert('Pick a document type'); return; }
    if (!photo) { Alert.alert('Upload a photo of your ID'); return; }
    setBusy(true);
    try {
      const res = await api<{ user: any }>('/auth/kyc', { token, body: { document_type: docType, image_base64: photo } });
      setUser(res.user);
      Alert.alert('Verified!', 'Your verified badge has been activated.', [
        { text: 'Continue', onPress: () => router.replace('/(tabs)/explore') }
      ]);
    } catch (e: any) {
      Alert.alert('KYC failed', e?.message ?? 'Try again');
    } finally { setBusy(false); }
  };

  return (
    <SafeAreaView style={styles.safe} edges={['top', 'bottom']}>
      <ScrollView contentContainerStyle={styles.scroll}>
        <View style={styles.header}>
          <TouchableOpacity testID="kyc-skip" onPress={() => router.replace('/(tabs)/explore')}>
            <Text style={styles.skip}>Skip</Text>
          </TouchableOpacity>
        </View>

        <View style={styles.badge}>
          <Ionicons name="shield-checkmark" size={28} color={colors.trust} />
        </View>
        <Text style={styles.title}>Verify your identity</Text>
        <Text style={styles.sub}>
          Reviews carry weight when readers can trust them. Verified travelers earn a badge, higher ranking, and full posting privileges.
        </Text>

        <Text style={styles.sectionLabel}>Choose document type</Text>
        <View style={styles.typeRow}>
          {TYPES.map(t => (
            <TouchableOpacity
              key={t.id}
              testID={`kyc-doctype-${t.id}`}
              onPress={() => setDocType(t.id)}
              style={[styles.typeCard, docType === t.id && styles.typeCardActive]}
            >
              <Ionicons name={t.icon} size={22} color={docType === t.id ? colors.accent : colors.textMuted} />
              <Text style={[styles.typeText, docType === t.id && { color: colors.accent }]}>{t.label}</Text>
            </TouchableOpacity>
          ))}
        </View>

        <Text style={styles.sectionLabel}>Upload a clear photo</Text>
        <TouchableOpacity testID="kyc-upload" onPress={pick} style={styles.upload}>
          {photo ? (
            <Image source={{ uri: photo }} style={styles.preview} />
          ) : (
            <>
              <Ionicons name="cloud-upload-outline" size={32} color={colors.textMuted} />
              <Text style={styles.uploadText}>Tap to choose from library</Text>
              <Text style={styles.uploadHint}>Government-issued ID — clearly visible</Text>
            </>
          )}
        </TouchableOpacity>

        <View style={{ flex: 1 }} />

        <TouchableOpacity testID="kyc-submit" onPress={submit} style={[styles.primary, (!docType || !photo) && styles.primaryDim]} disabled={busy || !docType || !photo}>
          {busy ? <ActivityIndicator color="#fff" /> : <Text style={styles.primaryText}>Verify me</Text>}
        </TouchableOpacity>
        <Text style={styles.note}>Demo mode: ID is mocked and not stored externally.</Text>
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.bg },
  scroll: { padding: spacing.lg, flexGrow: 1 },
  header: { flexDirection: 'row', justifyContent: 'flex-end', marginBottom: spacing.md },
  skip: { color: colors.textMuted, fontSize: 14, fontWeight: '600' },
  badge: { width: 56, height: 56, borderRadius: 28, backgroundColor: colors.trustBg, alignItems: 'center', justifyContent: 'center', marginBottom: spacing.md },
  title: { fontSize: 28, fontWeight: '700', color: colors.text, letterSpacing: -0.5, marginBottom: 8 },
  sub: { fontSize: 15, color: colors.textMuted, lineHeight: 22, marginBottom: spacing.xl },
  sectionLabel: { fontSize: 12, fontWeight: '700', color: colors.textMuted, letterSpacing: 2, marginBottom: spacing.sm, marginTop: spacing.md, textTransform: 'uppercase' },
  typeRow: { flexDirection: 'row', gap: 10 },
  typeCard: { flex: 1, alignItems: 'center', backgroundColor: colors.card, borderRadius: 16, paddingVertical: 18, paddingHorizontal: 8, borderWidth: 1.5, borderColor: colors.border, gap: 6 },
  typeCardActive: { borderColor: colors.accent, backgroundColor: '#FFF6F2' },
  typeText: { fontSize: 12, fontWeight: '600', color: colors.textMuted, textAlign: 'center' },
  upload: { backgroundColor: colors.card, borderRadius: 20, paddingVertical: 36, alignItems: 'center', borderWidth: 1.5, borderColor: colors.border, borderStyle: 'dashed', overflow: 'hidden' },
  uploadText: { color: colors.text, fontWeight: '600', marginTop: 12 },
  uploadHint: { color: colors.textFaint, fontSize: 12, marginTop: 4 },
  preview: { width: '100%', height: 200, borderRadius: 14 },
  primary: { backgroundColor: colors.accent, paddingVertical: 16, borderRadius: radii.pill, alignItems: 'center', marginTop: spacing.lg },
  primaryDim: { opacity: 0.5 },
  primaryText: { color: '#fff', fontSize: 16, fontWeight: '700' },
  note: { color: colors.textFaint, fontSize: 11, textAlign: 'center', marginTop: 10 },
});
