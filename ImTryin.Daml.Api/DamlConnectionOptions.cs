using System.ComponentModel.DataAnnotations;
using ImTryin.Daml.Api.AccessTokens;

namespace ImTryin.Daml.Api;

public class DamlConnectionOptions : IValidatableObject
{
    public string Address { get; set; } = "http://localhost:6865";

    public string? AccessToken { get; set; }

    public string? Party { get; set; }

    public class V1Args
    {
        public bool Admin { get; set; }
        public string ActAs { get; set; } = string.Empty;
        public string ReadAs { get; set; } = string.Empty;
    }

    public V1Args? V1 { get; set; }

    public class V2Args
    {
        public string User { get; set; } = string.Empty;
    }

    public V2Args? V2 { get; set; }

    public string ApplicationId { get; set; } = "ImTryin Daml Client";

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(Address))
            yield return new ValidationResult("Address must be specified.");

        if (!Address.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !Address.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            yield return new ValidationResult("Address must be valid fully-qualified http or https URL.");

        if (string.IsNullOrEmpty(AccessToken) && string.IsNullOrEmpty(Party) && V1 == null && V2 == null)
            yield return new ValidationResult("AccessToken or Party or V1 or V2 must be specified.");

        if (!string.IsNullOrEmpty(AccessToken) && !string.IsNullOrEmpty(Party))
            yield return new ValidationResult("AccessToken and Party cannot be specified simultaneously.");

        if (!string.IsNullOrEmpty(AccessToken) && V1 != null)
            yield return new ValidationResult("AccessToken and V1 cannot be specified simultaneously.");

        if (!string.IsNullOrEmpty(AccessToken) && V2 != null)
            yield return new ValidationResult("AccessToken and V2 cannot be specified simultaneously.");

        if (!string.IsNullOrEmpty(Party) && V1 != null)
            yield return new ValidationResult("Party and V1 cannot be specified simultaneously.");

        if (!string.IsNullOrEmpty(Party) && V2 != null)
            yield return new ValidationResult("Party and V2 cannot be specified simultaneously.");

        if (V1 != null && V2 != null)
            yield return new ValidationResult("V1 and V2 cannot be specified simultaneously.");

        if (V1 != null && V1.ActAs.Length == 0)
            yield return new ValidationResult("V1.ActAs must be specified.");
    }


    public class RuntimeArgs
    {
        public string AccessToken { get; internal set; } = string.Empty;

        public PayloadV1? PayloadV1 { get; internal set; }
        public PayloadV2? PayloadV2 { get; internal set; }
    }

    public RuntimeArgs? Runtime { get; internal set; }
}