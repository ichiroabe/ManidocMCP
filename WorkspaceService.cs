using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ManidocMCP;

public static class WorkspaceService
{
    private const string SettingsFile = "workspace.settings.json";

    // タイル色のミラー(openManidoc 互換)。本家Manidoc はプロジェクトの未知フィールドを
    // 保存時に落とすため、色の控えをこのファイルに持ち、剥がされていたら読み込み時に補う。
    // プロジェクトファイルではないので列挙対象からは除外する。
    private const string ColorsFile = "project.colors.json";

    public static string GetWorkspacePath()
    {
        var wp = Environment.GetEnvironmentVariable("MANIDOC_WORKSPACE");
        if (string.IsNullOrEmpty(wp))
            throw new InvalidOperationException("MANIDOC_WORKSPACE environment variable is not set");
        if (!Directory.Exists(wp))
            throw new InvalidOperationException($"Workspace not found: {wp}");
        return wp;
    }

    public static List<ManidocProject> GetAllProjects()
    {
        var wp = GetWorkspacePath();
        var projects = new List<ManidocProject>();
        foreach (var file in Directory.GetFiles(wp, "*.json"))
        {
            if (IsReservedFile(file)) continue;
            try
            {
                var content = File.ReadAllText(file);
                var p = JsonConvert.DeserializeObject<ManidocProject>(content);
                if (p?.Id is { Length: > 0 }) projects.Add(p);
            }
            catch { /* 壊れたファイルはスキップ */ }
        }
        return [.. projects.OrderBy(p => p.SortOrder)];
    }

    public static ManidocProject? GetProject(string projectId)
        => GetAllProjects().FirstOrDefault(p => p.Id == projectId);

    /// <summary>
    /// [touch] が false のときは lastModifiedAt を更新しない。タグ・タイル色のような
    /// 本文を書き換えない属性変更で「最終更新」を動かさないため（openManidoc と同じ扱い）。
    /// </summary>
    public static void SaveProject(ManidocProject project, bool touch = true)
    {
        var wp = GetWorkspacePath();
        foreach (var file in Directory.GetFiles(wp, "*.json"))
        {
            if (IsReservedFile(file)) continue;
            try
            {
                var content = File.ReadAllText(file);
                var p = JsonConvert.DeserializeObject<ManidocProject>(content);
                if (p?.Id == project.Id)
                {
                    if (touch) project.LastModifiedAt = DateTime.UtcNow.ToString("o");
                    File.WriteAllText(file, JsonConvert.SerializeObject(project, Formatting.Indented));
                    return;
                }
            }
            catch { }
        }
        throw new InvalidOperationException($"Project file not found: {project.Id}");
    }

    /// <summary>ワークスペースにあるがプロジェクトではない予約ファイルか。</summary>
    private static bool IsReservedFile(string file)
    {
        var name = Path.GetFileName(file);
        return name == SettingsFile || name == ColorsFile;
    }

    public static void SaveNewProject(ManidocProject project)
    {
        var wp = GetWorkspacePath();
        var filePath = Path.Combine(wp, $"{project.Id}.json");
        File.WriteAllText(filePath, JsonConvert.SerializeObject(project, Formatting.Indented));
    }

    public static List<FlatNode> FlattenNodes(List<ManidocNode> nodes, string parentPath = "")
    {
        var result = new List<FlatNode>();
        foreach (var node in nodes)
        {
            var currentPath = string.IsNullOrEmpty(parentPath) ? node.Title : $"{parentPath} > {node.Title}";
            result.Add(new FlatNode(node.Id, node.Title, currentPath));
            if (node.Children?.Count > 0)
                result.AddRange(FlattenNodes(node.Children, currentPath));
        }
        return result;
    }

    public static ManidocNode? FindNode(List<ManidocNode> nodes, string nodeId)
    {
        foreach (var node in nodes)
        {
            if (node.Id == nodeId) return node;
            var found = FindNode(node.Children ?? [], nodeId);
            if (found != null) return found;
        }
        return null;
    }

