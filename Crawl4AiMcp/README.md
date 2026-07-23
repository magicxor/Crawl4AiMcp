# Crawl4AiMcp

Local **stdio MCP server** that proxies to a configured
[crawl4ai](https://github.com/unclecode/crawl4ai) REST instance and writes the resulting
artifacts (Markdown, HTML, screenshots, PDFs, crawl results) to a directory the calling
agent specifies, returning only a compact summary (file paths, sizes, a short preview)
instead of dumping base64 blobs or huge JSON/Markdown inline.

## Tools

- `md` — page → Markdown (`.md`)
- `html` — page → preprocessed HTML (`.html`)
- `screenshot` — full-page PNG (`.png`)
- `pdf` — page → PDF (`.pdf`)
- `execute_js` — run JS, save full crawl result (`.json`), return the small `js_execution_result` inline
- `crawl` — crawl 1–100 URLs, write per-URL `.md`/`.png`/`.pdf`/`.json`, return a manifest
- `ask` — query crawl4ai's own code/docs context; **inline** results, `query` required

Every tool except `ask` requires an `outputDirectory` argument (created if missing).

## Configuration

Set the target instance via the `Crawl4Ai` config section (env vars or `appsettings.json`):

- `Crawl4Ai__BaseUrl` — default `http://localhost:11235`
- `Crawl4Ai__ApiToken` — bearer token; required unless the instance is open on loopback
- `Crawl4Ai__TimeoutSeconds` — default `300`
- `Crawl4Ai__AllowedOutputPatterns__0`, `__1`, … — regex allow-list for output directories

### Output path safety

Every write is validated before touching disk: `outputDirectory` must be absolute/rooted with
no `.`/`..`/all-dots/empty/invalid segments (paths are validated as-is, never normalized), any
`fileName` must be a bare leaf name, and the directory must match at least one regex in
`AllowedOutputPatterns`. **An empty allow-list denies all writes** — configure at least one
pattern. Invalid regexes fail fast at startup. On rejection the tool returns
`{ "success": false, "error": "..." }` and writes nothing.

## Local development

```bash
dotnet run --project Crawl4AiMcp
```

Register with an MCP client over stdio, e.g. Claude Code:

```bash
claude mcp add crawl4ai-local \
  -e Crawl4Ai__BaseUrl=http://localhost:11235 \
  -e Crawl4Ai__ApiToken=<your-token> \
  -- dotnet run --project /abs/path/to/Crawl4AiMcp
```

## Packaging

This project is set up as an MCP-server NuGet tool (`PackAsTool`, `PackageType=McpServer`).
Build a package with `dotnet pack -c Release`; the `.mcp/server.json` manifest describes the
stdio transport and the `Crawl4Ai__*` environment variables.
