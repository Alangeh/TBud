import { useEffect, useState } from 'react';
import { View, Text, ScrollView, StyleSheet, TouchableOpacity, Image, ActivityIndicator } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { api } from '@/src/lib/api';
import { colors, radii, spacing } from '@/src/constants/theme';

type Place = { place_id: string; name: string; category: string; description: string; photos: string[]; rating: number; review_count: number };

const CATS = ['all', 'attraction', 'restaurant', 'hotel'] as const;

export default function CityScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const [places, setPlaces] = useState<Place[]>([]);
  const [cityName, setCityName] = useState('');
  const [cat, setCat] = useState<typeof CATS[number]>('all');
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    (async () => {
      setLoading(true);
      try {
        const param = cat === 'all' ? '' : `?category=${cat}`;
        const r = await api<{ places: Place[] }>(`/cities/${id}/places${param}`);
        setPlaces(r.places);
        if (r.places[0]) {
          const p = await api<{ city: any }>(`/places/${r.places[0].place_id}`);
          setCityName(p.city?.name ?? '');
        }
      } finally { setLoading(false); }
    })();
  }, [id, cat]);

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.header}>
        <TouchableOpacity testID="back-btn" onPress={() => router.back()}>
          <Ionicons name="chevron-back" size={26} color={colors.text} />
        </TouchableOpacity>
        <Text style={styles.headerTitle}>{cityName || 'City'}</Text>
        <View style={{ width: 26 }} />
      </View>

      <View style={styles.catRow}>
        {CATS.map(c => (
          <TouchableOpacity
            key={c}
            testID={`filter-${c}`}
            onPress={() => setCat(c)}
            style={[styles.catChip, cat === c && styles.catChipActive]}
          >
            <Text style={[styles.catText, cat === c && styles.catTextActive]}>{c[0].toUpperCase() + c.slice(1)}</Text>
          </TouchableOpacity>
        ))}
      </View>

      {loading ? <ActivityIndicator color={colors.accent} style={{ marginTop: 30 }} /> : (
        <ScrollView contentContainerStyle={styles.scroll} testID="city-screen">
          {places.length === 0 && (
            <Text style={styles.empty}>No places yet in this category.</Text>
          )}
          {places.map(p => (
            <TouchableOpacity
              key={p.place_id}
              testID={`place-card-${p.place_id}`}
              style={styles.placeCard}
              onPress={() => router.push(`/place/${p.place_id}`)}
            >
              <Image source={{ uri: p.photos?.[0] }} style={styles.placeImg} />
              <View style={styles.placeBody}>
                <Text style={styles.placeCat}>{p.category?.toUpperCase()}</Text>
                <Text style={styles.placeName}>{p.name}</Text>
                <Text numberOfLines={2} style={styles.placeDesc}>{p.description}</Text>
                <View style={styles.ratingRow}>
                  <Ionicons name="star" size={14} color={colors.star} />
                  <Text style={styles.ratingText}>{p.rating > 0 ? p.rating.toFixed(1) : 'New'}</Text>
                  <Text style={styles.ratingCount}>· {p.review_count} review{p.review_count !== 1 && 's'}</Text>
                </View>
              </View>
            </TouchableOpacity>
          ))}
          <View style={{ height: 32 }} />
        </ScrollView>
      )}
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.bg },
  header: { flexDirection: 'row', alignItems: 'center', justifyContent: 'space-between', paddingHorizontal: spacing.md, paddingVertical: spacing.sm },
  headerTitle: { fontSize: 18, fontWeight: '700', color: colors.text },
  catRow: { flexDirection: 'row', gap: 8, paddingHorizontal: spacing.lg, paddingVertical: spacing.sm },
  catChip: { paddingHorizontal: 14, paddingVertical: 8, borderRadius: radii.pill, backgroundColor: colors.bgAlt },
  catChipActive: { backgroundColor: colors.text },
  catText: { fontSize: 13, fontWeight: '600', color: colors.textMuted },
  catTextActive: { color: '#fff' },
  scroll: { padding: spacing.lg, gap: 14 },
  empty: { color: colors.textMuted, textAlign: 'center', marginTop: spacing.xl },
  placeCard: { backgroundColor: colors.card, borderRadius: radii.lg, overflow: 'hidden', borderWidth: 1, borderColor: colors.border },
  placeImg: { width: '100%', height: 180 },
  placeBody: { padding: spacing.md },
  placeCat: { color: colors.accent, fontSize: 10, fontWeight: '700', letterSpacing: 1.5 },
  placeName: { fontSize: 20, fontWeight: '700', color: colors.text, marginTop: 4 },
  placeDesc: { color: colors.textMuted, fontSize: 13, marginTop: 6, lineHeight: 18 },
  ratingRow: { flexDirection: 'row', alignItems: 'center', marginTop: 10, gap: 4 },
  ratingText: { color: colors.text, fontSize: 13, fontWeight: '700' },
  ratingCount: { color: colors.textMuted, fontSize: 13 },
});
