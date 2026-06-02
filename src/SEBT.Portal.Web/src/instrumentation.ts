export async function register() {
  // Guard: OTel Node.js SDK uses async_hooks and native modules incompatible
  // with the edge runtime. Only initialize in the Node.js runtime.
  if (process.env.NEXT_RUNTIME === 'nodejs') {
    await import('./telemetry/sdk')
  }
}
