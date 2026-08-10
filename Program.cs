using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using ManidocMCP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

var builder = Host.CreateApplicationBuilder(args);

// System.Text.Json の既定エンコーダは非 ASCII を \uXXXX に逃がす。
// これはブラウザに埋め込む用途向けの安全策で、MCP の stdio では不要。
// 日本語が 1 文字 6 バイトに膨れ、ツール結果がクライアント側の文字数上限で
// 切られるうえ、小型ローカル LLM がエスケープ列を復元できず本文を読み違える。
// 日本語(と全 Unicode)、および階層パスの区切りに使う ">" をそのまま出す。
// Relaxed は「JSON を HTML に直接埋め込むと危険」という意味で、JSON-RPC を
// パーサ経由で読む MCP では該当しない。
var toolJsonOptions = new JsonSerializerOptions(McpJsonUtilities.DefaultOptions)
{
    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
};

// stdout は JSON-RPC 専用。MCP 2026-07-28 で logging 機能（notifications/message）が
// 非推奨になったため、診断ログは stderr に出す（仕様が推奨する移行先）。
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInfo = new Implementation
        {
            Name = "manidoc",
            Title = "Manidoc MCP Server",
            Version = BuildInfo.Version,
            Description = "Read and write Manidoc projects, articles and comments from an MCP client.",
            WebsiteUrl = "https://github.com/ichiroabe/manidocMCP",
        };

        options.ServerInstructions =
            """
            Manidoc のワークスペース（MANIDOC_WORKSPACE 環境変数で指定したフォルダ）にある
            プロジェクト JSON を直接読み書きするサーバーです。

            - プロジェクトは複数のノードからなる木構造で、各ノードが article（Markdown 本文）と
              comment（補足メモ）を持ちます。
            - ID が分かっている場合は get_article / save_article、分からない場合は
              search_fulltext または list_projects → list_nodes で辿ってから使ってください。
            - 追記したいときは append_article を使ってください。save_article /
              save_article_by_title は既存の本文を丸ごと置き換えるため、書き戻さなかった
              部分は失われます。
            - 既存プロジェクトにページを足すときは add_node です。
              import_markdown_as_project は必ず新しいプロジェクトを作ります。
            - タイトル指定のツールは候補が複数あると中断し、project_id と node_id 付きの
              候補一覧を返します。そのどれかを選んで ID 指定のツールを呼んでください。
            - すべてのツールは structuredContent を返します。outputSchema を参照してください。
            """;

        // SEP-2549: tools/list はキャッシュ可能な結果。ツール一覧はプロセス生存中は不変なので
        // 明示的に TTL を与える。ワークスペースの内容には依存しないが、利用者ごとの
        // インストールに紐づくため cacheScope は private とする。
        options.Filters.Request.ListToolsFilters.Add(next => async (request, cancellationToken) =>
        {
            var result = await next(request, cancellationToken);

            // 2026-07-28 の推奨: クライアント側キャッシュとプロンプトキャッシュを効かせるため
            // tools/list は決定的な順序で返す。
            result.Tools = [.. result.Tools.OrderBy(tool => tool.Name, StringComparer.Ordinal)];
            result.TimeToLive = TimeSpan.FromMinutes(60);
            result.CacheScope = CacheScope.Private;
            return result;
        });
    })
    .WithStdioServerTransport()
    .WithTools<ManidocTools>(toolJsonOptions);

await builder.Build().RunAsync();
