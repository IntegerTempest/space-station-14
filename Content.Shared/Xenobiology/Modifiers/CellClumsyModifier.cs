using Content.Shared.Xenobiology.Components.Container;
using Content.Shared.Xenobiology.Systems;
using Content.Shared.Clumsy;

namespace Content.Shared.Xenobiology.Modifiers;

public sealed partial class CellClumsyModifier : CellModifier
{
    public override void OnAdd(Entity<CellContainerComponent> ent, Cell cell, IEntityManager entityManager)
    {
        entityManager.EnsureComponent<ClumsyComponent>(ent);
    }

    public override void OnRemove(Entity<CellContainerComponent> ent, Cell cell, IEntityManager entityManager)
    {
    }
}
