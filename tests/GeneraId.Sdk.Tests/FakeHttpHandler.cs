using System.Net;
using System.Text;

namespace GeneraId.Sdk.Tests;

/// <summary>Handler fake: devolve respostas enfileiradas e captura as requisições (com corpo lido).</summary>
public sealed class FakeHttpHandler : HttpMessageHandler
{
    private readonly Queue<HttpResponseMessage> _responses = new();

    public List<(HttpRequestMessage Request, string? Body)> Calls { get; } = [];

    public FakeHttpHandler Enqueue(HttpStatusCode status, string? json = null, string? retryAfter = null)
    {
        var response = new HttpResponseMessage(status);
        if (json is not null)
        {
            response.Content = new StringContent(json, Encoding.UTF8, "application/json");
        }

        if (retryAfter is not null)
        {
            response.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
        }

        _responses.Enqueue(response);
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken);
        Calls.Add((request, body));

        return _responses.Count > 0
            ? _responses.Dequeue()
            : new HttpResponseMessage(HttpStatusCode.NotImplemented);
    }
}
