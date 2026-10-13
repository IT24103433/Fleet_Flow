import React, { useState } from 'react';
import { CarProfileIcon } from '@phosphor-icons/react/dist/csr/CarProfile';

const base = '/assets/photography/luxury-coupe-hero';
const sources = format => [960, 1440, 1920].map(width => `${base}-${width}.${format} ${width}w`).join(', ');

export default function HeroMedia() {
  const [state, setState] = useState('loading');
  return <figure className="cinematic-media container-scroll-frame" data-media-state={state}>
    {state !== 'failed' && <picture>
      <source type="image/avif" srcSet={sources('avif')} sizes="100vw" />
      <source type="image/webp" srcSet={sources('webp')} sizes="100vw" />
      <img className="cinematic-media-image" src={`${base}-1920.webp`} alt="Original campaign artwork of a graphite luxury coupe in a dark studio" width="1920" height="1080" fetchPriority="high" onLoad={() => setState('ready')} onError={() => setState('failed')} />
    </picture>}
    {state === 'failed' && <div className="cinematic-media-fallback"><CarProfileIcon size={48} weight="regular" aria-hidden="true" /><p>Photo unavailable</p></div>}
    <figcaption><a href="/credits.html">Campaign artwork & credits</a></figcaption>
  </figure>;
}
