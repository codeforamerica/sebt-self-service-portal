'use client'

interface AdentifiPixelsProps {
  pixelId: string
  href: string
}

export function AdentifiPixels({ pixelId, href }: AdentifiPixelsProps) {
  const nonce = Math.random() * 10000000000000
  const url = encodeURIComponent(href)
  const src = `https://px.adentifi.com/Pixels?a_id=${pixelId};p_url=${url};uq=${nonce}`

  // on initial load href may be blank
  if (!href) {
    return <></>
  }

  return <img src={src} width={1} height={1} style={{display: 'none'}} alt='' />
}
