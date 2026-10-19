using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace GrafcetStudio.Domain.Models;

public class PlcBlock
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    [JsonPropertyName("memberVarIds")]
    public IList<string> MemberVarIds { get; init; } = new List<string>();

    [JsonPropertyName("comment")]
    public string Comment { get; init; } = string.Empty;
}
