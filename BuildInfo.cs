using System.Reflection;

namespace ManidocMCP;

/// <summary>
/// server/discover の _meta.serverInfo と get_server_status で同じバージョン文字列を使うためのヘルパー。
/// </summary>
public static class BuildInfo
{
    public static string Version { get; } = ResolveVersion();

    private static string ResolveVersion()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var informational = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        if (string.IsNullOrEmpty(informational))
            return assembly.GetName().Version?.ToString(3) ?? "0.0.0";

        // SourceLink が付ける "2.0.0+<commit sha>" のビルドメタデータを落とす
        var plus = informational.IndexOf('+');
        return plus < 0 ? informational : informational[..plus];
    }
}
