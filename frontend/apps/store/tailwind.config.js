const tokens = require('./lib/design-tokens');

/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./app/**/*.{ts,tsx}', './components/**/*.{ts,tsx}', './features/**/*.{ts,tsx}'],
  presets: [require('nativewind/preset')],
  theme: {
    extend: {
      colors: tokens.colorVariables,
      spacing: tokens.spacing,
      fontSize: tokens.fontSize,
      borderRadius: tokens.borderRadius,
      minHeight: tokens.minHeight,
      minWidth: tokens.minWidth,
      maxWidth: tokens.maxWidth,
      width: tokens.width,
      height: tokens.height,
    },
  },
  plugins: [],
};
