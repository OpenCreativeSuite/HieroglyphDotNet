using Donkeh.UILab;
using HieroglyphDotNet.Core;
using static Donkeh.UILab.HtmlHelper;
using static Donkeh.UILab._;

namespace HieroglyphDotNet.Components;

/// <summary>
/// The open drop-down, rendered above the whole shell. The menu bar is only
/// 30px tall, so a dropdown clipped to that box could be painted but never
/// receive pointer input; owning its own layer keeps it above the canvas.
/// </summary>
internal sealed class MenuOverlay : Component
{
    /// <inheritdoc/>
    public override Node Render()
    {
        FontEditor session = UseContext(SessionContext.Value);
        MenuDefinition? menu = MenuBar.Find(session.OpenMenu);
        if (menu is null)
        {
            return F();
        }

        return Div(new()
        {
            ClassName = "menu-float",
            Attributes = new()
            {
                ["style"] = $"left:{Ui.Number(session.MenuX)}px;top:{Ui.Number(session.MenuY)}px;",
            },
        },
        MenuBar.Rows(session, menu));
    }
}
