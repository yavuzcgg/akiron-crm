/** "1:30", "1,5", "90" → minutes; null when it does not read as a duration. */
export function parseDuration(value: string): number | null {
  const text = value.trim().replace(",", ".");
  const clock = /^(\d{1,2}):([0-5]\d)$/.exec(text);
  if (clock) return Number(clock[1]) * 60 + Number(clock[2]);
  if (/^\d+(\.\d+)?$/.test(text)) {
    const number = Number(text);
    // Up to 24 reads as hours ("2" = 2 h, "1.5" = 90 min); more reads as minutes ("45").
    return Math.round(number <= 24 ? number * 60 : number);
  }
  return null;
}
