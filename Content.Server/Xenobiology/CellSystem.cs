using Content.Server.Animals.Components;
using Content.Server.Chemistry.Components;
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Server.Speech.Components;
using Content.Shared.Animals;
using Content.Shared.Clumsy;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Movement.Components;
using Content.Shared.Nutrition.Components;
using Content.Shared.Overlays;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Radiation.Components;
using Content.Shared.Slippery;
using Content.Shared.Species.Components;
using Content.Shared.Spider;
using Content.Shared.Stealth.Components;
using Content.Shared.Weapons.Reflect;
using Content.Shared.Whitelist;
using Content.Shared.Xenobiology;
using Content.Shared.Xenobiology.Components.Container;
using Content.Shared.Xenobiology.Components.Tools;
using Content.Shared.Xenobiology.Systems;
using Content.Shared.Xenobiology.Visuals;
using Robust.Server.GameObjects;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Collision.Shapes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Random;

namespace Content.Server.Xenobiology;

/// <summary>
/// Server-side cell system that handles trait detection, random cell generation,
/// and collector tool interactions (biopsy/transfer).
/// </summary>
public sealed partial class CellSystem : SharedCellSystem
{
    [Dependency] private AppearanceSystem _appearance = default!;
    [Dependency] private DamageableSystem _damageable = default!;
    [Dependency] private DoAfterSystem _doAfter = default!;
    [Dependency] private EntityWhitelistSystem _entityWhitelist = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IRobustRandom _random = default!;

    /// <summary>
    /// Random name prefixes for generated cells.
    /// </summary>
    private static readonly string[] CellPrefixes =
    [
        "Crypto", "Xeno", "Neo", "Proto", "Meta",
        "Hyper", "Micro", "Nano", "Poly", "Terra",
        "Aqua", "Aero", "Ferro", "Cryo", "Electro",
        "Photo", "Thermo", "Bio", "Chrono", "Helio",
        "Magneto", "Omni", "Pseudo", "Quantum", "Synthe"
    ];

    /// <summary>
    /// Random name suffixes for generated cells.
    /// </summary>
    private static readonly string[] CellSuffixes =
    [
        "cyte", "blast", "morph", "plasm", "some",
        "zyme", "gene", "mer", "oid", "ase",
        "ite", "ium", "in", "ex", "oma",
        "ism", "arch", "phil", "phage", "trope",
        "noid", "form", "type", "gen", "sphere"
    ];

