using Content.Shared.Physics;
using Content.Shared.Xenobiology.Components.Container;
using JetBrains.Annotations;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Systems;

namespace Content.Shared.Xenobiology.Modifiers;

[Serializable, UsedImplicitly]
public sealed partial class CellSmallSizeModifier : CellModifier
{
    public override void OnAdd(Entity<CellContainerComponent> ent, Cell cell, IEntityManager entityManager)
    {
        base.OnAdd(ent, cell, entityManager);

        if (!entityManager.TryGetComponent<FixturesComponent>(ent, out var fixtures))
            return;

        var physicsSys = entityManager.System<SharedPhysicsSystem>();

        foreach (var (fixtureId, fixture) in fixtures.Fixtures)
        {
            if (fixture.Shape is PhysShapeCircle)
            {
                physicsSys.SetRadius(ent, fixtureId, fixture, fixture.Shape, 0.20f);
                physicsSys.SetCollisionMask(ent, fixtureId, fixture, (int)CollisionGroup.SmallMobMask);
                physicsSys.SetCollisionLayer(ent, fixtureId, fixture, (int)CollisionGroup.SmallMobLayer);
            }
        }
    }

    public override void OnRemove(Entity<CellContainerComponent> ent, Cell cell, IEntityManager entityManager)
    {
        base.OnRemove(ent, cell, entityManager);
    }
}
