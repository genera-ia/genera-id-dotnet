using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace GeneraId.Sdk.Tests;

public class WebhookSignatureTests
{
    private const string Secret = "gid_whsec_teste";
    private const string Body = """{"event":"user.created","data":{"id":"abc"}}""";
    private const string Timestamp = "1700000000";

    private static readonly DateTimeOffset Now =
        DateTimeOffset.FromUnixTimeSeconds(1_700_000_010); // 10s depois do envio

    private static string Sign(string timestamp, string body, string secret = Secret)
    {
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}"));
        return "v1=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    [Fact]
    public void Aceita_assinatura_valida_dentro_da_tolerancia() =>
        Assert.True(WebhookSignature.Verify(Secret, Timestamp, Body, Sign(Timestamp, Body), now: Now));

    [Fact]
    public void Rejeita_corpo_adulterado() =>
        Assert.False(WebhookSignature.Verify(
            Secret, Timestamp, Body.Replace("abc", "xyz"), Sign(Timestamp, Body), now: Now));

    [Fact]
    public void Rejeita_segredo_errado() =>
        Assert.False(WebhookSignature.Verify(
            Secret, Timestamp, Body, Sign(Timestamp, Body, "gid_whsec_outro"), now: Now));

    [Fact]
    public void Rejeita_timestamp_fora_da_tolerancia() =>
        Assert.False(WebhookSignature.Verify(
            Secret, Timestamp, Body, Sign(Timestamp, Body), now: Now.AddSeconds(301)));

    [Fact]
    public void Aceita_timestamp_antigo_com_tolerancia_desligada() =>
        Assert.True(WebhookSignature.Verify(
            Secret, Timestamp, Body, Sign(Timestamp, Body), tolerance: TimeSpan.Zero, now: Now.AddDays(2)));

    [Fact]
    public void Rejeita_assinatura_de_tamanho_diferente_sem_lancar() =>
        Assert.False(WebhookSignature.Verify(Secret, Timestamp, Body, "v1=curta", now: Now));

    [Fact]
    public void Rejeita_timestamp_nao_numerico() =>
        Assert.False(WebhookSignature.Verify(Secret, "ontem", Body, Sign("ontem", Body), now: Now));
}
