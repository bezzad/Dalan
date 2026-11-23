// Rule: CLI-46
// A WebM/Opus recording (tests/samples/voice.webm: 2 seconds, written like a live recording) rewritten into Ogg/Opus.
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { test } from "node:test";
import { opusSamples, readWebmOpus, recorderType, voiceFile, webmToOgg } from "../src/ogg.ts";

const webm = new Uint8Array(readFileSync(new URL("./samples/voice.webm", import.meta.url)));

/** Reads an Ogg file back: each page's header, its granule, and the packets joined from the lacing values. */
function readOgg(bytes: Uint8Array): { types: number[]; granules: bigint[]; sequences: number[]; packets: Uint8Array[] } {
  const view = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  const result = { types: [] as number[], granules: [] as bigint[], sequences: [] as number[], packets: [] as Uint8Array[] };
  let at = 0;
  let open: number[] = [];
  while (at < bytes.length) {
    assert.equal(new TextDecoder().decode(bytes.subarray(at, at + 4)), "OggS");
    result.types.push(bytes[at + 5]!);
    result.granules.push(view.getBigUint64(at + 6, true));
    result.sequences.push(view.getUint32(at + 18, true));
    const count = bytes[at + 26]!;
    const lacing = [...bytes.subarray(at + 27, at + 27 + count)];
    let body = at + 27 + count;
    for (const size of lacing) {
      open.push(...bytes.subarray(body, body + size));
      body += size;
      if (size < 255) {
        result.packets.push(Uint8Array.from(open));
        open = [];
      }
    }
    at = body;
  }
  return result;
}

test("CLI-46 a WebM recording becomes Ogg with the same Opus packets and its length", () => {
  const { ogg, seconds } = webmToOgg(webm);
  const read = readOgg(ogg);

  assert.equal(new TextDecoder().decode(read.packets[0]!.subarray(0, 8)), "OpusHead");
  assert.equal(new TextDecoder().decode(read.packets[1]!.subarray(0, 8)), "OpusTags");
  assert.deepEqual(read.packets.slice(2), readWebmOpus(webm).packets);
  assert.equal(read.types[0], 0x02, "the first page begins the stream");
  assert.equal(read.types.at(-1), 0x04, "the last page ends it");
  assert.deepEqual(read.sequences, read.sequences.map((_, i) => i));
  assert.ok(Math.abs(seconds - 2) < 0.05, `about 2 seconds, got ${seconds}`);
  const preSkip = new DataView(read.packets[0]!.buffer).getUint16(10, true);
  assert.equal(Number(read.granules.at(-1)!) - preSkip, Math.round(seconds * 48000));
});

test("CLI-46 big packets never put more than 255 lacing values in one page", () => {
  // A fake WebM: EBML header, an unknown-size Segment and Cluster, and 60 SimpleBlocks of 1200 bytes (5 lacing values each).
  const block = (size: number) => [0xa3, 0x40 | ((size + 4) >> 8), (size + 4) & 0xff, 0x81, 0, 0, 0x80, 0xfc, ...new Array(size - 1).fill(7)];
  const bytes = Uint8Array.from([0x1a, 0x45, 0xdf, 0xa3, 0x80, 0x18, 0x53, 0x80, 0x67, 0x01, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff,
    0x1f, 0x43, 0xb6, 0x75, 0x01, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, 0xff, ...Array.from({ length: 60 }, () => block(1200)).flat()]);
  const { ogg } = webmToOgg(bytes);
  const read = readOgg(ogg);
  assert.equal(read.packets.length, 62);
  assert.ok(read.packets.slice(2).every(packet => packet.length === 1200));
  let at = 0;
  while (at < ogg.length) {
    const count = ogg[at + 26]!;
    assert.ok(count <= 255);
    at += 27 + count + [...ogg.subarray(at + 27, at + 27 + count)].reduce((sum, value) => sum + value, 0);
  }
});

test("CLI-46 an Opus packet's length comes from its first byte", () => {
  assert.equal(opusSamples(Uint8Array.of(0xfc)), 960, "CELT 20 ms, one frame");
  assert.equal(opusSamples(Uint8Array.of(0x08)), 960, "SILK 20 ms");
  assert.equal(opusSamples(Uint8Array.of(0xfd)), 1920, "two frames");
  assert.equal(opusSamples(Uint8Array.of(0xf3, 0x03)), 3 * 480, "three 10 ms frames");
});

test("CLI-46 the type: Ogg, else WebM, else MP4", () => {
  const only = (type: string) => (asked: string) => asked === type;
  assert.equal(recorderType(only("audio/ogg;codecs=opus")), "audio/ogg;codecs=opus");
  assert.equal(recorderType(only("audio/webm;codecs=opus")), "audio/webm;codecs=opus");
  assert.equal(recorderType(only("audio/mp4")), "audio/mp4");
  assert.equal(recorderType(() => false), "");
});

test("CLI-46 the voice is sent as Ogg when it can be, else as recorded with its real type", () => {
  assert.equal(voiceFile(webm, "audio/webm;codecs=opus").type, "audio/ogg");
  assert.equal(voiceFile(webm, "audio/webm;codecs=opus").name, "voice.ogg");
  const broken = voiceFile(Uint8Array.of(1, 2, 3), "audio/webm");
  assert.deepEqual([broken.type, broken.name, broken.bytes.length], ["audio/webm", "voice.webm", 3]);
  assert.deepEqual([voiceFile(Uint8Array.of(1), "audio/mp4").type, voiceFile(Uint8Array.of(1), "audio/mp4").name], ["audio/mp4", "voice.m4a"]);
  assert.equal(voiceFile(Uint8Array.of(1), "audio/ogg; codecs=opus").type, "audio/ogg");
});
