import { useState, useEffect } from 'react';
import { View, Text, ScrollView, StyleSheet, TouchableOpacity, TextInput, Image, ActivityIndicator, Alert, KeyboardAvoidingView, Platform } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import * as ImagePicker from 'expo-image-picker';
import { api } from '@/src/lib/api';
import { useAuth } from '@/src/contexts/AuthContext';
import { colors, radii, spacing } from '@/src/constants/theme';

export default function WriteReview() {
  const { placeId } = useLocalSearchParams<{ placeId: string }>();
  const router = useRouter();
  const { token, user } = useAuth();
  const [place, setPlace] = useState<any>(null);
  const [rating, setRating] = useState(0);
  const [text, setText] = useState('');
  const [photos, setPhotos] = useState<string[]>([]);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    (async () => {
      try {
        const p = await api<{ place: any }>(`/places/${placeId}`);
        setPlace(p.place);
      } catch {}
    })();
  }, [placeId]);

  const addPhoto = async () => {
    if (photos.length >= 10) { Alert.alert('Limit', 'Max 10 photos'); return; }
    const perm = await ImagePicker.requestMediaLibraryPermissionsAsync();
    if (!perm.granted) { Alert.alert('Permission needed', 'Allow photo access to attach images.'); return; }
    const res = await ImagePicker.launchImageLibraryAsync({
      mediaTypes: ImagePicker.MediaTypeOptions.Images,
      base64: true,
      quality: 0.4,
    });
    if (!res.canceled && res.assets[0]?.base64) {
      setPhotos(p => [...p, `data:image/jpeg;base64,${res.assets[0].base64}`]);
    }
  };

  const submit = async () => {
    if (rating === 0) { Alert.alert('Pick a star rating'); return; }
    if (text.trim().length < 5) { Alert.alert('Write a bit more', 'Reviews should be at least 5 characters.'); return; }
    setBusy(true);
    try {
      await api('/reviews', { token, body: { place_id: placeId, rating, text: text.trim(), photos } });
      Alert.alert('Review posted!', 'Thanks for contributing.', [
        { text: 'OK', onPress: () => router.back() }
      ]);
    } catch (e: any) {
      Alert.alert('Failed', e?.message ?? 'Try again');
    } finally { setBusy(false); }
  };

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : 'height'}>
        <View style={styles.header}>
          <TouchableOpacity testID="close-btn" onPress={() => router.back()}>
            <Ionicons name="close" size={26} color={colors.text} />
          </TouchableOpacity>
          <Text style={styles.headerTitle}>Write a review</Text>
          <View style={{ width: 26 }} />
        </View>

        <ScrollView contentContainerStyle={styles.scroll} keyboardShouldPersistTaps="handled" testID="write-review-screen">
          {place && (
            <View style={styles.placeRow}>
              <Image source={{ uri: place.photos?.[0] }} style={styles.placeImg} />
              <View>
                <Text style={styles.placeCat}>{place.category?.toUpperCase()}</Text>
                <Text style={styles.placeName}>{place.name}</Text>
              </View>
            </View>
          )}

          <Text style={styles.label}>Your rating</Text>
          <View style={styles.starRow}>
            {[1, 2, 3, 4, 5].map(i => (
              <TouchableOpacity key={i} testID={`star-${i}`} onPress={() => setRating(i)}>
                <Ionicons name={i <= rating ? 'star' : 'star-outline'} size={42} color={colors.star} />
              </TouchableOpacity>
            ))}
          </View>

          <Text style={styles.label}>Your experience</Text>
          <TextInput
            testID="review-text"
            value={text}
            onChangeText={setText}
            multiline
            placeholder="Tell other travelers what made this place memorable..."
            placeholderTextColor={colors.textFaint}
            style={styles.textarea}
          />

          <Text style={styles.label}>Photos (optional, up to 10)</Text>
          <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={{ gap: 10 }}>
            <TouchableOpacity testID="add-photo" onPress={addPhoto} style={styles.addPhoto}>
              <Ionicons name="add" size={28} color={colors.textMuted} />
              <Text style={styles.addPhotoText}>Add</Text>
            </TouchableOpacity>
            {photos.map((p, idx) => (
              <View key={idx} style={styles.photoWrap}>
                <Image source={{ uri: p }} style={styles.photo} />
                <TouchableOpacity onPress={() => setPhotos(ps => ps.filter((_, i) => i !== idx))} style={styles.photoRemove}>
                  <Ionicons name="close" size={14} color="#fff" />
                </TouchableOpacity>
              </View>
            ))}
          </ScrollView>

          {!user?.verified && (
            <View style={styles.verifyHint}>
              <Ionicons name="information-circle" size={16} color={colors.trust} />
              <Text style={styles.verifyHintText}>Verified travelers' reviews rank higher. Get verified in your profile.</Text>
            </View>
          )}

          <TouchableOpacity testID="review-submit" onPress={submit} style={[styles.primary, (rating === 0 || text.length < 5) && { opacity: 0.5 }]} disabled={busy || rating === 0 || text.length < 5}>
            {busy ? <ActivityIndicator color="#fff" /> : <Text style={styles.primaryText}>Post review</Text>}
          </TouchableOpacity>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.bg },
  header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', padding: spacing.md },
  headerTitle: { fontSize: 16, fontWeight: '700', color: colors.text },
  scroll: { padding: spacing.lg, paddingBottom: spacing.xxl },
  placeRow: { flexDirection: 'row', gap: 12, alignItems: 'center', backgroundColor: colors.card, padding: 12, borderRadius: radii.md, borderWidth: 1, borderColor: colors.border, marginBottom: spacing.lg },
  placeImg: { width: 56, height: 56, borderRadius: 10 },
  placeCat: { color: colors.accent, fontSize: 10, fontWeight: '700', letterSpacing: 1.5 },
  placeName: { fontSize: 16, fontWeight: '700', color: colors.text },
  label: { fontSize: 13, fontWeight: '700', color: colors.textMuted, marginBottom: 10, marginTop: spacing.md, letterSpacing: 1, textTransform: 'uppercase' },
  starRow: { flexDirection: 'row', gap: 6, justifyContent: 'center', marginVertical: spacing.sm },
  textarea: { backgroundColor: colors.bgAlt, borderRadius: 14, padding: 14, minHeight: 140, fontSize: 15, color: colors.text, textAlignVertical: 'top' },
  addPhoto: { width: 84, height: 84, borderRadius: 14, borderWidth: 1.5, borderColor: colors.border, borderStyle: 'dashed', alignItems: 'center', justifyContent: 'center', backgroundColor: colors.card },
  addPhotoText: { color: colors.textMuted, fontSize: 12, fontWeight: '600' },
  photoWrap: { position: 'relative' },
  photo: { width: 84, height: 84, borderRadius: 14 },
  photoRemove: { position: 'absolute', top: 4, right: 4, width: 22, height: 22, borderRadius: 11, backgroundColor: 'rgba(0,0,0,0.6)', alignItems: 'center', justifyContent: 'center' },
  verifyHint: { flexDirection: 'row', gap: 6, backgroundColor: colors.trustBg, padding: 12, borderRadius: 12, marginTop: spacing.lg, alignItems: 'flex-start' },
  verifyHintText: { color: colors.trust, fontSize: 12, flex: 1, lineHeight: 18 },
  primary: { backgroundColor: colors.accent, paddingVertical: 16, borderRadius: radii.pill, alignItems: 'center', marginTop: spacing.xl },
  primaryText: { color: '#fff', fontSize: 16, fontWeight: '700' },
});
