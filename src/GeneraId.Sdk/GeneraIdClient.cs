using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GeneraId.Sdk;

public sealed class GeneraIdClientOptions
{
    /// <summary>Chave de tenant (`gid_sk_…`) ou de plataforma, conforme os recursos usados.</summary>
    public required string ApiKey { get; init; }

    /// <summary>Origem do serviço, ex.: `https://genera-id.onrender.com`.</summary>
    public required Uri BaseUrl { get; init; }

    /// <summary>Tentativas extras em 429/5xx/erro de rede (padrão 2; 0 desliga).</summary>
    public int MaxRetries { get; init; } = 2;
}

/// <summary>Cliente da Management API do Genera ID (`/api/v1/*`).</summary>
public sealed class GeneraIdClient : IDisposable
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly HashSet<HttpStatusCode> RetryableStatus =
    [
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout,
    ];

    private readonly HttpClient _http;
    private readonly bool _ownsHttpClient;
    private readonly GeneraIdClientOptions _options;

    /// <param name="httpClient">
    /// Opcional — informe o HttpClient do seu container de DI (IHttpClientFactory);
    /// sem ele, o cliente cria e gerencia o próprio.
    /// </param>
    public GeneraIdClient(GeneraIdClientOptions options, HttpClient? httpClient = null)
    {
        if (string.IsNullOrEmpty(options.ApiKey))
        {
            throw new ArgumentException("ApiKey é obrigatória.", nameof(options));
        }

        _options = options;
        _ownsHttpClient = httpClient is null;
        _http = httpClient ?? new HttpClient();

        Tenant = new TenantResource(this);
        Tenants = new TenantsResource(this);
        ApiKeys = new ApiKeysResource(this);
        Applications = new ApplicationsResource(this);
        Webhooks = new WebhooksResource(this);
        Organizations = new OrganizationsResource(this);
        Users = new UsersResource(this);
        Audits = new AuditsResource(this);
    }

    /// <summary>O próprio tenant da chave `gid_sk_…`.</summary>
    public TenantResource Tenant { get; }

    /// <summary>Onboarding e listagem de tenants — requer a chave de PLATAFORMA.</summary>
    public TenantsResource Tenants { get; }

    public ApiKeysResource ApiKeys { get; }

    public ApplicationsResource Applications { get; }

    public WebhooksResource Webhooks { get; }

    /// <summary>Organizações (workspaces) do tenant — criação, membros e convites.</summary>
    public OrganizationsResource Organizations { get; }

    public UsersResource Users { get; }

    public AuditsResource Audits { get; }

    public void Dispose()
    {
        if (_ownsHttpClient)
        {
            _http.Dispose();
        }
    }

    public sealed class TenantResource(GeneraIdClient client)
    {
        public Task<Tenant> GetAsync(CancellationToken cancellationToken = default) =>
            client.SendAsync<Tenant>(HttpMethod.Get, "/api/v1/tenant", null, cancellationToken);

        public Task<Tenant> UpdateAsync(UpdateTenantRequest request, CancellationToken cancellationToken = default) =>
            client.SendAsync<Tenant>(HttpMethod.Patch, "/api/v1/tenant", request, cancellationToken);

        /// <summary>Rotaciona as chaves de assinatura; as antigas seguem no JWKS por 30 dias.</summary>
        public Task<KeyRotationResult> RotateKeysAsync(CancellationToken cancellationToken = default) =>
            RotateKeysAsync(revokeOldKeysNow: false, cancellationToken);

        /// <summary>
        /// Rotaciona as chaves de assinatura. Com <paramref name="revokeOldKeysNow"/> = true
        /// (emergência, chave comprometida) as antigas são aposentadas na hora — saem do JWKS
        /// e todo token assinado com elas passa a ser rejeitado; senão seguem no JWKS por 30 dias.
        /// </summary>
        public Task<KeyRotationResult> RotateKeysAsync(bool revokeOldKeysNow, CancellationToken cancellationToken = default) =>
            client.SendAsync<KeyRotationResult>(HttpMethod.Post, "/api/v1/tenant/keys/rotate",
                new RotateKeysRequest(revokeOldKeysNow), cancellationToken);
    }

    public sealed class TenantsResource(GeneraIdClient client)
    {
        /// <summary>A `ApiKey` retornada (`gid_sk_…`) aparece uma única vez.</summary>
        public Task<CreatedTenant> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken = default) =>
            client.SendAsync<CreatedTenant>(HttpMethod.Post, "/api/v1/tenants", request, cancellationToken);

        public Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken cancellationToken = default) =>
            client.SendAsync<IReadOnlyList<Tenant>>(HttpMethod.Get, "/api/v1/tenants", null, cancellationToken);
    }

    public sealed class ApiKeysResource(GeneraIdClient client)
    {
        public Task<IReadOnlyList<ApiKey>> ListAsync(CancellationToken cancellationToken = default) =>
            client.SendAsync<IReadOnlyList<ApiKey>>(HttpMethod.Get, "/api/v1/api-keys", null, cancellationToken);

        /// <summary>O segredo retornado em `Key` aparece uma única vez.</summary>
        public Task<CreatedApiKey> CreateAsync(string name, CancellationToken cancellationToken = default) =>
            client.SendAsync<CreatedApiKey>(HttpMethod.Post, "/api/v1/api-keys", new { name }, cancellationToken);

        /// <summary>Revogação com efeito imediato.</summary>
        public Task RevokeAsync(Guid id, CancellationToken cancellationToken = default) =>
            client.SendAsync<object?>(HttpMethod.Delete, $"/api/v1/api-keys/{id}", null, cancellationToken);
    }

    public sealed class ApplicationsResource(GeneraIdClient client)
    {
        public Task<IReadOnlyList<Application>> ListAsync(CancellationToken cancellationToken = default) =>
            client.SendAsync<IReadOnlyList<Application>>(HttpMethod.Get, "/api/v1/applications", null, cancellationToken);

        /// <summary>Clients confidential recebem `ClientSecret` uma única vez. PKCE é sempre obrigatório.</summary>
        public Task<Application> CreateAsync(CreateApplicationRequest request, CancellationToken cancellationToken = default) =>
            client.SendAsync<Application>(HttpMethod.Post, "/api/v1/applications", request, cancellationToken);

        public Task<Application> GetAsync(string clientId, CancellationToken cancellationToken = default) =>
            client.SendAsync<Application>(HttpMethod.Get, $"/api/v1/applications/{Uri.EscapeDataString(clientId)}", null, cancellationToken);

        public Task<Application> UpdateAsync(string clientId, UpdateApplicationRequest request, CancellationToken cancellationToken = default) =>
            client.SendAsync<Application>(HttpMethod.Put, $"/api/v1/applications/{Uri.EscapeDataString(clientId)}", request, cancellationToken);

        /// <summary>Remove o client e revoga autorizações e tokens em cascata.</summary>
        public Task DeleteAsync(string clientId, CancellationToken cancellationToken = default) =>
            client.SendAsync<object?>(HttpMethod.Delete, $"/api/v1/applications/{Uri.EscapeDataString(clientId)}", null, cancellationToken);
    }

    public sealed class WebhooksResource(GeneraIdClient client)
    {
        public Task<IReadOnlyList<WebhookEndpoint>> ListAsync(CancellationToken cancellationToken = default) =>
            client.SendAsync<IReadOnlyList<WebhookEndpoint>>(HttpMethod.Get, "/api/v1/webhooks", null, cancellationToken);

        /// <summary>O `Secret` (`gid_whsec_…`) aparece uma única vez.</summary>
        public Task<WebhookEndpoint> CreateAsync(CreateWebhookRequest request, CancellationToken cancellationToken = default) =>
            client.SendAsync<WebhookEndpoint>(HttpMethod.Post, "/api/v1/webhooks",
                new { request.Url, Events = request.Events ?? [] }, cancellationToken);

        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            client.SendAsync<object?>(HttpMethod.Delete, $"/api/v1/webhooks/{id}", null, cancellationToken);

        /// <summary>Histórico de entregas do endpoint (mais recente primeiro; retenção de 30 dias).</summary>
        public Task<PagedResult<WebhookDeliveryRecord>> ListDeliveriesAsync(
            Guid id, int? page = null, int? pageSize = null, CancellationToken cancellationToken = default) =>
            client.SendAsync<PagedResult<WebhookDeliveryRecord>>(HttpMethod.Get,
                WithQuery($"/api/v1/webhooks/{id}/deliveries", ("page", page?.ToString()), ("pageSize", pageSize?.ToString())),
                null, cancellationToken);

        /// <summary>Reenvia a entrega (mesmo payload, byte a byte) — qualquer estado, inclusive sucesso.</summary>
        public Task<WebhookDeliveryRecord> ReplayAsync(Guid id, Guid deliveryId, CancellationToken cancellationToken = default) =>
            client.SendAsync<WebhookDeliveryRecord>(HttpMethod.Post,
                $"/api/v1/webhooks/{id}/deliveries/{deliveryId}/replay", null, cancellationToken);
    }

    public sealed class UsersResource(GeneraIdClient client)
    {
        public Task<PagedResult<User>> ListAsync(
            string? query = null, int? page = null, int? pageSize = null, CancellationToken cancellationToken = default) =>
            client.SendAsync<PagedResult<User>>(HttpMethod.Get,
                WithQuery("/api/v1/users", ("query", query), ("page", page?.ToString()), ("pageSize", pageSize?.ToString())),
                null, cancellationToken);

        public Task<User> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            client.SendAsync<User>(HttpMethod.Get, $"/api/v1/users/{id}", null, cancellationToken);

        /// <summary>Organizações do usuário no tenant, com o papel em cada uma.</summary>
        public Task<IReadOnlyList<UserOrganization>> ListOrganizationsAsync(Guid id, CancellationToken cancellationToken = default) =>
            client.SendAsync<IReadOnlyList<UserOrganization>>(HttpMethod.Get, $"/api/v1/users/{id}/organizations", null, cancellationToken);
    }

    /// <summary>
    /// Organizações (workspaces) do tenant. Criação fica só aqui (Management API)
    /// — sem self-service no Genera ID; papéis de membership são strings opacas.
    /// </summary>
    public sealed class OrganizationsResource(GeneraIdClient client)
    {
        public Task<PagedResult<Organization>> ListAsync(
            string? query = null, int? page = null, int? pageSize = null, CancellationToken cancellationToken = default) =>
            client.SendAsync<PagedResult<Organization>>(HttpMethod.Get,
                WithQuery("/api/v1/organizations", ("query", query), ("page", page?.ToString()), ("pageSize", pageSize?.ToString())),
                null, cancellationToken);

        public Task<Organization> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken = default) =>
            client.SendAsync<Organization>(HttpMethod.Post, "/api/v1/organizations", request, cancellationToken);

        public Task<Organization> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
            client.SendAsync<Organization>(HttpMethod.Get, $"/api/v1/organizations/{id}", null, cancellationToken);

        /// <summary>Slug não muda — recrie a organização se precisar de outro.</summary>
        public Task<Organization> UpdateAsync(Guid id, UpdateOrganizationRequest request, CancellationToken cancellationToken = default) =>
            client.SendAsync<Organization>(HttpMethod.Patch, $"/api/v1/organizations/{id}", request, cancellationToken);

        /// <summary>Remove em cascata memberships e convites.</summary>
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            client.SendAsync<object?>(HttpMethod.Delete, $"/api/v1/organizations/{id}", null, cancellationToken);

        public MembershipsResource Memberships { get; } = new(client);

        public InvitationsResource Invitations { get; } = new(client);
    }

    public sealed class MembershipsResource(GeneraIdClient client)
    {
        public Task<PagedResult<Membership>> ListAsync(
            Guid organizationId, int? page = null, int? pageSize = null, CancellationToken cancellationToken = default) =>
            client.SendAsync<PagedResult<Membership>>(HttpMethod.Get,
                WithQuery($"/api/v1/organizations/{organizationId}/memberships",
                    ("page", page?.ToString()), ("pageSize", pageSize?.ToString())),
                null, cancellationToken);

        /// <summary>Adiciona um usuário já existente direto — sem convite.</summary>
        public Task<Membership> AddAsync(Guid organizationId, CreateMembershipRequest request, CancellationToken cancellationToken = default) =>
            client.SendAsync<Membership>(HttpMethod.Post, $"/api/v1/organizations/{organizationId}/memberships", request, cancellationToken);

        /// <summary>Rebaixar o único membro "owner" é rejeitado (409) — a organização nunca fica sem nenhum.</summary>
        public Task<Membership> UpdateRoleAsync(
            Guid organizationId, Guid userId, UpdateMembershipRequest request, CancellationToken cancellationToken = default) =>
            client.SendAsync<Membership>(HttpMethod.Patch, $"/api/v1/organizations/{organizationId}/memberships/{userId}", request, cancellationToken);

        /// <summary>Remover o único membro "owner" é rejeitado (409).</summary>
        public Task RemoveAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken = default) =>
            client.SendAsync<object?>(HttpMethod.Delete, $"/api/v1/organizations/{organizationId}/memberships/{userId}", null, cancellationToken);
    }

    public sealed class InvitationsResource(GeneraIdClient client)
    {
        public Task<PagedResult<Invitation>> ListAsync(
            Guid organizationId, string? status = null, int? page = null, int? pageSize = null,
            CancellationToken cancellationToken = default) =>
            client.SendAsync<PagedResult<Invitation>>(HttpMethod.Get,
                WithQuery($"/api/v1/organizations/{organizationId}/invitations",
                    ("status", status), ("page", page?.ToString()), ("pageSize", pageSize?.ToString())),
                null, cancellationToken);

        /// <summary>
        /// Cria o convite e dispara o e-mail; <see cref="Invitation.Link"/> no
        /// retorno aparece só aqui (como o secret de webhook) — use se não
        /// quiser depender só do e-mail.
        /// </summary>
        public Task<Invitation> CreateAsync(Guid organizationId, CreateInvitationRequest request, CancellationToken cancellationToken = default) =>
            client.SendAsync<Invitation>(HttpMethod.Post, $"/api/v1/organizations/{organizationId}/invitations", request, cancellationToken);

        /// <summary>Só convites pendentes podem ser revogados (409 caso contrário).</summary>
        public Task<Invitation> RevokeAsync(Guid organizationId, Guid invitationId, CancellationToken cancellationToken = default) =>
            client.SendAsync<Invitation>(HttpMethod.Post,
                $"/api/v1/organizations/{organizationId}/invitations/{invitationId}/revoke", null, cancellationToken);
    }

    public sealed class AuditsResource(GeneraIdClient client)
    {
        public Task<PagedResult<LoginAudit>> ListAsync(
            Guid? userId = null, int? page = null, int? pageSize = null, CancellationToken cancellationToken = default) =>
            client.SendAsync<PagedResult<LoginAudit>>(HttpMethod.Get,
                WithQuery("/api/v1/audits", ("userId", userId?.ToString()), ("page", page?.ToString()), ("pageSize", pageSize?.ToString())),
                null, cancellationToken);
    }

    private static string WithQuery(string path, params (string Name, string? Value)[] parameters)
    {
        var builder = new StringBuilder(path);
        var separator = '?';
        foreach (var (name, value) in parameters)
        {
            if (value is not null)
            {
                builder.Append(separator).Append(name).Append('=').Append(Uri.EscapeDataString(value));
                separator = '&';
            }
        }

        return builder.ToString();
    }

    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, CancellationToken cancellationToken)
    {
        var url = new Uri(_options.BaseUrl, path);

        Exception? lastError = null;
        for (var attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            using var request = new HttpRequestMessage(method, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            if (body is not null)
            {
                request.Content = new StringContent(JsonSerializer.Serialize(body, Json), Encoding.UTF8, "application/json");
            }

            HttpResponseMessage response;
            try
            {
                response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException networkError)
            {
                lastError = networkError;
                if (attempt < _options.MaxRetries)
                {
                    await Task.Delay(BackoffDelay(attempt), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                throw new GeneraIdException($"Falha de rede ao chamar {method} {path}.", 0, inner: networkError);
            }

            using (response)
            {
                if (RetryableStatus.Contains(response.StatusCode) && attempt < _options.MaxRetries)
                {
                    await Task.Delay(RetryAfterDelay(response, attempt), cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new GeneraIdException(
                        $"{method} {path} respondeu {(int)response.StatusCode}.", (int)response.StatusCode, content);
                }

                if (response.StatusCode == HttpStatusCode.NoContent || string.IsNullOrEmpty(content))
                {
                    return default!;
                }

                return JsonSerializer.Deserialize<T>(content, Json)
                    ?? throw new GeneraIdException($"{method} {path} devolveu um corpo inesperado.", (int)response.StatusCode, content);
            }
        }

        // Inalcançável: o laço sempre retorna ou lança.
        throw new GeneraIdException($"Falha ao chamar {method} {path}.", 0, inner: lastError);
    }

    private static TimeSpan BackoffDelay(int attempt) => TimeSpan.FromMilliseconds(250 * Math.Pow(2, attempt));

    private static TimeSpan RetryAfterDelay(HttpResponseMessage response, int attempt)
    {
        var delta = response.Headers.RetryAfter?.Delta;
        if (delta is { } value && value >= TimeSpan.Zero && value <= TimeSpan.FromSeconds(60))
        {
            return value;
        }

        return BackoffDelay(attempt);
    }
}
