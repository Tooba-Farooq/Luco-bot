import { Platform } from 'react-native';

export const COLORS = {
    primary: '#007BFF',
    secondary: '#6C757D',
    background: Platform.OS === 'ios' ? '#F8F9FA' : '#E9ECEF',
    text: '#212529',
};

export const FONT_SIZES = {
    small: 12,
    medium: 16,
    large: 20,
};

export const SPACING = {
    small: 8,
    medium: 16,
    large: 24,
};