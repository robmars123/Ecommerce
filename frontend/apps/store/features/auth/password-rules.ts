/** The backend's real password policy (Identity PasswordPolicy: 8 to 128 characters). The server stays the authority. */
export const PASSWORD_MIN_LENGTH = 8;
export const PASSWORD_MAX_LENGTH = 128;

export type PasswordRule = { label: string; met: boolean };

export function passwordRules(password: string): PasswordRule[] {
  return [
    { label: `At least ${PASSWORD_MIN_LENGTH} characters`, met: password.length >= PASSWORD_MIN_LENGTH },
    { label: `No more than ${PASSWORD_MAX_LENGTH} characters`, met: password.length <= PASSWORD_MAX_LENGTH },
  ];
}
