export const rentalStateLabel = state => ({ PickupPending: 'Pickup awaiting confirmation', Active: 'Rental active',
  CompletionPending: 'Return awaiting confirmation', Completed: 'Rental completed' })[state] || 'Rental record';
