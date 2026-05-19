import { useEffect, useState, useCallback } from 'react';
import { View, Text, ScrollView, TouchableOpacity, StyleSheet, Image, ActivityIndicator, RefreshControl } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useRouter } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { LinearGradient } from 'expo-linear-gradient';
import { api } from '@/src/lib/api';
import { useAuth } from '@/src/contexts/AuthContext';
import { colors, radii, spacing, shadow } from '@/src/constants/theme';

type Country = { country_id: string; name: string; description: string; image: string; code: string };
type City = { city_id: string; country_id: string; name: string; image: string; description: string };

export default function Explore() {
  const router = useRouter();
  const { user } = useAuth();
  const [countries, setCountries] = useState<Country[]>([]);
  const [trending, setTrending] = useState<City[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async () => {
    try {
      const c = await api<{ countries: Country[] }>('/countries');
      setCountries(c.countries);
      // First city of each country = trending sample
      const trendingCities: City[] = [];
      for (const co of c.countries.slice(0, 5)) {
        const r = await api<{ cities: City[] }>(`/countries/${co.country_id}/cities`);
        if (r.cities[0]) trendingCities.push(r.cities[0]);
      }
      setTrending(trendingCities);
    } finally { setLoading(false); }
  }, []);

  useEffect(() => { load(); }, [load]);
  const onRefresh = async () => { setRefreshing(true); await load(); setRefreshing(false); };

  if (loading) return <SafeAreaView style={styles.loading}><ActivityIndicator color={colors.accent} size="large" /></SafeAreaView>;

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <ScrollView
        contentContainerStyle={styles.scroll}
        showsVerticalScrollIndicator={false}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} tintColor={colors.accent} />}
        testID="explore-screen"
      >
        <View style={styles.header}>
          <View>
            <Text style={styles.greeting}>Hello, {user?.name?.split(' ')[0] ?? 'Explorer'}</Text>
            <Text style={styles.headerSub}>Where to next?</Text>
          </View>
          <TouchableOpacity testID="header-profile" onPress={() => router.push('/(tabs)/profile')} style={styles.avatar}>
            {user?.picture ? <Image source={{ uri: user.picture }} style={styles.avatarImg} /> : <Ionicons name="person" size={20} color={colors.text} />}
          </TouchableOpacity>
        </View>

        <Text style={styles.sectionTitle}>Featured destinations</Text>
        <ScrollView horizontal showsHorizontalScrollIndicator={false} contentContainerStyle={styles.hScroll}>
          {countries.map(c => (
            <TouchableOpacity
              key={c.country_id}
              testID={`country-card-${c.country_id}`}
              style={styles.featureCard}
              onPress={() => router.push(`/country/${c.country_id}`)}
            >
              <Image source={{ uri: c.image }} style={StyleSheet.absoluteFillObject} />
              <LinearGradient colors={['transparent', 'rgba(0,0,0,0.7)']} style={StyleSheet.absoluteFillObject} />
              <View style={styles.featureContent}>
                <Text style={styles.featureCode}>{c.code}</Text>
                <Text style={styles.featureName}>{c.name}</Text>
                <Text style={styles.featureDesc} numberOfLines={2}>{c.description}</Text>
              </View>
            </TouchableOpacity>
          ))}
        </ScrollView>

        <Text style={styles.sectionTitle}>Trending cities</Text>
        <View style={{ gap: 12 }}>
          {trending.map(ct => (
            <TouchableOpacity
              key={ct.city_id}
              testID={`trending-${ct.city_id}`}
              style={styles.cityRow}
              onPress={() => router.push(`/city/${ct.city_id}`)}
            >
              <Image source={{ uri: ct.image }} style={styles.cityImg} />
              <View style={{ flex: 1 }}>
                <Text style={styles.cityName}>{ct.name}</Text>
                <Text style={styles.cityDesc} numberOfLines={1}>{ct.description}</Text>
              </View>
              <Ionicons name="chevron-forward" size={20} color={colors.textFaint} />
            </TouchableOpacity>
          ))}
        </View>

        <View style={{ height: 32 }} />
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.bg },
  loading: { flex: 1, backgroundColor: colors.bg, alignItems: 'center', justifyContent: 'center' },
  scroll: { paddingHorizontal: spacing.lg, paddingBottom: spacing.xl },
  header: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', paddingTop: spacing.md, marginBottom: spacing.xl },
  greeting: { fontSize: 24, fontWeight: '700', color: colors.text, letterSpacing: -0.5 },
  headerSub: { color: colors.textMuted, fontSize: 14, marginTop: 2 },
  avatar: { width: 44, height: 44, borderRadius: 22, backgroundColor: colors.bgAlt, alignItems: 'center', justifyContent: 'center', overflow: 'hidden' },
  avatarImg: { width: '100%', height: '100%' },
  sectionTitle: { fontSize: 18, fontWeight: '700', color: colors.text, marginBottom: spacing.md, marginTop: spacing.sm, letterSpacing: -0.3 },
  hScroll: { gap: 14, paddingRight: spacing.md, marginBottom: spacing.lg },
  featureCard: { width: 240, height: 320, borderRadius: radii.lg, overflow: 'hidden', backgroundColor: colors.bgAlt, ...shadow.soft },
  featureContent: { position: 'absolute', bottom: 0, padding: spacing.md, gap: 4 },
  featureCode: { color: colors.accent, fontSize: 11, fontWeight: '700', letterSpacing: 2 },
  featureName: { color: '#fff', fontSize: 26, fontWeight: '700', letterSpacing: -0.5 },
  featureDesc: { color: 'rgba(255,255,255,0.85)', fontSize: 13, lineHeight: 18 },
  cityRow: { flexDirection: 'row', alignItems: 'center', gap: 14, backgroundColor: colors.card, padding: 12, borderRadius: radii.md, borderWidth: 1, borderColor: colors.border },
  cityImg: { width: 64, height: 64, borderRadius: 12 },
  cityName: { fontSize: 16, fontWeight: '700', color: colors.text },
  cityDesc: { color: colors.textMuted, fontSize: 13, marginTop: 2 },
});
