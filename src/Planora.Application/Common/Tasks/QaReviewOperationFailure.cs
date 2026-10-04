namespace Planora.Application.Common.Tasks;

public enum QaReviewOperationFailure
{
    None = 0,
    NotFound = 1,
    Forbidden = 2,
    Invalid = 3,
    Conflict = 4,
    ReadOnly = 5
}
