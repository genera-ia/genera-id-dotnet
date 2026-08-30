namespace GeneraId.Sdk;

/// <summary>Erro de uma chamada à Management API do Genera ID.</summary>
public class GeneraIdException(string message, int statusCode, string? body = null, Exception? inner = null)
    : Exception(message, inner)
{
    /// <summary>Status HTTP da resposta (0 quando a requisição nem chegou ao servidor).</summary>
    public int StatusCode { get; } = statusCode;

    /// <summary>Corpo bruto da resposta, quando houver (ProblemDetails do ASP.NET ou texto).</summary>
    public string? Body { get; } = body;
}
