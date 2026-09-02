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
    string? CustomDomain);

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
    string? ClientSecret = null);

public sealed record CreateApplicationRequest(
    string ClientId,
    string DisplayName,
    IReadOnlyList<string> RedirectUris,
    string ClientType = "public",
    string ConsentType = "implicit",
    IReadOnlyList<string>? PostLogoutRedirectUris = null);

public sealed record UpdateApplicationRequest(
    string DisplayName,
    IReadOnlyList<string> RedirectUris,
    string? ConsentType = null,
    IReadOnlyList<string>? PostLogoutRedirectUris = null);

public sealed record WebhookEndpoint(
    Guid Id,
    string Url,
    IReadOnlyList<string> Events,
    DateTimeOffset CreatedAt,
    string? Secret = null);

/// <summary>`Events` vazio/nulo = todos (user.created, user.updated, session.created).</summary>
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

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
