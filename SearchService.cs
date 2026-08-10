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

    private static readonly char[] TermSeparators = [' ', '　', '\t', '\n'];

    /// <summary>
    /// キーワードは文字列そのままの部分一致で探す。ただし空白区切りの複数語で
    /// 1件も当たらなかったときは、語ごとに分けて一致数の多い順に返し直す。
    /// LLM は「API 利用ガイド 標準プラン レート制限」のような自然文を渡してくる。
    /// 素の部分一致では絶対に当たらず、0 件を「情報が無い」と誤解して探索を
    /// 打ち切ってしまうため、ここで受け止める。
    /// </summary>
    public static (List<SearchResult> Shown, List<(string ProjectId, string ProjectName, int Total, int Shown, int Omitted)> Summary, int TotalMatches, bool UsedAndFallback) Search(
        string keyword, string? projectId, int limit)
    {
        var literal = SearchLiteral(keyword, projectId, limit);
        if (literal.TotalMatches > 0) return (literal.Shown, literal.Summary, literal.TotalMatches, false);

        var terms = keyword.Split(TermSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (terms.Length < 2) return (literal.Shown, literal.Summary, literal.TotalMatches, false);

        var byTerms = SearchByTerms(terms, projectId, limit);
        return byTerms.TotalMatches > 0
            ? (byTerms.Shown, byTerms.Summary, byTerms.TotalMatches, true)
            : (literal.Shown, literal.Summary, literal.TotalMatches, false);
    }

    /// <summary>
    /// 語ごとに分けて探し、一致した語数の多い順に返す。
    /// LLM の問い合わせは本文の言い回しと完全には一致しない
    /// （本文「標準 | 60 req/min」に対して「標準プラン」と聞いてくる）ので、
    /// 全語一致を要求すると救済にならない。部分一致でも上位を見せる方が到達できる。
    /// </summary>
    private static (List<SearchResult> Shown, List<(string ProjectId, string ProjectName, int Total, int Shown, int Omitted)> Summary, int TotalMatches) SearchByTerms(
        string[] terms, string? projectId, int limit)
    {
        var scored = new List<(int Score, SearchResult Result)>();

        foreach (var project in ProjectsToSearch(projectId))
        {
            void Walk(List<ManidocNode> nodes, string parentPath)
            {
                foreach (var node in nodes)
                {
                    var nodePath = string.IsNullOrEmpty(parentPath) ? node.Title : $"{parentPath} > {node.Title}";
                    var haystack = string.Join('\n',
                        project.Name,
                        node.Title,
                        StripMarkdown(node.Article ?? ""),
                        StripMarkdown(node.Comment ?? ""));

                    var hitTerms = terms
                        .Where(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (hitTerms.Count > 0)
                    {
                        // 抜粋は最も長い語を中心にする。短い語("API"など)は
                        // プロジェクト名の先頭に当たりがちで、抜粋が中身を映さない。
                        var focus = hitTerms.OrderByDescending(t => t.Length).First();
                        scored.Add((hitTerms.Count, new SearchResult(
                            project.Id, project.Name, node.Id, node.Title, nodePath,
                            $"terms:{hitTerms.Count}/{terms.Length}",
                            MakeSnippet(haystack, focus, before: 20, after: 80))));
                    }

                    if (node.Children?.Count > 0) Walk(node.Children, nodePath);
                }
            }

            Walk(project.RootNodes ?? [], "");
        }

        // 一致語数の多い順。同点はワークスペースの並び順のまま。
        var ordered = scored
            .Select((s, i) => (s.Score, Index: i, s.Result))
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Index)
            .Select(s => s.Result)
            .ToList();

        var shown = ordered.Take(limit).ToList();
        var summary = ordered
            .GroupBy(r => (r.ProjectId, r.ProjectName))
            .Select(g =>
            {
                int shownCount = shown.Count(r => r.ProjectId == g.Key.ProjectId);
                return (g.Key.ProjectId, g.Key.ProjectName, g.Count(), shownCount, g.Count() - shownCount);
            })
            .ToList();

        return (shown, summary, ordered.Count);
    }

    private static List<ManidocProject> ProjectsToSearch(string? projectId)
        => projectId != null
            ? (WorkspaceService.GetProject(projectId) is { } p ? [p] : [])
            : WorkspaceService.GetAllProjects();

    private static (List<SearchResult> Shown, List<(string ProjectId, string ProjectName, int Total, int Shown, int Omitted)> Summary, int TotalMatches) Paginate(
        List<(string ProjectId, string ProjectName, List<SearchResult> Results)> perProject, int limit)
    {
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

    private static (List<SearchResult> Shown, List<(string ProjectId, string ProjectName, int Total, int Shown, int Omitted)> Summary, int TotalMatches) SearchLiteral(
        string keyword, string? projectId, int limit)
    {
        var projects = ProjectsToSearch(projectId);

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

        return Paginate(perProject, limit);
    }
}
