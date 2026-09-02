# GeneraId.Sdk

SDK oficial do [Genera ID](https://genera-id.onrender.com/docs) para .NET: cliente tipado da **Management API** (`/api/v1/*`) e verificação de assinatura de **webhooks**.

> A integração de login (OIDC) não precisa de SDK — aponte `AddOpenIdConnect`/`AddJwtBearer` para o discovery do seu tenant. Veja [a documentação](https://genera-id.onrender.com/docs/oidc).

Requisitos: .NET 8+. Sem dependências além da BCL.

## Instalação

```bash
dotnet add package GeneraId.Sdk
```

Enquanto a primeira versão não sai no NuGet, referencie o projeto ou o repositório `genera-ia/genera-id-dotnet`.

## Management API

```csharp
using GeneraId.Sdk;

using var generaId = new GeneraIdClient(new GeneraIdClientOptions
{
    ApiKey = Environment.GetEnvironmentVariable("GENERA_ID_API_KEY")!, // gid_sk_…
    BaseUrl = new Uri("https://genera-id.onrender.com"),
});

// Cadastre a aplicação OIDC do seu produto
var app = await generaId.Applications.CreateAsync(new CreateApplicationRequest(
    ClientId: "portal",
    DisplayName: "Portal Acme",
    RedirectUris: ["https://portal.acme.com.br/callback"]));

// Usuários e auditoria (somente leitura)
var usuarios = await generaId.Users.ListAsync(query: "ana@acme");

// Rotação de chaves de assinatura (30 dias de graça no JWKS)
await generaId.Tenant.RotateKeysAsync();
// Emergência (chave comprometida): aposenta as antigas na hora
await generaId.Tenant.RotateKeysAsync(revokeOldKeysNow: true);

// Organizações: workspaces dentro do tenant, com membros e convites
var org = await generaId.Organizations.CreateAsync(new CreateOrganizationRequest("Acme Corp"));
await generaId.Organizations.Memberships.AddAsync(org.Id, new CreateMembershipRequest(userId, "owner"));
var invitation = await generaId.Organizations.Invitations.CreateAsync(
    org.Id, new CreateInvitationRequest("ana@acme.com", "member"));
// invitation.Link aparece só na criação — use se não quiser depender só do e-mail
```

Em apps ASP.NET, injete um `HttpClient` do `IHttpClientFactory` no segundo parâmetro do construtor. Recursos: `Tenant` (Get/Update/RotateKeys), `Tenants` (chave de plataforma), `ApiKeys`, `Applications`, `Webhooks`, `Organizations` (com `.Memberships` e `.Invitations`), `Users` (com `.ListOrganizationsAsync`), `Audits`. Erros viram `GeneraIdException` com `StatusCode` e `Body`; `429`/`5xx` têm retry automático com backoff (configure com `MaxRetries`).

## Webhooks

O corpo **bruto** da requisição é obrigatório — verifique antes de qualquer parse:

```csharp
app.MapPost("/webhooks/genera-id", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var body = await reader.ReadToEndAsync();

    var valid = WebhookSignature.Verify(
        secret: Environment.GetEnvironmentVariable("GENERA_ID_WEBHOOK_SECRET")!, // gid_whsec_…
        timestamp: request.Headers["X-GeneraId-Timestamp"].ToString(),
        body: body,
        signature: request.Headers["X-GeneraId-Signature"].ToString());

    if (!valid) return Results.BadRequest();

    // Trate como entrega "ao menos uma vez": handler idempotente.
    return Results.Accepted();
});
```

A comparação é de tempo constante e timestamps além da tolerância (padrão 5 minutos) são rejeitados.

## Desenvolvimento

```bash
dotnet test GeneraIdSdk.slnx
```

[Changelog](CHANGELOG.md) · Licença: MIT.
