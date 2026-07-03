using Robust.Shared.Prototypes;

namespace Content.Shared.Xenobiology.Systems;

public abstract partial class SharedCellSystem
{
    public string GetCellModifiersString(List<ProtoId<CellModifierPrototype>> modifiers)
    {
        var message = string.Empty;

        foreach (var modifierId in modifiers)
        {
            if (!_prototype.TryIndex(modifierId, out var modifier))
                continue;

            var color = modifier.Color.A == 0
                ? Color.White
                : modifier.Color;
            var modifiersMessage = Loc.GetString("cell-sequencer-menu-cell-modifier-message",
                ("name", Loc.GetString(modifier.Name)),
                ("color", color.ToHex()));

            message += $"{modifiersMessage}\r\n";
        }

        return message;
    }
}
