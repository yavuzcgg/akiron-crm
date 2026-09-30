/**
 * The same check-digit rules the API applies (Crm TaxNumber), so a typo is caught while typing.
 * The API stays the authority.
 */
export function isValidVkn(value: string): boolean {
  if (!/^\d{10}$/.test(value)) return false;

  let sum = 0;
  for (let index = 0; index < 9; index++) {
    const shifted = (Number(value[index]) + 9 - index) % 10;
    let weighted = (shifted * 2 ** (9 - index)) % 9;
    if (shifted !== 0 && weighted === 0) weighted = 9;
    sum += weighted;
  }

  return (10 - (sum % 10)) % 10 === Number(value[9]);
}

export function isValidTckn(value: string): boolean {
  if (!/^[1-9]\d{10}$/.test(value)) return false;

  const digits = [...value].map(Number);
  const odd = digits[0] + digits[2] + digits[4] + digits[6] + digits[8];
  const even = digits[1] + digits[3] + digits[5] + digits[7];
  const tenth = (((odd * 7 - even) % 10) + 10) % 10;
  const firstTen = digits.slice(0, 10).reduce((total, digit) => total + digit, 0);

  return tenth === digits[9] && firstTen % 10 === digits[10];
}

/** Companies are identified by VKN; persons by TCKN, or a sole trader's VKN. */
export function isValidTaxNumber(kind: "company" | "person", value: string): boolean {
  return kind === "company" ? isValidVkn(value) : isValidTckn(value) || isValidVkn(value);
}
