using ManidocMCP;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

var builder = Host.CreateApplicationBuilder(args);

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
            WebsiteUrl = "https://github.com/ichiroabe/manidocMCP_MAC",
        };

        options.ServerInstructions =
            """
            Manidoc のワークスペース（MANIDOC_WORKSPACE 環境変数で指定したフォルダ）にある
            プロジェクト JSON を直接読み書きするサーバーです。

            - プロジェクトは複数のノードからなる木構造で、各ノードが article（Markdown 本文）と
              comment（補足メモ）を持ちます。
            - ID が分かっている場合は get_article / save_article、分からない場合は
              search_fulltext または list_projects → list_nodes で辿ってから使ってください。
            - save_article / save_article_by_title は既存の本文を丸ごと置き換えます。
              追記したい場合は先に get_article で現在の内容を取得してください。
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
    .WithTools<ManidocTools>();

await builder.Build().RunAsync();
