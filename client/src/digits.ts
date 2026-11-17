// CLI-12: Persian keyboards type Persian digits; the server reads Latin ones.

/** Turns Persian (۰-۹) and Arabic (٠-٩) digits into 0-9 and drops spaces around the text. */
export function latinDigits(text: string): string {
  return text.trim().replace(/[۰-۹٠-٩]/g, digit => String(digit.charCodeAt(0) & 0xf));
}
