// CLI-10: the small proof of work the server asks for before it sends an SMS (SEC-06).
// The browser's own SHA-256 does the hashing; the server counts the bits the same way.

/** How many zero bits the hash starts with. */
export function leadingZeroBits(hash: Uint8Array): number {
  let bits = 0;
  for (const byte of hash) {
    if (byte !== 0) return bits + Math.clz32(byte) - 24; // clz32 counts in 32 bits; a byte uses the last 8
    bits += 8;
  }
  return bits;
}

export async function sha256(text: string): Promise<Uint8Array> {
  return new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(text)));
}

/** Tries 0, 1, 2, ... until SHA-256 of "challenge.nonce" starts with `difficulty` zero bits. */
export async function solve(challenge: string, difficulty: number): Promise<string> {
  for (let nonce = 0; ; nonce++) {
    if (leadingZeroBits(await sha256(`${challenge}.${nonce}`)) >= difficulty) return String(nonce);
  }
}
