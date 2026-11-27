// The sample report (m32 task 3.1): one HTML file like a bot's sprint review, about 1 MB, with base64 fonts and pictures,
// three pages turned by ← →, by its buttons and by a swipe. Built at test time from the client's own fonts and icons,
// so the repository keeps no 1 MB file. It also tries to reach the app (SEC-16) and writes what it got into body[data-probe].
import { readFileSync } from "node:fs";
import { randomBytes } from "node:crypto";

/** A 24-bit BMP of random pixels: a big picture that does not compress, like a screenshot in a real report. */
function noise(side) {
  const row = side * 3;
  const size = 54 + row * side;
  const file = Buffer.alloc(size);
  file.write("BM", 0);
  file.writeUInt32LE(size, 2);
  file.writeUInt32LE(54, 10);
  file.writeUInt32LE(40, 14);
  file.writeInt32LE(side, 18);
  file.writeInt32LE(side, 22);
  file.writeUInt16LE(1, 26);
  file.writeUInt16LE(24, 28);
  randomBytes(row * side).copy(file, 54);
  return file;
}

export function report({ appOrigin, fonts, pictures }) {
  const font = (path, name) => `@font-face { font-family: ${name}; src: url(data:font/woff2;base64,${readFileSync(path).toString("base64")}) format("woff2"); }`;
  const png = path => `data:image/png;base64,${readFileSync(path).toString("base64")}`;
  const bmp = `data:image/bmp;base64,${noise(440).toString("base64")}`;
  return `<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Sprint review</title>
<style>
${fonts.map((path, i) => font(path, `F${i}`)).join("\n")}
html, body { margin: 0; height: 100%; font-family: F0, F1, sans-serif; touch-action: pan-y; }
section { display: none; height: 100%; box-sizing: border-box; padding: 24px; }
section.on { display: block; }
img { max-width: 40%; }
nav { position: fixed; inset-block-end: 12px; inset-inline: 0; display: flex; justify-content: center; gap: 12px; }
</style>
</head>
<body>
<section class="on" id="p1"><h1>Page 1</h1><p>Sprint 42 — done</p><img src="${png(pictures[0])}" alt="logo"></section>
<section id="p2"><h1>Page 2</h1><p>دالان — نمودار</p><img src="${bmp}" alt="chart"></section>
<section id="p3"><h1>Page 3</h1><p>Next sprint</p></section>
<nav><button id="prev">Prev</button><button id="next">Next</button></nav>
<script>
  const pages = [...document.querySelectorAll("section")];
  let page = 0;
  const show = n => { page = Math.max(0, Math.min(pages.length - 1, n)); pages.forEach((p, i) => p.classList.toggle("on", i === page)); document.body.dataset.page = String(page + 1); };
  show(0);
  addEventListener("keydown", e => { if (e.key === "ArrowRight") show(page + 1); if (e.key === "ArrowLeft") show(page - 1); });
  document.getElementById("next").onclick = () => show(page + 1);
  document.getElementById("prev").onclick = () => show(page - 1);
  // A swipe, as many reports do it: pointer events (a finger on an iPhone sends them too).
  let startX = null;
  addEventListener("pointerdown", e => { startX = e.clientX; });
  addEventListener("pointerup", e => {
    if (startX === null) return;
    const dx = e.clientX - startX;
    startX = null;
    if (Math.abs(dx) > 40) show(page + (dx < 0 ? 1 : -1));
  });
  // SEC-16: none of these may reach the app.
  const tryIt = f => { try { const v = f(); return v === "" || v === null || v === undefined ? "empty" : "got:" + String(v).slice(0, 40); } catch (e) { return "throws"; } };
  const probe = {
    cookie: tryIt(() => document.cookie),
    localStorage: tryIt(() => localStorage.getItem("token")),
    parent: tryIt(() => parent.document.title),
    fetch: "pending",
  };
  fetch(${JSON.stringify(appOrigin + "/index.html")}).then(() => "got", () => "blocked").then(r => { probe.fetch = r; document.body.dataset.probe = JSON.stringify(probe); });
</script>
</body>
</html>`;
}
