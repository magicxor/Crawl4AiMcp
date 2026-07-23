# Crawl4AiMcp

A local **stdio MCP server** (C# / .NET 10) that proxies work to a configured
[crawl4ai](https://github.com/unclecode/crawl4ai) REST instance and **writes the resulting
artifacts to a directory the AI agent supplies** in a mandatory argument.

It mirrors crawl4ai's official MCP tool set (`md`, `html`, `screenshot`, `pdf`,
`execute_js`, `crawl`, `ask`), but changes the output contract: where the official MCP
returns base64 blobs or huge JSON/Markdown *inline* (straight into the agent's context),
this server saves them to disk and returns a small summary — the file path(s), size and a
short preview. Only the purely-textual `ask` tool stays inline.

## Why

crawl4ai's own MCP bridge auto-generates its tools from FastAPI routes and wraps every
result as a single JSON text block. Screenshots and PDFs come back as base64 strings inside
that JSON, and `crawl`/`execute_js`/`md`/`html` can return very large payloads. Feeding all
of that back into an LLM context is wasteful. This server keeps the big/binary output on
disk and hands the agent just what it needs to find and preview it.

## Tools

| Tool | Purpose | Output |
|------|---------|--------|
| `md` | Page → Markdown (fit/raw/bm25/llm) | writes `.md`, returns path + size + preview |
| `html` | Page → preprocessed/sanitized HTML | writes `.html`, returns path + size + preview |
| `screenshot` | Full-page PNG | writes `.png`, returns path + metadata (no base64) |
| `pdf` | Page → PDF | writes `.pdf`, returns path + metadata (no base64) |
| `execute_js` | Run JS, capture full crawl result | writes `.json`, returns path + small `js_execution_result` inline |
| `crawl` | Crawl 1–100 URLs | per URL: `.md`, `.png`/`.pdf` if present, full `.json`; returns a manifest |
| `ask` | Query crawl4ai's own code/docs context | **inline** results; `query` is required; writes nothing |

Every tool except `ask` takes a **required `outputDirectory`** argument (created if missing)
and an optional `fileName` (path components are stripped; the name is otherwise derived from
the URL, with a numeric suffix on collision). Text is written UTF-8 without BOM.

`ask` requires a non-empty `query` (so results stay small) and returns them inline.

## Configuration

Configure the target crawl4ai instance via the `Crawl4Ai` config section — either
`appsettings.json` or environment variables (the standard MCP-client mechanism):

| Setting | Env var | Default | Notes |
|---------|---------|---------|-------|
| `BaseUrl` | `Crawl4Ai__BaseUrl` | `http://localhost:11235` | crawl4ai REST base URL |
| `ApiToken` | `Crawl4Ai__ApiToken` | *(empty)* | `Authorization: Bearer <token>`; required unless the instance is open on loopback |
| `TimeoutSeconds` | `Crawl4Ai__TimeoutSeconds` | `300` | per-request HTTP timeout |
| `AllowedOutputPatterns` | `Crawl4Ai__AllowedOutputPatterns__0`, `__1`, … | *(empty ⇒ deny all)* | regex allow-list for output directories (see below) |

### Output path safety

Every file write is guarded at a single choke point before anything touches the filesystem:

1. **Absolute paths only** — `outputDirectory` must be fully qualified/rooted (e.g. `C:\out`,
   `\\server\share\out`). Relative or drive-relative paths are rejected.
2. **No traversal / trash segments** — any `.`, `..`, all-dots, empty (double-separator), or
   invalid-character path segment is rejected outright. The path is validated as-is (never
   normalized), so `..` is refused rather than silently collapsed. An optional `fileName` must
   be a bare leaf name (no separators, not just dots).
3. **Allow-list** — the directory must match at least one regex in `AllowedOutputPatterns`
   (matched case-insensitively). **An empty list denies everything** — you must configure at
   least one pattern before any tool can write. Invalid regexes fail fast at startup.

Configure the allow-list in `appsettings.json`:

```json
{
  "Crawl4Ai": {
    "AllowedOutputPatterns": [
      "^C:\\\\Users\\\\me\\\\crawl-out(\\\\|$)",
      "^\\\\\\\\nas\\\\crawl(\\\\|$)"
    ]
  }
}
```

or via environment variables (indexed): `Crawl4Ai__AllowedOutputPatterns__0=^C:\\out(\\|$)`.
On any rejection the tool returns `{ "success": false, "error": "..." }` and writes nothing.

## Running it

You need a running crawl4ai instance. For example:

```bash
docker run -d --shm-size=1g -p 11235:11235 unclecode/crawl4ai:latest
```

By default crawl4ai enables authentication, so set a token on the server
(`-e CRAWL4AI_API_TOKEN=<token>`) and pass the same value to this server via
`Crawl4Ai__ApiToken`.

Build and run this MCP server:

```bash
dotnet build
dotnet run --project Crawl4AiMcp
```

### Registering with an MCP client

Claude Code (stdio), pointing at your crawl4ai instance:

```bash
claude mcp add crawl4ai-local \
  -e Crawl4Ai__BaseUrl=http://localhost:11235 \
  -e Crawl4Ai__ApiToken=<your-token> \
  -- dotnet run --project /abs/path/to/Crawl4AiMcp
```

## Notes

- `execute_js` requires the crawl4ai server to be started with
  `CRAWL4AI_EXECUTE_JS_ENABLED=true`; otherwise the tool returns a clean error.
- `crawl` accepts optional `browserConfig` / `crawlerConfig` / `crawlerConfigs` as JSON
  strings that are forwarded verbatim; crawl4ai validates them under its "untrusted"
  boundary. Hooks are intentionally not exposed.
- On any crawl4ai error (auth, network, server), tools return
  `{ "success": false, "error": "...", "statusCode": <code> }` and write no files.
