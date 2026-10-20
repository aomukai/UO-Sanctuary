// SPDX-License-Identifier: GPL-3.0-or-later
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class RecognitionCertificate : Item
{
    [SerializableField(0)] private uint _recipient;
    [SerializableField(1)] private string _recipientName;
    [SerializableField(2)] private RecognitionHonor _honor;
    [Constructible]
    public RecognitionCertificate(PlayerMobile player, RecognitionHonor honor) : base(0xEA0)
    {
        _recipient = player.Serial.Value; _recipientName = player.Name; _honor = honor;
        Weight = 1; Name = $"{TownRecognition.Title(honor)} — {player.Name}";
    }
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"{"Awarded to"}\t{_recipientName}");
        list.Add($"{"Honor"}\t{TownRecognition.Title(_honor)}");
        list.Add($"{"A certificate of civic recognition. Display it in your home."}");
    }
}

[SerializationGenerator(0)]
[Flippable(0xA97, 0xA99)]
public partial class EarnedWardrobe : Item
{
    [SerializableField(0)] private uint _recipient;
    [SerializableField(1)] private string _recipientName;
    [Constructible]
    public EarnedWardrobe(PlayerMobile player) : base(0xA97)
    {
        _recipient = player.Serial.Value; _recipientName = player.Name; Weight = 1;
        Name = $"{player.Name}'s wardrobe";
    }
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"{"Awarded to"}\t{_recipientName}");
        list.Add($"{"Earned wardrobe access remains with the character."}");
    }
    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile player || player.Serial.Value != _recipient || !TownRecognition.HasAccess(player))
        { from.SendMessage("This wardrobe belongs to its recognized recipient."); return; }
        if (!player.Alive || !player.CanSee(this) || !player.InRange(GetWorldLocation(), 2) || !player.InLOS(this)
            || RootParent != player && (Parent != null || Map != player.Map))
        { player.SendMessage("Approach your wardrobe while alive to use it."); return; }
        if (WardrobeAccess.SourceValid(player, this)) { WardrobeGump.DisplayTo(player, this); }
        else { TownRecognition.Open(player, TownRecognition.NearbyOffice(player), this); }
    }
}

[SerializationGenerator(0)]
public partial class PublicRecognitionWardrobe : Item
{
    [SerializableField(0)] private string _site;
    [Constructible]
    public PublicRecognitionWardrobe(string site, string town) : base(0xA97)
    { _site = site; Movable = false; Name = $"{town} public wardrobe and recognition office"; }
    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player && ClassProfessions.Affected(player) && TownRecognition.AtOffice(player, _site))
        {
            if (TownRecognition.HasAccess(player)) { WardrobeGump.DisplayTo(player, this); }
            else { TownRecognition.Open(player, _site, this); }
        }
    }
}

[SerializationGenerator(0)]
public partial class CivicRegistrar : BaseCreature
{
    [SerializableField(0)] private string _site;
    [Constructible]
    public CivicRegistrar(string site, string town) : base(AIType.AI_Vendor, FightMode.None, 10, 1)
    {
        _site = site; Name = "the civic registrar"; Title = $"of {town}";
        Body = 0x190; Hue = 0x83EA; CantWalk = true;
        AddItem(new Robe(0x47E)); AddItem(new Sandals());
    }
    public override bool IsInvulnerable => true;
    public override bool CanTeach => false;
    public override void OnDoubleClick(Mobile from)
    {
        if (from is PlayerMobile player && ClassProfessions.Affected(player) && TownRecognition.AtOffice(player, _site))
        { TownRecognition.Open(player, _site); }
    }
}
