// Builds the PWA into client/dist: compiled scripts, the files of client/public, and the font.
// Plain Node, so it runs the same on Linux, macOS and Windows. The server embeds client/dist (CLI-02).
import { execFileSync } from "node:child_process";
import { cpSync, mkdirSync, rmSync } from "node:fs";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL(".", import.meta.url));
const path = (...parts) => root + parts.join("/");

rmSync(path("dist"), { recursive: true, force: true });
execFileSync(process.execPath, [path("node_modules/typescript/bin/tsc"), "-p", path("tsconfig.build.json")], { stdio: "inherit" });
cpSync(path("public"), path("dist"), { recursive: true });

// The Persian font comes from the npm package and is served by us, never by a foreign host (CLAUDE.md rule 7).
mkdirSync(path("dist/fonts"), { recursive: true });
cpSync(path("node_modules/vazirmatn/fonts/webfonts/Vazirmatn[wght].woff2"), path("dist/fonts/vazirmatn.woff2"));
cpSync(path("node_modules/vazirmatn/OFL.txt"), path("dist/fonts/OFL.txt"));
