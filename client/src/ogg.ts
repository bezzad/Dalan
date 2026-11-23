// CLI-46: a browser that records Opus only in WebM gets it rewritten into Ogg, the container of a Telegram voice.
// The Opus packets are copied as they are (no decoding), so this is fast and loses nothing.

/** The Opus packets of a WebM file, and its Opus header (CodecPrivate) when it has one. */
export interface OpusTrack { head: Uint8Array | null; packets: Uint8Array[] }

const Segment = 0x18538067, Cluster = 0x1f43b675, Tracks = 0x1654ae6b, TrackEntry = 0xae, BlockGroup = 0xa0;
const SimpleBlock = 0xa3, Block = 0xa1, CodecPrivate = 0x63a2;
/** Elements whose children are read; their size may be "unknown", as a live recording writes it. */
const containers = new Set([Segment, Cluster, Tracks, TrackEntry, BlockGroup]);

/** Reads the WebM element by element. Throws on anything it does not expect, so the caller can keep the WebM. */
export function readWebmOpus(bytes: Uint8Array): OpusTrack {
  let at = 0;
  const vint = (keepMarker: boolean): { value: number; unknown: boolean } => {
    const first = bytes[at];
    if (first === undefined || first === 0) throw new Error("bad WebM");
    const length = Math.clz32(first) - 23;
    let value = keepMarker ? first : first & (0xff >> length);
    let allOnes = value === (0xff >> length);
    for (let i = 1; i < length; i++) {
      const byte = bytes[at + i];
      if (byte === undefined) throw new Error("bad WebM");
      value = value * 256 + byte;
      allOnes &&= byte === 0xff;
    }
    at += length;
    return { value, unknown: !keepMarker && allOnes };
  };

  if (bytes[0] !== 0x1a || bytes[1] !== 0x45 || bytes[2] !== 0xdf || bytes[3] !== 0xa3) throw new Error("not WebM");
  let head: Uint8Array | null = null;
  const packets: Uint8Array[] = [];
  while (at < bytes.length) {
    const id = vint(true).value;
    const size = vint(false);
    if (containers.has(id)) continue;
    if (size.unknown || at + size.value > bytes.length) {
      // A recording cut at the end: keep what was read.
      if (id === SimpleBlock || id === Block) break;
      throw new Error("bad WebM");
    }
    const end = at + size.value;
    if (id === CodecPrivate) head = bytes.slice(at, end);
    else if (id === SimpleBlock || id === Block) {
      vint(false); // track number: a recording has one track
      const flags = bytes[at + 2]!;
      if ((flags & 0x06) !== 0) throw new Error("laced blocks");
      packets.push(bytes.slice(at + 3, end));
    }
    at = end;
  }
  if (packets.length === 0) throw new Error("no audio");
  return { head, packets };
}

/** Samples at 48 kHz in one Opus packet, from its first byte (RFC 6716, section 3.1). */
export function opusSamples(packet: Uint8Array): number {
  const toc = packet[0] ?? 0;
  const config = toc >> 3;
  const tenths = config < 12 ? [100, 200, 400, 600][config % 4]! // SILK: 10 to 60 ms
    : config < 16 ? [100, 200][config % 2]! // hybrid: 10 or 20 ms
      : [25, 50, 100, 200][config % 4]!; // CELT: 2.5 to 20 ms
  const code = toc & 3;
  const frames = code === 0 ? 1 : code === 3 ? (packet[1] ?? 0) & 0x3f : 2;
  return frames * tenths * 48 / 10;
}

const crcTable = (() => {
  const table = new Uint32Array(256);
  for (let i = 0; i < 256; i++) {
    let r = i << 24;
    for (let j = 0; j < 8; j++) r = r & 0x80000000 ? (r << 1) ^ 0x04c11db7 : r << 1;
    table[i] = r >>> 0;
  }
  return table;
})();

function crc(bytes: Uint8Array): number {
  let value = 0;
  for (const byte of bytes) value = ((value << 8) ^ crcTable[((value >>> 24) ^ byte) & 0xff]!) >>> 0;
  return value;
}

