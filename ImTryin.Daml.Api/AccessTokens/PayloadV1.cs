using System.Text.Json.Serialization;

namespace ImTryin.Daml.Api.AccessTokens;

public record PayloadV1([property: JsonPropertyName("https://daml.com/ledger-api")] PayloadV1.PayloadV1Data Data) : IPayload
{
    public record PayloadV1Data(
        string LedgerId,
        string ApplicationId,
        bool Admin,
        string[] ActAs,
        string[] ReadAs
    );
}