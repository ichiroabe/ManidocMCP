# Manidoc MCP Server — ユーザーマニュアル / User Manual

---

## 目次 / Table of Contents

- [日本語](#日本語)
- [English](#english)

---

## 日本語

### 概要

Manidoc MCP Server は、ドキュメント管理アプリ [Manidoc](https://github.com/ichiroabe/manidoc) のデータをAIエージェントから読み書きするための MCP（Model Context Protocol）サーバーです。macOS と Windows で動作します。

> Windows をお使いの場合は [Windows でのセットアップ](#windows-でのセットアップ) を参照してください。

Claude Desktop などのMCP対応AIクライアントと連携することで、以下のことが自然言語で行えます。

- Manidoc のプロジェクト・記事の閲覧・編集
- Markdown テキストからのプロジェクト一括インポート
- 全文検索

---

### 対応する MCP 仕様

MCP 仕様 **2026-07-28** に対応しています（実装は C# SDK `ModelContextProtocol` 2.1.0）。

| 項目 | 対応内容 |
| --- | --- |
| プロトコルバージョン | 2026-07-28 |
| 接続方式 | ステートレス。`initialize` ハンドシェイクは不要で、各リクエストが `_meta` の `io.modelcontextprotocol/protocolVersion` と `io.modelcontextprotocol/clientCapabilities` でバージョンと能力を宣言します |
| `server/discover` | 対応。対応バージョン・サーバー能力・サーバー識別情報（名前・バージョン・説明・URL）を1回のリクエストで返します |
| 後方互換 | 2025-11-25 以前のクライアントは従来の `initialize` ハンドシェイクでそのまま接続できます（Claude Desktop の既存設定は変更不要） |
| 構造化出力 | 全ツールが `outputSchema` を宣言し、結果を `structuredContent` として返します。同じ内容を JSON テキストでも返すため、構造化出力に未対応のクライアントでも従来どおり読めます |
| ツール注釈 | 全ツールが `readOnlyHint` / `destructiveHint` / `idempotentHint` / `openWorldHint` を宣言します。クライアントはこれを見て確認ダイアログの要否などを判断します |
| キャッシュヒント | `tools/list` は `ttlMs`（60分）と `cacheScope`（private）を返します。ツールは名前順で返るため、クライアント側のキャッシュが効きます |
| ログ | 仕様で logging 機能（`notifications/message`）が非推奨になったため、診断ログは stderr に出力します。stdout は JSON-RPC 専用です |
| トランスポート | stdio |

> トラブル時は stderr を見てください。Claude Desktop の場合は `~/Library/Logs/Claude/mcp-server-manidoc.log` に記録されます。

---

### 必要なもの

| 項目 | 内容 |
| --- | --- |
| OS | macOS 13 (Ventura) 以降、または Windows 10 / 11 |
| ランタイム | .NET 8.0 SDK 以上（**ソースからビルドする場合のみ**。配布バイナリはランタイム同梱） |
| Manidoc | インストール済みであること |
| AIクライアント | Claude Desktop など MCP 対応のもの |

#### .NET 8.0 SDK のインストール

```bash
brew install dotnet@8
```

Windows の場合:

```powershell
winget install Microsoft.DotNet.SDK.8
```

---

### インストール手順（macOS）

#### 1. ビルドとインストール

```bash
cd /path/to/manidocMCP
bash installer/install.sh
```

これにより以下が実行されます:
- .NET 8 でセルフコンテインドバイナリをビルド
- `~/Applications/ManidocMCP/` にインストール

#### 2. Claude Desktop の設定

Claude Desktop の設定ファイルにサーバーを登録します。

設定ファイルの場所:

```
~/Library/Application Support/Claude/claude_desktop_config.json
```

設定例:

```json
{
  "mcpServers": {
    "manidoc": {
      "command": "/Users/abeichiro/Applications/ManidocMCP/ManidocMCP",
      "env": {
        "MANIDOC_WORKSPACE": "/Users/abeichiro/Google Drive/マイドライブ/manidocAndroid"
      }
    }
  }
}
```

| 設定項目 | 説明 |
| --- | --- |
| `command` | `ManidocMCP` バイナリのフルパス |
| `MANIDOC_WORKSPACE` | Manidoc のデータフォルダ（`.json` プロジェクトファイルが入っているフォルダ） |

> `MANIDOC_WORKSPACE` が未設定または存在しないパスの場合、ドキュメント操作ツール呼び出し時にエラーになります。`get_server_status` で事前に確認することをお勧めします。

#### 3. Claude Desktop を再起動

設定を保存後、Claude Desktop を再起動するとMCPサーバーが有効になります。

#### 4. 接続確認

AIエージェントに以下を指示して、サーバーが正常に動作しているか確認します。

```text
Manidocサーバーのステータスを確認して
```

正常であればワークスペースのパスとプロジェクト数が返ります。

---

### Windows でのセットアップ

2通りあります。**ビルド済みバイナリを使う方法A を推奨します**（.NET SDK が不要です）。

#### 方法A: ビルド済みバイナリを使う（推奨）

1. [Releases](https://github.com/ichiroabe/manidocMCP/releases) から CPU に合う zip をダウンロードします。

   | CPU | ファイル |
   | --- | --- |
   | Intel / AMD（一般的な PC） | `ManidocMCP-win-x64.zip` |
   | Arm64（Snapdragon X 搭載機など） | `ManidocMCP-win-arm64.zip` |

2. 任意のフォルダに展開します（例: `%LOCALAPPDATA%\Programs\ManidocMCP`）。
3. 展開した `ManidocMCP.exe` のフルパスを控えます。

#### 方法B: ソースからビルドする

.NET 8.0 SDK が必要です。

```powershell
winget install Microsoft.DotNet.SDK.8
git clone https://github.com/ichiroabe/manidocMCP
cd manidocMCP
powershell -ExecutionPolicy Bypass -File installer\install.ps1
```

`install.ps1` は CPU アーキテクチャ（x64 / Arm64）を自動判定して
`%LOCALAPPDATA%\Programs\ManidocMCP` にインストールし、そのまま貼り付けられる設定 JSON を表示します。

> `-ExecutionPolicy Bypass` は、既定の実行ポリシーでスクリプトがブロックされるのを回避するためです。
> この指定はそのプロセス内だけに効き、システムの設定は変更しません。

#### Claude Desktop の設定（Windows）

設定ファイルの場所:

```
%APPDATA%\Claude\claude_desktop_config.json
```

設定例。**Windows のパスはバックスラッシュを2つ重ねて書く**必要があります（JSON のエスケープ）:

```json
{
  "mcpServers": {
    "manidoc": {
      "command": "C:\\Users\\yourname\\AppData\\Local\\Programs\\ManidocMCP\\ManidocMCP.exe",
      "env": {
        "MANIDOC_WORKSPACE": "C:\\Users\\yourname\\ManidocData"
      }
    }
  }
}
```

保存後、Claude Desktop を再起動すると有効になります。接続確認は macOS と同じく
「Manidocサーバーのステータスを確認して」で行えます。

#### トラブルシューティング（Windows）

| 症状 | 対処 |
| --- | --- |
| スクリプトが実行できない | `powershell -ExecutionPolicy Bypass -File installer\install.ps1` の形で実行する |
| 設定を書いてもサーバーが出てこない | JSON のバックスラッシュが 1 つになっていないか確認する（`\\` が必要） |
| 起動直後に落ちる | `MANIDOC_WORKSPACE` のフォルダが存在するか確認する |
| 詳細を見たい | Claude Desktop のログフォルダ `%APPDATA%\Claude\logs` の `mcp-server-manidoc.log` を確認する |

---

### Gemini CLI での設定

Claude Desktop 以外に、Gemini CLI（バージョン 0.35.3 以降）でも利用できます。

#### 1. 作業ディレクトリの準備

```bash
cd ~/Documents
mkdir GeminiWorkspace
cd GeminiWorkspace
```

#### 2. MCP設定ファイルの作成

```bash
mkdir -p .gemini
cat > .gemini/settings.json << 'EOF'
{
  "mcpServers": {
    "manidoc-mac": {
      "command": "/Users/abeichiro/Applications/ManidocMCP/ManidocMCP",
      "env": {
        "MANIDOC_WORKSPACE": "/Users/abeichiro/Google Drive/マイドライブ/manidocAndroid"
      }
    }
  }
}
EOF
```

> 注意: 設定ファイルは `mcp.json` ではなく `.gemini/settings.json` を使います。

#### 3. Gemini CLI の起動と信頼設定

```bash
gemini
```

初回起動時に「このフォルダーを信頼しますか？」と表示された場合は 1. Trust folder を選択。

#### トラブルシューティング

| 症状 | 対処 |
| --- | --- |
| MCPサーバーが認識されない | `gemini mcp list` で設定を確認 |
| 権限エラー | `chmod +x` でバイナリに実行権限を付与 |
| 環境変数が効かない | `settings.json` の `env` セクションを確認 |

---

### 機能一覧

#### サーバー確認

| ツール名 | 説明 | 注釈 |
| --- | --- | --- |
| `get_server_status` | サーバーの動作確認。ワークスペースパス・プロジェクト数・サーバーバージョンを返す | 読み取り専用 |

`get_server_status` はワークスペースが開けない場合もエラーにはせず、`ok: false` と `error` に理由を入れて返します。

#### ドキュメント操作

| ツール名 | 説明 | 注釈 |
| --- | --- | --- |
| `list_projects` | ワークスペース内の全プロジェクト一覧を返す | 読み取り専用 |
| `list_nodes` | 指定プロジェクトのノード（見出し）一覧を返す | 読み取り専用 |
| `get_article` | プロジェクトID・ノードID で記事（Markdown）を取得 | 読み取り専用 |
| `save_article` | プロジェクトID・ノードID で記事（Markdown）を上書き保存 | 上書き（destructive） |
| `get_article_by_title` | プロジェクト名・ノードタイトルの部分一致で記事を取得 | 読み取り専用 |
| `save_article_by_title` | プロジェクト名・ノードタイトルの部分一致で記事を保存 | 上書き（destructive） |
| `import_markdown_as_project` | Markdown テキストを新規プロジェクトとして一括インポート | 新規作成 |
| `search_fulltext` | 全プロジェクトを対象にキーワード全文検索 | 読み取り専用 |

> 保存系ツールは既存の本文を丸ごと置き換えます（`destructiveHint: true`）。追記したい場合は先に `get_article` で現在の内容を取得してから、結合した全文を保存してください。

#### 構造化された戻り値

各ツールは `structuredContent` として構造化データを返します。主なフィールドは次のとおりです。

| ツール | 主なフィールド |
| --- | --- |
| `get_server_status` | `ok`, `workspace`, `projectCount`, `serverVersion`, `error` |
| `list_projects` | `projects[]`（`id`, `name`, `tag`）, `count` |
| `list_nodes` | `projectId`, `projectName`, `nodes[]`（`id`, `title`, `path`）, `count` |
| `get_article` / `get_article_by_title` | `projectId`, `projectName`, `nodeId`, `nodeTitle`, `path`, `content`, `comment` |
| `save_article` / `save_article_by_title` | `projectId`, `nodeId`, `nodeTitle`, `previousLength`, `savedLength` |
| `import_markdown_as_project` | `projectId`, `projectName`, `nodeCount` |
| `search_fulltext` | `keyword`, `results[]`, `byProject[]`, `totalMatches`, `shownCount`, `hint` |

正確な定義は `tools/list` が返す各ツールの `outputSchema` を参照してください。

---

### AIエージェントへのコマンド例

#### 接続確認

```text
Manidocサーバーのステータスを確認して
```

#### プロジェクト・記事の閲覧

```text
Manidocのプロジェクト一覧を見せて
「仙台」プロジェクトのノード一覧を表示して
「仙台」プロジェクトの「歴史」というノードの記事を取得して
```

#### 記事の編集・作成

```text
「仙台」プロジェクトの「観光」ノードに以下の内容をMarkdownで保存して：
# 観光スポット
- 青葉城跡
- 瑞鳳殿
```

#### Markdownインポート

```text
以下のMarkdownを新しいプロジェクトとしてManidocにインポートして：
# 東北地方
## 宮城県
仙台市を県庁所在地とする...
```

#### 全文検索

```text
Manidocで「伊達政宗」というキーワードを全文検索して
```

---

### 注意事項

- AIエージェントの能力への依存: このMCPサーバーの動作結果の品質は、接続するAIエージェントの能力に左右されます。
- テスト環境: Claude Desktop（Claude Sonnet 4.5）でテストしています。
- ワークスペースパスにスペースや日本語が含まれる場合でも動作します。

---

## English

### Overview

Manidoc MCP Server is an MCP (Model Context Protocol) server that allows AI agents to read and write data in [Manidoc](https://github.com/ichiroabe/manidoc), a document management application. It runs on macOS and Windows.

> On Windows, see [Setup on Windows](#setup-on-windows).

By integrating with MCP-compatible AI clients such as Claude Desktop, you can use natural language to:

- Browse and edit Manidoc projects and articles
- Bulk-import projects from Markdown text
- Perform full-text search

---

### MCP Specification Support

This server implements MCP specification **2026-07-28** (via the C# SDK `ModelContextProtocol` 2.1.0).

| Item | Details |
| --- | --- |
| Protocol version | 2026-07-28 |
| Connection model | Stateless. There is no `initialize` handshake; every request declares its version and capabilities via `_meta` (`io.modelcontextprotocol/protocolVersion`, `io.modelcontextprotocol/clientCapabilities`) |
| `server/discover` | Supported. Returns supported versions, server capabilities, and server identity (name, version, description, website) in one request |
| Backward compatibility | Clients on 2025-11-25 and earlier can still connect via the legacy `initialize` handshake — no config change needed for existing Claude Desktop setups |
| Structured output | Every tool declares an `outputSchema` and returns `structuredContent`. The same payload is also returned as JSON text, so clients without structured-output support keep working |
| Tool annotations | Every tool declares `readOnlyHint` / `destructiveHint` / `idempotentHint` / `openWorldHint` |
| Cache hints | `tools/list` returns `ttlMs` (60 minutes) and `cacheScope` (private), and lists tools in a deterministic name order so clients can cache it |
| Logging | The logging feature (`notifications/message`) is deprecated by the specification, so diagnostics go to stderr. stdout carries JSON-RPC only |
| Transport | stdio |

> When troubleshooting, check stderr. With Claude Desktop it is captured in `~/Library/Logs/Claude/mcp-server-manidoc.log`.

---

### Requirements

| Item | Details |
| --- | --- |
| OS | macOS 13 (Ventura) or later, or Windows 10 / 11 |
| Runtime | .NET 8.0 SDK or later (**only when building from source** — released binaries bundle the runtime) |
| Manidoc | Must be installed |
| AI Client | Claude Desktop or any MCP-compatible client |

#### Installing .NET 8.0 SDK

```bash
brew install dotnet@8
```

On Windows:

```powershell
winget install Microsoft.DotNet.SDK.8
```

---

### Installation (macOS)

#### 1. Build and Install

```bash
cd /path/to/manidocMCP
bash installer/install.sh
```

#### 2. Configure Claude Desktop

Location:

```
~/Library/Application Support/Claude/claude_desktop_config.json
```

Example:

```json
{
  "mcpServers": {
    "manidoc": {
      "command": "/Users/yourname/Applications/ManidocMCP/ManidocMCP",
      "env": {
        "MANIDOC_WORKSPACE": "/path/to/your/ManidocData"
      }
    }
  }
}
```

#### 3. Restart Claude Desktop

#### 4. Verify the Connection

```text
Check the Manidoc server status.
```

---

### Setup on Windows

Two options. **Option A (prebuilt binary) is recommended** — it needs no .NET SDK.

#### Option A: Use a prebuilt binary (recommended)

1. Download the zip matching your CPU from [Releases](https://github.com/ichiroabe/manidocMCP/releases).

   | CPU | File |
   | --- | --- |
   | Intel / AMD (most PCs) | `ManidocMCP-win-x64.zip` |
   | Arm64 (e.g. Snapdragon X devices) | `ManidocMCP-win-arm64.zip` |

2. Extract it anywhere (e.g. `%LOCALAPPDATA%\Programs\ManidocMCP`).
3. Note the full path to `ManidocMCP.exe`.

#### Option B: Build from source

Requires the .NET 8.0 SDK.

```powershell
winget install Microsoft.DotNet.SDK.8
git clone https://github.com/ichiroabe/manidocMCP
cd manidocMCP
powershell -ExecutionPolicy Bypass -File installer\install.ps1
```

`install.ps1` detects your CPU architecture (x64 / Arm64), installs into
`%LOCALAPPDATA%\Programs\ManidocMCP`, and prints a ready-to-paste config snippet.

> `-ExecutionPolicy Bypass` only avoids the default policy blocking the script for that one
> process; it does not change any system setting.

#### Configure Claude Desktop (Windows)

Location:

```
%APPDATA%\Claude\claude_desktop_config.json
```

Example — note that **Windows paths need doubled backslashes** (JSON escaping):

```json
{
  "mcpServers": {
    "manidoc": {
      "command": "C:\\Users\\yourname\\AppData\\Local\\Programs\\ManidocMCP\\ManidocMCP.exe",
      "env": {
        "MANIDOC_WORKSPACE": "C:\\Users\\yourname\\ManidocData"
      }
    }
  }
}
```

Restart Claude Desktop to apply.

#### Troubleshooting (Windows)

| Symptom | Fix |
| --- | --- |
| The script will not run | Invoke it as `powershell -ExecutionPolicy Bypass -File installer\install.ps1` |
| Server does not appear after editing the config | Check that backslashes are doubled (`\\`) in the JSON |
| Exits immediately on start | Check that the `MANIDOC_WORKSPACE` folder exists |
| Need more detail | Read `mcp-server-manidoc.log` in the Claude Desktop log folder `%APPDATA%\Claude\logs` |

---

### Tool Reference

#### Server Status

| Tool | Description | Annotation |
| --- | --- | --- |
| `get_server_status` | Verifies the server is running. Returns workspace path, project count and server version. | read-only |

`get_server_status` does not fail when the workspace cannot be opened; it returns `ok: false` with the reason in `error`.

#### Document Operations

| Tool | Description | Annotation |
| --- | --- | --- |
| `list_projects` | Returns a list of all projects in the workspace | read-only |
| `list_nodes` | Returns a list of nodes (headings) in a specified project | read-only |
| `get_article` | Retrieves an article (Markdown) by project ID and node ID | read-only |
| `save_article` | Overwrites an article by project ID and node ID | destructive |
| `get_article_by_title` | Retrieves an article by partial match on project name and node title | read-only |
| `save_article_by_title` | Saves an article by partial match on project name and node title | destructive |
| `import_markdown_as_project` | Imports Markdown text as a new project | creates new data |
| `search_fulltext` | Full-text keyword search across all projects | read-only |

> The save tools replace the article body in full (`destructiveHint: true`). To append, call `get_article` first and save the combined text.

#### Structured Results

Every tool returns `structuredContent`. Main fields:

| Tool | Main fields |
| --- | --- |
| `get_server_status` | `ok`, `workspace`, `projectCount`, `serverVersion`, `error` |
| `list_projects` | `projects[]` (`id`, `name`, `tag`), `count` |
| `list_nodes` | `projectId`, `projectName`, `nodes[]` (`id`, `title`, `path`), `count` |
| `get_article` / `get_article_by_title` | `projectId`, `projectName`, `nodeId`, `nodeTitle`, `path`, `content`, `comment` |
| `save_article` / `save_article_by_title` | `projectId`, `nodeId`, `nodeTitle`, `previousLength`, `savedLength` |
| `import_markdown_as_project` | `projectId`, `projectName`, `nodeCount` |
| `search_fulltext` | `keyword`, `results[]`, `byProject[]`, `totalMatches`, `shownCount`, `hint` |

See each tool's `outputSchema` in `tools/list` for the exact definition.

---

### Notes

- Quality of results depends on the connected AI agent's capabilities.
- Tested with Claude Desktop (Claude Sonnet 4.5).
- Workspace paths with spaces or non-ASCII characters are supported.
