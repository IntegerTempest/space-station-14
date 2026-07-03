using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Xenobiology;

/// <summary>
/// Fires after injection delay to transfer the cell from dish into the animal.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class CellMutagenicInjectionDoAfter : SimpleDoAfterEvent
{
    public CellMutagenicInjectionDoAfter()
    {
    }

    public CellMutagenicInjectionDoAfter(CellMutagenicInjectionDoAfter doAfter)
    {
    }

    public override DoAfterEvent Clone()
    {
        return new CellMutagenicInjectionDoAfter(this);
    }
}
