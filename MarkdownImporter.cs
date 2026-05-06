using System.Text.RegularExpressions;

namespace ManidocMCP;

public enum BlockType { Heading, Blockquote, Code, List, Html, Paragraph }

public record MarkdownBlock(BlockType Type, int Level, string Raw);

public static class MarkdownImporter
{
    public static List<MarkdownBlock> Tokenize(string markdown)
    {
        var lines = markdown.Split('\n');
        var blocks = new List<MarkdownBlock>();
        int i = 0;

        while (i < lines.Length)
        {
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) { i++; continue; }

            if (line.StartsWith("```"))
            {
                var raw = new List<string> { line };
                i++;
                while (i < lines.Length && !lines[i].StartsWith("```")) raw.Add(lines[i++]);
                if (i < lines.Length) raw.Add(lines[i++]);
                blocks.Add(new MarkdownBlock(BlockType.Code, 0, string.Join('\n', raw)));
                continue;
            }

            var hm = Regex.Match(line, @"^(#{1,6})\s+(.+)");
            if (hm.Success)
            {
                blocks.Add(new MarkdownBlock(BlockType.Heading, hm.Groups[1].Length, hm.Groups[2].Value.Trim()));
                i++;
                continue;
            }

            if (line.StartsWith(">"))
            {
                var raw = new List<string>();
                while (i < lines.Length)
                {
                    if (lines[i].StartsWith(">"))
                        raw.Add(lines[i++]);
                    else if (string.IsNullOrWhiteSpace(lines[i]) && raw.Count > 0
                             && i + 1 < lines.Length && lines[i + 1].StartsWith(">"))
                        raw.Add(lines[i++]);
                    else break;
                }
                blocks.Add(new MarkdownBlock(BlockType.Blockquote, 0, string.Join('\n', raw)));
                continue;
            }

            if (Regex.IsMatch(line, @"^(\s*[-*+]|\s*\d+\.)\s"))
            {
                var raw = new List<string> { line };
                i++;
                while (i < lines.Length)
                {
                    if (Regex.IsMatch(lines[i], @"^(\s*[-*+]|\s*\d+\.)\s") || Regex.IsMatch(lines[i], @"^\s{2,}"))
                        raw.Add(lines[i++]);
                    else if (string.IsNullOrWhiteSpace(lines[i]) && i + 1 < lines.Length
                             && (Regex.IsMatch(lines[i + 1], @"^(\s*[-*+]|\s*\d+\.)\s") || Regex.IsMatch(lines[i + 1], @"^\s{2,}")))
                        raw.Add(lines[i++]);
                    else break;
                }
                blocks.Add(new MarkdownBlock(BlockType.List, 0, string.Join('\n', raw)));
                continue;
            }

            if (Regex.IsMatch(line, @"^<[a-zA-Z]"))
            {
                var raw = new List<string> { line };
                i++;
                while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i])) raw.Add(lines[i++]);
                blocks.Add(new MarkdownBlock(BlockType.Html, 0, string.Join('\n', raw)));
                continue;
            }

            {
                var raw = new List<string> { line };
                i++;
                while (i < lines.Length
                       && !string.IsNullOrWhiteSpace(lines[i])
                       && !Regex.IsMatch(lines[i], @"^#{1,6}\s")
                       && !lines[i].StartsWith(">")
                       && !lines[i].StartsWith("```")
                       && !Regex.IsMatch(lines[i], @"^(\s*[-*+]|\s*\d+\.)\s"))
                    raw.Add(lines[i++]);
                blocks.Add(new MarkdownBlock(BlockType.Paragraph, 0, string.Join('\n', raw)));
            }
        }

        return blocks;
    }

    public static ManidocProject Import(string markdownText, string workspacePath)
    {
        var blocks = Tokenize(markdownText);
        if (!blocks.Any(b => b.Type == BlockType.Heading && b.Level == 1))
            throw new InvalidOperationException(
                "No H1 heading found in the markdown text.\n" +
                "The first line must be a project name formatted as:\n" +
                "  # Project Name\n" +
                "Please add an H1 heading at the top and call this tool again.");

        var projectId = Guid.NewGuid().ToString();
        var rootNodes = new List<ManidocNode>();
        var nodeStack = new Stack<(int Level, ManidocNode Node)>();
        ManidocNode? currentNode = null;
        bool foundTitle = false;
        string projectName = "";
        string projectDescription = "";

        foreach (var block in blocks)
        {
            if (block.Type == BlockType.Heading)
            {
                int level = block.Level;
                if (level == 1 && !foundTitle)
                {
                    projectName = block.Raw;
                    foundTitle = true;
                    continue;
                }

                var newNode = new ManidocNode
                {
                    Id = Guid.NewGuid().ToString(),
                    Title = block.Raw,
                };

                while (nodeStack.Count > 0 && nodeStack.Peek().Level >= level)
                    nodeStack.Pop();

                if (nodeStack.Count == 0)
                    rootNodes.Add(newNode);
                else
                    nodeStack.Peek().Node.Children.Add(newNode);

                nodeStack.Push((level, newNode));
                currentNode = newNode;
            }
            else if (currentNode != null)
            {
                ApplyBlock(block, currentNode);
            }
            else if (foundTitle)
            {
                var overview = new ManidocNode { Id = Guid.NewGuid().ToString(), Title = "概要" };
                rootNodes.Add(overview);
                nodeStack.Push((99, overview));
                currentNode = overview;
                ApplyBlock(block, currentNode);
            }
            else if (!foundTitle && block.Type == BlockType.Paragraph)
            {
                projectDescription = block.Raw;
            }
        }

        var now = DateTime.UtcNow.ToString("o");
        return new ManidocProject
        {
            Id = projectId,
            Name = projectName,
            Description = projectDescription,
            CreatedAt = now,
            LastModifiedAt = now,
            RootNodes = rootNodes,
        };
    }

    private static void ApplyBlock(MarkdownBlock block, ManidocNode node)
    {
        if (block.Type == BlockType.Blockquote)
        {
            node.Comment = string.IsNullOrEmpty(node.Comment) ? block.Raw : node.Comment + "\n" + block.Raw;
        }
        else
        {
            var text = block.Raw;
            node.Article = string.IsNullOrEmpty(node.Article) ? text : node.Article + "\n" + text;
        }
    }
}
