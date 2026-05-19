import { View, Text, ImageBackground, TouchableOpacity, StyleSheet, Platform } from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import { useRouter } from 'expo-router';
import { SafeAreaView } from 'react-native-safe-area-context';
import { Ionicons } from '@expo/vector-icons';
import { colors, spacing, radii } from '@/src/constants/theme';

const HERO = 'https://images.unsplash.com/photo-1527631615371-98cbbff5125a?w=1200';

export default function Welcome() {
  const router = useRouter();
  return (
    <View style={styles.root} testID="welcome-screen">
      <ImageBackground source={{ uri: HERO }} style={StyleSheet.absoluteFill} resizeMode="cover" />
      <LinearGradient
        colors={['transparent', 'rgba(0,0,0,0.4)', 'rgba(0,0,0,0.85)']}
        style={StyleSheet.absoluteFill}
      />
      <SafeAreaView style={styles.safe} edges={['top', 'bottom']}>
        <View style={styles.top}>
          <View style={styles.brand}>
            <Ionicons name="compass" size={22} color="#fff" />
            <Text style={styles.brandText}>TravelReview</Text>
          </View>
          <View style={styles.verifiedPill}>
            <Ionicons name="shield-checkmark" size={12} color={colors.trust} />
            <Text style={styles.verifiedPillText}>Verified Reviews</Text>
          </View>
        </View>
        <View style={styles.bottom}>
          <Text style={styles.kicker}>TRUST THE JOURNEY</Text>
          <Text style={styles.title}>Real reviews{'\n'}from real travelers.</Text>
          <Text style={styles.subtitle}>Discover countries, cities, and unforgettable places — vetted by KYC-verified explorers.</Text>
          <TouchableOpacity
            testID="welcome-signup-btn"
            style={styles.primary}
            onPress={() => router.push('/(auth)/signup')}
          >
            <Text style={styles.primaryText}>Create your account</Text>
          </TouchableOpacity>
          <TouchableOpacity
            testID="welcome-login-btn"
            style={styles.secondary}
            onPress={() => router.push('/(auth)/login')}
          >
            <Text style={styles.secondaryText}>I already have an account</Text>
          </TouchableOpacity>
        </View>
      </SafeAreaView>
    </View>
  );
}

const styles = StyleSheet.create({
  root: { flex: 1, backgroundColor: '#000' },
  safe: { flex: 1, paddingHorizontal: spacing.lg, justifyContent: 'space-between' },
  top: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', paddingTop: spacing.md },
  brand: { flexDirection: 'row', alignItems: 'center', gap: 8 },
  brandText: { color: '#fff', fontSize: 16, fontWeight: '700', letterSpacing: 0.5 },
  verifiedPill: { flexDirection: 'row', alignItems: 'center', gap: 4, backgroundColor: '#fff', paddingHorizontal: 10, paddingVertical: 6, borderRadius: radii.pill },
  verifiedPillText: { color: colors.trust, fontSize: 11, fontWeight: '700' },
  bottom: { paddingBottom: spacing.lg, gap: spacing.md },
  kicker: { color: colors.accent, fontSize: 11, letterSpacing: 3, fontWeight: '700' },
  title: { color: '#fff', fontSize: 38, fontWeight: '700', lineHeight: 44, letterSpacing: -1 },
  subtitle: { color: 'rgba(255,255,255,0.8)', fontSize: 15, lineHeight: 22, marginBottom: spacing.md },
  primary: { backgroundColor: colors.accent, paddingVertical: 16, borderRadius: radii.pill, alignItems: 'center' },
  primaryText: { color: '#fff', fontSize: 16, fontWeight: '700' },
  secondary: { backgroundColor: 'rgba(255,255,255,0.12)', paddingVertical: 16, borderRadius: radii.pill, alignItems: 'center', borderWidth: 1, borderColor: 'rgba(255,255,255,0.3)' },
  secondaryText: { color: '#fff', fontSize: 15, fontWeight: '600' },
});
