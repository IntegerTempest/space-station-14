using Content.Shared.Xenobiology.Components.Container;
using Content.Shared.Xenobiology.Systems;
using Content.Shared.Weapons.Reflect;

namespace Content.Shared.Xenobiology.Modifiers;

public sealed partial class CellReflectionModifier : CellModifier
{
    public override void OnAdd(Entity<CellContainerComponent> ent, Cell cell, IEntityManager entityManager)
    {
        if (entityManager.TryGetComponent(ent, out ReflectComponent? reflect))
            return;

        var comp = entityManager.AddComponent<ReflectComponent>(ent);
        comp.ReflectProb = 0.5f;
        entityManager.Dirty(ent, comp);
    }

    public override void OnRemove(Entity<CellContainerComponent> ent, Cell cell, IEntityManager entityManager)
    {
    }
}
