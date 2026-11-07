import { passwordRules } from './password-rules';

const met = (password: string) => passwordRules(password).map((r) => r.met);

describe('passwordRules (the backend policy: 8 to 128 characters)', () => {
  it('fails the minimum below 8 characters', () => {
    expect(met('1234567')).toEqual([false, true]);
  });
  it('passes at the 8 and 128 boundaries', () => {
    expect(met('x'.repeat(8))).toEqual([true, true]);
    expect(met('x'.repeat(128))).toEqual([true, true]);
  });
  it('fails the maximum at 129 characters', () => {
    expect(met('x'.repeat(129))).toEqual([true, false]);
  });
});
