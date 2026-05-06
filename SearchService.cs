using System.Text.RegularExpressions;

namespace ManidocMCP;

public static class SearchService
{
    private static string StripMarkdown(string md)
        => Regex.Replace(md, @"(\*{1,3}|#{1,6} ?|`|~~|>\s*|\[|\]|\(|\))", "").Trim();

    private static string MakeSnippet(string text, string keyword, int before = 15, int after = 20)
    {
        int idx = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return text.Length > 50 ? text[..50] + "…" : text;
        int start = Math.Max(0, idx - before);
        int end = Math.Min(text.Length, idx + keyword.Length + after);
        var snippet = text[start..end];
        if (start > 0) snippet = "…" + snippet;
        if (end < text.Length) snippet += "…";
        return snippet;
    }

    private static void CollectFromNodes(
        ManidocProject project,
        List<ManidocNode> nodes,
        string keyword,
        List<SearchResult> results,
        string parentPath = "")
    {
        foreach (var node in nodes)
        {
            var nodePath = string.IsNullOrEmpty(parentPath) ? node.Title : $"{parentPath} > {node.Title}";

            if (node.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                results.Add(new SearchResult(project.Id, project.Name, node.Id, node.Title, nodePath, "title", MakeSnippet(node.Title, keyword)));

            if (!string.IsNullOrEmpty(node.Article))
            {
                var plain = StripMarkdown(node.Article);
                if (plain.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    results.Add(new SearchResult(project.Id, project.Name, node.Id, node.Title, nodePath, "article", MakeSnippet(plain, keyword)));
            }

            if (!string.IsNullOrEmpty(node.Comment))
            {
                var plain = StripMarkdown(node.Comment);
                if (plain.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    results.Add(new SearchResult(project.Id, project.Name, node.Id, node.Title, nodePath, "comment", MakeSnippet(plain, keyword)));
            }

            if (node.Children?.Count > 0)
                CollectFromNodes(project, node.Children, keyword, results, nodePath);
        }
    }

    public static (List<SearchResult> Shown, List<(string ProjectId, string ProjectName, int Total, int Shown, int Omitted)> Summary, int TotalMatches) Search(
        string keyword, string? projectId, int limit)
    {
        var projects = projectId != null
            ? (WorkspaceService.GetProject(projectId) is { } p ? [p] : [])
            : WorkspaceService.GetAllProjects();

        var perProject = new List<(string ProjectId, string ProjectName, List<SearchResult> Results)>();
        foreach (var project in projects)
        {
            var pResults = new List<SearchResult>();
            if (project.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                pResults.Add(new SearchResult(project.Id, project.Name, null, null, null, "name", MakeSnippet(project.Name, keyword)));
            CollectFromNodes(project, project.RootNodes ?? [], keyword, pResults);
            if (pResults.Count > 0)
                perProject.Add((project.Id, project.Name, pResults));
        }

        int totalMatches = perProject.Sum(p => p.Results.Count);
        var shown = new List<SearchResult>();
        int remaining = limit;
        foreach (var (_, _, pResults) in perProject)
        {
            int take = Math.Min(pResults.Count, remaining);
            shown.AddRange(pResults[..take]);
            remaining -= take;
            if (remaining <= 0) break;
        }

        var summary = perProject.Select(p =>
        {
            int shownCount = shown.Count(r => r.ProjectId == p.ProjectId);
            return (p.ProjectId, p.ProjectName, p.Results.Count, shownCount, p.Results.Count - shownCount);
        }).ToList();

        return (shown, summary, totalMatches);
    }
}
