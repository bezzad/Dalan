// CLI-21: notifications on this device, with the Web Push standard.
import { api } from "./api.ts";

export type PushState = "hidden" | "unsupported" | "blocked" | "on" | "off";

/** What the notifications switch shows. */
export function pushState(input: {
  key: string | null;
  supported: boolean;
  permission: NotificationPermission;
  subscribed: boolean;
}): PushState {
  if (input.key === null) return "hidden";
  if (!input.supported) return "unsupported";
  if (input.permission === "denied") return "blocked";
  return input.permission === "granted" && input.subscribed ? "on" : "off";
}

/** False on an iPhone before "Add to Home Screen", and in old browsers. */
export function pushSupported(): boolean {
  return "serviceWorker" in navigator && "PushManager" in window && "Notification" in window;
}

async function subscription(): Promise<PushSubscription | null> {
  if (!pushSupported()) return null;
  return (await navigator.serviceWorker.ready).pushManager.getSubscription();
}

export async function currentPushState(key: string | null): Promise<PushState> {
  const supported = pushSupported();
  return pushState({
    key,
    supported,
    permission: supported ? Notification.permission : "default",
    subscribed: supported && (await subscription()) !== null,
  });
}

/** Asks the permission, subscribes with the server's key, and tells the server. */
export async function turnOn(key: string): Promise<PushState> {
  if (await Notification.requestPermission() !== "granted") return currentPushState(key);

  const registration = await navigator.serviceWorker.ready;
  const created = await registration.pushManager.subscribe({ userVisibleOnly: true, applicationServerKey: fromBase64Url(key) });
  await api.subscribe(created.toJSON());
  return "on";
}

/** Removes this device's subscription on the server and in the browser. Also the first step of logout. */
export async function turnOff(): Promise<void> {
  const existing = await subscription();
  if (!existing) return;
  await api.unsubscribe(existing.endpoint);
  await existing.unsubscribe();
}

function fromBase64Url(text: string): Uint8Array<ArrayBuffer> {
  const base64 = text.replace(/-/g, "+").replace(/_/g, "/");
  return Uint8Array.from(atob(base64), char => char.charCodeAt(0));
}
