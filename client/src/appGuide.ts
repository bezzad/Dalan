// CLI-39: the commands of the "My apps" guide. Pure, so tests run without a browser.

/** Step 1: the one-line install, for Linux and macOS, and for Windows (PowerShell). */
export function installCommands(siteUrl: string): { unix: string; windows: string } {
  return {
    unix: `curl -fsSL ${siteUrl}/install.sh | sh`,
    windows: `irm ${siteUrl}/install.ps1 | iex`,
  };
}

/** Step 2: log in once with an access key. The server comes from Sites:TunnelUrl, so a test server works the same. */
export function loginCommand(key: string, tunnelUrl: string): string {
  return `dalan login ${key} --server ${tunnelUrl}`;
}

/** Step 3: one site, or more sites in one agent. */
export function httpCommand(sites: { port: number; name: string }[]): string {
  return "dalan http " + sites.map(site => `${site.port} --name ${site.name}`).join(" ");
}
