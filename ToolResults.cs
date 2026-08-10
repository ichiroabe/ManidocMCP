using System.ComponentModel;
using System.Text.Json.Serialization;

namespace ManidocMCP;

// MCP 2026-07-28 のツールは outputSchema を宣言し、結果を structuredContent として返す。
// ここで定義する型がそのまま JSON Schema として公開されるため、
// ワイヤ上の名前は [JsonPropertyName]、説明は [Description] で固定しておく。

public sealed class ServerStatusResult
{
    [JsonPropertyName("ok")]
    [Description("Whether the workspace could be opened.")]
    public required bool Ok { get; init; }

    [JsonPropertyName("workspace")]
    [Description("Absolute path of the Manidoc workspace (null when it could not be resolved).")]
    public string? Workspace { get; init; }

    [JsonPropertyName("projectCount")]
    [Description("Number of projects found in the workspace (null when it could not be resolved).")]
    public int? ProjectCount { get; init; }

    [JsonPropertyName("serverVersion")]
    [Description("Version of this MCP server.")]
    public required string ServerVersion { get; init; }

    [JsonPropertyName("error")]
    [Description("Why the workspace could not be opened; null when ok is true.")]
    public string? Error { get; init; }
}

public sealed class ProjectSummary
{
    [JsonPropertyName("id")]
    [Description("Project ID. Pass this to list_nodes, get_article and save_article.")]
    public required string Id { get; init; }

    [JsonPropertyName("name")]
    [Description("Project name.")]
    public required string Name { get; init; }

    [JsonPropertyName("tag")]
    [Description("User-defined tag; empty string when unset.")]
    public required string Tag { get; init; }
}

public sealed class ListProjectsResult
{
    [JsonPropertyName("projects")]
    [Description("Projects in the workspace, in the order Manidoc displays them.")]
    public required IList<ProjectSummary> Projects { get; init; }

    [JsonPropertyName("count")]
    [Description("Number of projects returned.")]
    public required int Count { get; init; }
}

public sealed class NodeSummary
{
    [JsonPropertyName("id")]
    [Description("Node ID. Pass this to get_article and save_article.")]
    public required string Id { get; init; }

    [JsonPropertyName("title")]
    [Description("Node title.")]
    public required string Title { get; init; }

    [JsonPropertyName("path")]
    [Description("Hierarchical path from the root node, joined with \" > \".")]
    public required string Path { get; init; }
}

public sealed class ListNodesResult
{
    [JsonPropertyName("projectId")]
    [Description("ID of the project the nodes belong to.")]
    public required string ProjectId { get; init; }

    [JsonPropertyName("projectName")]
    [Description("Name of the project the nodes belong to.")]
    public required string ProjectName { get; init; }

    [JsonPropertyName("nodes")]
    [Description("All nodes of the project, flattened depth-first.")]
    public required IList<NodeSummary> Nodes { get; init; }

    [JsonPropertyName("count")]
    [Description("Number of nodes returned.")]
    public required int Count { get; init; }
}

public sealed class ArticleResult
{
    [JsonPropertyName("projectId")]
    [Description("ID of the project the node belongs to.")]
    public required string ProjectId { get; init; }

    [JsonPropertyName("projectName")]
    [Description("Name of the project the node belongs to.")]
    public required string ProjectName { get; init; }

    [JsonPropertyName("nodeId")]
    [Description("ID of the resolved node.")]
    public required string NodeId { get; init; }

    [JsonPropertyName("nodeTitle")]
    [Description("Title of the resolved node.")]
    public required string NodeTitle { get; init; }

    [JsonPropertyName("path")]
    [Description("Hierarchical path of the node, joined with \" > \".")]
    public required string Path { get; init; }

    [JsonPropertyName("content")]
    [Description("The node's article body, in Markdown.")]
    public required string Content { get; init; }

    [JsonPropertyName("comment")]
    [Description("The node's supplementary note; empty string when unset.")]
    public required string Comment { get; init; }
}

public sealed class SaveArticleResult
{
    [JsonPropertyName("projectId")]
    [Description("ID of the project that was written to.")]
    public required string ProjectId { get; init; }

    [JsonPropertyName("projectName")]
    [Description("Name of the project that was written to.")]
    public required string ProjectName { get; init; }

    [JsonPropertyName("nodeId")]
    [Description("ID of the node that was written to.")]
    public required string NodeId { get; init; }

    [JsonPropertyName("nodeTitle")]
    [Description("Title of the node that was written to.")]
    public required string NodeTitle { get; init; }

