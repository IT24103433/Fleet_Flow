export function accountName(account) {
  return account?.fullName?.trim() || account?.username?.trim() || 'Account';
}
