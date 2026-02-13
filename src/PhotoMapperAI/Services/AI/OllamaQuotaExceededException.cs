namespace PhotoMapperAI.Services.AI;

/// <summary>
/// Raised when Ollama cloud returns HTTP 409 due to quota/limit exhaustion.
/// </summary>
public sealed class OllamaQuotaExceededException : Exception
{
    public OllamaQuotaExceededException(string message)
        : base(message)
    {
    }
}

