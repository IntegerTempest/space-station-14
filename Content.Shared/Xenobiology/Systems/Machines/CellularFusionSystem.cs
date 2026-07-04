using System.Linq;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.GameTicking;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Content.Shared.Xenobiology.Components.Container;
using Content.Shared.Xenobiology.Components.Machines;
using Content.Shared.Xenobiology.Events;
using Content.Shared.Xenobiology.Systems;
using Content.Shared.Xenobiology.Systems.Machines.Connection;
using Content.Shared.Xenobiology.UI;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Shared.Xenobiology.Systems.Machines;

public sealed class CellularFusionSystem : EntitySystem
{
    [Dependency] private readonly CellClientSystem _cellClient = default!;
    [Dependency] private readonly CellServerSystem _cellServer = default!;

    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly SharedMaterialStorageSystem _materialStorage = default!;
    [Dependency] private readonly SharedCellSystem _cell = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly INetManager _netManager = default!;

    private static readonly ProtoId<CellModifierPrototype>[] MutagenicModifiers =
    [
        "ToxicMutation",
        "BioLuminescentMutation",
        "VolatileMutation"
    ];

    private static readonly ProtoId<CellModifierPrototype>[] BaseModifiers =
    [
        "Pacifism", "OwO", "NightVision", "Flight",
        "SmallSize", "LargeSize", "MediumSize",
        "EggLayer", "Lactation", "Wooly",
        "Venomous", "WebSpinning", "Regeneration",
        "NoSlip", "Reflection", "Invisibility",
        "Radiation", "Reform", "Clumsy", "MessyDrinker",
        "ParrotSpeech", "Bleating"
    ];

    private static Dictionary<string, ProtoId<CellModifierPrototype>>? _mutationRecipes;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CellularFusionComponent, MaterialAmountChangedEvent>(OnMaterialAmountChanged);

        SubscribeLocalEvent<CellularFusionComponent, CellularFusionUiSpliceMessage>(OnSpliceMessage);

