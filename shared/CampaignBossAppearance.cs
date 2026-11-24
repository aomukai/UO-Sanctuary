// SPDX-License-Identifier: GPL-3.0-or-later
namespace Sanctuary;

public static class CampaignBossAppearance
{
    // Contiguous monster bodies in campaign-bosses.idx, immediately after the raven flocks.
    public const ushort Leoric = 2005;
    public const ushort Lazarus = 2006;
    public const ushort Diablo = 2007;
    public const byte TurnGroup = 8;
    public const int WalkFramesPerStep = 4;
    public const int BreathFrames = 12;
    public const int BreathPeriodMs = 8400;
    public static bool IsBoss(ushort graphic) => graphic is >= Leoric and <= Diablo;
    // Final four frames of either Diablo death group sustain the corpse fire.
    public const int FirstFireFrame = 6;
    public const int CorpseFireFrames = 4;
    public const int Count = Diablo - Leoric + 1;
}
