/** „1 porcija", „2 porcije", „5 porcija" (11–14 su „porcija"). */
export function porcijeTekst(n: number): string {
  const m100 = n % 100;
  const m10 = n % 10;
  if (m100 >= 11 && m100 <= 14) return `${n} porcija`;
  if (m10 === 1) return `${n} porcija`;
  if (m10 >= 2 && m10 <= 4) return `${n} porcije`;
  return `${n} porcija`;
}

/** „1 sastojak dodan", „2 sastojka dodana", „5 sastojaka dodano". */
export function dodanoSastojakaTekst(n: number): string {
  const m100 = n % 100;
  const m10 = n % 10;
  if (m100 >= 11 && m100 <= 14) return `${n} sastojaka dodano`;
  if (m10 === 1) return `${n} sastojak dodan`;
  if (m10 >= 2 && m10 <= 4) return `${n} sastojka dodana`;
  return `${n} sastojaka dodano`;
}
