// AUTH-37, CLI-92: the second factor. Pure, so tests run without a browser.
import type { LoginAnswer, TotpStep } from "./api.ts";

/** True when a login answered the second-factor step instead of the user. */
export function isTotpStep(answer: LoginAnswer): answer is TotpStep {
  return (answer as TotpStep).totp_required === true;
}

/** The ticket of a GitHub login that needs the code: the server sends the browser to `/?totp=<ticket>`. */
export function totpTicketIn(search: string): string | null {
  return new URLSearchParams(search).get("totp");
}

/** CLI-92: the recovery codes as a file to keep: one per line. */
export function recoveryFile(codes: readonly string[], heading: string): string {
  return `${heading}\n\n${codes.join("\n")}\n`;
}
