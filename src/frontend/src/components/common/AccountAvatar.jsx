import React, { useState } from 'react';
import { UserIcon } from '@phosphor-icons/react/dist/csr/User';
import { getProfileImageUrl } from '../../utils/imageUrlUtils';
import { accountName } from '../../utils/accountPresentation';

function Photo({ src, name }) {
  const [failed, setFailed] = useState(false);
  return src && !failed
    ? <img src={src} alt={`${name}'s profile photo`} width="40" height="40" onError={() => setFailed(true)} />
    : <span className="account-avatar-fallback" role="img" aria-label={`${name}: profile photo unavailable`}><UserIcon size={24} weight="regular" aria-hidden="true" /></span>;
}

export default function AccountAvatar({ account, className = '' }) {
  const src = getProfileImageUrl(account?.profileImageUrl);
  // Remount on identity OR URL change, even when two accounts share a URL.
  return <span className={`account-avatar ${className}`.trim()}><Photo key={`${account?.id || account?.username || ''}:${src || ''}`} src={src} name={accountName(account)} /></span>;
}
