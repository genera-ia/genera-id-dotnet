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
    public async Task Monta_as_rotas_de_historico_e_replay_de_webhooks()
    {
        var webhookId = Guid.NewGuid();
        var handler = new FakeHttpHandler().Enqueue(HttpStatusCode.OK,
            """{"items":[],"page":3,"pageSize":20,"totalCount":0}""");
        using var client = CreateClient(handler);

        await client.Webhooks.ListDeliveriesAsync(webhookId, page: 3);

        var (request, _) = Assert.Single(handler.Calls);
        Assert.Equal($"/api/v1/webhooks/{webhookId}/deliveries?page=3", request.RequestUri!.PathAndQuery);

        var deliveryId = Guid.NewGuid();
        var replayHandler = new FakeHttpHandler().Enqueue(HttpStatusCode.Accepted, $$"""
            {"id":"{{deliveryId}}","eventType":"user.created","status":"pending","attempts":1,
             "lastStatusCode":null,"lastError":null,"createdAt":"2026-08-30T00:00:00+00:00",
             "deliveredAt":null,"nextAttemptAt":"2026-08-30T00:00:01+00:00","payloadJson":"{}"}
            """);
        using var replayClient = CreateClient(replayHandler);

        var replayed = await replayClient.Webhooks.ReplayAsync(webhookId, deliveryId);

        var (replayRequest, _) = Assert.Single(replayHandler.Calls);
        Assert.Equal(HttpMethod.Post, replayRequest.Method);
        Assert.Equal($"/api/v1/webhooks/{webhookId}/deliveries/{deliveryId}/replay",
            replayRequest.RequestUri!.PathAndQuery);
        Assert.Equal("pending", replayed.Status);
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

    [Fact]
    public async Task RotateKeys_envia_revoke_old_keys_now_conforme_o_modo()
    {
        const string payload =
            """{"signingKeyThumbprint":"AB","oldKeysRetireAt":"2026-01-01T00:00:00+00:00","oldKeysRevokedImmediately":true}""";

        var emergencyHandler = new FakeHttpHandler().Enqueue(HttpStatusCode.OK, payload);
        using var emergencyClient = CreateClient(emergencyHandler);
        var emergency = await emergencyClient.Tenant.RotateKeysAsync(revokeOldKeysNow: true);
        var (request, body) = Assert.Single(emergencyHandler.Calls);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://id.example.com/api/v1/tenant/keys/rotate", request.RequestUri!.ToString());
        using var json = JsonDocument.Parse(body!);
        Assert.True(json.RootElement.GetProperty("revokeOldKeysNow").GetBoolean());
        Assert.True(emergency.OldKeysRevokedImmediately);

        // Sem argumento: rotação de rotina (revokeOldKeysNow = false).
        var routineHandler = new FakeHttpHandler().Enqueue(HttpStatusCode.OK, payload);
        using var routineClient = CreateClient(routineHandler);
        await routineClient.Tenant.RotateKeysAsync();
        var (_, routineBody) = Assert.Single(routineHandler.Calls);
        using var routineJson = JsonDocument.Parse(routineBody!);
        Assert.False(routineJson.RootElement.GetProperty("revokeOldKeysNow").GetBoolean());
    }

    [Fact]
    public async Task Cria_organizacao()
    {
        var handler = new FakeHttpHandler().Enqueue(HttpStatusCode.Created, """
            {"id":"0f8fad5b-d9cb-469f-a165-70867728950e","name":"Acme Corp","slug":"acme-corp",
             "metadataJson":null,"createdByUserId":null,"createdAt":"2026-09-01T00:00:00+00:00"}
            """);
        using var client = CreateClient(handler);

        var organization = await client.Organizations.CreateAsync(new CreateOrganizationRequest("Acme Corp"));

        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://id.example.com/api/v1/organizations", request.RequestUri!.ToString());
        using var json = JsonDocument.Parse(body!);
        Assert.Equal("Acme Corp", json.RootElement.GetProperty("name").GetString());
        Assert.Equal("acme-corp", organization.Slug);
    }

    [Fact]
    public async Task Monta_as_rotas_de_memberships_e_rejeita_remover_o_ultimo_owner()
    {
        var organizationId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var listHandler = new FakeHttpHandler().Enqueue(HttpStatusCode.OK,
            """{"items":[],"page":2,"pageSize":50,"totalCount":0}""");
        using var listClient = CreateClient(listHandler);
        await listClient.Organizations.Memberships.ListAsync(organizationId, page: 2);
        var (listRequest, _) = Assert.Single(listHandler.Calls);
        Assert.Equal($"/api/v1/organizations/{organizationId}/memberships?page=2", listRequest.RequestUri!.PathAndQuery);

        var addHandler = new FakeHttpHandler().Enqueue(HttpStatusCode.Created, $$"""
            {"id":"{{Guid.NewGuid()}}","organizationId":"{{organizationId}}","userId":"{{userId}}",
             "userEmail":null,"userDisplayName":null,"role":"owner","createdAt":"2026-09-01T00:00:00+00:00"}
            """);
        using var addClient = CreateClient(addHandler);
        var membership = await addClient.Organizations.Memberships.AddAsync(
            organizationId, new CreateMembershipRequest(userId, "owner"));
        Assert.Equal("owner", membership.Role);

        var updateHandler = new FakeHttpHandler().Enqueue(HttpStatusCode.OK, $$"""
            {"id":"{{Guid.NewGuid()}}","organizationId":"{{organizationId}}","userId":"{{userId}}",
             "userEmail":null,"userDisplayName":null,"role":"admin","createdAt":"2026-09-01T00:00:00+00:00"}
            """);
        using var updateClient = CreateClient(updateHandler);
        await updateClient.Organizations.Memberships.UpdateRoleAsync(
            organizationId, userId, new UpdateMembershipRequest("admin"));
        var (updateRequest, _) = Assert.Single(updateHandler.Calls);
        Assert.Equal(HttpMethod.Patch, updateRequest.Method);
        Assert.Equal($"/api/v1/organizations/{organizationId}/memberships/{userId}", updateRequest.RequestUri!.PathAndQuery);

        // A API rejeita remover o único "owner" com 409.
        var conflictHandler = new FakeHttpHandler().Enqueue(HttpStatusCode.Conflict,
            """{"title":"A organização precisa de pelo menos um membro com papel 'owner'."}""");
        using var conflictClient = CreateClient(conflictHandler);
        var exception = await Assert.ThrowsAsync<GeneraIdException>(() =>
            conflictClient.Organizations.Memberships.RemoveAsync(organizationId, userId));
        Assert.Equal(409, exception.StatusCode);
    }

    [Fact]
    public async Task Cria_convite_com_link_uma_unica_vez_e_revoga()
    {
        var organizationId = Guid.NewGuid();
        var invitationId = Guid.NewGuid();

        var createHandler = new FakeHttpHandler().Enqueue(HttpStatusCode.Created, $$"""
            {"id":"{{invitationId}}","organizationId":"{{organizationId}}","email":"ana@acme.com","role":"member",
             "status":"pending","expiresAt":"2026-09-08T00:00:00+00:00","createdAt":"2026-09-01T00:00:00+00:00",
             "acceptedAt":null,"link":"https://acme.accounts.genera.ia.br/organizations/invitations/accept?token=abc"}
            """);
        using var createClient = CreateClient(createHandler);
        var invitation = await createClient.Organizations.Invitations.CreateAsync(
            organizationId, new CreateInvitationRequest("ana@acme.com", "member"));
        Assert.Contains("token=abc", invitation.Link);

        var revokeHandler = new FakeHttpHandler().Enqueue(HttpStatusCode.OK, $$"""
            {"id":"{{invitationId}}","organizationId":"{{organizationId}}","email":"ana@acme.com","role":"member",
             "status":"revoked","expiresAt":"2026-09-08T00:00:00+00:00","createdAt":"2026-09-01T00:00:00+00:00",
             "acceptedAt":null}
            """);
        using var revokeClient = CreateClient(revokeHandler);
        var revoked = await revokeClient.Organizations.Invitations.RevokeAsync(organizationId, invitationId);
        var (revokeRequest, _) = Assert.Single(revokeHandler.Calls);
        Assert.Equal(HttpMethod.Post, revokeRequest.Method);
        Assert.Equal($"/api/v1/organizations/{organizationId}/invitations/{invitationId}/revoke",
            revokeRequest.RequestUri!.PathAndQuery);
        Assert.Equal("revoked", revoked.Status);
    }

    [Fact]
    public async Task Lista_organizacoes_do_usuario()
    {
        var userId = Guid.NewGuid();
        var handler = new FakeHttpHandler().Enqueue(HttpStatusCode.OK, $$"""
            [{"organizationId":"{{Guid.NewGuid()}}","organizationName":"Acme Corp","organizationSlug":"acme-corp",
              "role":"owner","createdAt":"2026-09-01T00:00:00+00:00"}]
            """);
        using var client = CreateClient(handler);

        var organizations = await client.Users.ListOrganizationsAsync(userId);

        Assert.Equal($"/api/v1/users/{userId}/organizations", handler.Calls[0].Request.RequestUri!.PathAndQuery);
        Assert.Equal("owner", Assert.Single(organizations).Role);
    }

    [Fact]
    public async Task ResetMfa_faz_delete_em_users_id_mfa()
    {
        var userId = Guid.NewGuid();
        var handler = new FakeHttpHandler().Enqueue(HttpStatusCode.NoContent);
        using var client = CreateClient(handler);

        await client.Users.ResetMfaAsync(userId);

        var (request, _) = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal($"/api/v1/users/{userId}/mfa", request.RequestUri!.PathAndQuery);
    }

    [Fact]
    public async Task Cria_application_com_requireMfa_e_backChannelLogoutUri()
    {
        var handler = new FakeHttpHandler().Enqueue(HttpStatusCode.Created, """
            {"clientId":"painel","displayName":"Painel","clientType":"public","consentType":"implicit",
             "redirectUris":["https://acme.com/cb"],"postLogoutRedirectUris":[],
             "backChannelLogoutUri":"https://acme.com/bcl","requireMfa":true}
            """);
        using var client = CreateClient(handler);

        var application = await client.Applications.CreateAsync(new CreateApplicationRequest(
            "painel", "Painel", ["https://acme.com/cb"],
            BackChannelLogoutUri: "https://acme.com/bcl", RequireMfa: true));

        var body = handler.Calls[0].Body!;
        Assert.Contains("\"requireMfa\":true", body);
        Assert.Contains("\"backChannelLogoutUri\":\"https://acme.com/bcl\"", body);
        Assert.True(application.RequireMfa);
        Assert.Equal("https://acme.com/bcl", application.BackChannelLogoutUri);
    }

    private const string SamlConnectionJson = """
        {"id":"7c9e6679-7425-40de-944b-e07fc1f90ae7","name":"Acme","enabled":true,"idpConfigured":true,
         "idpEntityId":"https://sts.windows.net/abc/","idpSsoUrl":"https://login.microsoftonline.com/abc/saml2",
         "idpMetadataUrl":null,"metadataRefreshedAt":null,"metadataRefreshError":null,
         "idpCertificates":[{"thumbprint":"AB","subject":"CN=idp","notAfter":"2030-01-01T00:00:00+00:00","retireAt":null}],
         "attributeMapping":{"email":"mail"},"stableIdAttribute":null,"jitProvisioning":true,"trustIdpMfa":false,
         "organizationId":null,"defaultRole":"member","domains":[{"domain":"acme.com.br","enforceSso":true}],
         "serviceProvider":{"entityId":"https://acme.accounts.genera.ia.br/saml/7c9e6679-7425-40de-944b-e07fc1f90ae7",
           "acsUrls":["https://acme.accounts.genera.ia.br/saml/7c9e6679-7425-40de-944b-e07fc1f90ae7/acs"],
           "metadataUrl":"https://acme.accounts.genera.ia.br/saml/7c9e6679-7425-40de-944b-e07fc1f90ae7/metadata"},
         "createdAt":"2026-09-26T00:00:00+00:00","updatedAt":"2026-09-26T00:00:00+00:00"}
        """;

    [Fact]
    public async Task Cria_conexao_SAML_e_le_o_service_provider()
    {
        var handler = new FakeHttpHandler().Enqueue(HttpStatusCode.Created, SamlConnectionJson);
        using var client = CreateClient(handler);

        var connection = await client.SamlConnections.CreateAsync(new CreateSamlConnectionRequest(
            "Acme", [new SamlDomain("acme.com.br", EnforceSso: true)],
            IdpMetadataUrl: "https://login.microsoftonline.com/abc/federationmetadata.xml"));

        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://id.example.com/api/v1/saml-connections", request.RequestUri!.ToString());
        using var json = JsonDocument.Parse(body!);
        Assert.True(json.RootElement.GetProperty("domains")[0].GetProperty("enforceSso").GetBoolean());
        Assert.False(json.RootElement.TryGetProperty("idpMetadataXml", out _));
        Assert.Equal("https://acme.accounts.genera.ia.br/saml/7c9e6679-7425-40de-944b-e07fc1f90ae7/acs",
            connection.ServiceProvider.AcsUrls[0]);
        Assert.Equal("mail", connection.AttributeMapping!["email"]);
    }

    [Fact]
    public async Task Monta_as_rotas_de_update_dominios_e_exclusao_da_conexao()
    {
        var id = Guid.Parse("7c9e6679-7425-40de-944b-e07fc1f90ae7");
        var handler = new FakeHttpHandler()
            .Enqueue(HttpStatusCode.OK, SamlConnectionJson)
            .Enqueue(HttpStatusCode.OK, SamlConnectionJson)
            .Enqueue(HttpStatusCode.NoContent);
        using var client = CreateClient(handler);

        await client.SamlConnections.UpdateAsync(id, new UpdateSamlConnectionRequest(TrustIdpMfa: true, OrganizationId: ""));
        await client.SamlConnections.ReplaceDomainsAsync(id, [new SamlDomain("acme.com")]);
        await client.SamlConnections.DeleteAsync(id);

        Assert.Equal(HttpMethod.Patch, handler.Calls[0].Request.Method);
        Assert.Equal($"https://id.example.com/api/v1/saml-connections/{id}", handler.Calls[0].Request.RequestUri!.ToString());
        using (var update = JsonDocument.Parse(handler.Calls[0].Body!))
        {
            Assert.True(update.RootElement.GetProperty("trustIdpMfa").GetBoolean());
            Assert.Equal("", update.RootElement.GetProperty("organizationId").GetString());
            Assert.False(update.RootElement.TryGetProperty("name", out _));
        }

        Assert.Equal(HttpMethod.Put, handler.Calls[1].Request.Method);
        Assert.EndsWith($"/saml-connections/{id}/domains", handler.Calls[1].Request.RequestUri!.ToString());
        using (var domains = JsonDocument.Parse(handler.Calls[1].Body!))
        {
            Assert.Equal("acme.com", domains.RootElement.GetProperty("domains")[0].GetProperty("domain").GetString());
        }

        Assert.Equal(HttpMethod.Delete, handler.Calls[2].Request.Method);
    }

    [Fact]
    public async Task Tenants_update_libera_o_SSO_pela_plataforma()
    {
        var tenantId = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");
        var handler = new FakeHttpHandler().Enqueue(HttpStatusCode.OK, """
            {"id":"0f8fad5b-d9cb-469f-a165-70867728950e","slug":"acme","name":"Acme","status":"active",
             "createdAt":"2026-08-30T00:00:00+00:00","brandingJson":null,"settingsJson":null,"customDomain":null,
             "ssoEnabled":true}
            """);
        using var client = CreateClient(handler);

        var tenant = await client.Tenants.UpdateAsync(tenantId, new UpdateTenantPlatformRequest(SsoEnabled: true));

        var (request, body) = Assert.Single(handler.Calls);
        Assert.Equal(HttpMethod.Patch, request.Method);
        Assert.Equal($"https://id.example.com/api/v1/tenants/{tenantId}", request.RequestUri!.ToString());
        Assert.Contains("\"ssoEnabled\":true", body);
        Assert.True(tenant.SsoEnabled);
    }
}
