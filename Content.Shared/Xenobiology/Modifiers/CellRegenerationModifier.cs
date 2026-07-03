using Content.Shared.Xenobiology.Components.Container;
using Content.Shared.Xenobiology.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs;

namespace Content.Shared.Xenobiology.Modifiers;

public sealed partial class CellRegenerationModifier : CellModifier
{
    public override void OnAdd(Entity<CellContainerComponent> ent, Cell cell, IEntityManager entityManager)
    {
        if (entityManager.TryGetComponent(ent, out PassiveDamageComponent? passive))
            return;

        var comp = entityManager.AddComponent<PassiveDamageComponent>(ent);
        comp.Damage = new DamageSpecifier();
        comp.Interval = 3f;
        comp.AllowedStates.Add(MobState.Alive);
        entityManager.Dirty(ent, comp);
    }

    public override void OnRemove(Entity<CellContainerComponent> ent, Cell cell, IEntityManager entityManager)
    {
    }
}
