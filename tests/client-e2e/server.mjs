// The test server of the browser tests (CLI-87, CLI-89, CLI-90, SEC-16). Two origins, like Dalan in production:
// - the app (http://localhost:APP_PORT): client/dist (and index.html for a few app pages) and harness.html, which sets a cookie and localStorage like a logged-in page;
// - the view host (http://127.0.0.1:VIEW_PORT): only GET /v/report, the sample report with the SEC-16 headers.
// Plain Node, no packages.
import { createServer } from "node:http";
import { readFileSync, existsSync, statSync } from "node:fs";
import { extname, join, normalize } from "node:path";
import { fileURLToPath } from "node:url";
import { report } from "./report.mjs";

const here = fileURLToPath(new URL(".", import.meta.url));
const dist = join(here, "../../client/dist");
const appPort = Number(process.env.APP_PORT ?? 4173);
const viewPort = Number(process.env.VIEW_PORT ?? 4174);
const appOrigin = `http://localhost:${appPort}`;
const types = { ".html": "text/html; charset=utf-8", ".js": "text/javascript", ".css": "text/css", ".png": "image/png", ".svg": "image/svg+xml", ".woff2": "font/woff2" };

createServer((request, response) => {
  const path = new URL(request.url, appOrigin).pathname;
  const harness = ["/harness.html", "/dashboard.html"].includes(path);
  // CLI-101: a page of the app itself; the spec answers its /api calls.
  const app = ["/guide/site", "/guide/bot", "/faq"].includes(path);
  const file = harness ? join(here, path.slice(1)) : normalize(join(dist, app ? "index.html" : path));
  if (!file.startsWith(dist) && !harness || !existsSync(file) || statSync(file).isDirectory()) {
    response.writeHead(404).end();
    return;
  }
  response.writeHead(200, { "Content-Type": types[extname(file)] ?? "application/octet-stream" }).end(readFileSync(file));
}).listen(appPort, "localhost");

// SEC-16, word for word: the file's script runs in an opaque origin and reaches nothing.
const viewHeaders = {
  "Content-Type": "text/html; charset=utf-8",
  "X-Content-Type-Options": "nosniff",
  "Referrer-Policy": "no-referrer",
  "Cache-Control": "private, no-store",
  "Content-Security-Policy": "sandbox allow-scripts allow-popups; default-src 'none'; script-src 'unsafe-inline' data: blob:; " +
    "style-src 'unsafe-inline' data: blob:; img-src data: blob:; font-src data:; media-src data: blob:; connect-src 'none'; " +
    `form-action 'none'; base-uri 'none'; frame-ancestors ${appOrigin}`,
};
const body = report({ appOrigin, fonts: [join(dist, "fonts/inter.woff2"), join(dist, "fonts/vazirmatn.woff2")], pictures: [join(dist, "icons/icon-512.png")] });

createServer((request, response) => {
  if (request.method === "GET" && new URL(request.url, "http://x").pathname === "/v/report") response.writeHead(200, viewHeaders).end(body);
  else response.writeHead(404).end();
}).listen(viewPort, "127.0.0.1");

console.log(`app ${appOrigin}, view http://127.0.0.1:${viewPort}, report ${(body.length / 1024 / 1024).toFixed(1)} MB`);
