import { useState } from 'react';
import { View, Text, TextInput, TouchableOpacity, StyleSheet, KeyboardAvoidingView, Platform, ScrollView, Alert, ActivityIndicator } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useRouter } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '@/src/contexts/AuthContext';
import { colors, radii, spacing } from '@/src/constants/theme';
import GoogleButton from '@/src/components/GoogleButton';

export default function Login() {
  const { login } = useAuth();
  const router = useRouter();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    if (!email || !password) { Alert.alert('Missing', 'Enter email and password'); return; }
    setBusy(true);
    try {
      await login(email.trim(), password);
      router.replace('/(tabs)/explore');
    } catch (e: any) {
      Alert.alert('Login failed', e?.message ?? 'Try again');
    } finally { setBusy(false); }
  };

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : 'height'}>
        <ScrollView contentContainerStyle={styles.scroll} keyboardShouldPersistTaps="handled">
          <TouchableOpacity testID="back-btn" onPress={() => router.back()} style={styles.back}>
            <Ionicons name="chevron-back" size={26} color={colors.text} />
          </TouchableOpacity>
          <Text style={styles.kicker}>WELCOME BACK</Text>
          <Text style={styles.title}>Sign in to continue your journey.</Text>

          <View style={styles.field}>
            <Text style={styles.label}>Email</Text>
            <TextInput
              testID="login-email"
              style={styles.input}
              value={email}
              onChangeText={setEmail}
              autoCapitalize="none"
              keyboardType="email-address"
              placeholder="you@example.com"
              placeholderTextColor={colors.textFaint}
            />
          </View>
          <View style={styles.field}>
            <Text style={styles.label}>Password</Text>
            <TextInput
              testID="login-password"
              style={styles.input}
              value={password}
              onChangeText={setPassword}
              secureTextEntry
              placeholder="••••••••"
              placeholderTextColor={colors.textFaint}
            />
          </View>

          <TouchableOpacity testID="login-submit" style={styles.primary} onPress={submit} disabled={busy}>
            {busy ? <ActivityIndicator color="#fff" /> : <Text style={styles.primaryText}>Sign in</Text>}
          </TouchableOpacity>

          <View style={styles.divider}>
            <View style={styles.line} />
            <Text style={styles.dividerText}>OR</Text>
            <View style={styles.line} />
          </View>

          <GoogleButton onSuccess={() => router.replace('/(tabs)/explore')} />

          <TouchableOpacity testID="goto-signup" onPress={() => router.replace('/(auth)/signup')} style={{ marginTop: spacing.lg, alignSelf: 'center' }}>
            <Text style={styles.linkText}>New here? <Text style={{ color: colors.accent, fontWeight: '700' }}>Create account</Text></Text>
          </TouchableOpacity>
        </ScrollView>
      </KeyboardAvoidingView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.bg },
  scroll: { padding: spacing.lg, paddingTop: spacing.sm },
  back: { width: 40, height: 40, justifyContent: 'center', marginBottom: spacing.md },
  kicker: { color: colors.accent, fontSize: 11, letterSpacing: 3, fontWeight: '700' },
  title: { fontSize: 30, fontWeight: '700', color: colors.text, letterSpacing: -0.5, marginTop: 8, marginBottom: spacing.xl, lineHeight: 36 },
  field: { marginBottom: spacing.md },
  label: { color: colors.textMuted, fontSize: 13, fontWeight: '600', marginBottom: 6 },
  input: { backgroundColor: colors.bgAlt, borderRadius: 14, paddingHorizontal: 16, paddingVertical: 16, fontSize: 16, color: colors.text, borderWidth: 1, borderColor: 'transparent' },
  primary: { backgroundColor: colors.accent, paddingVertical: 16, borderRadius: radii.pill, alignItems: 'center', marginTop: spacing.md },
  primaryText: { color: '#fff', fontSize: 16, fontWeight: '700' },
  divider: { flexDirection: 'row', alignItems: 'center', marginVertical: spacing.lg, gap: 12 },
  line: { flex: 1, height: 1, backgroundColor: colors.border },
  dividerText: { color: colors.textFaint, fontSize: 12, fontWeight: '600' },
  linkText: { color: colors.textMuted, fontSize: 14 },
});
