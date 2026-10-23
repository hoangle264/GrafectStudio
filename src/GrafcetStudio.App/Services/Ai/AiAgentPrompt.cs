using System.Text;
using System.Text.Json.Nodes;

namespace GrafcetStudio.App.Services.Ai;

/// <summary>System prompt and tool declarations for the GrafcetStudio assistant.</summary>
public static class AiAgentPrompt
{
    public const int MaxMessageChars = 4000;

    /// <summary>Read tools and the context scope whose permission each one needs (enforced in the WebView).</summary>
    public static readonly IReadOnlyDictionary<string, string> ReadToolScopes = new Dictionary<string, string>
    {
        ["list_diagrams"] = "diagram",
        ["get_diagram"] = "step",
        ["list_variables"] = "variable",
        ["get_io_mapping"] = "io",
        ["list_device_types"] = "structure",
        ["list_units"] = "unit"
    };

    public static string BuildSystemPrompt()
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are the GrafcetStudio assistant for PLC/robot automation engineers. The user is an expert; be concise and technical.");
        sb.AppendLine("Reply in Vietnamese by default (keep identifiers, variable names and technical terms as they are). Use another language only if the user clearly writes in it.");
        sb.AppendLine();
        sb.AppendLine("### HOW YOU WORK");
        sb.AppendLine("- Every turn you must call tools. Read only the data you need for the request.");
        sb.AppendLine("- The user approves each kind of data before it is shared. A tool may return {\"denied\": true}; then work with what you have or explain what is missing. Never guess project content.");
        sb.AppendLine("- Never invent ids, variable names or IO points; use only values returned by tools.");
        sb.AppendLine("- Finish with exactly ONE of:");
        sb.AppendLine("  * submit_proposal: when the user wants to change the project. The app validates it; if it returns accepted=false, fix the listed errors and submit again.");
        sb.AppendLine("  * reply: for questions, explanations and reviews, or when you cannot make a valid proposal.");
        sb.AppendLine();
        sb.AppendLine("### PROPOSALS");
        sb.AppendLine($"proposal_json is one JSON object with schemaVersion \"{AiContractGuard.SchemaVersion}\", id, intent, status \"draft\", requestId \"agent\", summary, warnings (array), data.");
        sb.AppendLine("Supported intents and their data:");
        foreach (var intent in AiIntentNames)
            sb.AppendLine($"- {intent}: {AiPromptBuilder.IntentRequirements(intent)}");
        sb.AppendLine("A proposal never changes the project by itself; the user previews and applies it.");
        sb.AppendLine();
        sb.AppendLine("### REVIEWING A FLOW");
        sb.AppendLine("When asked to review/check a Grafcet flow: call list_diagrams if you need the id, then get_diagram, and if conditions/actions reference variables, list_variables.");
        sb.AppendLine("Report concrete findings with step numbers / transition ids, most severe first:");
        sb.AppendLine("- steps without an outgoing transition (dead end) other than an intended final step; steps unreachable from the initial step; missing or multiple initial steps;");
        sb.AppendLine("- alternative branches whose conditions are identical or can be true at the same time (an EMPTY transition condition is valid: it means true or an implicit wait for the previous step to complete, so never report it as a problem);");
        sb.AppendLine("- actions or conditions that use variables that do not exist;");
        sb.AppendLine("- connections that reference missing steps/transitions.");
        sb.AppendLine("If nothing is wrong, say so. Do not propose changes unless the user asks; reviews are read-only.");
        return sb.ToString();
    }

    private static readonly string[] AiIntentNames = { "create-variable", "clone-variable", "map-io", "create-flow", "create-structure" };

    public static JsonArray BuildTools()
    {
        var declarations = new JsonArray
        {
            Tool("list_diagrams", "List the project's Grafcet diagrams (id, name, mode, unit)."),
            Tool("get_diagram", "Get one diagram's steps, transitions, connections and actions.", ("diagramId", "string", "Diagram id from list_diagrams.", true)),
            Tool("list_variables", "List project variables (label, format, address, signal addresses, comment)."),
            Tool("get_io_mapping", "Get physical IO points and their current mapping to application variables."),
            Tool("list_device_types", "List names of existing device types/structures."),
            Tool("list_units", "List machine units."),
            Tool("submit_proposal", "Submit one change proposal for the user to preview and apply.", ("proposal_json", "string", "The proposal as a JSON object serialized to a string.", true)),
            Tool("reply", "Show the final answer to the user and end the turn.", ("text", "string", "Answer in the user's language. Plain text; use '- ' for lists.", true))
        };
        return new JsonArray { new JsonObject { ["functionDeclarations"] = declarations } };
    }

    private static JsonObject Tool(string name, string description, params (string Name, string Type, string Description, bool Required)[] parameters)
    {
        var tool = new JsonObject { ["name"] = name, ["description"] = description };
        if (parameters.Length == 0) return tool;

        var properties = new JsonObject();
        foreach (var p in parameters)
            properties[p.Name] = new JsonObject { ["type"] = p.Type, ["description"] = p.Description };
        tool["parameters"] = new JsonObject
        {
            ["type"] = "object",
            ["properties"] = properties,
            ["required"] = new JsonArray(parameters.Where(p => p.Required).Select(p => (JsonNode)JsonValue.Create(p.Name)!).ToArray())
        };
        return tool;
    }
}
