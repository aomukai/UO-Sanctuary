// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

public sealed record ConvictionLease(uint Target,uint Source,int Rank,int DefensePercent,int Fire,int Cold,int Lightning,DateTime UntilUtc);
public sealed class PaladinConvictionProfiles:GenericPersistence
{
    public PaladinConvictionProfiles():base("SanctuaryPaladinConviction",126){}
    public override void Serialize(IGenericWriter writer){writer.Write(0);writer.Write(JsonSerializer.Serialize(PaladinConviction.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown Conviction version");}
        PaladinConviction.Restore(JsonSerializer.Deserialize<ConvictionLease[]>(reader.ReadString()));
    }
}
public static class PaladinConviction
{
    private static readonly Dictionary<uint,ConvictionLease> _leases=new();
    private static readonly List<uint> _expired=new();
    public static PaladinConvictionProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    public static void Initialize()=>Timer.StartTimer(TimeSpan.Zero,TimeSpan.FromMilliseconds(40),Tick);
    public static ConvictionLease Effect(Mobile target)=>target is{Alive:true,Deleted:false} && _leases.TryGetValue(target.Serial.Value,out var lease)
        && lease.UntilUtc>TimedEffects.Now ? lease:null;
    private static int ClassSupplierRank(Mobile target)=>target==null?0:PaladinAuras.Effect(target,CombatAbility.Conviction)?.Rank ?? 0;
    public static int SupplierRank(Mobile target)=>Math.Max(ClassSupplierRank(target),EliteAuras.OwnConvictionRank(target));
    public static bool Managed(Mobile target)=>target!=null && (Effect(target)!=null || ClassSupplierRank(target)>0);
    public static bool Protected(Mobile target,int incomingRank)=>SupplierRank(target)>incomingRank;
    public static int ImmunityDelta(Mobile target,int baseline,int delta)=>delta<0 && target is BaseCreature and not BaseHire && baseline>=100?delta/5:delta;
    private static int LegacyRank(Mobile target)=>EliteAuras.Level(target,EliteAura.Conviction,true);
    private static int Armor(int rank)
    {
        var definition=SkillMechanics.For(CombatAbility.Conviction);int min=definition.Parameter(5),max=definition.Parameter(6);
        return min+(max-min)*Math.Min(100,110*rank/(rank+6))/100;
    }
    public static int DefensePercent(Mobile target)
    {
        if(!Managed(target)){return 0;}
        var lease=Effect(target);int legacy=LegacyRank(target),rank=Math.Max(lease?.Rank ?? 0,legacy);
        if(rank==0 || Protected(target,rank)){return 0;}
        return lease!=null && lease.Rank>=legacy?-lease.DefensePercent:-Armor(rank);
    }
    public static int UnderlyingResistance(Mobile target,string element,int current)=>Managed(target)
        ?current-EliteAuras.ConvictionOffset(target,element):current;
    public static int ResistanceDelta(Mobile target,string element,int baseline)
    {
        if(element is not ("fire" or "cold" or "ltng") || !Managed(target)){return 0;}
        var lease=Effect(target);int legacy=LegacyRank(target),rank=Math.Max(lease?.Rank ?? 0,legacy);
        if(rank==0 || Protected(target,rank)){return 0;}
        if(lease!=null && lease.Rank>=legacy){return element switch{"fire"=>lease.Fire,"cold"=>lease.Cold,_=>lease.Lightning};}
        int amount=Math.Min(150,30+(legacy-1)*5);
        return ImmunityDelta(target,baseline,-amount);
    }
    internal static bool Apply(PlayerMobile owner,Mobile target,SkillValues values,DateTime until)
    {
        if(Protected(target,values.Rank)){return false;}
        var old=Effect(target);
        if(old!=null && old.Rank>values.Rank){return false;}
        int Delta(string element,int native)=>ImmunityDelta(target,native-EliteAuras.ConvictionOffset(target,element),(int)values.Eval("aurastatcalc2"));
        _leases[target.Serial.Value]=new(target.Serial.Value,old?.Rank==values.Rank?old.Source:owner.Serial.Value,values.Rank,
            -(int)values.Eval("aurastatcalc1"),Delta("fire",target.FireResistance),Delta("cold",target.ColdResistance),Delta("ltng",target.EnergyResistance),until);
        return true;
    }
    internal static void Pulse(PlayerMobile owner,SkillValues values,DateTime now)
    {
        int frames=Math.Max(5,(int)values.Eval("perdelay"));var until=now.AddMilliseconds((frames+1)*40);
        // The supplier's zero-stat state participates in the same skill-level replacement comparison.
        PaladinAuras.ElementalWeapon(new(owner.Serial.Value,owner.Serial.Value,CombatAbility.Conviction,values.Rank,0,until));
        if(Effect(owner) is{} own && own.Rank<values.Rank){_leases.Remove(owner.Serial.Value);}
        if(SpellHelper.IsTown(owner.Location,owner)){return;}
        double radius=SkillGeometry.Tiles(values.Eval("aurarangecalc"));
        foreach(var target in owner.Map.GetMobilesInRange<Mobile>(owner.Location,(int)Math.Ceiling(radius)))
        {
            double dx=target.X-owner.X,dy=target.Y-owner.Y;
            if(ClassCombat.IsHostile(owner,target) && CorpseSkillRules.AllowsAura(target) && Math.Abs(target.Z-owner.Z)<16
                && dx*dx+dy*dy<=radius*radius && !SpellHelper.IsTown(target.Location,target) && Apply(owner,target,values,until))
            {target.FixedEffect(0x376A,5,5,0x481,0);} // Placeholder hostile aura overlay.
        }
        owner.FixedEffect(0x375A,10,10,0x481,0); // Placeholder supplier aura ring.
    }
    private static void Tick()
    {
        _expired.Clear();
        foreach(var row in _leases)
        {if(row.Value.UntilUtc<=TimedEffects.Now || World.FindMobile((Serial)row.Key) is not{Alive:true,Deleted:false}){_expired.Add(row.Key);}}
        foreach(uint target in _expired){_leases.Remove(target);}
    }
    public static ConvictionLease[] Snapshot()=>new List<ConvictionLease>(_leases.Values).ToArray();
    public static void Restore(ConvictionLease[] saved)
    {
        if(saved==null){throw new InvalidDataException("Missing Conviction leases");}
        _leases.Clear();
        foreach(var lease in saved)
        {
            if(lease.Target==0 || lease.Source==0 || lease.Rank<1 || lease.DefensePercent is <0 or >100
                || lease.Fire is < -150 or >0 || lease.Cold is < -150 or >0 || lease.Lightning is < -150 or >0 || !_leases.TryAdd(lease.Target,lease))
            {throw new InvalidDataException("Invalid Conviction lease");}
        }
    }
}
