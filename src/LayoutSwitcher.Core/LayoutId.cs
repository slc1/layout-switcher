namespace LayoutSwitcher.Core;

public enum LayoutId
{
    En,
    Ua,
}

public static class LayoutIdExtensions
{
    public static LayoutId Other(this LayoutId id) => id == LayoutId.En ? LayoutId.Ua : LayoutId.En;
}