/** One Ogg page with whole packets (each under 255 * 255 bytes, which an Opus packet always is). */
function page(packets: Uint8Array[], granule: number, sequence: number, serial: number, type: number): Uint8Array {
  const lacing: number[] = [];
  for (const packet of packets) {
    for (let left = packet.length; ; left -= 255) {
      lacing.push(Math.min(left, 255));
      if (left < 255) break;
    }
  }
  const bodySize = packets.reduce((sum, packet) => sum + packet.length, 0);
  const out = new Uint8Array(27 + lacing.length + bodySize);
  const view = new DataView(out.buffer);
  out.set([0x4f, 0x67, 0x67, 0x53, 0, type]);
  view.setBigUint64(6, BigInt(granule), true);
  view.setUint32(14, serial, true);
  view.setUint32(18, sequence, true);
  out[26] = lacing.length;
  out.set(lacing, 27);
  let at = 27 + lacing.length;
  for (const packet of packets) {
    out.set(packet, at);
    at += packet.length;
  }
  view.setUint32(22, crc(out), true);
  return out;
}

const ascii = (text: string) => new TextEncoder().encode(text);

/** The OpusHead for one channel at 48 kHz, for a WebM without CodecPrivate. */
function defaultHead(): Uint8Array {
  const head = new Uint8Array(19);
  head.set(ascii("OpusHead"));
  const view = new DataView(head.buffer);
  head[8] = 1;
  head[9] = 1;
  view.setUint16(10, 312, true);
  view.setUint32(12, 48000, true);
  return head;
}

function tags(): Uint8Array {
  const vendor = ascii("Dalan");
  const out = new Uint8Array(8 + 4 + vendor.length + 4);
  const view = new DataView(out.buffer);
  out.set(ascii("OpusTags"));
  view.setUint32(8, vendor.length, true);
  out.set(vendor, 12);
  return out;
}

/** Packets per audio page: about one second of 20 ms packets, like other Ogg Opus writers. */
const packetsPerPage = 50;

/** CLI-46: the same Opus packets in an Ogg file, and the length in seconds. */
export function webmToOgg(webm: Uint8Array, serial = 0x44616c61): { ogg: Uint8Array; seconds: number } {
  const track = readWebmOpus(webm);
  const head = track.head && track.head.length >= 19 && new TextDecoder().decode(track.head.subarray(0, 8)) === "OpusHead"
    ? track.head : defaultHead();
  const preSkip = new DataView(head.buffer, head.byteOffset).getUint16(10, true);
  const pages = [page([head], 0, 0, serial, 0x02), page([tags()], 0, 1, serial, 0)];
  let granule = preSkip;
  let group: Uint8Array[] = [];
  let segments = 0;
  const flush = (last: boolean) => {
    pages.push(page(group, granule, pages.length, serial, last ? 0x04 : 0));
    group = [];
    segments = 0;
  };
  for (const packet of track.packets) {
    // A page holds at most 255 lacing values; a packet takes one per 255 bytes, plus one.
    const needs = Math.floor(packet.length / 255) + 1;
    if (group.length === packetsPerPage || segments + needs > 255) flush(false);
    group.push(packet);
    segments += needs;
    granule += opusSamples(packet);
  }
  flush(true);
  const ogg = new Uint8Array(pages.reduce((sum, p) => sum + p.length, 0));
  let at = 0;
  for (const p of pages) {
    ogg.set(p, at);
    at += p.length;
  }
  return { ogg, seconds: (granule - preSkip) / 48000 };
}

/** CLI-46: the recorder type to ask for: Ogg first, then WebM (rewritten later), then MP4 (Safari); "" for the browser's own. */
export function recorderType(supported: (type: string) => boolean): string {
  return ["audio/ogg;codecs=opus", "audio/webm;codecs=opus", "audio/mp4"].find(supported) ?? "";
}

/** CLI-46: the file to send for a recording: Ogg when it can be, else the recording as it is, with its real type. */
export function voiceFile(bytes: Uint8Array, recordedType: string): { bytes: Uint8Array; type: string; name: string } {
  const type = recordedType.split(";")[0]!.trim() || "audio/webm";
  if (type === "audio/webm") {
    try {
      return { bytes: webmToOgg(bytes).ogg, type: "audio/ogg", name: "voice.ogg" };
    } catch {
      // Keep the WebM: the bot still gets a voice, with its real type.
    }
  }
  const extension = type === "audio/ogg" ? "ogg" : type === "audio/mp4" ? "m4a" : type === "audio/webm" ? "webm" : "audio";
  return { bytes, type, name: `voice.${extension}` };
}
