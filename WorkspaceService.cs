using Newtonsoft.Json;

namespace ManidocMCP;

public static class WorkspaceService
{
    private const string SettingsFile = "workspace.settings.json";

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
            if (Path.GetFileName(file) == SettingsFile) continue;
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

    public static void SaveProject(ManidocProject project)
    {
        var wp = GetWorkspacePath();
        foreach (var file in Directory.GetFiles(wp, "*.json"))
        {
            if (Path.GetFileName(file) == SettingsFile) continue;
            try
            {
                var content = File.ReadAllText(file);
                var p = JsonConvert.DeserializeObject<ManidocProject>(content);
                if (p?.Id == project.Id)
                {
                    project.LastModifiedAt = DateTime.UtcNow.ToString("o");
                    File.WriteAllText(file, JsonConvert.SerializeObject(project, Formatting.Indented));
                    return;
                }
            }
            catch { }
        }
        throw new InvalidOperationException($"Project file not found: {project.Id}");
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
}
