// @ts-check

import { defineConfig } from 'eslint/config';
import tseslint from 'typescript-eslint';

export default defineConfig(
  {
    files: ['apphost.mts', 'config.mts', 'states/**/*.mts'],
    extends: [tseslint.configs.base],
    languageOptions: {
      parserOptions: {
        project: './tsconfig.apphost.json',
        tsconfigRootDir: import.meta.dirname,
      },
    },
    rules: {
      '@typescript-eslint/no-floating-promises': ['error', { checkThenables: true }],
    },
  },
  {
    // config.mts owns the whole environment surface, so every knob is declared and
    // defaulted in one place and resource modules read the resolved AppHostConfig. That
    // rule was previously a comment, which did not stop states/apps.mts from reading
    // ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL directly. config.mts is absent from `files`
    // below, which is what exempts it.
    files: ['apphost.mts', 'states/**/*.mts'],
    rules: {
      'no-restricted-properties': [
        'error',
        {
          object: 'process',
          property: 'env',
          message:
            'Read the environment only in config.mts. Add the value to AppHostConfig and pass the resolved config instead.',
        },
      ],
    },
  },
);
