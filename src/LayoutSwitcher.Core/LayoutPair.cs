namespace LayoutSwitcher.Core;

public sealed record LayoutPair(LayoutTable En, LayoutTable Ua)
{
    public LayoutTable this[LayoutId id] => id == LayoutId.En ? En : Ua;
}
