// Makes a VAPID key pair for Web Push (NTF-05). Run once, then put the two lines in the server's environment.
// The private key is a secret: never commit it. A new pair makes every browser subscribe again.
//   node scripts/vapid-keys.mjs
import { generateKeyPairSync } from "node:crypto";

const { privateKey } = generateKeyPairSync("ec", { namedCurve: "prime256v1" });
const jwk = privateKey.export({ format: "jwk" });
const bytes = text => Buffer.from(text, "base64url");
const publicKey = Buffer.concat([Buffer.from([4]), bytes(jwk.x), bytes(jwk.y)]).toString("base64url");

console.log(`Notification__Push__PublicKey=${publicKey}`);
console.log(`Notification__Push__PrivateKey=${jwk.d}`);
