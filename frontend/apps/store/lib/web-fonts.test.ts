import fs from 'node:fs';
import path from 'node:path';

import { webFontFamily } from '@/lib/design-tokens';

// In `single` web output Expo uses public/index.html as the page template (app/+html.tsx is ignored), so the fonts are linked there.
const html = fs.readFileSync(path.join(process.cwd(), 'public', 'index.html'), 'utf8');

describe('web page template', () => {
  it('F3: links the Google Fonts stylesheet with a preconnect', () => {
    expect(html).toContain('rel="preconnect" href="https://fonts.googleapis.com"');
    expect(html).toContain(
      'https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@500&display=swap',
    );
  });

  it('F4: applies the same sans stack as the token file (quotes and line breaks may differ)', () => {
    expect(html.replace(/'/g, '"').replace(/,\s+/g, ', ')).toContain(webFontFamily.sans);
  });

  it('F6: applies the mono family from the token file to .font-mono, after the sans rule', () => {
    const normalised = html.replace(/\r/g, '').replace(/'/g, '"');
    expect(normalised).toContain(`#root .font-mono,
      #root .font-mono * {
        font-family: ${webFontFamily.mono};`);
    expect(normalised.indexOf('#root .font-mono')).toBeGreaterThan(normalised.indexOf('#root *'));
  });

  it('F5: keeps the placeholders Expo fills in', () => {
    expect(html).toContain('%LANG_ISO_CODE%');
    expect(html).toContain('%WEB_TITLE%');
    expect(html).toContain('id="root"');
  });
});
