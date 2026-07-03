using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.Xenobiology.Components.Machines;

/// <summary>
/// Injects a fused cell from a Petri dish into a live animal for later mutation in a Growing Vat.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CellMutagenicInjectorComponent : Component
{
    public const string BodyContainerId = "injector-bodyContainer";

    [DataField]
    public string DishSlot = "dishSlot";

    [DataField]
    public TimeSpan InjectionDelay = TimeSpan.FromSeconds(5);

    [ViewVariables]
    public ContainerSlot BodyContainer = default!;

    [ViewVariables]
    public Cell? LoadedCell;

    [ViewVariables]
    public bool HasInjected;
}
