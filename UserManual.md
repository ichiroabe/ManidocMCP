# Manidoc MCP Server (macOS) — ユーザーマニュアル / User Manual

---

## 目次 / Table of Contents

- [日本語](#日本語)
- [English](#english)

---

## 日本語

### 概要

Manidoc MCP Server (macOS版) は、ドキュメント管理アプリ [Manidoc](https://github.com/ichiroabe/manidoc) のデータをAIエージェントから読み書きするための MCP（Model Context Protocol）サーバーです。

Claude Desktop などのMCP対応AIクライアントと連携することで、以下のことが自然言語で行えます。

- Manidoc のプロジェクト・記事の閲覧・編集
- Markdown テキストからのプロジェクト一括インポート
- 全文検索

---

### 必要なもの

| 項目 | 内容 |
| --- | --- |
| OS | macOS 13 (Ventura) 以降 |
| ランタイム | .NET 8.0 SDK 以上 |
| Manidoc | インストール済みであること |
| AIクライアント | Claude Desktop など MCP 対応のもの |

#### .NET 8.0 SDK のインストール

```bash
brew install dotnet@8
```

---

### インストール手順

#### 1. ビルドとインストール

```bash
cd /Volumes/SDD/AIProject/manidocMCP_MAC
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

| ツール名 | 説明 |
| --- | --- |
| `get_server_status` | サーバーの動作確認。ワークスペースパス・プロジェクト数を返す |

#### ドキュメント操作

| ツール名 | 説明 |
| --- | --- |
| `list_projects` | ワークスペース内の全プロジェクト一覧を返す |
| `list_nodes` | 指定プロジェクトのノード（見出し）一覧を返す |
| `get_article` | プロジェクトID・ノードID で記事（Markdown）を取得 |
| `save_article` | プロジェクトID・ノードID で記事（Markdown）を上書き保存 |
| `get_article_by_title` | プロジェクト名・ノードタイトルの部分一致で記事を取得 |
| `save_article_by_title` | プロジェクト名・ノードタイトルの部分一致で記事を保存 |
| `import_markdown_as_project` | Markdown テキストを新規プロジェクトとして一括インポート |
| `search_fulltext` | 全プロジェクトを対象にキーワード全文検索 |

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

Manidoc MCP Server (macOS edition) is an MCP (Model Context Protocol) server that allows AI agents to read and write data in [Manidoc](https://github.com/ichiroabe/manidoc), a document management application.

By integrating with MCP-compatible AI clients such as Claude Desktop, you can use natural language to:

- Browse and edit Manidoc projects and articles
- Bulk-import projects from Markdown text
- Perform full-text search

---

### Requirements

| Item | Details |
| --- | --- |
| OS | macOS 13 (Ventura) or later |
| Runtime | .NET 8.0 SDK or later |
| Manidoc | Must be installed |
| AI Client | Claude Desktop or any MCP-compatible client |

#### Installing .NET 8.0 SDK

```bash
brew install dotnet@8
```

---

### Installation

#### 1. Build and Install

```bash
cd /path/to/manidocMCP_MAC
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

### Tool Reference

#### Server Status

| Tool | Description |
| --- | --- |
| `get_server_status` | Verifies the server is running. Returns workspace path and project count. |

#### Document Operations

| Tool | Description |
| --- | --- |
| `list_projects` | Returns a list of all projects in the workspace |
| `list_nodes` | Returns a list of nodes (headings) in a specified project |
| `get_article` | Retrieves an article (Markdown) by project ID and node ID |
| `save_article` | Overwrites an article by project ID and node ID |
| `get_article_by_title` | Retrieves an article by partial match on project name and node title |
| `save_article_by_title` | Saves an article by partial match on project name and node title |
| `import_markdown_as_project` | Imports Markdown text as a new project |
| `search_fulltext` | Full-text keyword search across all projects |

---

### Notes

- Quality of results depends on the connected AI agent's capabilities.
- Tested with Claude Desktop (Claude Sonnet 4.5).
- Workspace paths with spaces or non-ASCII characters are supported.
