using System.Net;
using System.Text.Json;
using Xunit;

namespace GeneraId.Sdk.Tests;

public class GeneraIdClientTests
{
    private static GeneraIdClient CreateClient(FakeHttpHandler handler, int maxRetries = 0) =>
        new(new GeneraIdClientOptions
        {
            ApiKey = "gid_sk_teste",
            BaseUrl = new Uri("https://id.example.com"),
            MaxRetries = maxRetries,
        }, new HttpClient(handler));

    [Fact]
    public void Exige_api_key()
    {
        Assert.Throws<ArgumentException>(() => new GeneraIdClient(new GeneraIdClientOptions
        {
            ApiKey = "",
            BaseUrl = new Uri("https://id.example.com"),
        }));
    }

    [Fact]
    public async Task Envia_authorization_e_monta_a_url()
    {
        var handler = new FakeHttpHandler().Enqueue(HttpStatusCode.OK, "[]");
        using var client = CreateClient(handler);

        await client.Applications.ListAsync();

        var (request, _) = Assert.Single(handler.Calls);
        Assert.Equal("https://id.example.com/api/v1/applications", request.RequestUri!.ToString());
        Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
        Assert.Equal("gid_sk_teste", request.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task Serializa_o_body_em_camel_case_no_post()
    {
        var handler = new FakeHttpHandler().Enqueue(HttpStatusCode.Created,
            """{"clientId":"portal","redirectUris":[],"postLogoutRedirectUris":[]}""");
        using var client = CreateClient(handler);

        var application = await client.Applications.CreateAsync(new CreateApplicationRequest(
            "portal", "Portal Acme", ["https://acme.com/callback"]));

        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, request.Method);
        using var json = JsonDocument.Parse(body!);
        Assert.Equal("portal", json.RootElement.GetProperty("clientId").GetString());
        Assert.Equal("public", json.RootElement.GetProperty("clientType").GetString());
        Assert.Equal("portal", application.ClientId);
    }

    [Fact]
    public async Task Patch_do_tenant_omite_campos_nulos()
    {
        var tenantJson = """
            {"id":"0f8fad5b-d9cb-469f-a165-70867728950e","slug":"acme","name":"Acme","status":"active",
             "createdAt":"2026-08-30T00:00:00+00:00","brandingJson":null,"settingsJson":null,"customDomain":null}
            """;
        var handler = new FakeHttpHandler().Enqueue(HttpStatusCode.OK, tenantJson);
        using var client = CreateClient(handler);

        await client.Tenant.UpdateAsync(new UpdateTenantRequest(Name: "Acme Corp"));

        var (_, body) = Assert.Single(handler.Calls);
        using var json = JsonDocument.Parse(body!);
        Assert.Equal("Acme Corp", json.RootElement.GetProperty("name").GetString());
        Assert.False(json.RootElement.TryGetProperty("customDomain", out _));
    }

    [Fact]
    public async Task Monta_a_query_de_paginacao_de_usuarios()
    {
        var handler = new FakeHttpHandler().Enqueue(HttpStatusCode.OK,
            """{"items":[],"page":2,"pageSize":50,"totalCount":0}""");
        using var client = CreateClient(handler);

        var result = await client.Users.ListAsync(query: "ana@acme", page: 2, pageSize: 50);

        var (request, _) = Assert.Single(handler.Calls);
        Assert.Equal("/api/v1/users?query=ana%40acme&page=2&pageSize=50", request.RequestUri!.PathAndQuery);
        Assert.Equal(2, result.Page);
    }

    [Fact]
    public async Task Trata_204_como_conclusao_sem_corpo()
    {
        var handler = new FakeHttpHandler().Enqueue(HttpStatusCode.NoContent);
        using var client = CreateClient(handler);

        await client.ApiKeys.RevokeAsync(Guid.NewGuid());

        var (request, _) = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Delete, request.Method);
    }

    [Fact]
    public async Task Erro_4xx_vira_excecao_com_status_e_corpo()
    {
        var handler = new FakeHttpHandler().Enqueue(HttpStatusCode.Conflict, """{"detail":"slug em uso"}""");
        using var client = CreateClient(handler);

        var exception = await Assert.ThrowsAsync<GeneraIdException>(() =>
            client.Tenants.CreateAsync(new CreateTenantRequest("acme", "Acme")));

        Assert.Equal(409, exception.StatusCode);
        Assert.Contains("slug em uso", exception.Body);
    }

    [Fact]
    public async Task Faz_retry_em_429_e_depois_sucede()
    {
        var handler = new FakeHttpHandler()
            .Enqueue(HttpStatusCode.TooManyRequests, retryAfter: "0")
            .Enqueue(HttpStatusCode.OK, """{"items":[],"page":1,"pageSize":20,"totalCount":0}""");
        using var client = CreateClient(handler, maxRetries: 2);

        var result = await client.Audits.ListAsync();

        Assert.Equal(2, handler.Calls.Count);
        Assert.Equal(0, result.TotalCount);
    }

    [Fact]
    public async Task Nao_faz_retry_em_4xx_comum()
    {
        var handler = new FakeHttpHandler().Enqueue(HttpStatusCode.NotFound, "\"nada\"");
        using var client = CreateClient(handler, maxRetries: 2);

        var exception = await Assert.ThrowsAsync<GeneraIdException>(() => client.Users.GetAsync(Guid.NewGuid()));

        Assert.Equal(404, exception.StatusCode);
        Assert.Single(handler.Calls);
    }

    [Fact]
    public async Task Desserializa_o_envelope_de_criacao_de_tenant()
    {
        var handler = new FakeHttpHandler().Enqueue(HttpStatusCode.Created, """
            {"tenant":{"id":"0f8fad5b-d9cb-469f-a165-70867728950e","slug":"acme","name":"Acme",
             "status":"active","createdAt":"2026-08-30T00:00:00+00:00","brandingJson":null,
             "settingsJson":null,"customDomain":null},"apiKey":"gid_sk_nova"}
            """);
        using var client = CreateClient(handler);

        var created = await client.Tenants.CreateAsync(new CreateTenantRequest("acme", "Acme"));

        Assert.Equal("acme", created.Tenant.Slug);
        Assert.Equal("gid_sk_nova", created.ApiKey);
    }
}
