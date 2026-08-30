using System.Security.Cryptography;
using System.Text;

namespace GeneraId.Sdk;

/// <summary>Verificação da assinatura de entregas de webhook do Genera ID.</summary>
public static class WebhookSignature
{
    /// <summary>
    /// Verifica <c>v1=</c> + HMAC-SHA256(<paramref name="secret"/>, <c>{timestamp}.{body}</c>)
    /// em comparação de tempo constante, rejeitando timestamps fora da tolerância (replay).
    /// </summary>
    /// <param name="secret">Segredo `gid_whsec_…`, recebido uma única vez na criação do endpoint.</param>
    /// <param name="timestamp">Cabeçalho <c>X-GeneraId-Timestamp</c> (Unix, em segundos).</param>
    /// <param name="body">Corpo BRUTO da requisição, antes de qualquer parse.</param>
    /// <param name="signature">Cabeçalho <c>X-GeneraId-Signature</c> (<c>v1=&lt;hex&gt;</c>).</param>
    /// <param name="tolerance">Idade máxima aceita (padrão 5 minutos; <see cref="TimeSpan.Zero"/> desliga).</param>
    /// <param name="now">Instante atual (padrão <see cref="DateTimeOffset.UtcNow"/>; útil em testes).</param>
    public static bool Verify(
        string secret,
        string timestamp,
        string body,
        string signature,
        TimeSpan? tolerance = null,
        DateTimeOffset? now = null)
    {
        var maxAge = tolerance ?? TimeSpan.FromMinutes(5);
        if (maxAge > TimeSpan.Zero)
        {
            if (!long.TryParse(timestamp, out var sentAtUnix))
            {
                return false;
            }

            var sentAt = DateTimeOffset.FromUnixTimeSeconds(sentAtUnix);
            var delta = (now ?? DateTimeOffset.UtcNow) - sentAt;
            if (delta > maxAge || delta < -maxAge)
            {
                return false;
            }
        }

        var payload = Encoding.UTF8.GetBytes($"{timestamp}.{body}");
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), payload);
        var expected = Encoding.UTF8.GetBytes("v1=" + Convert.ToHexString(hash).ToLowerInvariant());
        var received = Encoding.UTF8.GetBytes(signature);

        return CryptographicOperations.FixedTimeEquals(expected, received);
    }
}
