using Content.Shared.Climbing.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Destructible;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Content.Shared.Xenobiology.Components.Container;
using Content.Shared.Xenobiology.Components.Machines;
using Content.Shared.Xenobiology.Visuals;
using Robust.Shared.Containers;

namespace Content.Shared.Xenobiology.Systems.Machines;

/// <summary>
/// Handles drag-drop of animals, dish loading, and cell injection via empty-hand click.
/// Shared between client (for drag-drop highlights) and server (for actual injection).
/// </summary>
public abstract partial class SharedMutagenicInjectorSystem : EntitySystem
{
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private ItemSlotsSystem _itemSlots = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] protected SharedAppearanceSystem Appearance = default!;
    [Dependency] private SharedCellSystem _cell = default!;
    [Dependency] private ClimbSystem _climb = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CellMutagenicInjectorComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<CellMutagenicInjectorComponent, CanDropTargetEvent>(OnCanDropTarget);
        SubscribeLocalEvent<CellMutagenicInjectorComponent, DragDropTargetEvent>(OnDragDrop);
        SubscribeLocalEvent<CellMutagenicInjectorComponent, ActivateInWorldEvent>(OnActivateInWorld);
        SubscribeLocalEvent<CellMutagenicInjectorComponent, CellMutagenicInjectionDoAfter>(OnInjectionDoAfter);
        SubscribeLocalEvent<CellMutagenicInjectorComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<CellMutagenicInjectorComponent, GetVerbsEvent<AlternativeVerb>>(AddAlternativeVerbs);
    }

    /// <summary>
    /// Creates the body container and opens the door.
    /// </summary>
    private void OnComponentInit(Entity<CellMutagenicInjectorComponent> ent, ref ComponentInit args)
    {
        ent.Comp.BodyContainer = _container.EnsureContainer<ContainerSlot>(ent, CellMutagenicInjectorComponent.BodyContainerId);
        UpdateDoorVisual(ent, false);
    }

    /// <summary>
    /// Shows green highlight if the dragged mob can be accepted.
    /// </summary>
    private void OnCanDropTarget(Entity<CellMutagenicInjectorComponent> ent, ref CanDropTargetEvent args)
    {
        args.Handled = true;
        args.CanDrop = CanAccept(ent, args.Dragged);
    }

    /// <summary>
    /// True if the injector is empty, unused, and the dragged entity has CellContainerComponent.
    /// </summary>
    private bool CanAccept(Entity<CellMutagenicInjectorComponent> ent, EntityUid dragged)
    {
        if (ent.Comp.HasInjected)
            return false;

        if (ent.Comp.BodyContainer.ContainedEntity is not null)
            return false;

        if (!HasComp<CellContainerComponent>(dragged))
            return false;

        return true;
    }

    /// <summary>
    /// Inserts the animal into the body container (does NOT start injection).
    /// </summary>
    private void OnDragDrop(Entity<CellMutagenicInjectorComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        if (!CanAccept(ent, args.Dragged))
            return;

        InsertBody(ent, args.Dragged);
        args.Handled = true;
    }

    /// <summary>
    /// Adds an Eject verb for the contained animal.
    /// </summary>
    private void AddAlternativeVerbs(Entity<CellMutagenicInjectorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (ent.Comp.BodyContainer.ContainedEntity is not null)
        {
            var contained = ent.Comp.BodyContainer.ContainedEntity.Value;
            var name = "Unknown";
            if (TryComp(contained, out MetaDataComponent? metadata))
                name = metadata.EntityName;

            AlternativeVerb ejectVerb = new()
            {
                Act = () => EjectBody(ent),
                Category = VerbCategory.Eject,
                Text = name,
                Priority = 1
            };
            args.Verbs.Add(ejectVerb);
        }
    }

    /// <summary>
    /// Clicking the injector starts the injection process.
    /// </summary>
    private void OnActivateInWorld(Entity<CellMutagenicInjectorComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        TryStartInjection(ent);
        args.Handled = true;
    }

    /// <summary>
    /// Validates prerequisites and starts the injection DoAfter.
    /// </summary>
    private void TryStartInjection(Entity<CellMutagenicInjectorComponent> ent)
    {
        if (!ValidateInjection(ent))
            return;

        var mob = ent.Comp.BodyContainer.ContainedEntity!.Value;

        var doAfterArgs = new DoAfterArgs(EntityManager, mob, ent.Comp.InjectionDelay, new CellMutagenicInjectionDoAfter(), ent, target: ent)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            MovementThreshold = 0.5f,
            BlockDuplicate = true,
        };

        _doAfter.TryStartDoAfter(doAfterArgs);
    }

    /// <summary>
    /// Puts the animal in the container and closes the door.
    /// </summary>
    private void InsertBody(Entity<CellMutagenicInjectorComponent> ent, EntityUid toInsert)
    {
        if (ent.Comp.BodyContainer.ContainedEntity is not null)
            return;

        var xform = Transform(toInsert);
        _container.Insert((toInsert, xform), ent.Comp.BodyContainer);
        UpdateDoorVisual(ent, true);
    }

    /// <summary>
    /// Removes the animal and opens the door.
    /// </summary>
    public void EjectBody(Entity<CellMutagenicInjectorComponent> ent)
    {
        if (ent.Comp.BodyContainer.ContainedEntity is not { Valid: true } contained)
            return;

        _container.Remove(contained, ent.Comp.BodyContainer);
        _climb.ForciblySetClimbing(contained, ent);
        UpdateDoorVisual(ent, false);
    }

    /// <summary>
    /// Toggles the door sprite between open and closed.
    /// </summary>
    private void UpdateDoorVisual(Entity<CellMutagenicInjectorComponent> ent, bool occupied)
    {
        Appearance.SetData(ent, MutagenicInjectorVisuals.DoorState, occupied);
    }

    /// <summary>
    /// Transfers the cell from dish to animal and ejects it.
    /// </summary>
    private void OnInjectionDoAfter(Entity<CellMutagenicInjectorComponent> ent, ref CellMutagenicInjectionDoAfter args)
    {
        if (args.Handled || args.Cancelled)
            return;

        if (!ValidateInjection(ent))
            return;

        var dishUid = _itemSlots.GetItemOrNull(ent, ent.Comp.DishSlot)!.Value;
        var dishContainer = Comp<CellContainerComponent>(dishUid);
        var cell = dishContainer.Cells[0];

        var mob = ent.Comp.BodyContainer.ContainedEntity!.Value;

        if (TryComp<CellContainerComponent>(mob, out var mobContainer))
        {
            _cell.AddCell((mob, mobContainer), cell);
        }

        _cell.RemoveCell((dishUid, dishContainer), cell);
        ent.Comp.HasInjected = true;

        EjectBody(ent);

        _popup.PopupEntity(Loc.GetString("mutagenic-injector-success"), ent, PopupType.Medium);
        args.Handled = true;
    }

    /// <summary>
    /// Ejects the animal when the machine is destroyed.
    /// </summary>
    private void OnDestruction(Entity<CellMutagenicInjectorComponent> ent, ref DestructionEventArgs args)
    {
        EjectBody(ent);
    }

    /// <summary>
    /// Checks dish, cells, animal, and re-use flag; shows popup on failure.
    /// </summary>
    private bool ValidateInjection(Entity<CellMutagenicInjectorComponent> ent)
    {
        if (ent.Comp.HasInjected)
        {
            _popup.PopupEntity(Loc.GetString("mutagenic-injector-already-used"), ent, PopupType.MediumCaution);
            return false;
        }

        var dish = _itemSlots.GetItemOrNull(ent, ent.Comp.DishSlot);
        if (dish is null)
        {
            _popup.PopupEntity(Loc.GetString("mutagenic-injector-no-dish"), ent, PopupType.MediumCaution);
            return false;
        }

        if (!TryComp<CellContainerComponent>(dish.Value, out var dishContainer) || dishContainer.Empty)
        {
            _popup.PopupEntity(Loc.GetString("mutagenic-injector-no-cells"), ent, PopupType.MediumCaution);
            return false;
        }

        if (ent.Comp.BodyContainer.ContainedEntity is null)
        {
            _popup.PopupEntity(Loc.GetString("mutagenic-injector-no-target"), ent, PopupType.MediumCaution);
            return false;
        }

        return true;
    }
}
