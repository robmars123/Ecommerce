import { avatarInitial } from './avatar-initial';

describe('avatarInitial', () => {
  it('T1: is the upper-cased first letter of the display name', () => {
    expect(avatarInitial({ displayName: 'nathaniel', email: 'n@example.test' })).toBe('N');
  });

  it('T2: falls back to the email when the display name is blank', () => {
    expect(avatarInitial({ displayName: '   ', email: 'customer@example.test' })).toBe('C');
  });

  it('T3: keeps an astral first character whole', () => {
    const initial = avatarInitial({ displayName: '𝒜lice', email: 'a@example.test' });

    expect(initial).toBe('𝒜');
    expect(initial).toHaveLength(2);
  });

  it('T4: is "?" when both are empty', () => {
    expect(avatarInitial({ displayName: '', email: '' })).toBe('?');
  });
});
