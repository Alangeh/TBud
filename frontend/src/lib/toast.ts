// Lightweight toast helper around react-native-toast-message.
// Single source of truth for success/error/info notifications across the app.
import Toast from 'react-native-toast-message';

export const showSuccess = (text1: string, text2?: string) =>
  Toast.show({
    type: 'success',
    text1,
    text2,
    position: 'top',
    topOffset: 60,
    visibilityTime: 2500,
    autoHide: true,
  });

export const showError = (text1: string, text2?: string) =>
  Toast.show({
    type: 'error',
    text1,
    text2,
    position: 'top',
    topOffset: 60,
    visibilityTime: 3500,
    autoHide: true,
  });

export const showInfo = (text1: string, text2?: string) =>
  Toast.show({
    type: 'info',
    text1,
    text2,
    position: 'top',
    topOffset: 60,
    visibilityTime: 2500,
    autoHide: true,
  });

export const hideToast = () => Toast.hide();
