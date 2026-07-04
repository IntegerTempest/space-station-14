using Content.Shared.Materials;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared.Xenobiology.Components.Machines;

[RegisterComponent, NetworkedComponent]
public sealed partial class CellularFusionComponent : Component
{
    [DataField]
    public string DishSlot = "dishSlot";

    [DataField]
    public ProtoId<MaterialPrototype> RequiredMaterial = "Plasma";

    [ViewVariables]
    public int MaterialAmount;

    [DataField]
    public float BaseFailureChance = 0.05f;

    [DataField]
    public float StabilityMultiplier = 0.5f;

    [DataField]
    public float BaseMutationChance = 0.05f;

    [DataField]
    public float SpliceDelay = 5f;

    [ViewVariables]
    public bool SpliceInProgress;
}
