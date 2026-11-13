// What `@/lib/platform` looks like to callers and to tsc (architecture 5.2, 13.2 "Stripe client SDKs"). Metro and Jest
// pick platform.native.tsx or platform.web.tsx at bundle time; platform.conformance.ts proves each one matches this file.
import type { JSX } from 'react';

/** What the payment form reports back. `processing` and `succeeded` both wait for the server (the webhook is the proof). */
export type PaymentFormResult = {
  status: 'succeeded' | 'processing' | 'failed' | 'canceled';
  /** Customer-safe text from the payment provider, when it gave one. */
  message?: string;
};

export type PaymentFormProps = {
  clientSecret: string;
  /** App path the payment provider returns to after a redirect (3-D Secure, bank pages). */
  returnPath: string;
  onResult: (result: PaymentFormResult) => void;
};

/** Collects the card and confirms the payment. Card data never reaches our code: the provider UI holds it. */
export declare function PaymentForm(props: PaymentFormProps): JSX.Element;

/** Web keeps the refresh token in an httpOnly cookie the script cannot read; native keeps it in the body and SecureStore. */
export declare function refreshTokenTransport(): 'cookie' | 'body';

export declare const refreshTokenStorage: {
  get(): Promise<string | null>;
  set(token: string): Promise<void>;
  clear(): Promise<void>;
};

/** Hands a text file to the user: a download on web, the share sheet on native (the dashboard CSV export). */
export declare function saveTextFile(fileName: string, mimeType: string, text: string): Promise<void>;