    /// <summary>
    /// Subscribes to collector interaction events.
    /// </summary>
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CellCollectorComponent, BeforeRangedInteractEvent>(OnCollectorInteract);
        SubscribeLocalEvent<CellCollectorComponent, CellCollectorDoAfter>(OnCollectorCollectDoAfter);
    }

    /// <summary>
    /// Starts a DoAfter for biopsy or cell transfer when interacting with a target.
    /// Determines direction based on whether the target has cells or detectable traits.
    /// </summary>
    private void OnCollectorInteract(Entity<CellCollectorComponent> ent, ref BeforeRangedInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is null)
            return;

        if (!TryComp<CellContainerComponent>(args.Target, out var containerComponent))
            return;

        var hasTraits = DetectAnimalTraits(args.Target.Value).Count > 0;
        var direction = containerComponent.Empty && !hasTraits
            ? CellCollectorDirection.Transfer
            : CellCollectorDirection.Collection;

        if (!CollectorInteractValidate(ent, (args.Target.Value, containerComponent), direction))
            return;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, ent.Comp.Delay, new CellCollectorDoAfter(direction), ent, target: args.Target, used: ent)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.5f,
            BlockDuplicate = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
        args.Handled = true;
    }

    /// <summary>
    /// Completes the collector DoAfter. For collection: copies existing cells or generates
    /// a random cell from animal traits, then caches it on the target. For transfer: copies
    /// cells to the target and clears the collector.
    /// </summary>
    private void OnCollectorCollectDoAfter(Entity<CellCollectorComponent> ent, ref CellCollectorDoAfter args)
    {
        if (args.Handled || args.Cancelled || args.Target is null)
            return;

        if (!CollectorInteractValidate(ent, args.Target.Value, args.Direction))
            return;

        switch (args.Direction)
        {
            case CellCollectorDirection.Collection:
                if (TryComp<CellContainerComponent>(args.Target.Value, out var targetComp) &&
                    targetComp.Cells.Count > 0)
                {
                    CopyCells(ent.Owner, args.Target.Value);
                }
                else
                {
                    var modifiers = DetectAnimalTraits(args.Target.Value);
                    var cell = GenerateRandomCell(modifiers);
                    AddCell(ent.Owner, cell);
                    // Cache on animal so subsequent biopsies copy instead of re-generating
                    if (TryComp<CellContainerComponent>(args.Target.Value, out var animalContainer))
                    {
                        animalContainer.Cells.Add(cell);
                        Dirty(args.Target.Value, animalContainer);
                    }
                }

                _popup.PopupPredicted(Loc.GetString("cell-collector-collected"), ent, null);

                if (ent.Comp.Damage is not null)
                    _damageable.TryChangeDamage(args.Target.Value, ent.Comp.Damage);

                ent.Comp.Usages--;
                break;

            case CellCollectorDirection.Transfer:
                CopyCells(args.Target.Value, ent.Owner);
                ClearCells(ent.Owner);

                _popup.PopupPredicted(Loc.GetString("cell-collector-transfer"), ent, null);
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }

        UpdateCollectorAppearance(ent);
        args.Handled = true;
    }

    /// <summary>
    /// Scans an entity for biological components and fixture properties to determine
    /// which cell modifiers its cells should contain. Checks for vision, movement,
    /// production, combat, and size-related components.
    /// </summary>
    private List<ProtoId<CellModifierPrototype>> DetectAnimalTraits(EntityUid source)
    {
        var modifiers = new List<ProtoId<CellModifierPrototype>>();

        if (HasComp<NightVisionComponent>(source))
            modifiers.Add("NightVision");

        if (HasComp<CanMoveInAirComponent>(source))
            modifiers.Add("Flight");

        if (HasComp<UdderComponent>(source))
            modifiers.Add("Lactation");

        if (HasComp<EggLayerComponent>(source))
            modifiers.Add("EggLayer");

        if (HasComp<WoolyComponent>(source))
            modifiers.Add("Wooly");

        if (HasComp<MeleeChemicalInjectorComponent>(source))
            modifiers.Add("Venomous");

        if (HasComp<SpiderComponent>(source))
            modifiers.Add("WebSpinning");

        if (HasComp<PassiveDamageComponent>(source))
            modifiers.Add("Regeneration");

        if (HasComp<NoSlipComponent>(source))
            modifiers.Add("NoSlip");

        if (HasComp<ReflectComponent>(source))
            modifiers.Add("Reflection");

        if (HasComp<StealthComponent>(source) || HasComp<StealthOnMoveComponent>(source))
            modifiers.Add("Invisibility");

        if (HasComp<RadiationSourceComponent>(source))
            modifiers.Add("Radiation");

        if (HasComp<ReformComponent>(source))
            modifiers.Add("Reform");

        if (HasComp<ClumsyComponent>(source))
            modifiers.Add("Clumsy");

        if (HasComp<MessyDrinkerComponent>(source))
            modifiers.Add("MessyDrinker");

        if (HasComp<ParrotListenerComponent>(source))
            modifiers.Add("ParrotSpeech");

        if (HasComp<BleatingAccentComponent>(source))
            modifiers.Add("Bleating");

        if (TryComp<FixturesComponent>(source, out var fixtures))
        {
            foreach (var (_, fixture) in fixtures.Fixtures)
            {
                if (fixture.Shape is not PhysShapeCircle circle)
                    continue;

                var mask = (CollisionGroup)fixture.CollisionMask;
                if (mask == CollisionGroup.SmallMobMask)
                {
                    modifiers.Add("SmallSize");
                    break;
                }

                if (circle.Radius >= 0.45f)
                {
                    modifiers.Add("LargeSize");
                    break;
                }

                if (circle.Radius >= 0.25f)
                {
                    modifiers.Add("MediumSize");
                    break;
                }
            }
        }

        return modifiers;
    }

    /// <summary>
    /// Creates a new Cell with a random name, random color, and stability/cost
    /// derived from the number of modifiers. Higher modifier count = lower stability + higher cost.
    /// </summary>
    private Cell GenerateRandomCell(List<ProtoId<CellModifierPrototype>> modifiers)
    {
        var prefix = CellPrefixes[_random.Next(CellPrefixes.Length)];
        var suffix = CellSuffixes[_random.Next(CellSuffixes.Length)];
        var name = prefix + suffix;

        var color = new Color(
            _random.NextFloat(),
            _random.NextFloat(),
            _random.NextFloat());

        var modifierCount = modifiers.Count;
        var stability = MathF.Max(0.1f, 1.0f - modifierCount * 0.05f + _random.NextFloat(-0.03f, 0.03f));
        var cost = 5 + modifierCount * 3;

        return new Cell(
            id: null,
            color: color,
            name: name,
            stability: stability,
            cost: cost,
            modifiers: modifiers);
    }

    /// <summary>
    /// Updates the collector sprite state based on whether its cell container is empty.
    /// </summary>
    private void UpdateCollectorAppearance(Entity<CellCollectorComponent, CellContainerComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp2))
            return;

        _appearance.SetData(ent, CellCollectorVisuals.State, ent.Comp2.Empty);
    }

    /// <summary>
    /// Validates a collector interaction. For collection: checks collector has space, usages left,
    /// and target allows collection. For transfer: checks whitelist and that collector has cells.
    /// Shows popups on failure when <paramref name="popup"/> is true.
    /// </summary>
    private bool CollectorInteractValidate(Entity<CellCollectorComponent, CellContainerComponent?> ent,
        Entity<CellContainerComponent?> target,
        CellCollectorDirection direction,
        bool popup = true)
    {
        if (!Resolve(ent, ref ent.Comp2) || !Resolve(target, ref target.Comp))
            return false;

        switch (direction)
        {
            case CellCollectorDirection.Collection:
                if (!ent.Comp2.Empty)
                {
                    if (!popup)
                        return false;

                    _popup.PopupPredicted(Loc.GetString("cell-collector-full"), ent, null, PopupType.SmallCaution);
                    return false;
                }

                if (ent.Comp1.Usages == 0)
                {
                    if (!popup)
                        return false;

                    _popup.PopupPredicted(Loc.GetString("cell-collector-already-used"), ent, null, PopupType.SmallCaution);
                    return false;
                }

                if (!target.Comp.AllowCollection)
                {
                    if (!popup)
                        return false;

                    _popup.PopupPredicted(Loc.GetString("cell-collector-target-cant-collected"), ent, null, PopupType.SmallCaution);
                    return false;
                }
                break;

            case CellCollectorDirection.Transfer:
                if (_entityWhitelist.IsWhitelistFail(target.Comp.ToolsTransferWhitelist, ent) ||
                    target.Comp.ToolsTransferWhitelist is null ||
                    !target.Comp.AllowTransfer)
                    return false;

                if (ent.Comp2.Empty)
                {
                    if (!popup)
                        return false;

                    _popup.PopupPredicted(Loc.GetString("cell-collector-empty"), ent, null, PopupType.SmallCaution);
                    return false;
                }
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
        }

        return true;
    }
}
