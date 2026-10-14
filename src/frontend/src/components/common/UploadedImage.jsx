import React, { useState } from 'react';

export default function UploadedImage({ src, alt, className = '', fallback = 'Photo unavailable', ...props }) {
  const [failed, setFailed] = useState(null);
  if (!src || failed === src) return <div className={`image-fallback ${className}`} role="img" aria-label={`${alt || 'Image'}: photo unavailable`}>{fallback}</div>;
  return <img {...props} src={src} alt={alt} className={className} onError={() => setFailed(src)} />;
}
