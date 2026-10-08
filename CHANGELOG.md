# Changelog

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/); versionamento [SemVer](https://semver.org/lang/pt-BR/).

## [0.6.0] — 2026-10-05

### Adicionado

- `CreateInvitationRequest.ApplicationClientId`: client_id de uma application do tenant para onde o convidado segue depois de aceitar ("Continuar para" na tela de aceite). `Invitation.ApplicationClientId` volta na listagem e na criação.

## [0.5.1] — 2026-10-04

### Documentação

- README: explica de onde vem a chave `gid_sk_…` — o Genera ID não tem mais cadastro self-service, e os tenants são criados pela equipe da Genera ([contato@genera.ia.br](mailto:contato@genera.ia.br)). Sai o aviso de que o pacote ainda não estava publicado. Nenhuma mudança de código.

## [0.5.0] — 2026-09-27

### Adicionado

- `SamlConnectionsResource` (SSO corporativo, SAML): `List`/`Create`/`Get`/`Update`/`ReplaceDomains`/`Delete`. A conexão traz `ServiceProvider` (Entity ID, ACS e metadata para cadastrar no IdP), domínios com `EnforceSso`, `IdpMetadataUrl` com atualização diária e certificados com `RetireAt` durante a rotação, `TrustIdpMfa`, `OrganizationId`/`DefaultRole` (membership automática), `JitProvisioning`, `AttributeMapping` e `StableIdAttribute`. A conexão pode ser criada sem os dados do IdP (`IdpConfigured = false`), recebendo a metadata depois.
- `Tenants.UpdateAsync(id, UpdateTenantPlatformRequest)` (chave de plataforma) e `Tenant.SsoEnabled`.
- Novos eventos de webhook documentados: `samlConnection.created`, `samlConnection.updated`, `samlConnection.deleted`.

Parâmetros novos entram no fim dos records, com valor padrão: o código existente continua compilando.

## [0.4.0] — 2026-09-23

### Adicionado

- `Users.ResetMfaAsync(id)`: reset de MFA de quem perdeu o autenticador. Desliga o MFA, encerra as sessões no IdP e avisa os apps (back-channel logout) e o usuário (e-mail). Idempotente.
- `Application.RequireMfa` e `RequireMfa` em `CreateApplicationRequest`/`UpdateApplicationRequest`: todo login no client exige segundo fator (no update, `null` não altera).
- `Application.BackChannelLogoutUri` e `BackChannelLogoutUri` nos requests de application (Back-Channel Logout 1.0, já suportado pela API desde 19/09).
- Novos eventos de webhook documentados: `user.mfaEnabled`, `user.mfaDisabled`, `user.mfaReset`. O payload de usuário agora traz `twoFactorEnabled`.

Parâmetros novos entram no fim dos records, com valor padrão: o código existente continua compilando.

## [0.3.0] — 2026-09-02

### Adicionado

- `OrganizationsResource`: CRUD de organizações (workspaces dentro do tenant), `MembershipsResource` (`List`/`Add`/`UpdateRole`/`Remove`) e `InvitationsResource` (`List`/`Create`/`Revoke`). `Invitations.CreateAsync` devolve `Link` uma única vez na resposta, como o secret de webhook.
- `Users.ListOrganizationsAsync` — organizações de um usuário no tenant, com o papel em cada uma.
- `Tenant.RotateKeysAsync(revokeOldKeysNow: true)` para revogação emergencial (chave comprometida): aposenta as chaves antigas na hora em vez da graça de 30 dias. Retrocompatível — o overload sem flag (e `RotateKeysAsync()`) continua fazendo a rotação de rotina. `KeyRotationResult` ganha `OldKeysRevokedImmediately`; novo `RotateKeysRequest`.

## [0.2.0] — 2026-08-30

### Adicionado

- `Webhooks.ListDeliveriesAsync` e `Webhooks.ReplayAsync` — histórico de entregas do endpoint (30 dias de retenção) e reenvio do mesmo payload.

## [0.1.0] — 2026-08-30

### Adicionado

- Release inicial: cliente tipado da Management API (`Tenant`, `Tenants`, `ApiKeys`, `Applications`, `Webhooks`, `Users`, `Audits`), com retry automático em `429`/`5xx` (backoff configurável via `MaxRetries`) e erros tipados (`GeneraIdException`).
- `WebhookSignature.Verify` — verificação de assinatura de webhooks (HMAC-SHA256, comparação de tempo constante, tolerância de timestamp configurável).

[0.5.1]: https://github.com/genera-ia/genera-id-dotnet/compare/v0.5.0...v0.5.1
[0.5.0]: https://github.com/genera-ia/genera-id-dotnet/compare/v0.4.0...v0.5.0
[0.4.0]: https://github.com/genera-ia/genera-id-dotnet/compare/v0.3.0...v0.4.0
[0.3.0]: https://github.com/genera-ia/genera-id-dotnet/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/genera-ia/genera-id-dotnet/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/genera-ia/genera-id-dotnet/releases/tag/v0.1.0
