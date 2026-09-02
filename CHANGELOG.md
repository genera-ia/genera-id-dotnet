# Changelog

Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.0.0/); versionamento [SemVer](https://semver.org/lang/pt-BR/).

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

[0.3.0]: https://github.com/genera-ia/genera-id-dotnet/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/genera-ia/genera-id-dotnet/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/genera-ia/genera-id-dotnet/releases/tag/v0.1.0
