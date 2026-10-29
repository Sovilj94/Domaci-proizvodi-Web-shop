/**
 * Jača (zasićenija, tamnija) varijanta pastelne boje pločice (`BojaProizvoda` sa servera), za
 * pločice sa bijelim nazivom kao na farmitu. Ista namirnica zadržava svoj ton, samo je izraženija.
 */
export function jacaBoja(hex: string | undefined, rezerva = "#7A9A5B"): string {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex ?? "");
  if (!m) return rezerva;
  const n = parseInt(m[1], 16);
  const [h, s, l] = uHsl((n >> 16) & 255, (n >> 8) & 255, n & 255);
  const s2 = Math.min(0.6, s * 1.05 + 0.08);
  const l2 = Math.min(0.56, Math.max(0.36, 0.28 + 0.32 * l));
  return `hsl(${Math.round(h)} ${Math.round(s2 * 100)}% ${Math.round(l2 * 100)}%)`;
}

function uHsl(r: number, g: number, b: number): [number, number, number] {
  r /= 255;
  g /= 255;
  b /= 255;
  const max = Math.max(r, g, b);
  const min = Math.min(r, g, b);
  const l = (max + min) / 2;
  if (max === min) return [0, 0, l];
  const d = max - min;
  const s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
  const h =
    max === r ? ((g - b) / d + (g < b ? 6 : 0)) * 60 : max === g ? ((b - r) / d + 2) * 60 : ((r - g) / d + 4) * 60;
  return [h, s, l];
}
