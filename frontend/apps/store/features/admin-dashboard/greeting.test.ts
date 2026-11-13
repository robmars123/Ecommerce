import { greeting } from './greeting';

const at = (hour: number, minute: number) => new Date(2026, 10, 12, hour, minute);

describe('greeting', () => {
  it.each([
    [0, 0, 'Good evening'],
    [4, 59, 'Good evening'],
    [5, 0, 'Good morning'],
    [11, 59, 'Good morning'],
    [12, 0, 'Good afternoon'],
    [17, 59, 'Good afternoon'],
    [18, 0, 'Good evening'],
    [23, 59, 'Good evening'],
  ])('G1: %s:%s is "%s"', (hour, minute, expected) => {
    expect(greeting(at(hour, minute))).toBe(expected);
  });

  it.each([
    ['Ada', 'Good morning, Ada'],
    ['  Ada  ', 'Good morning, Ada'],
    ['   ', 'Good morning'],
    [undefined, 'Good morning'],
  ])('G2: name %j gives "%s"', (name, expected) => {
    expect(greeting(at(9, 0), name)).toBe(expected);
  });
});
