import { definePreset } from '@primeuix/themes';
import Aura from '@primeuix/themes/aura';

/**
 * BOON-IT brand theme (PrimeNG Aura preset).
 *
 * The teal ramp is anchored on the exact logo colours:
 *   - primary.700 (#276D64) is the dark teal of the "BOON-IT" wordmark → used for
 *     buttons, active states and links so the primary colour IS the brand colour.
 *   - #00EBB5 (the bright mint of the chain-link) is exposed as `--brand-accent`
 *     in styles.scss and used as a highlight (active nav bar, focus pops).
 */
export const BRAND = {
  wordmark: '#276D64',
  accent: '#00EBB5',
};

export const BoonItPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '#F3F9F8',
      100: '#E6F4F2',
      200: '#C9E9E5',
      300: '#98D7CF',
      400: '#5CC4B6',
      500: '#38A899',
      600: '#2D887C',
      700: '#276D64',
      800: '#1F5B53',
      900: '#1A4741',
      950: '#112F2B',
    },
    colorScheme: {
      light: {
        primary: {
          color: '{primary.700}',
          contrastColor: '#ffffff',
          hoverColor: '{primary.800}',
          activeColor: '{primary.900}',
        },
        highlight: {
          background: '{primary.50}',
          focusBackground: '{primary.100}',
          color: '{primary.800}',
          focusColor: '{primary.900}',
        },
      },
      dark: {
        primary: {
          color: '{primary.400}',
          contrastColor: '{primary.950}',
          hoverColor: '{primary.300}',
          activeColor: '{primary.200}',
        },
        highlight: {
          background: 'color-mix(in srgb, {primary.400}, transparent 84%)',
          focusBackground: 'color-mix(in srgb, {primary.400}, transparent 76%)',
          color: 'rgba(255,255,255,.87)',
          focusColor: 'rgba(255,255,255,.87)',
        },
      },
    },
  },
});
