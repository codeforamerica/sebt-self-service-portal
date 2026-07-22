'use client'

import { useState, useEffect } from 'react'
import { usePathname, useSearchParams } from 'next/navigation'

interface AdentifiPixelsProps {
  pixelId: string
}

export function AdentifiPixels({ pixelId }: AdentifiPixelsProps) {
  const [nonce, setNonce] = useState('')
  const pathname = usePathname()
  const searchParams = useSearchParams()
  const [fullUrl, setFullUrl] = useState('')

  useEffect(() => {
    const r = Buffer.from(crypto.randomUUID()).toString('base64')
    
    setNonce(r);

    if (window && window.location?.origin) {
      const url = `${window.location.origin}${pathname || ''}?${(searchParams || '').toString()}`;

      setFullUrl(url)
    }
  }, [pathname, searchParams])

  const url = encodeURIComponent(fullUrl)
  const src = `https://px.adentifi.com/Pixels?a_id=${pixelId};p_url=${url};uq=${nonce}`

  // on initial load href may be blank
  if (!fullUrl) {
    return <></>
  }

  return <img src={src} width={1} height={1} style={{display: 'none'}} alt='' />
}
