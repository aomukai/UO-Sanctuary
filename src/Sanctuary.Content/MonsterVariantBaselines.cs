// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Preserve actual native Normal values before a wild difficulty assignment.
// Revive can later return to Normal without constructing a substitute actor.
public sealed record MonsterNativeCombat(uint Actor,int Minimum,int Maximum,int Physical,int Fire,int Cold,int Poison,int Lightning,
    int PhysicalDamage,int FireDamage,int ColdDamage,int PoisonDamage,int LightningDamage,double Walk,double Idle,
    [property:System.Text.Json.Serialization.JsonIgnore(Condition=System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] int? Life256=null);
public sealed class MonsterVariantBaselineProfiles:GenericPersistence
{
    public MonsterVariantBaselineProfiles():base("SanctuaryMonsterVariantBaselines",-1){}
    public override void Serialize(IGenericWriter writer)
    {MonsterVariantBaselines.Prune();writer.Write(0);writer.Write(JsonSerializer.Serialize(MonsterVariantBaselines.Snapshot()));}
    public override void Deserialize(IGenericReader reader)
    {
        if(reader.ReadInt()!=0){throw new InvalidDataException("Unknown monster native baseline version");}
        MonsterVariantBaselines.Restore(JsonSerializer.Deserialize<MonsterNativeCombat[]>(reader.ReadString()));
    }
    public override void PostDeserialize()=>MonsterVariantBaselines.Prune();
}
public static class MonsterVariantBaselines
{
    private static Dictionary<uint,MonsterNativeCombat> States=new();
    public static MonsterVariantBaselineProfiles Profiles{get;private set;}
    public static void Configure()=>Profiles=new();
    internal static MonsterNativeCombat Capture(BaseCreature actor)=>new(actor.Serial.Value,actor.DamageMin,actor.DamageMax,
        actor.BasePhysicalResistance,actor.BaseFireResistance,actor.BaseColdResistance,actor.BasePoisonResistance,actor.BaseEnergyResistance,
        actor.PhysicalDamage,actor.FireDamage,actor.ColdDamage,actor.PoisonDamage,actor.EnergyDamage,actor.ActiveMoveSpeed,actor.PassiveMoveSpeed,SkillHealth.Maximum256(actor));
    internal static MonsterNativeCombat Find(BaseCreature actor)=>States.GetValueOrDefault(actor.Serial.Value);
    internal static void RememberNormal(BaseCreature actor,int? life256Override=null)
    {
        if(!States.ContainsKey(actor.Serial.Value)&&MonsterVariants.SpawnDifficulty(actor)==MonsterDifficulty.Normal)
        {var row=Capture(actor);if(life256Override is{} life){row=row with{Life256=life};}Validate(row);States.Add(row.Actor,row);}
        else if(MonsterVariants.SpawnDifficulty(actor)==MonsterDifficulty.Normal&&States.GetValueOrDefault(actor.Serial.Value)is{Life256:null} old)
        {var row=old with{Life256=life256Override??SkillHealth.Maximum256(actor)};Validate(row);States[actor.Serial.Value]=row;}
        else if(life256Override is{} expected&&MonsterVariants.SpawnDifficulty(actor)==MonsterDifficulty.Normal
            &&States.GetValueOrDefault(actor.Serial.Value)is{} existing&&existing.Life256!=expected)
        {throw new NotSupportedException("Saved Normal life baseline disagrees with the native unbuffed maximum");}
    }
    private static void Validate(MonsterNativeCombat row)
    {
        if(row==null||row.Actor==0||!((Serial)row.Actor).IsMobile||row.Minimum< -1||row.Maximum<row.Minimum||row.Life256 is<=0
            ||!double.IsFinite(row.Walk)||!double.IsFinite(row.Idle)||row.Walk<0||row.Idle<0
            ||new[]{row.PhysicalDamage,row.FireDamage,row.ColdDamage,row.PoisonDamage,row.LightningDamage}.Any(x=>x is <0 or >100))
        {throw new InvalidDataException("Invalid monster native combat baseline");}
    }
    internal static MonsterNativeCombat[] Snapshot()=>States.Values.OrderBy(x=>x.Actor).ToArray();
    internal static void Restore(MonsterNativeCombat[] rows)
    {
        ArgumentNullException.ThrowIfNull(rows);var states=new Dictionary<uint,MonsterNativeCombat>();
        foreach(var row in rows){Validate(row);if(!states.TryAdd(row.Actor,row)){throw new InvalidDataException("Duplicate monster native baseline");}}
        States=states;
    }
    internal static void Prune()
    {foreach(uint id in States.Keys.ToArray()){if(World.FindMobile((Serial)id) is not BaseCreature{Deleted:false}){States.Remove(id);}}}
    internal static void Apply(BaseCreature actor,MonsterNativeCombat basis,CreatureVariant profile=null)
    {
        bool mapped=profile is{Enabled:true,ApplyStats:true};
        actor.SetDamage(mapped?profile.Attack1.MinDamage??basis.Minimum:basis.Minimum,mapped?profile.Attack1.MaxDamage??basis.Maximum:basis.Maximum);
        var res=mapped?profile.Resistances:new CreatureResistances();
        actor.SetResistance(ResistanceType.Physical,res.Physical??basis.Physical);actor.SetResistance(ResistanceType.Fire,res.Fire??basis.Fire);
        actor.SetResistance(ResistanceType.Cold,res.Cold??basis.Cold);actor.SetResistance(ResistanceType.Poison,res.Poison??basis.Poison);
        actor.SetResistance(ResistanceType.Energy,res.Lightning??basis.Lightning);
        bool typed=mapped&&profile.Attack1.Physical!=null&&profile.Attack1.Fire!=null;
        actor.SetDamageType(ResistanceType.Physical,typed?profile.Attack1.Physical.Value:basis.PhysicalDamage);
        actor.SetDamageType(ResistanceType.Fire,typed?profile.Attack1.Fire.Value:basis.FireDamage);
        actor.SetDamageType(ResistanceType.Cold,typed?profile.Attack1.Cold??0:basis.ColdDamage);
        actor.SetDamageType(ResistanceType.Poison,typed?profile.Attack1.Poison??0:basis.PoisonDamage);
        actor.SetDamageType(ResistanceType.Energy,typed?profile.Attack1.Lightning??0:basis.LightningDamage);
        actor.ActiveMoveSpeed=mapped&&profile.Movement.WalkMs is{} walk?walk/1000.0:basis.Walk;
        actor.PassiveMoveSpeed=mapped&&profile.Movement.IdleMs is{} idle?idle/1000.0:basis.Idle;
        CreatureProfiles.Session(actor).DifficultyMelee=null;
    }
}
