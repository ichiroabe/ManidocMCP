#!/bin/bash
# Manidoc MCP Server — macOS インストールスクリプト
# 使い方: bash install.sh

set -e

INSTALL_DIR="$HOME/Applications/ManidocMCP"
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

echo "=== Manidoc MCP Server インストーラー (macOS) ==="
echo ""

# .NET SDK の確認
if ! command -v dotnet &> /dev/null; then
    echo "エラー: .NET SDK がインストールされていません。"
    echo "  brew install dotnet@8"
    echo "でインストールしてください。"
    exit 1
fi

DOTNET_VERSION=$(dotnet --version)
echo ".NET SDK バージョン: $DOTNET_VERSION"

# CPU アーキテクチャの判定（Apple Silicon / Intel）
if [ "$(uname -m)" = "arm64" ]; then
    RID="osx-arm64"
else
    RID="osx-x64"
fi
echo "ターゲット: $RID"

# ビルド
echo ""
echo "プロジェクトをビルドしています..."
dotnet publish "$PROJECT_DIR" -c Release -r "$RID" --self-contained true -o "$INSTALL_DIR" /p:PublishSingleFile=true

# appsettings.json コピー
if [ -f "$PROJECT_DIR/appsettings.json" ]; then
    echo "appsettings.json をコピーしています..."
    cp "$PROJECT_DIR/appsettings.json" "$INSTALL_DIR/appsettings.json"
fi

# 実行権限の付与
chmod +x "$INSTALL_DIR/ManidocMCP"

echo ""
echo "=== インストール完了 ==="
echo "インストール先: $INSTALL_DIR"
echo ""
echo "Claude Desktop の設定例:"
echo "  ファイル: ~/Library/Application Support/Claude/claude_desktop_config.json"
echo ""
echo '  {'
echo '    "mcpServers": {'
echo '      "manidoc": {'
echo "        \"command\": \"$INSTALL_DIR/ManidocMCP\","
echo '        "env": {'
echo '          "MANIDOC_WORKSPACE": "/path/to/your/ManidocData"'
echo '        }'
echo '      }'
echo '    }'
echo '  }'
echo ""
echo "MANIDOC_WORKSPACE にManidocのデータフォルダのパスを設定してください。"
