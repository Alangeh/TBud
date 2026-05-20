import { useEffect, useState, useCallback } from 'react';
import { View, Text, ScrollView, StyleSheet, TouchableOpacity, Image, ActivityIndicator, Alert, RefreshControl } from 'react-native';
import { SafeAreaView } from 'react-native-safe-area-context';
import { useRouter, useFocusEffect } from 'expo-router';
import { Ionicons } from '@expo/vector-icons';
import { useAuth } from '@/src/contexts/AuthContext';
import { api } from '@/src/lib/api';
import { colors, radii, spacing } from '@/src/constants/theme';
import { showSuccess, showError } from '@/src/lib/toast';

type Review = { review_id: string; place_id: string; place_name?: string; place_image?: string; rating: number; text: string; created_at: string };

export default function Profile() {
  const router = useRouter();
  const { user, token, logout } = useAuth();
  const [reviews, setReviews] = useState<Review[]>([]);
  const [loading, setLoading] = useState(true);
  const [refreshing, setRefreshing] = useState(false);

  const load = useCallback(async () => {
    if (!token) return;
    try {
      const r = await api<{ reviews: Review[] }>('/users/me/reviews', { token });
      setReviews(r.reviews);
    } finally { setLoading(false); }
  }, [token]);

  useFocusEffect(useCallback(() => { load(); }, [load]));
  const onRefresh = async () => { setRefreshing(true); await load(); setRefreshing(false); };

  const onLogout = () => {
    Alert.alert('Sign out', 'Are you sure?', [
      { text: 'Cancel', style: 'cancel' },
      { text: 'Sign out', style: 'destructive', onPress: async () => { await logout(); showSuccess('Signed out'); router.replace('/(auth)/welcome'); } }
    ]);
  };

  const editReview = (r: Review) => {
    router.push({ pathname: `/write-review/${r.place_id}`, params: { reviewId: r.review_id } });
  };

  const deleteReview = (r: Review) => {
    Alert.alert(
      'Delete review?',
      'This cannot be undone.',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Delete',
          style: 'destructive',
          onPress: async () => {
            try {
              await api(`/reviews/${r.review_id}`, { token, method: 'DELETE' });
              showSuccess('Review deleted');
              setReviews(rs => rs.filter(x => x.review_id !== r.review_id));
              load();
            } catch (e: any) {
              showError('Delete failed', e?.message);
            }
          },
        },
      ],
    );
  };

  return (
    <SafeAreaView style={styles.safe} edges={['top']}>
      <ScrollView
        contentContainerStyle={styles.scroll}
        refreshControl={<RefreshControl refreshing={refreshing} onRefresh={onRefresh} />}
        testID="profile-screen"
      >
        <View style={styles.headerRow}>
          <Text style={styles.heading}>Profile</Text>
          <TouchableOpacity testID="logout-btn" onPress={onLogout} style={styles.iconBtn}>
            <Ionicons name="log-out-outline" size={22} color={colors.text} />
          </TouchableOpacity>
        </View>

        <View style={styles.avatarWrap}>
          {user?.picture ? (
            <Image source={{ uri: user.picture }} style={styles.avatar} />
          ) : (
            <View style={[styles.avatar, { backgroundColor: colors.bgAlt, alignItems: 'center', justifyContent: 'center' }]}>
              <Ionicons name="person" size={48} color={colors.textMuted} />
            </View>
          )}
        </View>
        <Text style={styles.name}>{user?.name}</Text>
        {user?.verified ? (
          <View style={styles.verifiedBadge}>
            <Ionicons name="shield-checkmark" size={14} color={colors.trust} />
            <Text style={styles.verifiedText}>Verified Traveler</Text>
          </View>
        ) : (
          <TouchableOpacity testID="kyc-launch" onPress={() => router.push('/kyc')} style={styles.kycCta}>
            <Ionicons name="shield-checkmark" size={14} color={colors.accent} />
            <Text style={styles.kycCtaText}>Get verified</Text>
          </TouchableOpacity>
        )}

        <View style={styles.stats}>
          <View style={styles.statItem}>
            <Text style={styles.statNum}>{user?.review_count ?? 0}</Text>
            <Text style={styles.statLabel}>Reviews</Text>
          </View>
          <View style={styles.statDivider} />
          <View style={styles.statItem}>
            <Text style={styles.statNum}>{user?.countries_visited?.length ?? 0}</Text>
            <Text style={styles.statLabel}>Countries</Text>
          </View>
          <View style={styles.statDivider} />
          <View style={styles.statItem}>
            <Text style={styles.statNum}>{user?.follower_count ?? 0}</Text>
            <Text style={styles.statLabel}>Followers</Text>
          </View>
        </View>

        <Text style={styles.sectionTitle}>Your reviews</Text>
        {loading ? (
          <ActivityIndicator color={colors.accent} style={{ marginTop: 20 }} />
        ) : reviews.length === 0 ? (
          <View style={styles.empty}>
            <Ionicons name="document-text-outline" size={36} color={colors.border} />
            <Text style={styles.emptyText}>No reviews yet. Discover a place and share your experience!</Text>
            <TouchableOpacity testID="empty-go-explore" style={styles.cta} onPress={() => router.push('/(tabs)/explore')}>
              <Text style={styles.ctaText}>Start exploring</Text>
            </TouchableOpacity>
          </View>
        ) : (
          <View style={{ gap: 12 }}>
            {reviews.map(r => (
              <View key={r.review_id} style={styles.reviewCard} testID={`my-review-${r.review_id}`}>
                <TouchableOpacity style={{ flexDirection: 'row', gap: 12, flex: 1 }} onPress={() => router.push(`/place/${r.place_id}`)}>
                  {r.place_image && <Image source={{ uri: r.place_image }} style={styles.reviewImg} />}
                  <View style={{ flex: 1 }}>
                    <Text style={styles.reviewPlace}>{r.place_name}</Text>
                    <View style={{ flexDirection: 'row', gap: 2, marginVertical: 4 }}>
                      {[1, 2, 3, 4, 5].map(i => (
                        <Ionicons key={i} name={i <= r.rating ? 'star' : 'star-outline'} size={12} color={colors.star} />
                      ))}
                    </View>
                    <Text numberOfLines={2} style={styles.reviewText}>{r.text}</Text>
                  </View>
                </TouchableOpacity>
                <View style={styles.reviewActions}>
                  <TouchableOpacity testID={`edit-my-${r.review_id}`} onPress={() => editReview(r)} hitSlop={8} style={styles.actionBtn}>
                    <Ionicons name="pencil-outline" size={16} color={colors.textMuted} />
                  </TouchableOpacity>
                  <TouchableOpacity testID={`delete-my-${r.review_id}`} onPress={() => deleteReview(r)} hitSlop={8} style={styles.actionBtn}>
                    <Ionicons name="trash-outline" size={16} color={colors.danger} />
                  </TouchableOpacity>
                </View>
              </View>
            ))}
          </View>
        )}
      </ScrollView>
    </SafeAreaView>
  );
}

