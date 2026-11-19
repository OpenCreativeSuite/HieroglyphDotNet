using Donkeh.UILab;
using HieroglyphDotNet.Core;

namespace HieroglyphDotNet.Components;

/// <summary>Shares the editor session with every panel.</summary>
internal static class SessionContext
{
    public static readonly Context<FontEditor> Value = new(new FontEditor());
}
