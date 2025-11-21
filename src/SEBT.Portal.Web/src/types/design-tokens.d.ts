// design-tokens.d.ts
// Auto-generated TypeScript definitions for design tokens
// Source: design/states/dc.json
// DO NOT EDIT DIRECTLY - Regenerated from design tokens on build

/**
 * Design tokens for DC state
 * Auto-generated from design/states/dc.json
 * DO NOT EDIT DIRECTLY - Regenerated from design tokens
 */
export interface DCTheme {
  /** Token: theme-button-border-radius */
  buttonBorderRadius: string;

  /** Family tokens designate the overall color set and do not map to explicit values. Therefore, they are blank. */
  colorPrimaryFamily: string;

  /** Token: theme-focus-color */
  focusColor: string;

  /** Token: theme-font-role-alt */
  fontRoleAlt: string;

  /** Token: theme-font-role-heading */
  fontRoleHeading: string;

  /** In your HTML files, add a reference to the JavaScript and/or CSS files provided by the font hosting service. https://fonts.google.com/specimen/Urbanist

In your settings configuration, tell $theme-typeface-tokens to create a new typeface token. In the code example, we are creating a new typeface token:

 $theme-typeface-tokens: (
   "Urbanist": (
     "display-name": "Urbanist",
     "cap-height": 364px
   ),
 ),

Source: https://designsystem.digital.gov/design-tokens/typesetting/font-family/#adding-fonts-to-uswds-2 */
  fontTypeSans: string;

  /** In your HTML files, add a reference to the JavaScript and/or CSS files provided by the font hosting service. https://fonts.google.com/specimen/Urbanist

In your settings configuration, tell $theme-typeface-tokens to create a new typeface token. In the code example, we are creating a new typeface token:

 $theme-typeface-tokens: (
   "Urbanist": (
     "display-name": "Urbanist",
     "cap-height": 364px
   ),
 ),

Source: https://designsystem.digital.gov/design-tokens/typesetting/font-family/#adding-fonts-to-uswds-2 */
  fontTypeSerif: string;

  /** Token: theme-global-content-styles */
  globalContentStyles: boolean;

  /** Token: theme-global-link-styles */
  globalLinkStyles: boolean;

  /** Token: theme-global-paragraph-styles */
  globalParagraphStyles: boolean;

  /** Token: theme-link-color */
  linkColor: string;

  /** Token: theme-primary */
  primary: string;

  /** Token: theme-primary-dark */
  primaryDark: string;

  /** Token: theme-primary-darker */
  primaryDarker: string;

  /** Token: theme-primary-light */
  primaryLight: string;

  /** Token: theme-primary-lighter */
  primaryLighter: string;

  /** Token: theme-primary-lightest */
  primaryLightest: string;

  /** Token: theme-primary-vivid */
  primaryVivid: string;

  /** Token: theme-secondary */
  secondary: string;

  /** Token: theme-secondary-dark */
  secondaryDark: string;

  /** Token: theme-secondary-darker */
  secondaryDarker: string;

  /** Family tokens designate the overall color set and do not map to explicit values. Therefore, they are blank. */
  secondaryFamily: string;

  /** Token: theme-secondary-light */
  secondaryLight: string;

  /** Token: theme-secondary-lighter */
  secondaryLighter: string;

  /** Token: theme-secondary-vivid */
  secondaryVivid: string;

  /** Not well documented but necessary to make type size and line heights consistent with .usa-prose */
  styleBodyElement: boolean;

  /** Setting measures to 'none' prevents text from having a fixed width and allows it to flow. */
  textMeasure: string | number;

  /** Setting measures to 'none' prevents text from having a fixed width and allows it to flow. */
  textMeasureNarrow: string | number;

  /** Setting measures to 'none' prevents text from having a fixed width and allows it to flow. */
  textMeasureWide: string | number;

}

/**
 * Runtime token map for DC state
 * Maps property names to original SCSS variable names
 */
export const DC_TOKENS: Record<keyof DCTheme, string> = {
  buttonBorderRadius: '$theme-button-border-radius',
  colorPrimaryFamily: '$theme-color-primary-family',
  focusColor: '$theme-focus-color',
  fontRoleAlt: '$theme-font-role-alt',
  fontRoleHeading: '$theme-font-role-heading',
  fontTypeSans: '$theme-font-type-sans',
  fontTypeSerif: '$theme-font-type-serif',
  globalContentStyles: '$theme-global-content-styles',
  globalLinkStyles: '$theme-global-link-styles',
  globalParagraphStyles: '$theme-global-paragraph-styles',
  linkColor: '$theme-link-color',
  primary: '$theme-primary',
  primaryDark: '$theme-primary-dark',
  primaryDarker: '$theme-primary-darker',
  primaryLight: '$theme-primary-light',
  primaryLighter: '$theme-primary-lighter',
  primaryLightest: '$theme-primary-lightest',
  primaryVivid: '$theme-primary-vivid',
  secondary: '$theme-secondary',
  secondaryDark: '$theme-secondary-dark',
  secondaryDarker: '$theme-secondary-darker',
  secondaryFamily: '$theme-secondary-family',
  secondaryLight: '$theme-secondary-light',
  secondaryLighter: '$theme-secondary-lighter',
  secondaryVivid: '$theme-secondary-vivid',
  styleBodyElement: '$theme-style-body-element',
  textMeasure: '$theme-text-measure',
  textMeasureNarrow: '$theme-text-measure-narrow',
  textMeasureWide: '$theme-text-measure-wide'
};

/**
 * Example usage:
 *
 * import type { DCTheme } from './types/design-tokens';
 * import { DC_TOKENS } from './types/design-tokens';
 *
 * // Type-safe access
 * const primaryColor: DCTheme['colorPrimaryVivid'] = '#0f6460';
 *
 * // Runtime SCSS variable name
 * const scssVar = DC_TOKENS.colorPrimaryVivid; // '$theme-color-primary-vivid'
 */
