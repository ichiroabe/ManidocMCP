# Manidoc MCP Server (macOS)

ドキュメント管理アプリ [Manidoc](https://github.com/ichiroabe/manidoc) のデータを、Claude Desktop などの AI エージェントから読み書きするための MCP (Model Context Protocol) サーバーの macOS 版です。

**English:** The macOS build of the MCP server that lets AI agents (e.g. Claude Desktop) read and write data of the Manidoc document-management app — browse/edit projects and articles, bulk-import Markdown, and full-text search. Full English manual: [UserManual.md](UserManual.md).

## できること

- Manidoc のプロジェクト・記事の閲覧・編集
- Markdown テキストからのプロジェクト一括インポート
- 全文検索

AIエージェントの「永続的な知識置き場・成果物置き場」として機能します。API キー不要で、ローカル LLM とも連携できます。
※ 動画生成機能は Windows 版（[manidocMCP_CS](https://github.com/ichiroabe/manidocMCP_CS)）のみです。

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

| ツール | 機能 |
| --- | --- |
| `get_server_status` | サーバー状態の確認（ワークスペース・プロジェクト数） |
| `list_projects` | プロジェクト一覧 |
| `list_nodes` | プロジェクト内のノード一覧 |
| `get_article` / `save_article` | ID 指定で記事の取得・保存 |
| `get_article_by_title` / `save_article_by_title` | タイトル指定で記事の取得・保存 |
| `import_markdown_as_project` | Markdown をプロジェクトとして一括インポート |
| `search_fulltext` | 全文検索 |

詳細な仕様・注意事項は [UserManual.md](UserManual.md)（日本語 / English）を参照してください。

## 関連リンク

- **Manidoc 本体（Windows アプリ）**: [GitHub](https://github.com/ichiroabe/manidoc) / [Microsoft Store](https://apps.microsoft.com/detail/9n578k2wqxqn)
- **Windows 版 MCP サーバー**: [manidocMCP_CS](https://github.com/ichiroabe/manidocMCP_CS)

## サポート

個人開発のため手厚いサポートは難しく、返信は主に週末・休日となります。
お問い合わせ: manidoc@fusion.upper.jp
