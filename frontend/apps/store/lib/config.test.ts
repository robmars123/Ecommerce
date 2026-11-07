import { getApiBaseUrl, getStripePublishableKey } from './config';

afterEach(() => {
  delete process.env.EXPO_PUBLIC_API_BASE_URL;
});

describe('getApiBaseUrl', () => {
  it('is undefined when unset or blank (no hard-coded fallback)', () => {
    expect(getApiBaseUrl()).toBeUndefined();
    process.env.EXPO_PUBLIC_API_BASE_URL = '   ';
    expect(getApiBaseUrl()).toBeUndefined();
  });

  it('returns the value with trailing slashes trimmed', () => {
    process.env.EXPO_PUBLIC_API_BASE_URL = 'http://localhost:5080/';
    expect(getApiBaseUrl()).toBe('http://localhost:5080');
    process.env.EXPO_PUBLIC_API_BASE_URL = 'http://localhost:5080///';
    expect(getApiBaseUrl()).toBe('http://localhost:5080');
  });

  it('reads the variable at call time', () => {
    process.env.EXPO_PUBLIC_API_BASE_URL = 'http://a.test.invalid';
    expect(getApiBaseUrl()).toBe('http://a.test.invalid');
    process.env.EXPO_PUBLIC_API_BASE_URL = 'http://b.test.invalid';
    expect(getApiBaseUrl()).toBe('http://b.test.invalid');
  });
});

describe('getStripePublishableKey', () => {
  afterEach(() => {
    delete process.env.EXPO_PUBLIC_STRIPE_PUBLISHABLE_KEY;
  });

  it('is undefined when unset or blank, never a guessed key', () => {
    delete process.env.EXPO_PUBLIC_STRIPE_PUBLISHABLE_KEY;
    expect(getStripePublishableKey()).toBeUndefined();
    process.env.EXPO_PUBLIC_STRIPE_PUBLISHABLE_KEY = '   ';
    expect(getStripePublishableKey()).toBeUndefined();
  });

  it('returns the trimmed value', () => {
    process.env.EXPO_PUBLIC_STRIPE_PUBLISHABLE_KEY = '  pk-test-placeholder ';
    expect(getStripePublishableKey()).toBe('pk-test-placeholder');
  });
});
