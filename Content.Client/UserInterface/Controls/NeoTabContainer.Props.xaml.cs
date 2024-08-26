using System.Linq;
using Robust.Client.AutoGenerated;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Timing;

namespace Content.Client.UserInterface.Controls;

public sealed partial class NeoTabContainer
{
    // Too many computed properties...

    //TODO private Direction _tabLocation = Direction.North;
    /// <summary>
    ///     If true, the tabs will be displayed horizontally over the top of the contents
    ///     <br />
    ///     If false, the tabs will be displayed vertically to the left of the contents
    /// </summary>
    private bool _horizontal = true;
    /// <inheritdoc cref="_horizontal"/>
    public bool Horizontal
    {
        get => _horizontal;
        set => LayoutChanged(value);
    }

    /// If the <see cref="ContentContainer"/>'s horizontal scroll is enabled
    private bool _hScrollEnabled;
    /// <inheritdoc cref="_hScrollEnabled"/>
    public bool HScrollEnabled
    {
        get => _hScrollEnabled;
        set => ScrollingChanged(value, _vScrollEnabled);
    }

    /// If the <see cref="ContentContainer"/>'s vertical scroll is enabled
    private bool _vScrollEnabled;
    /// <inheritdoc cref="_vScrollEnabled"/>
    public bool VScrollEnabled
    {
        get => _vScrollEnabled;
        set => ScrollingChanged(_hScrollEnabled, value);
    }

    /// The margin around the whole UI element
    private Thickness _containerMargin = new(0);
    /// <inheritdoc cref="_containerMargin"/>
    public Thickness ContainerMargin
    {
        get => _containerMargin;
        set => ContainerMarginChanged(value);
    }

    /// The margin around the separator between the tabs and contents
    public Thickness SeparatorMargin
    {
        get => Separator.Margin;
        set => Separator.Margin = value;
    }

    private bool _firstTabOpenBoth;
    public bool FirstTabOpenBoth
    {
        get => _firstTabOpenBoth;
        set => TabStyleChanged(value, LastTabOpenBoth);
    }

    private bool _lastTabOpenBoth;
    public bool LastTabOpenBoth
    {
        get => _lastTabOpenBoth;
        set => TabStyleChanged(FirstTabOpenBoth, value);
    }


    /// <summary>
    ///     Changes the layout of the tabs and contents based on the value
    /// </summary>
    /// <param name="horizontal">See <see cref="Horizontal"/></param>
    private void LayoutChanged(bool horizontal)
    {
        _horizontal = horizontal;

        TabContainer.Orientation = horizontal ? LayoutOrientation.Horizontal : LayoutOrientation.Vertical;
        Container.Orientation = horizontal ? LayoutOrientation.Vertical : LayoutOrientation.Horizontal;

        var containerMargin = horizontal
            ? new Thickness(_containerMargin.Left, 0, _containerMargin.Right, _containerMargin.Bottom)
            : new Thickness(0, _containerMargin.Top, _containerMargin.Right, _containerMargin.Bottom);
        TabScrollContainer.Margin = containerMargin;
        ContentScrollContainer.Margin = containerMargin;

        TabScrollContainer.HorizontalExpand = horizontal;
        TabScrollContainer.VerticalExpand = !horizontal;
        TabScrollContainer.HScrollEnabled = horizontal;
        TabScrollContainer.VScrollEnabled = !horizontal;
    }

    private void ScrollingChanged(bool hScroll, bool vScroll)
    {
        _hScrollEnabled = hScroll;
        _vScrollEnabled = vScroll;

        ContentScrollContainer.HScrollEnabled = hScroll;
        ContentScrollContainer.VScrollEnabled = vScroll;
    }

    private void ContainerMarginChanged(Thickness value)
    {
        _containerMargin = value;
        LayoutChanged(Horizontal);
    }

    private void TabStyleChanged(bool firstTabOpenBoth, bool lastTabOpenBoth)
    {
        _firstTabOpenBoth = firstTabOpenBoth;
        _lastTabOpenBoth = lastTabOpenBoth;

        UpdateTabMerging();
    }
}
