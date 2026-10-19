namespace GrafcetStudio.App.Expressions;

public sealed class LogicExpressionParseException : Exception
{
    public LogicExpressionParseException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
