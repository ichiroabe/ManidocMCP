#Requires -Version 5.1
<#
    Manidoc MCP Server — Windows インストールスクリプト

    使い方（PowerShell）:
        powershell -ExecutionPolicy Bypass -File installer\install.ps1

    ソースからビルドする場合のスクリプトです。ビルド済みバイナリを使う場合は
    Releases の ManidocMCP-win-x64.zip / ManidocMCP-win-arm64.zip を展開してください。
#>

$ErrorActionPreference = "Stop"

$InstallDir = Join-Path $env:LOCALAPPDATA "Programs\ManidocMCP"
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Split-Path -Parent $ScriptDir

Write-Host "=== Manidoc MCP Server インストーラー (Windows) ==="
Write-Host ""

# .NET SDK の確認
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host "エラー: .NET SDK がインストールされていません。" -ForegroundColor Red
    Write-Host "  winget install Microsoft.DotNet.SDK.8"
    Write-Host "でインストールしてください。"
    exit 1
}

Write-Host ".NET SDK バージョン: $(dotnet --version)"

# CPU アーキテクチャの判定（32bit プロセスから実行された場合も OS 側を見る）
$osArch = [System.Runtime.InteropServices.RuntimeInformation]::OSArchitecture
$rid = if ($osArch -eq [System.Runtime.InteropServices.Architecture]::Arm64) { "win-arm64" } else { "win-x64" }
Write-Host "ターゲット: $rid ($osArch)"

# ビルド
Write-Host ""
Write-Host "プロジェクトをビルドしています..."
dotnet publish $ProjectDir -c Release -r $rid --self-contained true -o $InstallDir -p:PublishSingleFile=true
if ($LASTEXITCODE -ne 0) {
    Write-Host "エラー: ビルドに失敗しました。" -ForegroundColor Red
    exit 1
}

# appsettings.json のコピー
$appSettings = Join-Path $ProjectDir "appsettings.json"
if (Test-Path $appSettings) {
    Write-Host "appsettings.json をコピーしています..."
    Copy-Item $appSettings (Join-Path $InstallDir "appsettings.json") -Force
}

$exePath = Join-Path $InstallDir "ManidocMCP.exe"

Write-Host ""
Write-Host "=== インストール完了 ==="
Write-Host "インストール先: $InstallDir"
Write-Host ""
Write-Host "Claude Desktop の設定例:"
Write-Host "  ファイル: %APPDATA%\Claude\claude_desktop_config.json"
Write-Host ""

# パス中のバックスラッシュは JSON でエスケープが必要なため、手書きせず
# ConvertTo-Json に任せる。
$config = [ordered]@{
    mcpServers = [ordered]@{
        manidoc = [ordered]@{
            command = $exePath
            env     = [ordered]@{
                MANIDOC_WORKSPACE = "C:\path\to\your\ManidocData"
            }
        }
    }
}
Write-Host ($config | ConvertTo-Json -Depth 5)

Write-Host ""
Write-Host "MANIDOC_WORKSPACE に Manidoc のデータフォルダのパスを設定してください。"
Write-Host "設定後、Claude Desktop を再起動すると有効になります。"
