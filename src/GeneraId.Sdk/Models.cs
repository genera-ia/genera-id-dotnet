namespace GeneraId.Sdk;

/// <summary>Formatos de dados da Management API (`/api/v1/*`), espelhando os DTOs do servidor.</summary>
public sealed record Tenant(
    Guid Id,
    string Slug,
    string Name,
    string Status,
    DateTimeOffset CreatedAt,
    string? BrandingJson,
    string? SettingsJson,
    string? CustomDomain,
    bool SsoEnabled = false);

public sealed record CreateTenantRequest(
    string Slug,
    string Name,
    string? BrandingJson = null,
    string? SettingsJson = null);

/// <summary>Resposta do onboarding — <see cref="ApiKey"/> (`gid_sk_…`) aparece uma única vez.</summary>
public sealed record CreatedTenant(Tenant Tenant, string ApiKey);

/// <summary>Campos nulos não são enviados (não alteram); <c>CustomDomain = ""</c> remove o domínio.</summary>
public sealed record UpdateTenantRequest(
    string? Name = null,
    string? BrandingJson = null,
    string? SettingsJson = null,
    string? CustomDomain = null);

/// <summary>Ajustes que só a plataforma faz (chave de plataforma). Campos nulos não alteram.</summary>
/// <param name="SsoEnabled">Libera/bloqueia o SSO corporativo (SAML) — recurso comercial.</param>
public sealed record UpdateTenantPlatformRequest(bool? SsoEnabled = null);

/// <summary><c>RevokeOldKeysNow = true</c> (emergência) aposenta as chaves antigas na hora.</summary>
public sealed record RotateKeysRequest(bool RevokeOldKeysNow);

public sealed record KeyRotationResult(
    string SigningKeyThumbprint, DateTimeOffset OldKeysRetireAt, bool OldKeysRevokedImmediately = false);

public sealed record ApiKey(
    Guid Id,
    string Name,
    string Prefix,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? RevokedAt);

/// <summary>Resposta da criação — <see cref="Key"/> (`gid_sk_…`) aparece uma única vez.</summary>
public sealed record CreatedApiKey(ApiKey ApiKey, string Key);

public sealed record Application(
    string? ClientId,
    string? DisplayName,
    string? ClientType,
    string? ConsentType,
    IReadOnlyList<string> RedirectUris,
    IReadOnlyList<string> PostLogoutRedirectUris,
    string? ClientSecret = null,
    string? BackChannelLogoutUri = null,
    bool RequireMfa = false);

/// <param name="BackChannelLogoutUri">Endpoint que recebe o logout_token (Back-Channel Logout 1.0).</param>
/// <param name="RequireMfa">true = todo login neste client exige segundo fator.</param>
public sealed record CreateApplicationRequest(
    string ClientId,
    string DisplayName,
    IReadOnlyList<string> RedirectUris,
    string ClientType = "public",
    string ConsentType = "implicit",
    IReadOnlyList<string>? PostLogoutRedirectUris = null,
    string? BackChannelLogoutUri = null,
    bool RequireMfa = false);

/// <param name="BackChannelLogoutUri">Nulo/vazio remove o endpoint.</param>
/// <param name="RequireMfa">Nulo = não altera.</param>
public sealed record UpdateApplicationRequest(
    string DisplayName,
    IReadOnlyList<string> RedirectUris,
    string? ConsentType = null,
    IReadOnlyList<string>? PostLogoutRedirectUris = null,
    string? BackChannelLogoutUri = null,
    bool? RequireMfa = null);

public sealed record WebhookEndpoint(
    Guid Id,
    string Url,
    IReadOnlyList<string> Events,
    DateTimeOffset CreatedAt,
    string? Secret = null);

/// <summary>
/// `Events` vazio/nulo = todos. Ex.: user.created, user.updated, session.created,
/// user.mfaEnabled, user.mfaDisabled, user.mfaReset, organization.*.
/// </summary>
public sealed record CreateWebhookRequest(string Url, IReadOnlyList<string>? Events = null);

/// <summary>
/// Entrega persistida de um webhook (histórico de 30 dias; replay disponível).
/// `Status`: "pending" | "succeeded" | "failed"; `PayloadJson` é o corpo exato
/// enviado ao endpoint (byte a byte).
/// </summary>
public sealed record WebhookDeliveryRecord(
    Guid Id,
    string EventType,
    string Status,
    int Attempts,
    int? LastStatusCode,
    string? LastError,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DeliveredAt,
    DateTimeOffset? NextAttemptAt,
    string PayloadJson);

public sealed record User(
    Guid Id,
    string? UserName,
    string? Email,
    string? DisplayName,
    bool EmailConfirmed,
    bool TwoFactorEnabled,
    bool LockedOut,
    DateTimeOffset CreatedAt);

public sealed record LoginAudit(
    Guid Id,
    string Event,
    Guid? UserId,
    string? Identifier,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset CreatedAt);

/// <summary>
/// Organização (workspace) dentro do tenant. Papéis de membership são strings
/// opacas — o Genera ID só garante que nunca fica sem nenhum "owner".
/// </summary>
public sealed record Organization(
    Guid Id,
    string Name,
    string Slug,
    string? MetadataJson,
    Guid? CreatedByUserId,
    DateTimeOffset CreatedAt);

