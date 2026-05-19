import { Platform, TouchableOpacity, Text, StyleSheet, Alert, ActivityIndicator } from 'react-native';
import { useState } from 'react';
import * as WebBrowser from 'expo-web-browser';
import * as Linking from 'expo-linking';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '@/src/contexts/AuthContext';
import { colors, radii } from '@/src/constants/theme';

type Props = { onSuccess: () => void };

export default function GoogleButton({ onSuccess }: Props) {
  const { googleSession } = useAuth();
  const [busy, setBusy] = useState(false);

  const processSessionId = async (sessionId: string) => {
    // Get session_token from Emergent
    const r = await fetch('https://demobackend.emergentagent.com/auth/v1/env/oauth/session-data', {
      headers: { 'X-Session-ID': sessionId },
    });
    if (!r.ok) throw new Error('Google verification failed');
    const data = await r.json();
    await googleSession(data.session_token);
  };

  const start = async () => {
    setBusy(true);
    try {
      const redirectUrl =
        Platform.OS === 'web'
          ? `${window.location.origin}/`
          : Linking.createURL('auth');
      const authUrl = `https://auth.emergentagent.com/?redirect=${encodeURIComponent(redirectUrl)}`;
      if (Platform.OS === 'web') {
        window.location.href = authUrl;
        return;
      }
      const result = await WebBrowser.openAuthSessionAsync(authUrl, redirectUrl);
      if (result.type !== 'success' || !result.url) {
        setBusy(false); return;
      }
      // Extract session_id from hash or query
      const url = result.url;
      let sessionId: string | null = null;
      const hashMatch = url.match(/#session_id=([^&]+)/);
      const queryMatch = url.match(/[?&]session_id=([^&]+)/);
      sessionId = (hashMatch?.[1] || queryMatch?.[1]) ?? null;
      if (!sessionId) throw new Error('Missing session_id');
      await processSessionId(decodeURIComponent(sessionId));
      onSuccess();
    } catch (e: any) {
      Alert.alert('Google sign-in failed', e?.message ?? 'Try again');
    } finally { setBusy(false); }
  };

  return (
    <TouchableOpacity testID="google-signin" style={styles.btn} onPress={start} disabled={busy}>
      {busy ? <ActivityIndicator color={colors.text} /> : (
        <>
          <Ionicons name="logo-google" size={20} color={colors.text} />
          <Text style={styles.txt}>Continue with Google</Text>
        </>
      )}
    </TouchableOpacity>
  );
}

const styles = StyleSheet.create({
  btn: { backgroundColor: '#fff', borderWidth: 1, borderColor: colors.border, paddingVertical: 14, borderRadius: radii.pill, flexDirection: 'row', alignItems: 'center', justifyContent: 'center', gap: 10 },
  txt: { color: colors.text, fontSize: 15, fontWeight: '600' },
});
