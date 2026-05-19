import { useEffect, useState } from 'react';
import { View, Text, ScrollView, StyleSheet, TouchableOpacity, Image, ActivityIndicator } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { api } from '@/src/lib/api';
import { colors, radii, spacing } from '@/src/constants/theme';

type City = { city_id: string; country_id: string; name: string; image: string; description: string };
type Country = { country_id: string; name: string; description: string; image: string };

export default function CountryScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const router = useRouter();
  const [country, setCountry] = useState<Country | null>(null);
  const [cities, setCities] = useState<City[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    (async () => {
      try {
        const all = await api<{ countries: Country[] }>('/countries');
        setCountry(all.countries.find(c => c.country_id === id) ?? null);
        const r = await api<{ cities: City[] }>(`/countries/${id}/cities`);
        setCities(r.cities);
      } finally { setLoading(false); }
    })();
  }, [id]);

  if (loading) return <SafeAreaView style={styles.loading}><ActivityIndicator color={colors.accent} /></SafeAreaView>;

  return (
    <View style={styles.root}>
      <Image source={{ uri: country?.image }} style={styles.hero} />
      <SafeAreaView style={styles.headerOverlay} edges={['top']}>
        <TouchableOpacity testID="back-btn" onPress={() => router.back()} style={styles.backBtn}>
          <Ionicons name="chevron-back" size={24} color="#fff" />
        </TouchableOpacity>
      </SafeAreaView>
      <ScrollView style={styles.sheet} contentContainerStyle={styles.sheetContent} testID="country-screen">
        <Text style={styles.kicker}>EXPLORE</Text>
        <Text style={styles.title}>{country?.name}</Text>
        <Text style={styles.desc}>{country?.description}</Text>

        <Text style={styles.sectionTitle}>Cities ({cities.length})</Text>
        <View style={{ gap: 14 }}>
          {cities.map(c => (
            <TouchableOpacity
              key={c.city_id}
              testID={`city-card-${c.city_id}`}
              style={styles.cityCard}
              onPress={() => router.push(`/city/${c.city_id}`)}
            >
              <Image source={{ uri: c.image }} style={styles.cityImg} />
              <View style={{ flex: 1, padding: 14 }}>
                <Text style={styles.cityName}>{c.name}</Text>
                <Text style={styles.cityDesc} numberOfLines={2}>{c.description}</Text>
              </View>
            </TouchableOpacity>
          ))}
        </View>
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1, backgroundColor: colors.bg },
  loading: { flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bg },
  hero: { position: 'absolute', top: 0, left: 0, right: 0, height: 320 },
  headerOverlay: { position: 'absolute', top: 0, left: 0, right: 0, paddingHorizontal: spacing.md },
  backBtn: { width: 40, height: 40, borderRadius: 20, backgroundColor: 'rgba(0,0,0,0.4)', alignItems: 'center', justifyContent: 'center' },
  sheet: { flex: 1, marginTop: 260, backgroundColor: colors.bg, borderTopLeftRadius: 32, borderTopRightRadius: 32 },
  sheetContent: { padding: spacing.lg, paddingBottom: spacing.xxl },
  kicker: { color: colors.accent, fontSize: 11, letterSpacing: 3, fontWeight: '700' },
  title: { fontSize: 34, fontWeight: '700', color: colors.text, marginTop: 6, letterSpacing: -1 },
  desc: { color: colors.textMuted, fontSize: 15, lineHeight: 22, marginTop: 10 },
  sectionTitle: { fontSize: 18, fontWeight: '700', color: colors.text, marginTop: spacing.xl, marginBottom: spacing.md },
  cityCard: { flexDirection: 'row', backgroundColor: colors.card, borderRadius: radii.md, overflow: 'hidden', borderWidth: 1, borderColor: colors.border, alignItems: 'center' },
  cityImg: { width: 110, height: 110 },
  cityName: { fontSize: 18, fontWeight: '700', color: colors.text },
  cityDesc: { color: colors.textMuted, fontSize: 13, marginTop: 4, lineHeight: 18 },
});
