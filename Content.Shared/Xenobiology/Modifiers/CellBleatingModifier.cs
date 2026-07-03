using Content.Shared.Xenobiology.Components.Container;
using Content.Shared.Xenobiology.Systems;

namespace Content.Shared.Xenobiology.Modifiers;

public sealed partial class CellBleatingModifier : CellModifier
{
    public override void OnAdd(Entity<CellContainerComponent> ent, Cell cell, IEntityManager entityManager)
    {
    }

    public override void OnRemove(Entity<CellContainerComponent> ent, Cell cell, IEntityManager entityManager)
    {
    }
}
