namespace Planora.Domain.Enums;

public enum RequirementCoverageState
{
    MissingDesign = 1,
    MissingImplementation,
    MissingTest,
    NotVerified,
    VerificationFailed,
    Verified,
    ValidationPending,
    ValidationFailed,
    FullyValidated
}
