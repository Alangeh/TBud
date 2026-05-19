import { useEffect, useState, useCallback } from 'react';
import { View, Text, ScrollView, StyleSheet, TouchableOpacity, Image, ActivityIndicator } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useLocalSearchParams, useRouter, useFocusEffect } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { api } from '@/src/lib/api';
import { useAuth } from '@/src/contexts/AuthContext';
import { colors, radii, spacing } from '@/src/constants/theme';

type Place = { place_id: string; name: string; category: string; description: string; address: string; photos: string[]; rating: number; review_count: number };
type Review = { review_id: string; rating: number; text: string; photos: string[]; helpful_count: number; created_at: string; user_name: string; user_picture?: string; user_verified: boolean };

export default function PlaceScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const { token } = useAuth();
  const [place, setPlace] = useState<Place | null>(null);
  const [city, setCity] = useState<any>(null);
  const [reviews, setReviews] = useState<Review[]>([]);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    try {
      const p = await api<{ place: Place; city: any }>(`/places/${id}`);
      setPlace(p.place);
      setCity(p.city);
      const r = await api<{ reviews: Review[] }>(`/places/${id}/reviews`);
      setReviews(r.reviews);
    } finally { setLoading(false); }
  }, [id]);

  useFocusEffect(useCallback(() => { load(); }, [load]));

  const vote = async (rid: string) => {
    if (!token) return;
    const res = await api<{ helpful_count: number; voted: boolean }>(`/reviews/${rid}/helpful`, { token, method: 'POST' });
    setReviews(rs => rs.map(r => r.review_id === rid ? { ...r, helpful_count: res.helpful_count } : r));
  };

  if (loading || !place) return <SafeAreaView style={styles.loading}><ActivityIndicator color={colors.accent} /></SafeAreaView>;

  return (
    <View style={styles.root}>
      <ScrollView showsVerticalScrollIndicator={false} testID="place-screen">
        <Image source={{ uri: place.photos?.[0] }} style={styles.hero} />
        <SafeAreaView style={styles.overlay} edges={['top']}>
          <TouchableOpacity testID="back-btn" onPress={() => router.back()} style={styles.iconCircle}>
            <Ionicons name="chevron-back" size={22} color="#fff" />
          </TouchableOpacity>
          <TouchableOpacity testID="save-btn" style={styles.iconCircle}>
            <Ionicons name="bookmark-outline" size={20} color="#fff" />
          </TouchableOpacity>
        </SafeAreaView>

        <View style={styles.sheet}>
          <Text style={styles.cat}>{place.category?.toUpperCase()}</Text>
          <Text style={styles.title}>{place.name}</Text>
          <View style={styles.metaRow}>
            <Ionicons name="star" size={15} color={colors.star} />
            <Text style={styles.metaStrong}>{place.rating > 0 ? place.rating.toFixed(1) : 'New'}</Text>
            <Text style={styles.meta}> · {place.review_count} reviews</Text>
          </View>
          <View style={styles.addrRow}>
            <Ionicons name="location-outline" size={14} color={colors.textMuted} />
            <Text style={styles.meta}>{place.address}{city ? `, ${city.name}` : ''}</Text>
          </View>
          <Text style={styles.desc}>{place.description}</Text>

          <View style={styles.divider} />

          <View style={styles.reviewsHeader}>
            <Text style={styles.sectionTitle}>Reviews</Text>
            <TouchableOpacity testID="write-review-btn" onPress={() => router.push(`/write-review/${place.place_id}`)} style={styles.writeBtn}>
              <Ionicons name="create-outline" size={16} color={colors.accent} />
              <Text style={styles.writeBtnText}>Write a review</Text>
            </TouchableOpacity>
          </View>

          {reviews.length === 0 ? (
            <View style={styles.empty}>
              <Text style={styles.emptyText}>Be the first to share your experience.</Text>
            </View>
          ) : reviews.map(r => (
            <View key={r.review_id} style={styles.reviewCard} testID={`review-${r.review_id}`}>
              <View style={styles.reviewHead}>
                <View style={styles.reviewAvatar}>
                  {r.user_picture ? <Image source={{ uri: r.user_picture }} style={{ width: 36, height: 36, borderRadius: 18 }} /> : <Ionicons name="person" size={18} color={colors.textMuted} />}
                </View>
                <View style={{ flex: 1 }}>
                  <View style={{ flexDirection: 'row', alignItems: 'center', gap: 6 }}>
                    <Text style={styles.reviewName}>{r.user_name}</Text>
                    {r.user_verified && <Ionicons name="shield-checkmark" size={14} color={colors.trust} />}
                  </View>
                  <View style={{ flexDirection: 'row', gap: 2, marginTop: 2 }}>
                    {[1, 2, 3, 4, 5].map(i => <Ionicons key={i} name={i <= r.rating ? 'star' : 'star-outline'} size={12} color={colors.star} />)}
                  </View>
                </View>
              </View>
              <Text style={styles.reviewText}>{r.text}</Text>
              <TouchableOpacity testID={`helpful-${r.review_id}`} onPress={() => vote(r.review_id)} style={styles.helpful}>
                <Ionicons name="heart-outline" size={14} color={colors.textMuted} />
                <Text style={styles.helpfulText}>Helpful · {r.helpful_count}</Text>
              </TouchableOpacity>
            </View>
          ))}
          <View style={{ height: 80 }} />
        </View>
      </ScrollView>

      <View style={styles.fabBar}>
        <TouchableOpacity
          testID="cta-write-review"
          style={styles.fab}
          onPress={() => router.push(`/write-review/${place.place_id}`)}
        >
          <Ionicons name="create" size={18} color="#fff" />
          <Text style={styles.fabText}>Write a review</Text>
        </TouchableOpacity>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1, backgroundColor: colors.bg },
  loading: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bg },
  hero: { width: '100%', height: 340 },
  overlay: { position: 'absolute', top: 0, left: 0, right: 0, flexDirection: 'row', justifyContent: 'space-between', paddingHorizontal: spacing.md },
  iconCircle: { width: 40, height: 40, borderRadius: 20, backgroundColor: 'rgba(0,0,0,0.4)', alignItems: 'center', justifyContent: 'center' },
  sheet: { backgroundColor: colors.bg, borderTopLeftRadius: 32, borderTopRightRadius: 32, marginTop: -28, padding: spacing.lg, paddingTop: spacing.xl },
  cat: { color: colors.accent, fontSize: 11, fontWeight: '700', letterSpacing: 2 },
  title: { fontSize: 30, fontWeight: '700', color: colors.text, marginTop: 4, letterSpacing: -0.5 },
  metaRow: { flexDirection: 'row', alignItems: 'center', marginTop: 10, gap: 4 },
  metaStrong: { fontSize: 14, fontWeight: '700', color: colors.text },
  meta: { color: colors.textMuted, fontSize: 14 },
  addrRow: { flexDirection: 'row', alignItems: 'center', marginTop: 6, gap: 4 },
  desc: { color: colors.textMuted, fontSize: 15, lineHeight: 22, marginTop: spacing.md },
  divider: { height: 1, backgroundColor: colors.border, marginVertical: spacing.lg },
  reviewsHeader: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', marginBottom: spacing.md },
  sectionTitle: { fontSize: 20, fontWeight: '700', color: colors.text },
  writeBtn: { flexDirection: 'row', alignItems: 'center', gap: 4, paddingHorizontal: 12, paddingVertical: 6, borderRadius: radii.pill, backgroundColor: '#FFF6F2' },
  writeBtnText: { color: colors.accent, fontSize: 13, fontWeight: '700' },
  empty: { padding: spacing.md, alignItems: 'center' },
  emptyText: { color: colors.textMuted, fontSize: 14 },
  reviewCard: { backgroundColor: colors.card, padding: spacing.md, borderRadius: radii.md, borderWidth: 1, borderColor: colors.border, marginBottom: 12 },
  reviewHead: { flexDirection: 'row', alignItems: 'center', gap: 10, marginBottom: 8 },
  reviewAvatar: { width: 36, height: 36, borderRadius: 18, backgroundColor: colors.bgAlt, alignItems: 'center', justifyContent: 'center', overflow: 'hidden' },
  reviewName: { fontSize: 14, fontWeight: '700', color: colors.text },
  reviewText: { color: colors.textMuted, fontSize: 14, lineHeight: 20 },
  helpful: { flexDirection: 'row', alignItems: 'center', gap: 4, marginTop: 8 },
  helpfulText: { color: colors.textMuted, fontSize: 12, fontWeight: '600' },
  fabBar: { position: 'absolute', bottom: 0, left: 0, right: 0, padding: spacing.md, backgroundColor: 'rgba(250,249,246,0.95)', borderTopWidth: 1, borderTopColor: colors.border },
  fab: { backgroundColor: colors.accent, paddingVertical: 14, borderRadius: radii.pill, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 8 },
  fabText: { color: '#fff', fontSize: 15, fontWeight: '700' },
});
