import { fetchWithSession } from '../../services/sessionFetch';
export async function emailRequest(path = '', method = 'GET', signal) {
  const root = import.meta.env.VITE_NOTIFICATION_API_URL;
  if (!root) throw new Error('Email delivery service is not configured.');
  const response = await fetchWithSession(`${root}/api/email-deliveries${path}`, { method, headers: { Authorization: `Bearer ${sessionStorage.getItem('token')}` }, signal: signal || AbortSignal.timeout(15000) });
  const data = response.headers.get('content-type')?.includes('application/json') ? await response.json() : null;
  if (!response.ok) throw new Error(data?.message || 'Email delivery history is unavailable.');
  return data;
}
export const messageName = value => ({ Booking:'Booking update',Maintenance:'Maintenance update',Verification:'Email verification',Welcome:'Welcome to FleetFlow',Security:'Account security update',Support:'Customer enquiry',DriverReview:'Driving verification update',BookingConfirmed:'Booking confirmation',BookingCancelled:'Booking cancellation',RentalPickedUp:'Vehicle pickup',RentalCompleted:'Rental return summary',Deletion:'Account deletion',Restoration:'Account restored' }[value] || value.replace(/([a-z])([A-Z])/g,'$1 $2'));
export const deliveryName = value => ({ Sent:'Accepted by mail server',Queued:'Waiting / sending',Sending:'Sending now',Failed:'Needs attention',Expired:'Code expired',Skipped:'Not sent',Anonymized:'Account data removed' }[value] || value);
export const deliveryHelp = value => ({ RecipientNotAllowlisted:'This inbox is outside the local test list. Use a selected test inbox; retrying does not change that restriction.',ConfigurationMissing:'Email settings are incomplete. Ask your deployment administrator to check SMTP configuration.',DeliveryFailed:'The mail server did not confirm delivery. Check configuration and connectivity before retrying.' }[value] || 'No delivery error recorded.');
export const emailState = row => row.emailVerificationMethod==='AdminWaived'?'Approved by administrator':row.emailVerificationMethod==='Unverified'||row.needsInitialEmailOtp?'Awaiting verification':'Email verified';
