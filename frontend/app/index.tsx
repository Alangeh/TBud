import { useEffect } from 'react';
import { View, ActivityIndicator, StyleSheet } from 'react-native';
import { useRouter } from 'expo-router';
import { useAuth } from '@/src/contexts/AuthContext';
import { colors } from '@/src/constants/theme';

export default function Index() {
  const { user, loading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (loading) return;
    if (user) router.replace('/(tabs)/explore');
    else router.replace('/(auth)/welcome');
  }, [user, loading, router]);

  return (
    <View style={styles.c} testID="splash-screen">
      <ActivityIndicator size="large" color={colors.accent} />
    </View>
  );
}
const styles = StyleSheet.create({
  c: { flex: 1, backgroundColor: colors.bg, alignItems: 'center', justifyContent: 'center' },
});
