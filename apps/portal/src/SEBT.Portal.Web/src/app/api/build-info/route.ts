import { NextResponse } from 'next/server'

// Exposes the same build-time values already rendered into <meta name="build-sha">
// (see layout.tsx) as JSON, so external tooling (release automation) can read them
// without parsing HTML. No dynamic APIs are used here, so Next statically evaluates
// and caches this at build time.
export async function GET() {
  return NextResponse.json({
    buildSha: process.env.NEXT_PUBLIC_BUILD_SHA ?? null,
    dcConnectorSha: process.env.NEXT_PUBLIC_DC_CONNECTOR_SHA ?? null
  })
}
