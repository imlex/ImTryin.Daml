namespace ImTryin.Daml.Api.AccessTokens;

public record PayloadV2(string Sub, string Scope = "daml_ledger_api") : IPayload;