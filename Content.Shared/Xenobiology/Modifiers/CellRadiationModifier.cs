using Content.Shared.Xenobiology.Components.Container;
using Content.Shared.Xenobiology.Systems;
using Content.Shared.Radiation.Components;

namespace Content.Shared.Xenobiology.Modifiers;

public sealed partial class CellRadiationModifier : CellModifier
{
    public override void OnAdd(Entity<CellContainerComponent> ent, Cell cell, IEntityManager entityManager)
    {
        entityManager.EnsureComponent<RadiationSourceComponent>(ent);
    }

    public override void OnRemove(Entity<CellContainerComponent> ent, Cell cell, IEntityManager entityManager)
    {
    }
}
