// @ts-check

import { defineConfig } from 'eslint/config';
import tseslint from 'typescript-eslint';

export default defineConfig(
  {
    files: [
      'apphost.mts',
      'config.mts',
      'capabilities/**/*.mts',
      'states/**/*.mts',
    ],
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
    // config.mts owns the environment surface. Thus one file declares each value with
    // its default, and a resource module reads the resolved AppHostConfig. Before this
    // rule, that statement was a comment. The comment did not stop states/apps.mts from
    // a direct read of ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL. config.mts is absent from
    // `files` below, and that is what makes it an exception.
    files: ['apphost.mts', 'capabilities/**/*.mts', 'states/**/*.mts'],
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