    [JsonPropertyName("previousLength")]
    [Description("Character count of the article that was replaced.")]
    public required int PreviousLength { get; init; }

    [JsonPropertyName("savedLength")]
    [Description("Character count of the article that was saved.")]
    public required int SavedLength { get; init; }
}

public sealed class AddNodeResult
{
    [JsonPropertyName("projectId")]
    [Description("ID of the project the node was added to.")]
    public required string ProjectId { get; init; }

    [JsonPropertyName("projectName")]
    [Description("Name of the project the node was added to.")]
    public required string ProjectName { get; init; }

    [JsonPropertyName("nodeId")]
    [Description("ID of the newly created node.")]
    public required string NodeId { get; init; }

    [JsonPropertyName("nodeTitle")]
    [Description("Title of the newly created node.")]
    public required string NodeTitle { get; init; }

    [JsonPropertyName("path")]
    [Description("Hierarchical path of the new node, joined with \" > \".")]
    public required string Path { get; init; }

    [JsonPropertyName("parentNodePath")]
    [Description("Path of the parent node; null when the node was added at the top level.")]
    public string? ParentNodeTitle { get; init; }

    [JsonPropertyName("savedLength")]
    [Description("Character count of the article that was saved.")]
    public required int SavedLength { get; init; }
}

public sealed class ImportProjectResult
{
    [JsonPropertyName("projectId")]
    [Description("ID of the newly created project.")]
    public required string ProjectId { get; init; }

    [JsonPropertyName("projectName")]
    [Description("Name of the newly created project, taken from the H1 heading.")]
    public required string ProjectName { get; init; }

    [JsonPropertyName("nodeCount")]
    [Description("Number of nodes created from the Markdown headings.")]
    public required int NodeCount { get; init; }
}

public sealed class SearchHit
{
    [JsonPropertyName("projectId")]
    [Description("ID of the project the match was found in.")]
    public required string ProjectId { get; init; }

    [JsonPropertyName("projectName")]
    [Description("Name of the project the match was found in.")]
    public required string ProjectName { get; init; }

    [JsonPropertyName("nodeId")]
    [Description("ID of the matching node; null when the project name itself matched.")]
    public string? NodeId { get; init; }

    [JsonPropertyName("nodeTitle")]
    [Description("Title of the matching node; null when the project name itself matched.")]
    public string? NodeTitle { get; init; }

    [JsonPropertyName("nodePath")]
    [Description("Hierarchical path of the matching node; null when the project name itself matched.")]
    public string? NodePath { get; init; }

    [JsonPropertyName("area")]
    [Description("Where the keyword matched: \"name\", \"title\", \"article\" or \"comment\".")]
    public required string Area { get; init; }

    [JsonPropertyName("snippet")]
    [Description("Text around the match, with Markdown syntax stripped.")]
    public required string Snippet { get; init; }
}

public sealed class SearchProjectSummary
{
    [JsonPropertyName("projectId")]
    [Description("ID of the project this summary is about.")]
    public required string ProjectId { get; init; }

    [JsonPropertyName("projectName")]
    [Description("Name of the project this summary is about.")]
    public required string ProjectName { get; init; }

    [JsonPropertyName("total")]
    [Description("Total matches in this project.")]
    public required int Total { get; init; }

    [JsonPropertyName("shown")]
    [Description("Matches from this project included in results.")]
    public required int Shown { get; init; }

    [JsonPropertyName("omitted")]
    [Description("Matches from this project left out because of max_results.")]
    public required int Omitted { get; init; }
}

public sealed class SearchResultSet
{
    [JsonPropertyName("keyword")]
    [Description("The keyword that was searched for.")]
    public required string Keyword { get; init; }

    [JsonPropertyName("results")]
    [Description("Matches, capped at max_results.")]
    public required IList<SearchHit> Results { get; init; }

    [JsonPropertyName("byProject")]
    [Description("Per-project match counts, including projects whose matches were all omitted.")]
    public required IList<SearchProjectSummary> ByProject { get; init; }

    [JsonPropertyName("totalMatches")]
    [Description("Total matches across all searched projects, before capping.")]
    public required int TotalMatches { get; init; }

    [JsonPropertyName("shownCount")]
    [Description("Number of matches in results.")]
    public required int ShownCount { get; init; }

    [JsonPropertyName("hint")]
    [Description("How to narrow the search when matches were omitted; null when everything was returned.")]
    public string? Hint { get; init; }
}
