using System;
using HandlebarsDotNet;

namespace GrafcetStudio.CodeGen.Template;

/// <summary>Compiles a template with the real Handlebars engine to surface syntax errors.</summary>
public static class TemplateSyntaxChecker
{
    /// <returns>The engine's error message, or null when the template compiles.</returns>
    public static string? Check(string content)
    {
        try
        {
            Handlebars.Create().Compile(content);
            return null;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
