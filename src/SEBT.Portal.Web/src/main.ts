/**
 * SEBT Portal - Main Entry Point
 *
 * This is the main TypeScript entry point for the SEBT Self-Service Portal.
 * It initializes the application and imports USWDS styles.
 */

import './styles.scss';

// Mark CSS as loaded to prevent FOUC
document.body.classList.add('css-loaded');

// Development-only logging
if (import.meta.env.DEV) {
  console.log('🎨 SEBT Portal initialized with USWDS');
}
