'use client'

import { useState, useMemo } from "react";

interface AdentifiPixelsProps {
  pixelId: string
}

export function AdentifiPixels({ pixelId }: AdentifiPixelsProps) {
  const [nonce, setNonce] = useState("")
  const [href, setHref] = useState("")

  useMemo(() => {
    const r = Buffer.from(crypto.randomUUID()).toString('base64')
    
    setNonce(r)
    setHref(window.location.href)
  }, []) // Empty array ensures this only runs once when the component mounts

  const url = encodeURIComponent(href)
  const src = `https://px.adentifi.com/Pixels?a_id=${pixelId};p_url=${url};uq=${nonce}`

  // on initial load href may be blank
  if (!window) {
    return <></>
  }

  return <img src={src} width={1} height={1} style={{display: 'none'}} alt='' />
}
