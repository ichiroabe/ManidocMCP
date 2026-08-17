using System.ComponentModel;
using System.Text.RegularExpressions;
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
    [Description("Returns a list of projects in the Manidoc workspace. Each entry includes id, name, tag and the tile colors (cardForeColor / cardBackColor). Use tag and the tile colors to organize the workspace, then change them with set_project_attributes.")]
    public ListProjectsResult ListProjects()
    {
        var all = WorkspaceService.GetAllProjects();
        // 本家に色フィールドを剥がされていても、ミラーの控えで補って返す。
        WorkspaceService.FillColorsFromMirror(all);

        var projects = all
            .Select(p => new ProjectSummary
            {
                Id = p.Id,
                Name = p.Name,
                Tag = p.Tag ?? "",
                CardForeColor = p.CardForeColor ?? "",
                CardBackColor = p.CardBackColor ?? "",
            })
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
    [Description("Returns every node of the specified project, flattened depth first, with id, title and hierarchical path, plus a count field. Use this to enumerate or count the nodes of a project — search_fulltext only returns keyword matches and cannot tell you how many nodes a project has.")]
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
    [Description("Returns the article (Markdown) of a node found by its title. Omit project_name to search every project in the workspace — do that whenever the user names a node without saying which project it is in. Both arguments match on substrings. If more than one node matches, the call fails with the list of candidates and their ids; pick one and call get_article.")]
    public ArticleResult GetArticleByTitle(
        [Description("Node title (partial match)")] string node_title,
        [Description("Project name (partial match). Omit to search every project.")] string? project_name = null)
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
    [Description("Replaces the whole article of the specified node. Anything not included in content is lost — use append_article instead when you only want to add to the node.")]
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
    [Description("Replaces the whole article of a node found by project name and node title (both partial match). Anything not included in content is lost — use append_article instead when you only want to add to the node.")]
    public SaveArticleResult SaveArticleByTitle(
        [Description("Project name (partial match)")] string project_name,
        [Description("Node title (partial match)")] string node_title,
        [Description("Markdown content to save")] string content)
    {
        var (project, node, _) = ResolveByTitle(project_name, node_title);
        return Save(project, node, content);
    }

    [McpServerTool(
        Name = "append_article",
        Title = "記事への追記",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Appends text to the end of a node's article, keeping everything that is already there. Prefer this over save_article whenever you want to add to a node: save_article replaces the whole body, so anything you did not reproduce exactly is lost. To link to another node write [text](#node-title:its title); the server resolves it to the node id.")]
    public SaveArticleResult AppendArticle(
        [Description("Project name (partial match)")] string project_name,
        [Description("Node title (partial match)")] string node_title,
        [Description("Markdown text to append at the end")] string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new McpException("content is empty.");

        var (project, node, _) = ResolveByTitle(project_name, node_title);
        var existing = node.Article ?? "";
        var separator = existing.Length == 0 || existing.EndsWith('\n') ? "" : "\n";

        return Save(project, node, $"{existing}{separator}{content}");
    }

    [McpServerTool(
        Name = "add_node",
        Title = "ノードの追加",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Adds a new node to an existing project. Use this to add a page to a project you already have — import_markdown_as_project always creates a whole new project instead. Give parent_title to nest it under an existing node; omit it to add at the top level. Existing nodes are never modified.")]
    public AddNodeResult AddNode(
        [Description("Project name (partial match)")] string project_name,
        [Description("Title of the new node")] string title,
        [Description("Article body in Markdown (optional)")] string? content = null,
        [Description("Supplementary note (optional)")] string? comment = null,
        [Description("Title of an existing node to add this under (partial match). Omit to add at the top level.")] string? parent_title = null)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new McpException("title is empty.");

        var projects = MatchProjects(project_name);
        if (projects.Count > 1)
            throw new McpException(
                $"Ambiguous: \"{project_name}\" matches {projects.Count} projects: " +
                $"{NameList(projects.Select(p => p.Name))}. Use the full project name.");

        var project = projects[0];

        // 親を決める。指定が無ければトップレベル。
        List<ManidocNode> siblings;
        string parentPath;
        if (string.IsNullOrWhiteSpace(parent_title))
        {
            siblings = project.RootNodes ??= [];
            parentPath = "";
        }
        else
        {
            // 同じ project インスタンス上で親を探す(読み直すと保存が空振りする)
            var (_, parent, path) = ResolveIn([project], parent_title);
            siblings = parent.Children ??= [];
            parentPath = path;
        }

        // 同じ親に同名がある場合は作らない。小型 LLM が再試行で重複を量産するのを防ぐ。
        var duplicate = siblings.FirstOrDefault(n => n.Title.Equals(title, Ci));
        if (duplicate != null)
            throw new McpException(
                $"A node titled \"{title}\" already exists here (node_id={duplicate.Id}). " +
                $"Call save_article to overwrite it, or choose a different title.");

        var node = new ManidocNode
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Article = content ?? "",
            Comment = comment ?? "",
        };
        siblings.Add(node);
        // ツリーに入れてから解決する(自分自身のタイトルも参照できるように)
        node.Article = ResolveTitleLinks(project, node.Article);
        node.Comment = ResolveTitleLinks(project, node.Comment);
        WorkspaceService.SaveProject(project);

        return new AddNodeResult
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            NodeId = node.Id,
            NodeTitle = node.Title,
            Path = string.IsNullOrEmpty(parentPath) ? node.Title : $"{parentPath} > {node.Title}",
            ParentNodeTitle = string.IsNullOrWhiteSpace(parent_title) ? null : parentPath,
            SavedLength = node.Article.Length,
        };
    }

    [McpServerTool(
        Name = "import_markdown_as_project",
        Title = "Markdown をプロジェクトとして取り込み",
        ReadOnly = false,
        Destructive = false,
        Idempotent = false,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Imports Markdown text as a new Manidoc project. H1 becomes the project name, H2+ headings become nodes (hierarchical), blockquotes go to node.comment, paragraphs/code/lists go to node.article. To link from one node to another, write [text](#node-title:the other heading) — the server replaces it with that node's id after the nodes exist, so a document whose index links to its own sections can be created in this single call. Never write raw node ids yourself.")]
    public ImportProjectResult ImportMarkdownAsProject(
        [Description("Markdown text to import")] string markdown_text)
    {
        if (string.IsNullOrWhiteSpace(markdown_text))
            throw new McpException("markdown_text is empty");

        var workspace = WorkspaceService.GetWorkspacePath();
        var project = MarkdownImporter.Import(markdown_text, workspace);
        // 取り込み後はノードIDが決まっているので、この時点でタイトルリンクを解決できる。
        // これにより「見出しを並べて、本文からその見出しへリンクする」文書を
        // 1 回の呼び出しで完成させられる。
        ResolveTitleLinksInTree(project);
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
    [Description("Searches all projects (or a specific project) for a keyword, across project names, node titles, article bodies and comments. The keyword is matched as a literal substring, so a natural-language phrase usually finds nothing; prefer one distinctive word. If you do pass several words separated by spaces and the literal match finds nothing, the server retries for nodes containing every word. Zero results means the keyword is absent — try a different word or browse with list_projects and list_nodes instead of giving up.")]
    public SearchResultSet SearchFulltext(
        [Description("Search keyword (case-insensitive)")] string keyword,
        [Description("Limit search to a specific project ID (optional)")] string? project_id = null,
        [Description("Max number of results to return (default: 30)")] int max_results = 30)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            throw new McpException("keyword is empty");
        if (max_results <= 0)
            throw new McpException("max_results must be greater than 0");

        var (shown, summary, totalMatches, usedAndFallback) =
            SearchService.Search(keyword, project_id, max_results);

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
            Hint = BuildSearchHint(keyword, shown.Count, totalMatches, usedAndFallback),
        };
    }

    [McpServerTool(
        Name = "set_project_attributes",
        Title = "プロジェクト属性の設定（タグ・タイル色）",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Sets a project's tag and/or tile colors so you can organize the workspace. Identify the project by project_id (get it from list_projects). Only the parameters you pass are changed: omit one to leave it as-is, or pass an empty string to clear it. Colors are #RRGGBB (e.g. #4a90d9). Tags are just names — if the tag is not defined in the workspace yet, also call add_tag so it shows up in Manidoc. Article bodies and node structure are never touched, and the project's last-modified time is preserved.")]
    public SetProjectAttributesResult SetProjectAttributes(
        [Description("Project ID (get it from list_projects)")] string project_id,
        [Description("Tag name to assign; empty string clears the tag. Omit to leave unchanged.")] string? tag = null,
        [Description("Tile text color as #RRGGBB; empty string clears it. Omit to leave unchanged.")] string? card_fore_color = null,
        [Description("Tile background color as #RRGGBB; empty string clears it. Omit to leave unchanged.")] string? card_back_color = null)
    {
        if (tag is null && card_fore_color is null && card_back_color is null)
            throw new McpException("Nothing to change: pass at least one of tag, card_fore_color or card_back_color.");

        var project = RequireProject(project_id);

        // 現在の実効色(プロジェクトJSON→無ければミラーの控え)を起点に、指定された分だけ上書きする。
        // こうしないと「片方の色だけ変更」でもう片方の控えを失う。
        var mirror = WorkspaceService.LoadCardColors();
        mirror.TryGetValue(project.Id, out var saved);
        var curFore = !string.IsNullOrEmpty(project.CardForeColor) ? project.CardForeColor : saved.Fore ?? "";
        var curBack = !string.IsNullOrEmpty(project.CardBackColor) ? project.CardBackColor : saved.Back ?? "";

        var finalFore = card_fore_color is null ? curFore : NormalizeColor(card_fore_color, "card_fore_color");
        var finalBack = card_back_color is null ? curBack : NormalizeColor(card_back_color, "card_back_color");

        if (tag is not null) project.Tag = tag;
        project.CardForeColor = finalFore;
        project.CardBackColor = finalBack;

        // 属性変更は本文編集ではないので lastModifiedAt を動かさない。
        WorkspaceService.SaveProject(project, touch: false);
        // 本家Manidoc は未知フィールド(色)を保存時に落とすため、控えをミラーに残す。
        WorkspaceService.UpdateCardColor(project.Id, finalFore, finalBack);

        bool? tagDefined = string.IsNullOrEmpty(project.Tag)
            ? null
            : WorkspaceService.GetTags().Any(t => t.Name.Equals(project.Tag, Ci));

        return new SetProjectAttributesResult
        {
            ProjectId = project.Id,
            ProjectName = project.Name,
            Tag = project.Tag ?? "",
            CardForeColor = finalFore,
            CardBackColor = finalBack,
            TagDefined = tagDefined,
        };
    }

    [McpServerTool(
        Name = "list_tags",
        Title = "タグ定義の一覧",
        ReadOnly = true,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Returns the tag definitions of the workspace (name and optional thumbnail image path), as shown in Manidoc's tag manager. Check this before assigning tags with set_project_attributes so you reuse existing tags instead of inventing new names.")]
    public ListTagsResult ListTags()
    {
        var tags = WorkspaceService.GetTags()
            .Select(t => new TagSummary { Name = t.Name, ImagePath = t.ImagePath })
            .ToList();

        return new ListTagsResult { Tags = tags, Count = tags.Count };
    }

    [McpServerTool(
        Name = "add_tag",
        Title = "タグ定義の追加",
        ReadOnly = false,
        Destructive = false,
        Idempotent = true,
        OpenWorld = false,
        UseStructuredContent = true)]
    [Description("Adds a new tag definition to the workspace so it can be assigned to projects with set_project_attributes. If a tag with the same name already exists it is left unchanged (created=false). image_path is an optional absolute path to a thumbnail image.")]
    public AddTagResult AddTag(
        [Description("Tag name")] string name,
        [Description("Absolute path to a thumbnail image (optional)")] string? image_path = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new McpException("name is empty.");

        var tags = WorkspaceService.GetTags();
        var existing = tags.FirstOrDefault(t => t.Name.Equals(name, Ci));
        var created = existing is null;
        if (created)
        {
            tags.Add(new TagDefinition { Name = name, ImagePath = image_path ?? "" });
            WorkspaceService.SaveTags(tags);
        }

        return new AddTagResult
        {
            Name = existing?.Name ?? name,
            ImagePath = existing?.ImagePath ?? image_path ?? "",
            Created = created,
            TotalTags = tags.Count,
        };
    }

    // --- ヘルパー ---

    private static readonly Regex ColorPattern = new(@"^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

    /// <summary>色を検証する。空文字は「解除」としてそのまま通す。#RRGGBB 以外は例外。</summary>
    private static string NormalizeColor(string value, string field)
    {
        if (value.Length == 0) return "";
        if (!ColorPattern.IsMatch(value))
            throw new McpException(
                $"{field} must be a hex color like #4a90d9 (#RRGGBB), or an empty string to clear it. Got: \"{value}\".");
        return value;
    }

    /// <summary>
    /// 0 件のときこそヒントが要る。小型モデルは 0 件を「情報が存在しない」と受け取って
    /// 探索をやめ、そのまま作り話を始めるため、次に打つ手を明示する。
    /// </summary>
    private static string? BuildSearchHint(string keyword, int shownCount, int totalMatches, bool usedAndFallback)
    {
        if (totalMatches == 0)
        {
            var multiWord = keyword.Split([' ', '　', '\t'], StringSplitOptions.RemoveEmptyEntries).Length > 1;
            return multiWord
                ? $"\"{keyword}\" は 0 件でした。この検索は文字列そのままの一致です。" +
                  "語を 1 つに減らすか、list_projects → list_nodes で辿ってください。"
                : $"\"{keyword}\" は 0 件でした。別の語を試すか、list_projects → list_nodes で辿ってください。" +
                  "ワークスペースに無い内容を推測で答えないでください。";
        }

        var parts = new List<string>();
        if (usedAndFallback)
            parts.Add("文字列そのままでは 0 件だったため、語ごとに分けて一致数の多い順に返しました。" +
                      "area の terms:N/M は M 語中 N 語が一致した意味です。上位でも的外れなことがあるので、" +
                      "get_article で本文を確かめてから答えてください。");
        if (shownCount < totalMatches)
            parts.Add($"{totalMatches - shownCount} 件を省略しました。project_id を指定して絞り込むか、max_results を増やしてください。");

        return parts.Count > 0 ? string.Join(" ", parts) : null;
    }

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

    private const StringComparison Ci = StringComparison.OrdinalIgnoreCase;

    /// <summary>
    /// 名前にマッチするプロジェクトを「すべて」返す。完全一致があればそれを優先する。
    /// 空文字/未指定はワークスペース全体を対象にする。
    /// 1 件に絞らないのは、部分一致で最初に当たったプロジェクトだけを見ると
    /// 「マニュアル」が別のプロジェクトに吸われて、実在するノードを not found と
    /// 誤報するため（例: "マニュアル"+"はじめに"）。
    /// </summary>
    private static List<ManidocProject> MatchProjects(string? projectName)
    {
        var all = WorkspaceService.GetAllProjects();
        if (string.IsNullOrWhiteSpace(projectName)) return all;

        var exact = all.Where(p => p.Name.Equals(projectName, Ci)).ToList();
        if (exact.Count > 0) return exact;

        var partial = all.Where(p => p.Name.Contains(projectName, Ci)).ToList();
        if (partial.Count == 0)
            throw new McpException(
                $"Project \"{projectName}\" not found. Available projects: {NameList(all.Select(p => p.Name))}");

        return partial;
    }

    private static string NameList(IEnumerable<string> names, int max = 30)
    {
        var list = names.ToList();
        var head = string.Join(" / ", list.Take(max));
        return list.Count > max ? $"{head} …(他 {list.Count - max} 件)" : head;
    }

    /// <summary>
    /// プロジェクト名＋ノードタイトルの部分一致でノードを一意に決める。
    /// 候補が複数あるときは黙って先頭を選ばず、ID 付きの候補一覧を添えて失敗させる。
    /// 書き込み系がここを通るため、取り違えて上書きするより中断する方が安全。
    /// </summary>
    /// <summary>
    /// projectName が null / 空ならワークスペース全体からノードを探す。
    /// 読み取り側でこれを許すのは、利用者が「〇〇というノードの内容を教えて」と
    /// プロジェクトを言わずに尋ねるのが自然だから。必須にしていた頃は、
    /// LLM が埋めるものが無くて適当なプロジェクト名を捏造し、not found になっていた。
    /// </summary>
    private static (ManidocProject Project, ManidocNode Node, string Path) ResolveByTitle(string? projectName, string nodeTitle)
        => ResolveIn(MatchProjects(projectName), nodeTitle);

    /// <summary>
    /// 渡されたプロジェクト「インスタンス」の中からノードを一意に決める。
    /// add_node は親ノードと保存対象を同じインスタンス上で扱う必要があるため、
    /// プロジェクトを読み直さずに解決できるこの形にしてある
    /// （読み直すと別オブジェクトになり、子を足しても保存されない）。
    /// </summary>
    private static (ManidocProject Project, ManidocNode Node, string Path) ResolveIn(
        List<ManidocProject> projects, string nodeTitle)
    {
        if (string.IsNullOrWhiteSpace(nodeTitle))
            throw new McpException("node_title is empty.");

        var hits = new List<(ManidocProject Project, FlatNode Flat)>();
        foreach (var p in projects)
        {
            foreach (var f in WorkspaceService.FlattenNodes(p.RootNodes ?? []))
            {
                if (f.Title.Contains(nodeTitle, Ci)) hits.Add((p, f));
            }
        }

        if (hits.Count == 0)
        {
            var titles = projects
                .SelectMany(p => WorkspaceService.FlattenNodes(p.RootNodes ?? []).Select(f => f.Title));
            throw new McpException(
                $"Node \"{nodeTitle}\" not found in {NameList(projects.Select(p => p.Name))}. " +
                $"Available titles: {NameList(titles)}");
        }

        // 完全一致があれば部分一致より優先する（"はじめに" が "1. はじめに" に負けないように）
        var exact = hits.Where(h => h.Flat.Title.Equals(nodeTitle, Ci)).ToList();
        var final = exact.Count > 0 ? exact : hits;

        if (final.Count > 1)
        {
            var lines = final.Select((h, i) =>
                $"[{i + 1}] {h.Project.Name} > {h.Flat.Path} " +
                $"(project_id={h.Project.Id}, node_id={h.Flat.Id})");
            throw new McpException(
                $"Ambiguous: \"{nodeTitle}\" matches {final.Count} nodes. " +
                $"Pick one and call get_article / save_article with its project_id and node_id.\n" +
                string.Join("\n", lines));
        }

        var (project, flat) = final[0];
        var node = WorkspaceService.FindNode(project.RootNodes ?? [], flat.Id)
            ?? throw new McpException($"Node not found: {flat.Id}");

        return (project, node, flat.Path);
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

    private static readonly Regex TitleLinkPattern =
        new(@"\(#node-title:([^)]*)\)", RegexOptions.Compiled);

    /// <summary>
    /// <c>[表示テキスト](#node-title:ノードタイトル)</c> を <c>(#node:ノードID)</c> に解決する。
    ///
    /// LLM に 36 文字の GUID を転記させないための仕組み。カレンダーの日付リンクのように
    /// 数十本のリンクを張る場面では、1 本の写し間違いが「見た目は正常なのにクリックしても
    /// 飛ばない」という気づきにくい壊れ方になる。タイトルで書かせて ID の解決はサーバーが
    /// 引き受ける。
    ///
    /// 1 つでも解決できなければ例外にして一切書き込まない。
    /// 30 本正しくて 1 本壊れている状態が最も質が悪いため。
    /// </summary>
    private static string ResolveTitleLinks(ManidocProject project, string? content)
    {
        if (string.IsNullOrEmpty(content) || !content.Contains("#node-title:"))
            return content ?? "";

        var flat = WorkspaceService.FlattenNodes(project.RootNodes ?? []);
        var problems = new List<string>();

        var resolved = TitleLinkPattern.Replace(content, m =>
        {
            var title = m.Groups[1].Value.Trim();
            if (title.Length == 0)
            {
                problems.Add("ノードタイトルが空です");
                return m.Value;
            }

            var exact = flat.Where(n => n.Title.Equals(title, Ci)).ToList();
            var hits = exact.Count > 0
                ? exact
                : flat.Where(n => n.Title.Contains(title, Ci)).ToList();

            if (hits.Count == 0)
            {
                problems.Add($"\"{title}\" というノードが \"{project.Name}\" にありません");
                return m.Value;
            }
            if (hits.Count > 1)
            {
                problems.Add(
                    $"\"{title}\" は {hits.Count} 件に一致します: {NameList(hits.Select(h => h.Path), 5)}");
                return m.Value;
            }
            return $"(#node:{hits[0].Id})";
        });

        if (problems.Count > 0)
        {
            throw new McpException(
                "リンクを解決できないため保存しませんでした。" +
                "#node-title: にはこのプロジェクト内のノードのタイトルを書いてください。\n- " +
                string.Join("\n- ", problems.Distinct()));
        }

        return resolved;
    }

    /// <summary>プロジェクト内すべてのノードのタイトルリンクを解決する(取り込み時に使う)。</summary>
    private static void ResolveTitleLinksInTree(ManidocProject project)
    {
        void Walk(List<ManidocNode> nodes)
        {
            foreach (var n in nodes)
            {
                n.Article = ResolveTitleLinks(project, n.Article);
                n.Comment = ResolveTitleLinks(project, n.Comment);
                if (n.Children?.Count > 0) Walk(n.Children);
            }
        }

        Walk(project.RootNodes ?? []);
    }

    private static SaveArticleResult Save(ManidocProject project, ManidocNode node, string content)
    {
        content = ResolveTitleLinks(project, content);
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