/// <summary>Se <see cref="Slug"/> for omitido, é derivado do nome. Único por tenant, imutável após criado.</summary>
public sealed record CreateOrganizationRequest(string Name, string? Slug = null, string? MetadataJson = null);

public sealed record UpdateOrganizationRequest(string? Name = null, string? MetadataJson = null);

public sealed record Membership(
    Guid Id,
    Guid OrganizationId,
    Guid UserId,
    string? UserEmail,
    string? UserDisplayName,
    string Role,
    DateTimeOffset CreatedAt);

public sealed record CreateMembershipRequest(Guid UserId, string Role);

public sealed record UpdateMembershipRequest(string Role);

/// <summary>Organizações de um usuário, com o papel em cada uma — ver <c>Users.ListOrganizationsAsync</c>.</summary>
public sealed record UserOrganization(
    Guid OrganizationId, string OrganizationName, string OrganizationSlug, string Role, DateTimeOffset CreatedAt);

/// <summary>
/// `Status`: "pending" | "accepted" | "revoked" | "expired". TTL de 7 dias a
/// partir da criação. <see cref="Link"/> (o link de aceite) aparece só na
/// resposta da criação, uma única vez.
/// </summary>
public sealed record Invitation(
    Guid Id,
    Guid OrganizationId,
    string Email,
    string Role,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? AcceptedAt,
    string? Link = null);

public sealed record CreateInvitationRequest(string Email, string Role);

/// <summary>Domínio de e-mail atendido por uma conexão SAML (único por tenant).</summary>
/// <param name="EnforceSso">true = domínio só entra por SSO: sem login, recuperação, cadastro ou troca de senha.</param>
public sealed record SamlDomain(string Domain, bool EnforceSso = false);

/// <param name="RetireAt">Preenchido quando o certificado saiu da metadata do IdP: continua aceito até esta data.</param>
public sealed record SamlCertificate(string Thumbprint, string Subject, DateTimeOffset NotAfter, DateTimeOffset? RetireAt);

/// <summary>O que se cadastra no IdP da empresa (Entra: Identifier/Reply URL; Okta: Audience URI/SSO URL).</summary>
public sealed record SamlServiceProvider(string EntityId, IReadOnlyList<string> AcsUrls, string MetadataUrl);

/// <summary>Conexão de SSO corporativo: o Genera ID como SP SAML diante do IdP de uma empresa.</summary>
public sealed record SamlConnection(
    Guid Id,
    string Name,
    bool Enabled,
    string IdpEntityId,
    string IdpSsoUrl,
    string? IdpMetadataUrl,
    DateTimeOffset? MetadataRefreshedAt,
    string? MetadataRefreshError,
    IReadOnlyList<SamlCertificate> IdpCertificates,
    IReadOnlyDictionary<string, string>? AttributeMapping,
    string? StableIdAttribute,
    bool JitProvisioning,
    bool TrustIdpMfa,
    Guid? OrganizationId,
    string DefaultRole,
    IReadOnlyList<SamlDomain> Domains,
    SamlServiceProvider ServiceProvider,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Dados do IdP: <paramref name="IdpMetadataUrl"/> (HTTPS pública, atualizada todo dia),
/// <paramref name="IdpMetadataXml"/> ou os três campos manuais.
/// </summary>
/// <param name="AttributeMapping">Chaves: email, givenName, familyName, displayName → nome do atributo SAML.</param>
/// <param name="StableIdAttribute">Atributo usado como chave estável no lugar do NameID (ex.: objectidentifier no Entra).</param>
/// <param name="JitProvisioning">Cria a conta no primeiro login (padrão true no servidor); só para e-mails dos domínios.</param>
/// <param name="TrustIdpMfa">Aceita o MFA declarado pelo IdP como segundo fator.</param>
/// <param name="OrganizationId">Quem entra pela conexão vira membro desta organização.</param>
/// <param name="DefaultRole">Papel da membership automática (padrão "member" no servidor).</param>
public sealed record CreateSamlConnectionRequest(
    string Name,
    IReadOnlyList<SamlDomain> Domains,
    string? IdpMetadataUrl = null,
    string? IdpMetadataXml = null,
    string? IdpEntityId = null,
    string? IdpSsoUrl = null,
    IReadOnlyList<string>? IdpCertificates = null,
    IReadOnlyDictionary<string, string>? AttributeMapping = null,
    string? StableIdAttribute = null,
    bool? JitProvisioning = null,
    bool? Enabled = null,
    bool? TrustIdpMfa = null,
    Guid? OrganizationId = null,
    string? DefaultRole = null);

/// <summary>
/// Campos nulos não são enviados (não alteram). String vazia remove <see cref="IdpMetadataUrl"/>,
/// <see cref="StableIdAttribute"/> e <see cref="OrganizationId"/> (id da organização como string).
/// </summary>
public sealed record UpdateSamlConnectionRequest(
    string? Name = null,
    string? IdpMetadataUrl = null,
    string? IdpMetadataXml = null,
    string? IdpEntityId = null,
    string? IdpSsoUrl = null,
    IReadOnlyList<string>? IdpCertificates = null,
    IReadOnlyDictionary<string, string>? AttributeMapping = null,
    string? StableIdAttribute = null,
    bool? JitProvisioning = null,
    bool? Enabled = null,
    bool? TrustIdpMfa = null,
    string? OrganizationId = null,
    string? DefaultRole = null);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
