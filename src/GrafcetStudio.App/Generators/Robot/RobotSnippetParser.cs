using System;
using System.Collections.Generic;
using System.Text.Json;

namespace GrafcetStudio.App.Generators.Robot;

public record SnippetParseResult(
    bool IsOk,
    SnippetMapDocument? Document,
    List<string> Errors
);

public class RobotSnippetParser
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public SnippetParseResult Parse(string rawText)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(rawText))
        {
            errors.Add("AI completion returned empty text.");
            return new SnippetParseResult(false, null, errors);
        }

        var cleanText = StripMarkdownFences(rawText);

        try
        {
            var doc = JsonSerializer.Deserialize<SnippetMapDocument>(cleanText, JsonOpts);

            if (doc == null)
            {
                errors.Add("Deserialization resulted in null SnippetMapDocument.");
                return new SnippetParseResult(false, null, errors);
            }

            if (!string.Equals(doc.Schema, "grafectstudio/robot-snippet-map/v1", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"Invalid schema '{doc.Schema}'. Expected 'grafectstudio/robot-snippet-map/v1'.");
                return new SnippetParseResult(false, doc, errors);
            }

            return new SnippetParseResult(true, doc, errors);
        }
        catch (JsonException jex)
        {
            errors.Add($"JSON Parse Error: {jex.Message}");
            return new SnippetParseResult(false, null, errors);
        }
        catch (Exception ex)
        {
            errors.Add($"Unexpected Parse Error: {ex.Message}");
            return new SnippetParseResult(false, null, errors);
        }
    }

    private static string StripMarkdownFences(string input)
    {
        var text = input.Trim();
        if (text.StartsWith("```"))
        {
            int firstLineBreak = text.IndexOf('\n');
            if (firstLineBreak != -1)
            {
                text = text.Substring(firstLineBreak + 1);
            }
            if (text.EndsWith("```"))
            {
                text = text.Substring(0, text.Length - 3);
            }
        }
        return text.Trim();
    }
}
