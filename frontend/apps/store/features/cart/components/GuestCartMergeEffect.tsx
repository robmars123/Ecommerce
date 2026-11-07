import { useGuestCartMerge } from '../hooks/useGuestCartMerge';

/** Mounted once in the root layout: merges the device cart when the user is signed in. Renders nothing. */
export function GuestCartMergeEffect() {
  useGuestCartMerge();
  return null;
}
