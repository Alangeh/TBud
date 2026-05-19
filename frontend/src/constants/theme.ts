// Design tokens for TravelReview - warm, organic travel aesthetic.
export const colors = {
  bg: '#FAF9F6',
  bgAlt: '#F3F1ED',
  card: '#FFFFFF',
  overlay: 'rgba(0,0,0,0.4)',
  text: '#1A1A1A',
  textMuted: '#5C5C5C',
  textFaint: '#8A8A8A',
  inverse: '#FFFFFF',
  accent: '#E07A5F',
  accentDark: '#D36649',
  trust: '#3D5A80',
  trustBg: '#EAF0F6',
  success: '#81B29A',
  border: '#E5E3DB',
  star: '#F4A259',
  danger: '#C84B31',
};

export const radii = {
  sm: 8,
  md: 16,
  lg: 24,
  pill: 999,
};

export const spacing = {
  xs: 4,
  sm: 8,
  md: 16,
  lg: 24,
  xl: 32,
  xxl: 48,
};

export const shadow = {
  soft: {
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 8 },
    shadowOpacity: 0.04,
    shadowRadius: 30,
    elevation: 2,
  },
  floating: {
    shadowColor: '#000',
    shadowOffset: { width: 0, height: 20 },
    shadowOpacity: 0.08,
    shadowRadius: 40,
    elevation: 6,
  },
};

export const fonts = {
  heading: 'Outfit_700Bold',
  headingMed: 'Outfit_600SemiBold',
  body: 'Manrope_400Regular',
  bodyMed: 'Manrope_500Medium',
  bodyBold: 'Manrope_700Bold',
};
