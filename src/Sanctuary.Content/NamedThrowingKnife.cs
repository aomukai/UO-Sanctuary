// SPDX-License-Identifier: GPL-3.0-or-later
using ModernUO.Serialization;
using Server;
using Server.Items;

namespace Sanctuary;

[SerializationGenerator(0)]
public partial class NamedThrowingKnife : BaseThrown
{
    [Constructible]
    public NamedThrowingKnife() : base(0xF52) => Layer = Server.Layer.OneHanded;
    public override string DefaultName => "a throwing knife";
    public override double DefaultWeight => 1;
    public override int MinThrowRange => 2;
    public override WeaponAbility PrimaryAbility => WeaponAbility.ShadowStrike;
    public override WeaponAbility SecondaryAbility => WeaponAbility.InfectiousStrike;
    public override int AosStrengthReq => 0;
    public override int AosMinDamage => 8;
    public override int AosMaxDamage => 12;
    public override float MlSpeed => 2.0f;
    public override int InitMinHits => 31;
    public override int InitMaxHits => 60;
}
