import { defineConfig, globalIgnores } from "eslint/config";
import nextVitals from "eslint-config-next/core-web-vitals";
import nextTs from "eslint-config-next/typescript";
import security from "eslint-plugin-security";

// This package is a component library rather than a Next.js app, but its
// components render next/image and next/link and target the same WCAG 2.1 AA
// bar as the apps that consume them. Keeping the config aligned with
// SEBT.Portal.Web and SEBT.EnrollmentChecker.Web means a component behaves the
// same under lint here as it does at its call site.
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
    // Some of these scripts carry inline disables for the rules turned off
    // above, each with a written justification for why the access is safe.
    // Those comments are worth keeping as documentation, and they would matter
    // again if this exception were ever narrowed, so don't report them as
    // unused.
    linterOptions: {
      reportUnusedDisableDirectives: "off",
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
  globalIgnores([
    "build/**",
    // Generated USWDS output
    "design/css/**",
  ]),
]);

export default eslintConfig;