        SubscribeLocalEvent<CellularFusionComponent, AfterActivatableUIOpenEvent>(OnAfterOpen);
        SubscribeLocalEvent<CellServerDatabaseChangedEvent>(OnServerDatabaseChanged);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent _)
    {
        _mutationRecipes = null;
    }

    private void EnsureRecipesInitialized()
    {
        if (_mutationRecipes is not null)
            return;

        _mutationRecipes = new Dictionary<string, ProtoId<CellModifierPrototype>>();
        var available = BaseModifiers.ToList();

        foreach (var mutation in MutagenicModifiers)
        {
            if (available.Count < 2)
                break;

            var idx1 = _random.Next(available.Count);
            var a = available[idx1];
            available.RemoveAt(idx1);

            var idx2 = _random.Next(available.Count);
            var b = available[idx2];
            available.RemoveAt(idx2);

            var key = GetRecipeKey(a, b);
            _mutationRecipes[key] = mutation;
        }
    }

    private static string GetRecipeKey(ProtoId<CellModifierPrototype> a, ProtoId<CellModifierPrototype> b)
    {
        return string.Compare(a, b, StringComparison.Ordinal) <= 0
            ? $"{a}+{b}"
            : $"{b}+{a}";
    }

    private ProtoId<CellModifierPrototype>? CheckMutationRecipe(List<ProtoId<CellModifierPrototype>> modifiers)
    {
        EnsureRecipesInitialized();

        for (var i = 0; i < modifiers.Count; i++)
        {
            for (var j = i + 1; j < modifiers.Count; j++)
            {
                var key = GetRecipeKey(modifiers[i], modifiers[j]);
                if (_mutationRecipes!.TryGetValue(key, out var mutation))
                    return mutation;
            }
        }

        return null;
    }

    private void OnMaterialAmountChanged(Entity<CellularFusionComponent> ent, ref MaterialAmountChangedEvent args)
    {
        ent.Comp.MaterialAmount = _materialStorage.GetMaterialAmount(ent, ent.Comp.RequiredMaterial);
        UpdateUI(ent);
    }

    private void OnAfterOpen(Entity<CellularFusionComponent> ent, ref AfterActivatableUIOpenEvent args)
    {
        ent.Comp.MaterialAmount = _materialStorage.GetMaterialAmount(ent, ent.Comp.RequiredMaterial);
        UpdateUI(ent);
    }

    private void OnServerDatabaseChanged(CellServerDatabaseChangedEvent args)
    {
        if (!TryComp<CellularFusionComponent>(args.Client, out var comp))
            return;

        comp.MaterialAmount = _materialStorage.GetMaterialAmount(args.Client, comp.RequiredMaterial);
        UpdateUI((args.Client, comp));
    }

    private void OnSpliceMessage(Entity<CellularFusionComponent> ent, ref CellularFusionUiSpliceMessage args)
    {
        if (ent.Comp.SpliceInProgress)
            return;

        if (!_cellClient.TryGetCells((ent, null), out var cells))
            return;

        if (!cells.Contains(args.CellA) || !cells.Contains(args.CellB))
            return;

        var dishUid = _itemSlots.GetItemOrNull(ent, ent.Comp.DishSlot);
        if (dishUid is not { } dish)
        {
            _popup.PopupPredicted(Loc.GetString("cellular-fusion-no-dish"), ent, null, PopupType.MediumCaution);
            return;
        }

        if (!TryComp<CellContainerComponent>(dish, out var dishContainer))
            return;

        var cost = SharedCellSystem.GetMergedCost(args.CellA, args.CellB);
        if (cost > ent.Comp.MaterialAmount)
            return;

        if (!_materialStorage.TrySetMaterialAmount(ent, ent.Comp.RequiredMaterial, ent.Comp.MaterialAmount - cost))
            return;

        var uid = ent.Owner;
        var cellA = args.CellA;
        var cellB = args.CellB;

        ent.Comp.SpliceInProgress = true;
        UpdateUI(ent);

        Timer.Spawn(TimeSpan.FromSeconds(ent.Comp.SpliceDelay), () =>
        {
            if (!TryComp<CellularFusionComponent>(uid, out var comp) || !comp.SpliceInProgress)
                return;

            var fusionEnt = new Entity<CellularFusionComponent>(uid, comp);
            comp.SpliceInProgress = false;

            if (!TryComp<CellContainerComponent>(dish, out var finalDishContainer))
            {
                _materialStorage.TryChangeMaterialAmount(uid, comp.RequiredMaterial, cost);
                UpdateUI(fusionEnt, null);
                return;
            }

            var avgStability = (cellA.Stability + cellB.Stability) / 2f;
            var failureChance = comp.BaseFailureChance + (1f - avgStability) * comp.StabilityMultiplier;

            if (_random.Prob(failureChance))
            {
                _popup.PopupPredicted(Loc.GetString("cellular-fusion-splice-failure"), fusionEnt, null, PopupType.MediumCaution);
                UpdateUI(fusionEnt, null);
                return;
            }

            var modifiers = InheritModifiers(cellA, cellB);

            var mutation = CheckMutationRecipe(modifiers);
            if (mutation is not null)
                modifiers.Add(mutation.Value);

            var mergedStability = SharedCellSystem.GetMergedStability(cellA, cellB);
            var mergedColor = SharedCellSystem.GetMergedColor(cellA, cellB);
            var mergedName = SharedCellSystem.GetMergedName(cellA, cellB);

            var result = new Cell(
                id: null,
                color: mergedColor,
                name: mergedName,
                stability: mergedStability,
                cost: modifiers.Count * 3,
                modifiers: modifiers);

            _cell.AddCell((dish, finalDishContainer), result);
            _popup.PopupPredicted(Loc.GetString("cellular-fusion-splice-success"), fusionEnt, null, PopupType.Medium);

            UpdateUI(fusionEnt, result);
        });
    }

    private List<ProtoId<CellModifierPrototype>> InheritModifiers(Cell cellA, Cell cellB)
    {
        var combined = new List<ProtoId<CellModifierPrototype>>();
        var seen = new HashSet<ProtoId<CellModifierPrototype>>();

        foreach (var modifier in cellA.Modifiers)
        {
            if (_random.Prob(cellA.Stability) && seen.Add(modifier))
                combined.Add(modifier);
        }

        foreach (var modifier in cellB.Modifiers)
        {
            if (_random.Prob(cellB.Stability) && seen.Add(modifier))
                combined.Add(modifier);
        }

        return combined;
    }

    private void UpdateUI(Entity<CellularFusionComponent> ent, Cell? lastResult = null)
    {
        if (!_cellClient.TryGetServer(ent.Owner, out var serverEnt))
        {
            if (!_netManager.IsClient)
                _popup.PopupPredicted(Loc.GetString("cellular-fusion-no-connect"), ent, null, PopupType.MediumCaution);

            return;
        }

        var state = new CellularFusionUiState(serverEnt.Value.Comp.Cells, ent.Comp.MaterialAmount, ent.Comp.SpliceInProgress, lastResult);
        _userInterface.SetUiState(ent.Owner, CellularFusionUiKey.Key, state);
    }
}
