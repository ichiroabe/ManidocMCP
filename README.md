# Manidoc MCP Server (macOS)

ドキュメント管理アプリ [Manidoc](https://github.com/ichiroabe/manidoc) のデータを、Claude Desktop などの AI エージェントから読み書きするための MCP (Model Context Protocol) サーバーの macOS 版です。

**English:** The macOS build of the MCP server that lets AI agents (e.g. Claude Desktop) read and write data of the Manidoc document-management app — browse/edit projects and articles, bulk-import Markdown, and full-text search. Full English manual: [UserManual.md](UserManual.md).

## できること

- Manidoc のプロジェクト・記事の閲覧・編集
- Markdown テキストからのプロジェクト一括インポート
- 全文検索

AIエージェントの「永続的な知識置き場・成果物置き場」として機能します。API キー不要で、ローカル LLM とも連携できます。
※ 動画生成機能は Windows 版（[manidocMCP_CS](https://github.com/ichiroabe/manidocMCP_CS)）のみです。

## 対応する MCP 仕様

最新仕様 **2026-07-28** に対応しています（C# SDK `ModelContextProtocol` 2.1.0）。

| 項目 | 対応内容 |
| --- | --- |
| プロトコル | 2026-07-28。`initialize` ハンドシェイクを持たないステートレス方式で、各リクエストが `_meta` でバージョンとクライアント能力を宣言します |
| `server/discover` | 対応。クライアントは接続前にサーバーの対応バージョン・能力・識別情報を取得できます |
| 後方互換 | 2025-11-25 以前のクライアント（従来の `initialize` ハンドシェイク）もそのまま接続できます |
| 構造化出力 | 全ツールが `outputSchema` を宣言し、結果を `structuredContent` として返します（従来どおりテキストも同時に返すため、古いクライアントでも読めます） |
| ツール注釈 | 全ツールが `readOnlyHint` / `destructiveHint` / `idempotentHint` / `openWorldHint` を宣言します |
| キャッシュヒント | `tools/list` は `ttlMs`（60分）と `cacheScope` を返し、ツールは名前順の決定的な順序で返します |
| ログ | 仕様で logging 機能が非推奨になったため、診断ログは stderr に出力します（stdout は JSON-RPC 専用） |

## リリース成果物

`v*` タグを push すると GitHub Actions が以下をビルドして Release に添付します。いずれも
self-contained（.NET ランタイム同梱）の単一実行ファイルなので、**.NET SDK なしで動きます**。

| ファイル | 対象 |
| --- | --- |
| `ManidocMCP-osx-arm64.tar.gz` | macOS / Apple Silicon |
| `ManidocMCP-osx-x64.tar.gz` | macOS / Intel |
| `ManidocMCP-win-x64.zip` | Windows / x64 |
| `ManidocMCP-win-arm64.zip` | Windows / Arm64 |

本体は `net8.0` でプラットフォーム依存のコードを持たないため、同じソースが macOS でも
Windows でも動きます。ソースからビルドしたい場合は下記の手順に従ってください。

> Windows で動画生成機能まで使いたい場合は [manidocMCP_CS](https://github.com/ichiroabe/manidocMCP_CS) を使ってください。

## 必要なもの

| 項目 | 内容 |
| --- | --- |
| OS | macOS 13 (Ventura) 以降 |
| ランタイム | .NET 8.0 SDK 以上 |
| Manidoc | インストール済みであること |
| AIクライアント | Claude Desktop など MCP 対応クライアント |

### .NET 8.0 SDK のインストール

```bash
brew install dotnet@8
```

## セットアップ

### 1. クローンとインストール

```bash
git clone https://github.com/ichiroabe/manidocMCP_MAC
cd manidocMCP_MAC
bash installer/install.sh
```

### 2. Claude Desktop との接続

`~/Library/Application Support/Claude/claude_desktop_config.json`（Claude Desktop の Settings → Developer → Edit Config）に追記してアプリを再起動します:

```json
{
  "mcpServers": {
    "manidoc": {
      "command": "/Users/yourname/Applications/ManidocMCP/ManidocMCP",
      "env": {
        "MANIDOC_WORKSPACE": "/Users/yourname/Google Drive/マイドライブ/manidocAndroid"
      }
    }
  }
}
```

- `MANIDOC_WORKSPACE` には Manidoc のワークスペースフォルダを指定します（Google Drive 同期フォルダを指定すれば、デスクトップ/モバイルと同じ知識ベースを共有できます）。

## 使い方

接続後、Claude にそのまま日本語で話しかけるだけです:

- 「Manidoc のプロジェクト一覧を見せて」 → `list_projects`
- 「◯◯について全文検索して」 → `search_fulltext`
- 「この会話の内容を記事『◯◯』として保存して」 → `save_article_by_title`
- 「この Markdown をプロジェクトとして取り込んで」 → `import_markdown_as_project`

## MCP ツール一覧

| ツール | 機能 | 注釈 |
| --- | --- | --- |
| `get_server_status` | サーバー状態の確認（ワークスペース・プロジェクト数） | 読み取り専用 |
| `list_projects` | プロジェクト一覧 | 読み取り専用 |
| `list_nodes` | プロジェクト内のノード一覧 | 読み取り専用 |
| `get_article` / `get_article_by_title` | ID / タイトル指定で記事を取得 | 読み取り専用 |
| `save_article` / `save_article_by_title` | ID / タイトル指定で記事を保存 | **上書き（destructive）** |
| `import_markdown_as_project` | Markdown をプロジェクトとして一括インポート | 新規作成 |
| `search_fulltext` | 全文検索 | 読み取り専用 |

保存系ツールは既存の本文を丸ごと置き換えます。追記したい場合は先に `get_article` で現在の内容を取得してください。

詳細な仕様・注意事項は [UserManual.md](UserManual.md)（日本語 / English）を参照してください。

## 関連リンク

- **Manidoc 本体（Windows アプリ）**: [GitHub](https://github.com/ichiroabe/manidoc) / [Microsoft Store](https://apps.microsoft.com/detail/9n578k2wqxqn)
- **Windows 版 MCP サーバー**: [manidocMCP_CS](https://github.com/ichiroabe/manidocMCP_CS)

## サポート

個人開発のため手厚いサポートは難しく、返信は主に週末・休日となります。
お問い合わせ: manidoc@fusion.upper.jp
