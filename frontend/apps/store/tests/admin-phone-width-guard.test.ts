import fs from 'node:fs';
import path from 'node:path';

// P2.46: Jest has no layout engine, so this guards the constructs that cause horizontal scroll at 375 px by construction.
// The phone content width is 375 minus 2 x 16 (px-lg).
const MAX_WIDTH = 343;
const ROOT = process.cwd();

function listFiles(dir: string, accept: (file: string) => boolean): string[] {
  if (!fs.existsSync(dir)) return [];
  return fs.readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) return listFiles(full, accept);
    return accept(full) ? [full] : [];
  });
}

const isSource = (file: string) => /\.tsx?$/.test(file) && !/\.test\.tsx?$/.test(file);

function adminSources(): string[] {
  const featureDirs = fs
    .readdirSync(path.join(ROOT, 'features'), { withFileTypes: true })
    .filter((entry) => entry.isDirectory() && entry.name.startsWith('admin-'))
    .map((entry) => path.join(ROOT, 'features', entry.name));
  const shared = fs
    .readdirSync(path.join(ROOT, 'components'))
    .filter((name) => name.endsWith('.tsx') && isSource(name))
    .map((name) => path.join(ROOT, 'components', name));
  return [path.join(ROOT, 'app', 'admin'), ...featureDirs].flatMap((dir) => listFiles(dir, isSource)).concat(shared);
}

/** Every `file:line: text` where `pattern` matches and `isViolation` agrees. */
function findings(pattern: RegExp, isViolation: (match: RegExpExecArray) => boolean): string[] {
  const found: string[] = [];
  for (const file of adminSources()) {
    fs.readFileSync(file, 'utf8')
      .split(/\r?\n/)
      .forEach((text, index) => {
        const regex = new RegExp(pattern.source, 'g');
        let match = regex.exec(text);
        while (match) {
          if (isViolation(match)) found.push(`${path.relative(ROOT, file).split(path.sep).join('/')}:${index + 1}: ${text.trim()}`);
          match = regex.exec(text);
        }
      });
  }
  return found;
}

describe('admin screens stay inside a 375 px phone (P2.46)', () => {
  it('guard scans real files', () => {
    expect(adminSources().length).toBeGreaterThan(20);
  });

  // Prettier puts `horizontal` on its own line, so match the prop wherever it appears, not only after the tag name.
  it('PG1: no horizontal ScrollView or FlatList', () => {
    expect(findings(/(?:^|\s)horizontal(?=\s*(?:=|\/?>|$))/, () => true)).toEqual([]);
  });

  it('PG2: no fixed width class wider than 343 px and no whitespace-nowrap', () => {
    const wide = findings(/(?:^|[\s"'`])(?:min-w|w|basis)-\[(\d+)px\]/, (match) => Number(match[1]) > MAX_WIDTH);
    const nowrap = findings(/whitespace-nowrap/, () => true);
    expect([...wide, ...nowrap]).toEqual([]);
  });

  it('PG3: no numeric width or minWidth literal wider than 343 px in a style', () => {
    expect(findings(/\b(?:width|minWidth)\s*:\s*(\d+)\b/, (match) => Number(match[1]) > MAX_WIDTH)).toEqual([]);
  });
});
