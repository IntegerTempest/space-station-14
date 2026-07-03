using Content.Shared.Overlays;
using Content.Shared.Xenobiology.Components.Container;
using JetBrains.Annotations;

namespace Content.Shared.Xenobiology.Modifiers;

[Serializable, UsedImplicitly]
public sealed partial class CellNightVisionModifier : CellModifier
{
    public override void OnAdd(Entity<CellContainerComponent> ent, Cell cell, IEntityManager entityManager)
    {
        base.OnAdd(ent, cell, entityManager);
        entityManager.EnsureComponent<NightVisionComponent>(ent);
    }

    public override void OnRemove(Entity<CellContainerComponent> ent, Cell cell, IEntityManager entityManager)
    {
        base.OnRemove(ent, cell, entityManager);
    }
}
