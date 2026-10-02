/**
 * The same arithmetic as the API's QuoteMath, so the editor shows totals while typing. The API
 * recomputes on save and is the authority; rounding is half away from zero, per line.
 */
export function round2(value: number): number {
  const sign = value < 0 ? -1 : 1;
  return (sign * Math.round(Math.abs(value) * 100 + 1e-7)) / 100;
}

export interface LineInput {
  quantity: number;
  unitPrice: number;
  discountPercent: number;
  vatRate: number;
  withholdingTenths: number;
  priceIncludesVat?: boolean;
}

export interface LineAmounts {
  gross: number;
  discount: number;
  net: number;
  vat: number;
  withholding: number;
  total: number;
}

export function netUnitPrice(price: number, vatRate: number, includesVat = false): number {
  return includesVat ? Math.round(((price * 100) / (100 + vatRate)) * 10_000) / 10_000 : price;
}

export function lineAmounts(line: LineInput): LineAmounts {
  const price = netUnitPrice(line.unitPrice, line.vatRate, line.priceIncludesVat);
  const gross = round2(line.quantity * price);
  const net = round2((line.quantity * price * (100 - line.discountPercent)) / 100);
  const vat = round2((net * line.vatRate) / 100);
  const withholding = round2((vat * line.withholdingTenths) / 10);
  return { gross, discount: round2(gross - net), net, vat, withholding, total: round2(net + vat - withholding) };
}

export function quoteTotals(lines: LineInput[]): LineAmounts {
  const sum = (key: keyof LineAmounts) => round2(lines.map(lineAmounts).reduce((total, line) => total + line[key], 0));
  return { gross: sum("gross"), discount: sum("discount"), net: sum("net"), vat: sum("vat"), withholding: sum("withholding"), total: sum("total") };
}

/** "1.234,5" / "1234.5" / "1234,50" → 1234.5; NaN when it is not a number. */
export function parseDecimal(value: string): number {
  const text = value.trim().replace(/\s|₺/g, "");
  if (!text) return Number.NaN;
  const normalised = text.includes(",") || /^\d{1,3}(\.\d{3})+$/.test(text) ? text.replace(/\./g, "").replace(",", ".") : text;
  return /^-?\d+(\.\d+)?$/.test(normalised) ? Number(normalised) : Number.NaN;
}
