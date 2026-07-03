using Content.Shared.Movement.Components;
using Content.Shared.Xenobiology.Components.Container;
using JetBrains.Annotations;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Xenobiology.Modifiers;

[Serializable, UsedImplicitly]
public sealed partial class CellFlightModifier : CellModifier
{
    public override void OnAdd(Entity<CellContainerComponent> ent, Cell cell, IEntityManager entityManager)
    {
        base.OnAdd(ent, cell, entityManager);
        entityManager.EnsureComponent<CanMoveInAirComponent>(ent);

        if (entityManager.TryGetComponent<PhysicsComponent>(ent, out var physics))
        {
            var physicsSys = entityManager.System<SharedPhysicsSystem>();
            physicsSys.SetBodyStatus(ent, physics, BodyStatus.InAir);
        }
    }

    public override void OnRemove(Entity<CellContainerComponent> ent, Cell cell, IEntityManager entityManager)
    {
        base.OnRemove(ent, cell, entityManager);
    }
}