    public static int CountNodes(List<ManidocNode> nodes)
        => nodes.Sum(n => 1 + CountNodes(n.Children ?? []));

    // --- タイル色ミラー (project.colors.json) ---

    /// <summary>
    /// ミラーを読む。{ projectId: { "fore": "#rrggbb", "back": "#rrggbb" } }。
    /// 壊れていれば空を返す。
    /// </summary>
    public static Dictionary<string, (string Fore, string Back)> LoadCardColors()
    {
        var result = new Dictionary<string, (string, string)>();
        var path = Path.Combine(GetWorkspacePath(), ColorsFile);
        if (!File.Exists(path)) return result;
        try
        {
            var json = JObject.Parse(File.ReadAllText(path));
            foreach (var prop in json.Properties())
            {
                if (prop.Value is JObject o)
                    result[prop.Name] = (o["fore"]?.ToString() ?? "", o["back"]?.ToString() ?? "");
            }
        }
        catch { /* 壊れたミラーは無視 */ }
        return result;
    }

    /// <summary>
    /// プロジェクトJSONで色が空(本家に剥がされた等)のものを、ミラーの控えで補う。
    /// ディスクには書き戻さない(lastModifiedAt を動かさないため)。
    /// </summary>
    public static void FillColorsFromMirror(IEnumerable<ManidocProject> projects)
    {
        var mirror = LoadCardColors();
        if (mirror.Count == 0) return;
        foreach (var p in projects)
        {
            if (!mirror.TryGetValue(p.Id, out var c)) continue;
            if (string.IsNullOrEmpty(p.CardForeColor)) p.CardForeColor = c.Fore;
            if (string.IsNullOrEmpty(p.CardBackColor)) p.CardBackColor = c.Back;
        }
    }

    /// <summary>指定プロジェクトの色をミラーへ反映する(両方空ならエントリごと削除)。</summary>
    public static void UpdateCardColor(string projectId, string fore, string back)
    {
        var path = Path.Combine(GetWorkspacePath(), ColorsFile);
        var mirror = LoadCardColors();
        if (string.IsNullOrEmpty(fore) && string.IsNullOrEmpty(back))
            mirror.Remove(projectId);
        else
            mirror[projectId] = (fore, back);

        if (mirror.Count == 0)
        {
            if (File.Exists(path)) File.Delete(path);
            return;
        }

        var obj = new JObject();
        foreach (var kv in mirror)
            obj[kv.Key] = new JObject { ["fore"] = kv.Value.Fore, ["back"] = kv.Value.Back };
        File.WriteAllText(path, obj.ToString(Formatting.Indented));
    }

    // --- タグ定義 (workspace.settings.json の tags[]) ---

    /// <summary>workspace.settings.json の tags[] を読む(無ければ空)。</summary>
    public static List<TagDefinition> GetTags()
    {
        var path = Path.Combine(GetWorkspacePath(), SettingsFile);
        if (!File.Exists(path)) return [];
        try
        {
            var json = JObject.Parse(File.ReadAllText(path));
            if (json["tags"] is not JArray tags) return [];
            return [.. tags.OfType<JObject>().Select(o => new TagDefinition
            {
                Name = o["name"]?.ToString() ?? "",
                ImagePath = o["imagePath"]?.ToString() ?? "",
            })];
        }
        catch { return []; }
    }

    /// <summary>tags[] を保存する(settings.json の他のキーは保持する)。</summary>
    public static void SaveTags(List<TagDefinition> tags)
    {
        var path = Path.Combine(GetWorkspacePath(), SettingsFile);
        JObject json;
        try { json = File.Exists(path) ? JObject.Parse(File.ReadAllText(path)) : []; }
        catch { json = []; }

        json["tags"] = new JArray(tags.Select(t => new JObject
        {
            ["name"] = t.Name,
            ["imagePath"] = t.ImagePath,
        }));
        File.WriteAllText(path, json.ToString(Formatting.Indented));
    }
}
