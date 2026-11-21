// SPDX-License-Identifier: GPL-3.0-or-later
namespace Sanctuary;

internal static class BeltLanternGraphics
{
    public const ushort Lit = 0x0A17;
    public const ushort Unlit = 0x0A18;

    public static bool Matches(ushort graphic) => graphic is Lit or Unlit;
}
