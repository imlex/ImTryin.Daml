using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ImTryin.Daml.Api.AccessTokens;

public class DamlAccessTokenUtil
{
    private static readonly JsonWebTokenHandler __jsonWebTokenHandler = new()
    {
        SetDefaultTimesOnTokenCreation = false
    };

    public static IPayload Parse(string token)
    {
        var payloadString = Base64UrlEncoder.Decode(__jsonWebTokenHandler.ReadJsonWebToken(token).EncodedPayload);

        try
        {
            var payload = JsonSerializer.Deserialize<PayloadV1>(payloadString);
            if (payload == null)
                throw new ArgumentNullException(nameof(token));
            return payload;
        }
        catch
        {
            // ignored
        }

        try
        {
            var payload = JsonSerializer.Deserialize<PayloadV2>(payloadString);
            if (payload == null)
                throw new ArgumentNullException(nameof(token));
            return payload;
        }
        catch
        {
            // ignored
        }

        throw new ArgumentException("Unable to parse '" + token + "' access token!", nameof(token));
    }

    public static string GenerateSandboxTokenV1(string ledgerId, string applicationId, string party, out PayloadV1 payloadV1)
    {
        payloadV1 = new PayloadV1(new PayloadV1.PayloadV1Data(ledgerId, applicationId, false, new[] {party}, Array.Empty<string>()));
        return GenerateSandboxToken(payloadV1);
    }

    public static string GenerateSandboxTokenV1(string ledgerId, string applicationId, bool admin, string[] actAs, string[] readAs, out PayloadV1 payloadV1)
    {
        payloadV1 = new PayloadV1(new PayloadV1.PayloadV1Data(ledgerId, applicationId, admin, actAs, readAs));
        return GenerateSandboxToken(payloadV1);
    }

    public static string GenerateSandboxTokenV2(string user, out PayloadV2 payloadV2)
    {
        payloadV2 = new PayloadV2(user);
        return GenerateSandboxToken(payloadV2);
    }

    private static string GenerateSandboxToken(IPayload payload)
    {
        var payloadString = JsonSerializer.Serialize(payload);

        var token = __jsonWebTokenHandler.CreateToken(payloadString);

        return token;
    }
}