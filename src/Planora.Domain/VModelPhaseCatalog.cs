using Planora.Domain.Enums;
namespace Planora.Domain;
public static class VModelPhaseCatalog
{
    public static readonly IReadOnlyList<VModelPhaseType> Ordered =
    [VModelPhaseType.Requirements, VModelPhaseType.SystemDesign, VModelPhaseType.DetailedDesign,
     VModelPhaseType.Implementation, VModelPhaseType.UnitTesting, VModelPhaseType.IntegrationTesting,
     VModelPhaseType.SystemTesting, VModelPhaseType.AcceptanceValidation];
    public static int OrderOf(VModelPhaseType phase)
    {
        for (int index = 0; index < Ordered.Count; index++)
            if (Ordered[index] == phase) return index + 1;
        throw new ArgumentOutOfRangeException(nameof(phase));
    }
    public static VModelPhaseType ForTestLevel(VModelTestLevel level) => level switch
    { VModelTestLevel.Unit => VModelPhaseType.UnitTesting,
      VModelTestLevel.Integration => VModelPhaseType.IntegrationTesting,
      VModelTestLevel.System => VModelPhaseType.SystemTesting,
      VModelTestLevel.Acceptance => VModelPhaseType.AcceptanceValidation,
      _ => throw new ArgumentOutOfRangeException(nameof(level)) };
}
