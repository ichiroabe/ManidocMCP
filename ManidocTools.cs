using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace ManidocMCP;

/// <summary>
/// Manidoc ワークスペースを操作する MCP ツール群。
/// すべてローカルのファイルしか触らないため OpenWorld = false、
/// 参照系は ReadOnly = true / Idempotent = true を宣言する（MCP のツール注釈）。
/// </summary>
[McpServerToolType]
public class ManidocTools
{
    [McpServerTool(
        Name = "get_server_status",
        Title = "サーバー状態の確認",
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns the current status of the Manidoc MCP server: workspace path and number of projects. Use this to verify the server is running correctly.")]
    public ServerStatusResult GetServerStatus()
    {
        var version = BuildInfo.Version;

        try
        {
            var workspace = WorkspaceService.GetWorkspacePath();
            return new ServerStatusResult
            {
                Ok = true,
                Workspace = workspace,
                ProjectCount = WorkspaceService.GetAllProjects().Count,
                ServerVersion = version,
            };
        }
        catch (Exception ex)
        {
            // ワークスペース未設定は「サーバーが落ちている」わけではないので、
            // ツールエラーではなく状態として返す。
            return new ServerStatusResult
            {
                Ok = false,
                ServerVersion = version,
                Error = ex.Message,
            };
        }
    }

    [McpServerTool(
        Name = "list_projects",
        Title = "プロジェクト一覧",
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns a list of projects in the Manidoc workspace. Each entry includes id, name, and tag.")]
    public ListProjectsResult ListProjects()
    {
        var projects = WorkspaceService.GetAllProjects()
            .Select(p => new ProjectSummary { Id = p.Id, Name = p.Name, Tag = p.Tag ?? "" })
            .ToList();

        return new ListProjectsResult { Projects = projects, Count = projects.Count };
    }

    [McpServerTool(
        Name = "list_nodes",
        Title = "ノード一覧",
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns a list of nodes (titles) in the specified project. Each entry includes id, title, and hierarchical path.")]
    public ListNodesResult ListNodes(
        [Description("Project ID")] string project_id)
    {
        var project = RequireProject(project_id);
        var nodes = WorkspaceService.FlattenNodes(project.RootNodes ?? [])
            .Select(n => new NodeSummary { Id = n.Id, Title = n.Title, Path = n.Path })
            .ToList();

        return new ListNodesResult
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            Nodes = nodes,
            Count = nodes.Count,
        };
    }

    [McpServerTool(
        Name = "get_article",
        Title = "記事の取得（ID 指定）",
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns the article (Markdown) of the specified node.")]
    public ArticleResult GetArticle(
        [Description("Project ID")] string project_id,
        [Description("Node ID")] string node_id)
    {
        var (project, node, path) = ResolveById(project_id, node_id);
        return ToArticleResult(project, node, path);
    }

    [McpServerTool(
        Name = "get_article_by_title",
        Title = "記事の取得（タイトル指定）",
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns the article (Markdown) by project name and node title. Both support partial matching.")]
    public ArticleResult GetArticleByTitle(
        [Description("Project name (partial match)")] string project_name,
        [Description("Node title (partial match)")] string node_title)
    {
        var (project, node, path) = ResolveByTitle(project_name, node_title);
        return ToArticleResult(project, node, path);
    }

    [McpServerTool(
        Name = "save_article",
        Title = "記事の保存（ID 指定）",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Overwrites the article of the specified node with Markdown content. Existing content will be replaced — call get_article first if you intend to append.")]
    public SaveArticleResult SaveArticle(
        [Description("Project ID")] string project_id,
        [Description("Node ID")] string node_id,
        [Description("Markdown content to save")] string content)
    {
        var (project, node, _) = ResolveById(project_id, node_id);
        return Save(project, node, content);
    }

    [McpServerTool(
        Name = "save_article_by_title",
        Title = "記事の保存（タイトル指定）",
        ReadOnly = false,
        Destructive = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Saves an article in Markdown by project name and node title. Both support partial matching. Existing content will be replaced.")]
    public SaveArticleResult SaveArticleByTitle(
        [Description("Project name (partial match)")] string project_name,
        [Description("Node title (partial match)")] string node_title,
        [Description("Markdown content to save")] string content)
    {
        var (project, node, _) = ResolveByTitle(project_name, node_title);
        return Save(project, node, content);
    }

    [McpServerTool(
        Name = "import_markdown_as_project",
        Title = "Markdown をプロジェクトとして取り込み",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Imports Markdown text as a new Manidoc project. H1 becomes the project name, H2+ headings become nodes (hierarchical), blockquotes go to node.comment, paragraphs/code/lists go to node.article.")]
    public ImportProjectResult ImportMarkdownAsProject(
        [Description("Markdown text to import")] string markdown_text)
    {
        if (string.IsNullOrWhiteSpace(markdown_text))
            throw new McpException("markdown_text is empty");

        var workspace = WorkspaceService.GetWorkspacePath();
        var project = MarkdownImporter.Import(markdown_text, workspace);
        WorkspaceService.SaveNewProject(project);

        return new ImportProjectResult
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            NodeCount = WorkspaceService.CountNodes(project.RootNodes),
        };
    }

    [McpServerTool(
        Name = "search_fulltext",
        Title = "全文検索",
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Searches all projects (or a specific project) for a keyword. Searches project names, node titles, article body, and comments.")]
    public SearchResultSet SearchFulltext(
        [Description("Search keyword (case-insensitive)")] string keyword,
        [Description("Limit search to a specific project ID (optional)")] string? project_id = null,
        [Description("Max number of results to return (default: 30)")] int max_results = 30)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            throw new McpException("keyword is empty");
        if (max_results <= 0)
            throw new McpException("max_results must be greater than 0");

        var (shown, summary, totalMatches) = SearchService.Search(keyword, project_id, max_results);

        return new SearchResultSet
        {
            Keyword = keyword,
            Results = [.. shown.Select(r => new SearchHit
            {
                ProjectId = r.ProjectId,
                ProjectName = r.ProjectName,
                NodeId = r.NodeId,
                NodeTitle = r.NodeTitle,
                NodePath = r.NodePath,
                Area = r.Area,
                Snippet = r.Snippet,
            })],
            ByProject = [.. summary.Select(s => new SearchProjectSummary
            {
                ProjectId = s.ProjectId,
                ProjectName = s.ProjectName,
                Total = s.Total,
                Shown = s.Shown,
                Omitted = s.Omitted,
            })],
            TotalMatches = totalMatches,
            ShownCount = shown.Count,
            Hint = shown.Count < totalMatches
                ? $"{totalMatches - shown.Count} 件を省略しました。project_id を指定して絞り込むか、max_results を増やしてください。"
                : null,
        };
    }

    // --- ヘルパー ---

    private static ManidocProject RequireProject(string projectId)
        => WorkspaceService.GetProject(projectId)
           ?? throw new McpException($"Project not found: {projectId}. Call list_projects to see available IDs.");

    private static (ManidocProject Project, ManidocNode Node, string Path) ResolveById(string projectId, string nodeId)
    {
        var project = RequireProject(projectId);
        var node = WorkspaceService.FindNode(project.RootNodes ?? [], nodeId)
            ?? throw new McpException($"Node not found: {nodeId}. Call list_nodes to see available IDs.");
        var path = PathOf(project, nodeId) ?? node.Title;
        return (project, node, path);
    }

    private static (ManidocProject Project, ManidocNode Node, string Path) ResolveByTitle(string projectName, string nodeTitle)
    {
        var project = WorkspaceService.GetAllProjects()
            .FirstOrDefault(p => p.Name.Contains(projectName, StringComparison.OrdinalIgnoreCase))
            ?? throw new McpException($"Project \"{projectName}\" not found. Call list_projects to see available names.");

        var matched = WorkspaceService.FlattenNodes(project.RootNodes ?? [])
            .FirstOrDefault(n => n.Title.Contains(nodeTitle, StringComparison.OrdinalIgnoreCase))
            ?? throw new McpException($"Node \"{nodeTitle}\" not found in project \"{project.Name}\". Call list_nodes to see available titles.");

        var node = WorkspaceService.FindNode(project.RootNodes ?? [], matched.Id)
            ?? throw new McpException($"Node not found: {matched.Id}");

        return (project, node, matched.Path);
    }

    private static string? PathOf(ManidocProject project, string nodeId)
        => WorkspaceService.FlattenNodes(project.RootNodes ?? [])
            .FirstOrDefault(n => n.Id == nodeId)?.Path;

    private static ArticleResult ToArticleResult(ManidocProject project, ManidocNode node, string path)
        => new()
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            NodeId = node.Id,
            NodeTitle = node.Title,
            Path = path,
            Content = node.Article ?? "",
            Comment = node.Comment ?? "",
        };

    private static SaveArticleResult Save(ManidocProject project, ManidocNode node, string content)
    {
        var previousLength = (node.Article ?? "").Length;
        node.Article = content;
        WorkspaceService.SaveProject(project);

        return new SaveArticleResult
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            NodeId = node.Id,
            NodeTitle = node.Title,
            PreviousLength = previousLength,
            SavedLength = content.Length,
        };
    }
}
