using System.ComponentModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ManidocMCP;

public class ManidocNode
{
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("title")]
    public string Title { get; set; } = "";

    [JsonProperty("article")]
    public string Article { get; set; } = "";

    [JsonProperty("comment")]
    public string Comment { get; set; } = "";

    [JsonProperty("imagePath")]
    public string ImagePath { get; set; } = "";

    [JsonProperty("aiPrompt")]
    public string AiPrompt { get; set; } = "";

    [JsonProperty("children")]
    public List<ManidocNode> Children { get; set; } = [];

    // Manidoc アプリが持つが本サーバーが明示的に扱わないノード属性（articleSpeaker /
    // titleSpeaker など）を、保存(save_article)時に失わないよう未知フィールドを往復させる。
    [JsonExtensionData]
    private IDictionary<string, JToken>? Extra { get; set; }
}

public class ManidocProject
{
    [JsonProperty("id")]
    public string Id { get; set; } = "";

    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("tag")]
    public string Tag { get; set; } = "";

    [JsonProperty("sortOrder")]
    public int SortOrder { get; set; }

    [JsonProperty("createdAt")]
    public string CreatedAt { get; set; } = "";

    [JsonProperty("lastModifiedAt")]
    public string LastModifiedAt { get; set; } = "";

    [JsonProperty("description")]
    public string Description { get; set; } = "";

    // 一覧タイルの文字色 / 背景色。'#RRGGBB'、空文字なら既定(テーマ色)。
    // 本家Manidocは知らないフィールドなので、空のときはJSONに出力しない
    // （openManidoc と同じ作法。本家が読むJSONを不要に汚さないため）。
    [JsonProperty("cardForeColor", DefaultValueHandling = DefaultValueHandling.Ignore)]
    [DefaultValue("")]
    public string CardForeColor { get; set; } = "";

    [JsonProperty("cardBackColor", DefaultValueHandling = DefaultValueHandling.Ignore)]
    [DefaultValue("")]
    public string CardBackColor { get; set; } = "";

    [JsonProperty("rootNodes")]
    public List<ManidocNode> RootNodes { get; set; } = [];

    // lastSelectedNodeId / themeCssFileName など、本サーバーが明示的に扱わない
    // プロジェクト属性を、保存(save_article)時に失わないよう未知フィールドを往復させる。
    [JsonExtensionData]
    private IDictionary<string, JToken>? Extra { get; set; }
}

/// <summary>
/// ワークスペースのタグ定義(名前 + 任意のサムネイル画像パス)。
/// Manidoc の workspace.settings.json の tags[] と互換。
/// </summary>
public class TagDefinition
{
    [JsonProperty("name")]
    public string Name { get; set; } = "";

    [JsonProperty("imagePath")]
    public string ImagePath { get; set; } = "";
}

public record FlatNode(string Id, string Title, string Path);

public record SearchResult(
    string ProjectId,
    string ProjectName,
    string? NodeId,
    string? NodeTitle,
    string? NodePath,
    string Area,
    string Snippet
);
