type TokenMap = Record<string, string>;

export const touch: number;
export const colorVariables: TokenMap;
export const themes: { dark: TokenMap; light: TokenMap };
export const sidebarBreakpoint: number;
export const narrowBreakpoint: number;
export const webFontFamily: { sans: string; mono: string };
export const spacing: TokenMap;
export const fontSize: Record<string, string | [string, Record<string, string>]>;
export const borderRadius: TokenMap;
export const minHeight: TokenMap;
export const minWidth: TokenMap;
export const maxWidth: TokenMap;
export const width: TokenMap;
export const height: TokenMap;
export const boxShadow: TokenMap;
