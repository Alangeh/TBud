import { useState } from 'react';
import { View, Text, TextInput, TouchableOpacity, StyleSheet, KeyboardAvoidingView, Platform, ScrollView, Alert, ActivityIndicator } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useRouter } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '@/src/contexts/AuthContext';
import { colors, radii, spacing } from '@/src/constants/theme';
import GoogleButton from '@/src/components/GoogleButton';
import { showSuccess, showError } from '@/src/lib/toast';

export default function Signup() {
  const { register } = useAuth();
  const router = useRouter();
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    if (!name || !email || password.length < 6) { showError('Missing or invalid', 'Enter name, email and 6+ char password'); return; }
    setBusy(true);
    try {
      const u = await register(email.trim(), password, name.trim());
      showSuccess(`Welcome, ${u.name}!`, 'One more step — verify your identity.');
      router.replace('/kyc');
    } catch (e: any) {
      showError('Signup failed', e?.message ?? 'Try again');
    } finally { setBusy(false); }
  };

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <KeyboardAvoidingView style={{ flex: 1 }} behavior={Platform.OS === 'ios' ? 'padding' : 'height'}>
        <ScrollView contentContainerStyle={styles.scroll} keyboardShouldPersistTaps="handled">
          <TouchableOpacity testID="back-btn" onPress={() => router.back()} style={styles.back}>
            <Ionicons name="chevron-back" size={26} color={colors.text} />
          </TouchableOpacity>
          <Text style={styles.kicker}>JOIN THE TRIBE</Text>
          <Text style={styles.title}>Become a verified traveler.</Text>

          <View style={styles.field}>
            <Text style={styles.label}>Full name</Text>
            <TextInput testID="signup-name" style={styles.input} value={name} onChangeText={setName} placeholder="Mia Robertson" placeholderTextColor={colors.textFaint} />
          </View>
          <View style={styles.field}>
            <Text style={styles.label}>Email</Text>
            <TextInput testID="signup-email" style={styles.input} value={email} onChangeText={setEmail} autoCapitalize="none" keyboardType="email-address" placeholder="you@example.com" placeholderTextColor={colors.textFaint} />
          </View>
          <View style={styles.field}>
            <Text style={styles.label}>Password</Text>
            <TextInput testID="signup-password" style={styles.input} value={password} onChangeText={setPassword} secureTextEntry placeholder="At least 6 characters" placeholderTextColor={colors.textFaint} />
          </View>

          <TouchableOpacity testID="signup-submit" style={styles.primary} onPress={submit} disabled={busy}>
            {busy ? <ActivityIndicator color="#fff" /> : <Text style={styles.primaryText}>Create account</Text>}
          </TouchableOpacity>

          <View style={styles.divider}>
            <View style={styles.line} />
            <Text style={styles.dividerText}>OR</Text>
            <View style={styles.line} />
          </View>

          <GoogleButton onSuccess={() => router.replace('/kyc')} />

          <TouchableOpacity testID="goto-login" onPress={() => router.replace('/(auth)/login')} style={{ marginTop: spacing.lg, alignSelf: 'center' }}>
            <Text style={styles.linkText}>Already a member? <Text style={{ color: colors.accent, fontWeight: '700' }}>Sign in</Text></Text>
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
  input: { backgroundColor: colors.bgAlt, borderRadius: 14, paddingHorizontal: 16, paddingVertical: 16, fontSize: 16, color: colors.text },
  primary: { backgroundColor: colors.accent, paddingVertical: 16, borderRadius: radii.pill, alignItems: 'center', marginTop: spacing.md },
  primaryText: { color: '#fff', fontSize: 16, fontWeight: '700' },
  divider: { flexDirection: 'row', alignItems: 'center', marginVertical: spacing.lg, gap: 12 },
  line: { flex: 1, height: 1, backgroundColor: colors.border },
  dividerText: { color: colors.textFaint, fontSize: 12, fontWeight: '600' },
  linkText: { color: colors.textMuted, fontSize: 14 },
});
