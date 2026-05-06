using System.ComponentModel;
using ModelContextProtocol.Server;
using Newtonsoft.Json;

namespace ManidocMCP;

[McpServerToolType]
public class ManidocTools
{
    [McpServerTool(Name = "get_server_status")]
    [Description("Returns the current status of the Manidoc MCP server: workspace path and number of projects. Use this to verify the server is running correctly.")]
    public string GetServerStatus()
    {
        var lines = new List<string>();

        // ワークスペース確認
        try
        {
            var wp = WorkspaceService.GetWorkspacePath();
            var count = WorkspaceService.GetAllProjects().Count;
            lines.Add($"Workspace: {wp}");
            lines.Add($"Projects: {count}");
        }
        catch (Exception ex)
        {
            lines.Add($"Workspace: ERROR — {ex.Message}");
        }

        lines.Add($"Server: OK");
        return string.Join("\n", lines);
    }

    [McpServerTool(Name = "list_projects")]
    [Description("Returns a list of projects in the Manidoc workspace. Each entry includes id, name, and tag.")]
    public string ListProjects()
    {
        var projects = WorkspaceService.GetAllProjects()
            .Select(p => new { id = p.Id, name = p.Name, tag = p.Tag ?? "" });
        return JsonConvert.SerializeObject(projects, Formatting.Indented);
    }

    [McpServerTool(Name = "list_nodes")]
    [Description("Returns a list of nodes (titles) in the specified project. Each entry includes id, title, and hierarchical path.")]
    public string ListNodes(
        [Description("Project ID")] string project_id)
    {
        var project = WorkspaceService.GetProject(project_id)
            ?? throw new InvalidOperationException("Project not found");
        var nodes = WorkspaceService.FlattenNodes(project.RootNodes ?? []);
        return JsonConvert.SerializeObject(nodes, Formatting.Indented);
    }

    [McpServerTool(Name = "get_article")]
    [Description("Returns the article (Markdown) of the specified node.")]
    public string GetArticle(
        [Description("Project ID")] string project_id,
        [Description("Node ID")] string node_id)
    {
        var project = WorkspaceService.GetProject(project_id)
            ?? throw new InvalidOperationException("Project not found");
        var node = WorkspaceService.FindNode(project.RootNodes ?? [], node_id)
            ?? throw new InvalidOperationException("Node not found");
        return node.Article ?? "";
    }

    [McpServerTool(Name = "save_article")]
    [Description("Overwrites the article of the specified node with Markdown content. Existing content will be replaced.")]
    public string SaveArticle(
        [Description("Project ID")] string project_id,
        [Description("Node ID")] string node_id,
        [Description("Markdown content to save")] string content)
    {
        var project = WorkspaceService.GetProject(project_id)
            ?? throw new InvalidOperationException("Project not found");
        var node = WorkspaceService.FindNode(project.RootNodes ?? [], node_id)
            ?? throw new InvalidOperationException("Node not found");
        node.Article = content;
        WorkspaceService.SaveProject(project);
        return $"Saved: {project.Name} / {node.Title}";
    }

    [McpServerTool(Name = "get_article_by_title")]
    [Description("Returns the article (Markdown) by project name and node title. Both support partial matching.")]
    public string GetArticleByTitle(
        [Description("Project name (partial match)")] string project_name,
        [Description("Node title (partial match)")] string node_title)
    {
        var project = WorkspaceService.GetAllProjects()
            .FirstOrDefault(p => p.Name.Contains(project_name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Project \"{project_name}\" not found");
        var flat = WorkspaceService.FlattenNodes(project.RootNodes ?? []);
        var matched = flat.FirstOrDefault(n => n.Title.Contains(node_title, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Node \"{node_title}\" not found");
        var node = WorkspaceService.FindNode(project.RootNodes ?? [], matched.Id)
            ?? throw new InvalidOperationException("Node not found");
        return node.Article ?? "";
    }

    [McpServerTool(Name = "save_article_by_title")]
    [Description("Saves an article in Markdown by project name and node title. Both support partial matching.")]
    public string SaveArticleByTitle(
        [Description("Project name (partial match)")] string project_name,
        [Description("Node title (partial match)")] string node_title,
        [Description("Markdown content to save")] string content)
    {
        var project = WorkspaceService.GetAllProjects()
            .FirstOrDefault(p => p.Name.Contains(project_name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Project \"{project_name}\" not found");
        var flat = WorkspaceService.FlattenNodes(project.RootNodes ?? []);
        var matched = flat.FirstOrDefault(n => n.Title.Contains(node_title, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Node \"{node_title}\" not found");
        var node = WorkspaceService.FindNode(project.RootNodes ?? [], matched.Id)
            ?? throw new InvalidOperationException("Node not found");
        node.Article = content;
        WorkspaceService.SaveProject(project);
        return $"Saved: {project.Name} / {node.Title}";
    }

    [McpServerTool(Name = "import_markdown_as_project")]
    [Description("Imports Markdown text as a new Manidoc project. H1 becomes the project name, H2+ headings become nodes (hierarchical), blockquotes go to node.comment, paragraphs/code/lists go to node.article.")]
    public string ImportMarkdownAsProject(
        [Description("Markdown text to import")] string markdown_text)
    {
        var wp = WorkspaceService.GetWorkspacePath();
        var project = MarkdownImporter.Import(markdown_text, wp);
        WorkspaceService.SaveNewProject(project);
        int nodeCount = WorkspaceService.CountNodes(project.RootNodes);
        return $"Imported successfully.\nProject: {project.Name}\nID: {project.Id}\nNodes: {nodeCount}";
    }

    [McpServerTool(Name = "search_fulltext")]
    [Description("Searches all projects (or a specific project) for a keyword. Searches project names, node titles, article body, and comments.")]
    public string SearchFulltext(
        [Description("Search keyword (case-insensitive)")] string keyword,
        [Description("Limit search to a specific project ID (optional)")] string? project_id = null,
        [Description("Max number of results to return (default: 30)")] int max_results = 30)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            throw new InvalidOperationException("keyword is empty");

        var (shown, summary, totalMatches) = SearchService.Search(keyword, project_id, max_results);
        if (totalMatches == 0)
            return $"No results found for: \"{keyword}\"";

        var output = new Dictionary<string, object>
        {
            ["results"] = shown,
            ["summary"] = summary.Select(s => new { s.ProjectId, s.ProjectName, s.Total, s.Shown, s.Omitted }),
            ["totalMatches"] = totalMatches,
            ["shownCount"] = shown.Count,
        };
        if (shown.Count < totalMatches)
            output["hint"] = $"{totalMatches - shown.Count}件を省略。project_id を指定して再検索するか max_results を増やしてください。";

        return JsonConvert.SerializeObject(output, Formatting.Indented);
    }
}
