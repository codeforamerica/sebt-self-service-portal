'use client'

import { useState, useEffect } from 'react'

interface AdentifiPixelsProps {
  pixelId: string
}

export function AdentifiPixels({ pixelId }: AdentifiPixelsProps) {
  const [nonce, setNonce] = useState('')
  const [fullUrl, setFullUrl] = useState('')

  useEffect(() => {
    const r = Math.random() * 10000000000000000
    
    setNonce(r + '');

    if (window && window.location?.href) {
      setFullUrl(window.location.href)
    }
  }, [pixelId])

  const url = encodeURIComponent(fullUrl)
  const src = `https://px.adentifi.com/Pixels?a_id=${pixelId};p_url=${url};uq=${nonce}`

  // on initial load href may be blank
  if (!fullUrl) {
    return <></>
  }

  return <img src={src} width={1} height={1} className="display-none" alt='' />
}
