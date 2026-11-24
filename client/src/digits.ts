// CLI-12: Persian keyboards type Persian digits; the server reads Latin ones.

/** Turns Persian (۰-۹) and Arabic (٠-٩) digits into 0-9 and drops spaces around the text. */
export function latinDigits(text: string): string {
  return text.trim().replace(/[۰-۹٠-٩]/g, digit => String(digit.charCodeAt(0) & 0xf));
}

/**
 * CLI-60: a +98 mobile written in any usual way (+98 914 914 9202, 0098-914…, (0914) 914 9202, 9149149202,
 * Persian digits) becomes 09149149202. Any other text only gets Latin digits, so the server says what is wrong.
 */
export function phoneNumber(text: string): string {
  // Copied numbers often hold invisible direction marks.
  const latin = latinDigits(text.replace(/[‎‏‪-‮⁦-⁩]/g, ""));
  const mobile = /^(?:\+98|0098|98|0)?(9\d{9})$/.exec(latin.replace(/[\s\-().]/g, ""));
  return mobile ? "0" + mobile[1] : latin;
}
