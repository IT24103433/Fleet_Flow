import React from 'react';
import AccountAvatar from './AccountAvatar';
import { accountName } from '../../utils/accountPresentation';
import RoleBadge from './RoleBadge';

export default function AccountPresentation({ account, role, className = '' }) {
  return <span className={`account-presentation ${className}`.trim()}><AccountAvatar account={account} /><span className="account-presentation-text"><strong title={accountName(account)}>{accountName(account)}</strong><RoleBadge role={role || account?.role || account?.roles?.[0]} /></span></span>;
}
