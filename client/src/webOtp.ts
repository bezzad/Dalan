// CLI-59: Android Chrome reads the login code from an SMS whose last line is "@<site domain> #<code>" (WebOTP).
// iOS needs nothing here: it offers the code from autocomplete="one-time-code".

/** What the browser offers; a parameter so a test can play the browser. */
export interface Browser {
  webOtp: boolean;
  credentials: { get(options: CredentialRequestOptions): Promise<unknown> };
}

const realBrowser = (): Browser => ({ webOtp: "OTPCredential" in globalThis, credentials: navigator.credentials });

/** The code from the SMS, or null: no WebOTP, refused, or ended by the signal. Never throws. */
export async function readSmsCode(signal: AbortSignal, browser: Browser = realBrowser()): Promise<string | null> {
  if (!browser.webOtp) return null;
  try {
    // "otp" is not in TypeScript's DOM types yet.
    const credential = await browser.credentials.get({ otp: { transport: ["sms"] }, signal } as CredentialRequestOptions);
    const code = (credential as { code?: unknown } | null)?.code;
    return typeof code === "string" && code ? code : null;
  } catch {
    return null;
  }
}