const styles = StyleSheet.create({
  safe: { flex: 1, backgroundColor: colors.bg },
  scroll: { padding: spacing.lg, paddingBottom: spacing.xxl },
  headerRow: { flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' },
  heading: { fontSize: 28, fontWeight: '700', color: colors.text, letterSpacing: -0.5 },
  iconBtn: { width: 40, height: 40, alignItems: 'center', justifyContent: 'center', borderRadius: 20, backgroundColor: colors.bgAlt },
  avatarWrap: { alignItems: 'center', marginTop: spacing.lg },
  avatar: { width: 100, height: 100, borderRadius: 50 },
  name: { fontSize: 22, fontWeight: '700', color: colors.text, textAlign: 'center', marginTop: 12 },
  verifiedBadge: { alignSelf: 'center', flexDirection: 'row', alignItems: 'center', gap: 4, backgroundColor: colors.trustBg, paddingHorizontal: 12, paddingVertical: 6, borderRadius: radii.pill, marginTop: 8 },
  verifiedText: { color: colors.trust, fontSize: 12, fontWeight: '700' },
  kycCta: { alignSelf: 'center', flexDirection: 'row', alignItems: 'center', gap: 6, backgroundColor: '#FFF6F2', paddingHorizontal: 14, paddingVertical: 8, borderRadius: radii.pill, marginTop: 8, borderWidth: 1, borderColor: colors.accent },
  kycCtaText: { color: colors.accent, fontSize: 12, fontWeight: '700' },
  stats: { flexDirection: 'row', backgroundColor: colors.card, padding: spacing.md, borderRadius: radii.lg, marginTop: spacing.lg, borderWidth: 1, borderColor: colors.border },
  statItem: { flex: 1, alignItems: 'center' },
  statNum: { fontSize: 22, fontWeight: '700', color: colors.text },
  statLabel: { color: colors.textMuted, fontSize: 12, marginTop: 2 },
  statDivider: { width: 1, backgroundColor: colors.border },
  sectionTitle: { fontSize: 18, fontWeight: '700', color: colors.text, marginTop: spacing.xl, marginBottom: spacing.md },
  empty: { alignItems: 'center', paddingVertical: spacing.lg, gap: 10 },
  emptyText: { color: colors.textMuted, fontSize: 14, textAlign: 'center', paddingHorizontal: spacing.lg },
  cta: { backgroundColor: colors.accent, paddingHorizontal: 24, paddingVertical: 12, borderRadius: radii.pill, marginTop: 4 },
  ctaText: { color: '#fff', fontWeight: '700' },
  reviewCard: { flexDirection: 'row', gap: 12, backgroundColor: colors.card, padding: 12, borderRadius: 14, borderWidth: 1, borderColor: colors.border, alignItems: 'center' },
  reviewActions: { flexDirection: 'column', gap: 8, alignItems: 'center' },
  actionBtn: { width: 32, height: 32, borderRadius: 16, alignItems: 'center', justifyContent: 'center', backgroundColor: colors.bgAlt },
  reviewImg: { width: 64, height: 64, borderRadius: 10 },
  reviewPlace: { fontSize: 15, fontWeight: '700', color: colors.text },
  reviewText: { color: colors.textMuted, fontSize: 13, lineHeight: 18 },
});
