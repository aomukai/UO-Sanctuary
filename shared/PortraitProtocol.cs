using System;

namespace Sanctuary;

public static class PortraitProtocol
{
    public const ushort SubCommand = 0xF1;
    public const byte Version = 1;
    public const byte AvatarFace = 0, Conversation = 1, Closed = 2, InteractionEnabled = 3, StoryState = 4;
    public const byte Leave = 255;
    public const int FaceCount = 8, NpcPortraitCount = 20, MaxReplies = 4;
    public const int ReplyPacketLength = 11;
    public const int InteractPacketLength = 10;
    public const int NpcInteractionRange = 3;
    public const int MaxTextBytes = 512;
    private const int ExtendedCreation = 0xA0, ClassCount = 8;

    // Preserve the original class byte for face 1 and accept it from older clients.
    public static byte EncodeCreation(DiabloClass characterClass, byte face) => face == 0
        ? (byte)characterClass
        : (byte)(ExtendedCreation + (face - 1) * ClassCount + (int)characterClass - (int)DiabloClass.Amazon);

    public static bool TryDecodeCreation(int value, out DiabloClass characterClass, out byte face)
    {
        face = 0;
        if (DiabloClasses.TryFromWire(value, out characterClass))
            return true;
        int offset = value - ExtendedCreation;
        if (offset is < 0 or >= ((FaceCount - 1) * ClassCount))
            return false;
        face = (byte)(1 + offset / ClassCount);
        return DiabloClasses.TryFromWire((int)DiabloClass.Amazon + offset % ClassCount, out characterClass);
    }
}
