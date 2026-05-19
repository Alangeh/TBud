import { useState } from 'react';
import { View, Text, TextInput, ScrollView, StyleSheet, TouchableOpacity, Image, ActivityIndicator } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useRouter } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { api } from '@/src/lib/api';
import { colors, radii, spacing } from '@/src/constants/theme';

type SearchRes = {
  countries: any[];
  cities: any[];
  places: any[];
};

export default function Search() {
  const router = useRouter();
  const [q, setQ] = useState('');
  const [res, setRes] = useState<SearchRes | null>(null);
  const [busy, setBusy] = useState(false);

  const run = async (text: string) => {
    setQ(text);
    if (text.trim().length < 1) { setRes(null); return; }
    setBusy(true);
    try {
      const r = await api<SearchRes>(`/search?q=${encodeURIComponent(text.trim())}`);
      setRes(r);
    } finally { setBusy(false); }
  };

  const empty = !res || (res.countries.length + res.cities.length + res.places.length === 0);

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <View style={styles.header}>
        <Text style={styles.title}>Search</Text>
      </View>
      <View style={styles.searchBar}>
        <Ionicons name="search" size={20} color={colors.textMuted} />
        <TextInput
          testID="search-input"
          value={q}
          onChangeText={run}
          placeholder="Try 'Tokyo' or 'Eiffel'"
          placeholderTextColor={colors.textFaint}
          style={styles.searchInput}
          autoCapitalize="none"
        />
        {busy && <ActivityIndicator color={colors.accent} />}
      </View>

      <ScrollView contentContainerStyle={styles.scroll} keyboardShouldPersistTaps="handled">
        {q.length === 0 && (
          <View style={styles.placeholder}>
            <Ionicons name="map" size={48} color={colors.border} />
            <Text style={styles.placeholderText}>Start typing to discover countries, cities and places.</Text>
          </View>
        )}
        {q.length > 0 && empty && !busy && (
          <View style={styles.placeholder}>
            <Text style={styles.placeholderText}>No results for "{q}".</Text>
          </View>
        )}

        {res?.countries.map((c) => (
          <TouchableOpacity key={c.country_id} testID={`search-country-${c.country_id}`} style={styles.row} onPress={() => router.push(`/country/${c.country_id}`)}>
            <Image source={{ uri: c.image }} style={styles.img} />
            <View style={{ flex: 1 }}>
              <Text style={styles.kind}>COUNTRY</Text>
              <Text style={styles.rowTitle}>{c.name}</Text>
            </View>
            <Ionicons name="chevron-forward" size={18} color={colors.textFaint} />
          </TouchableOpacity>
        ))}
        {res?.cities.map((c) => (
          <TouchableOpacity key={c.city_id} testID={`search-city-${c.city_id}`} style={styles.row} onPress={() => router.push(`/city/${c.city_id}`)}>
            <Image source={{ uri: c.image }} style={styles.img} />
            <View style={{ flex: 1 }}>
              <Text style={styles.kind}>CITY</Text>
              <Text style={styles.rowTitle}>{c.name}</Text>
            </View>
            <Ionicons name="chevron-forward" size={18} color={colors.textFaint} />
          </TouchableOpacity>
        ))}
        {res?.places.map((p) => (
          <TouchableOpacity key={p.place_id} testID={`search-place-${p.place_id}`} style={styles.row} onPress={() => router.push(`/place/${p.place_id}`)}>
            <Image source={{ uri: p.photos?.[0] }} style={styles.img} />
            <View style={{ flex: 1 }}>
              <Text style={styles.kind}>{(p.category ?? 'PLACE').toUpperCase()}</Text>
              <Text style={styles.rowTitle}>{p.name}</Text>
            </View>
            <Ionicons name="chevron-forward" size={18} color={colors.textFaint} />
          </TouchableOpacity>
        ))}
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.bg },
  header: { paddingHorizontal: spacing.lg, paddingTop: spacing.md },
  title: { fontSize: 28, fontWeight: '700', color: colors.text, letterSpacing: -0.5 },
  searchBar: { flexDirection: 'row', alignItems: 'center', marginHorizontal: spacing.lg, marginTop: spacing.md, backgroundColor: colors.bgAlt, paddingHorizontal: 14, paddingVertical: 12, borderRadius: 14, gap: 8 },
  searchInput: { flex: 1, fontSize: 15, color: colors.text },
  scroll: { padding: spacing.lg, gap: 10 },
  placeholder: { alignItems: 'center', paddingVertical: spacing.xxl, gap: 12 },
  placeholderText: { color: colors.textMuted, fontSize: 14, textAlign: 'center', paddingHorizontal: spacing.lg },
  row: { flexDirection: 'row', alignItems: 'center', backgroundColor: colors.card, padding: 12, borderRadius: 14, gap: 12, borderWidth: 1, borderColor: colors.border },
  img: { width: 56, height: 56, borderRadius: 10, backgroundColor: colors.bgAlt },
  kind: { color: colors.accent, fontSize: 10, fontWeight: '700', letterSpacing: 1.5, marginBottom: 2 },
  rowTitle: { fontSize: 15, fontWeight: '700', color: colors.text },
});
