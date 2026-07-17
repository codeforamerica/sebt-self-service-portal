'use client'

import Script from 'next/script'

interface MetaPixelAnalyticsProps {
  pixelId: string
}

const META_PIXEL_STUB_SNIPPET = `!function(f,b,e,v,n,t,s){if(f.fbq)return;n=f.fbq=function(){n.callMethod?n.callMethod.apply(n,arguments):n.queue.push(arguments)};if(!f._fbq)f._fbq=n;n.push=n;n.loaded=!0;n.version='2.0';n.queue=[];t=b.createElement(e);t.async=!0;t.src=v;s=b.getElementsByTagName(e)[0];s.parentNode.insertBefore(t,s)}(window,document,'script','https://connect.facebook.net/en_US/fbevents.js');`

export function MetaPixelAnalytics({ pixelId }: MetaPixelAnalyticsProps) {
  return (
    <>
      {/* should be loaded inside <head> right before closing </head> tag */}
      <Script
        id="meta-pixel-stub"
        dangerouslySetInnerHTML={{ __html: META_PIXEL_STUB_SNIPPET }}
        strategy="afterInteractive"
        onReady={() => {
          console.log(window)
          (window as any).fbq('init', pixelId)
          (window as any).fbq('track', 'PageView')
        }}
      />
    </>
  )
}
