using Content.Shared.Xenobiology.Components.Container;
using Content.Shared.Xenobiology.Systems;
using Content.Shared.Stealth.Components;

namespace Content.Shared.Xenobiology.Modifiers;

public sealed partial class CellInvisibilityModifier : CellModifier
{
    public override void OnAdd(Entity<CellContainerComponent> ent, Cell cell, IEntityManager entityManager)
    {
        entityManager.EnsureComponent<StealthComponent>(ent);
        entityManager.EnsureComponent<StealthOnMoveComponent>(ent);
    }

    public override void OnRemove(Entity<CellContainerComponent> ent, Cell cell, IEntityManager entityManager)
    {
    }
}
