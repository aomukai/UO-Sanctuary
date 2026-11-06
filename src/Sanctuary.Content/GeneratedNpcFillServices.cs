// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
namespace Sanctuary;

// Explicit source target facts/services;appearance and UO native properties
// never infer these source monster categories or target-base-stat writes.
internal sealed record GeneratedNpcFillTarget(bool Present,bool Monster,bool Demon,bool Undead,int MonsterType,
    Func<int> Armor,Action<int> SetArmor,Func<int,int,bool> TypeMatches);
internal sealed class GeneratedNpcFillServices:NpcDamageFillServices
{
    private readonly GeneratedNpcWeaponFrame Frame;
    private readonly GeneratedNpcStatGraph Graph;
    private readonly GeneratedNpcFillTarget Target;
    private readonly int FirstType,Range,Hire,Crit;
    private readonly bool Good;
    private readonly Func<int,int> Bound;
    private readonly Func<uint> Random;
    private readonly Func<NpcFillPacket,NpcFillPacket> Event;
    private readonly Func<NpcFillStat,int> Read;
    internal GeneratedNpcFillServices(GeneratedNpcWeaponFrame frame,GeneratedNpcStatGraph graph,GeneratedNpcFillTarget target,
        int firstType,int range,bool good,int hireling,int critical,Func<int,int> limited,Func<uint> raw,Func<NpcFillPacket,NpcFillPacket> attacked,Func<NpcFillStat,int> read=null)
    {
        ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(graph);ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(target.Armor);ArgumentNullException.ThrowIfNull(target.SetArmor);ArgumentNullException.ThrowIfNull(target.TypeMatches);
        ArgumentNullException.ThrowIfNull(limited);ArgumentNullException.ThrowIfNull(raw);ArgumentNullException.ThrowIfNull(attacked);
        if(hireling<0||critical is <0 or >255){throw new ArgumentOutOfRangeException();}
        Frame=frame;Graph=graph;Target=target;FirstType=firstType;Range=range;Good=good;Hire=hireling;Crit=critical;Bound=limited;Random=raw;Event=attacked;Read=read??(stat=>Graph.Read((int)stat));
    }
    internal override bool AttackerPresent=>true;
    internal override bool DefenderPresent=>Target.Present;
    internal override int ActorKind=>Graph.Actor.Kind;
    internal override bool GoodAlignment=>Good;
    internal override bool TargetMonster=>Target.Monster;
    internal override bool Demon=>Target.Demon;
    internal override bool Undead=>Target.Undead;
    internal override int MonsterType=>Target.MonsterType;
    internal override int Armor{get=>Target.Armor();set=>Target.SetArmor(value);}
    internal override bool Blunt=>Frame.SelectedBlunt;
    internal override int Hireling=>Hire;
    internal override int IntrinsicCritical=>Crit;
    internal override int Stat(NpcFillStat stat)=>Read(stat);
    internal override IEnumerable<EquipmentStatValue> TypeDamage=>Graph.Stats.Where(s=>s.Stat==(int)NpcFillStat.DAMAGE_VS_MONTYPE);
    internal override bool TypeMatches(int layer,int monsterType)=>Target.TypeMatches(layer,monsterType);
    internal override int WeaponCritical()=>Frame.Mastery(2,FirstType,Range);
    internal override void Enter(bool ranged)=>Frame.Enter(ranged);
    internal override void Restore(bool ranged)=>Frame.Restore(ranged);
    internal override int Physical(int additional,int initial,int sourceDamage)=>NpcEquipmentDamage.Roll256(Frame.Physical(FirstType,Range,additional),Bound,sourceDamage,initial);
    internal override int Limited(int bound)=>Bound(bound);
    internal override uint Raw()=>Random();
    internal override NpcFillPacket AttackedInMelee(NpcFillPacket packet)=>Event(packet);
}
