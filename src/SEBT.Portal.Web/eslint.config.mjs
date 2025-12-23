import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";
import security from "eslint-plugin-security";

const eslintConfig = defineConfig([
  ...nextVitals,
  ...nextTs,
  // Security plugin - detects potential security issues
  {
    plugins: {
      security,
    },
    rules: {
      ...security.configs.recommended.rules,
    },
  },
  // Build scripts exception: These scripts run only at build time with trusted
  // repository files. Dynamic object access and filesystem operations use
  // controlled configuration values, not user input.
  {
    files: ["content/scripts/**/*.js", "design/scripts/**/*.js"],
    rules: {
      "security/detect-object-injection": "off",
      "security/detect-non-literal-fs-filename": "off",
    },
  },
  // Enhanced accessibility checks for USWDS compliance (extends Next.js defaults)
  {
    rules: {
      // WCAG 2.1 AA compliance rules
      '@next/next/no-html-link-for-pages': 'off',
      'jsx-a11y/anchor-is-valid': 'error',
      'jsx-a11y/aria-props': 'error',
      'jsx-a11y/aria-role': 'error',
      'jsx-a11y/heading-has-content': 'error',
      'jsx-a11y/label-has-associated-control': 'error',
      'jsx-a11y/no-noninteractive-element-interactions': 'warn',
    },
  },
  // Override default ignores of eslint-config-next.
  globalIgnores([
    // Default ignores of eslint-config-next:
    ".next/**",
    "out/**",
    "build/**",
    "next-env.d.ts",
  ]),
]);

export default eslintConfig;
