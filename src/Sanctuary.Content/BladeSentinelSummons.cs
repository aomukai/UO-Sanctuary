// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using Server;
using Server.Mobiles;
using Server.Spells;

namespace Sanctuary;

internal sealed record BladeSpawnContext(MonsterDifficulty Difficulty,int Speed);
internal sealed record BladeSpawnCapture(AssassinTrapCreationStats Creation,BladeSpawnContext Context);

// Actual owner/source-data collection. Learned input/payment remains a separate
// caller; no explicit synthetic spawn stats or passive callback is required.
internal static class BladeSentinelSummons
{
    internal static Func<int> SeedLow=()=>Utility.Random(int.MaxValue);
    internal static int InheritedValue(string name,int value)
    {
        if(name.StartsWith("item_pierce_",StringComparison.Ordinal) && value!=0)
        {throw new NotSupportedException("Blade inherited sunder consumer is not implemented: "+name);}
        return value;
    }
    private static int Accrued(PlayerMobile owner,string name)
    {
        string code=name switch
        {
            "item_pierce_damage_immunity"=>"pierce-immunity-damage","item_pierce_fire_immunity"=>"pierce-immunity-fire",
            "item_pierce_light_immunity"=>"pierce-immunity-light","item_pierce_poison_immunity"=>"pierce-immunity-poison",
            "item_pierce_cold_immunity"=>"pierce-immunity-cold",
            "passive_fire_mastery"=>"extra-fire","passive_ltng_mastery"=>"extra-ltng",
            "passive_cold_mastery"=>"extra-cold","passive_pois_mastery"=>"extra-pois",
            "passive_fire_pierce"=>"pierce-fire","passive_ltng_pierce"=>"pierce-ltng",
            "passive_cold_pierce"=>"pierce-cold","passive_pois_pierce"=>"pierce-pois",
            _=>throw new NotSupportedException("Unmapped inherited blade stat: "+name)
        };
        return InheritedValue(name,ClassGear.Bonus(owner,code));
    }
    internal static BladeSpawnCapture Capture(PlayerMobile owner)
    {
        if(owner is not{Alive:true,Deleted:false} || owner.Map==null || owner.Map==Map.Internal
            || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile) || profile.Class!=DiabloClass.Assassin
            || SkillMechanics.Rank(owner,CombatAbility.BladeSentinel) is <1 or >255)
        {throw new InvalidOperationException("Blade summon needs a live learned Assassin world owner");}
        var difficulty=SummonDifficulties.For(owner.Map);const string source="bladecreeper";
        int Field(string field)=>NecromancerSummons.Source(source,field,difficulty);
        // Non-evil alignment gives playerCount1/zero HP bonus in the source
        // initializer. noRatio uses the literal min/max HP and AC columns.
        // Current blade bounds coincide: there is no random HP result to choose.
        if(NecromancerSummons.Source(source,"noRatio")!=1 || NecromancerSummons.Source(source,"Align")!=1
            || Field("minHP")<=0 || Field("minHP")!=Field("maxHP") || Field("A1TH")!=0)
        {throw new NotSupportedException("Blade source spawn needs an unsupported ratio/alignment/HP/attack initializer");}
        // Also check cold before the intentionally malformed formula skips its
        // callback. Do not silently admit an unsupported equipped sunder state.
        foreach(string name in new[]{"item_pierce_damage_immunity","item_pierce_fire_immunity","item_pierce_light_immunity","item_pierce_poison_immunity","item_pierce_cold_immunity"})
        {Accrued(owner,name);}
        var values=SkillMechanics.Values(owner,CombatAbility.BladeSentinel);
        var creation=AssassinTrapCreation.Prepare(values,ClassStats.Level(owner),(int)difficulty,true,
            new(checked(Field("minHP")*256),Field("AC"),0),name=>Accrued(owner,name));
        int speed=SourceActorMotion.Speed(NecromancerSummons.Source(source,"Velocity"),0,75,0).Velocity;
        return new(creation,new(difficulty,speed));
    }
    internal static BladeSentinelBody Create(PlayerMobile owner,Point3D destination,DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Blade summon clock must be UTC");}
        var capture=Capture(owner);var map=owner.Map;
        if(SpellHelper.IsTown(owner.Location,owner) || SpellHelper.IsTown(destination,owner))
        {throw new InvalidOperationException("Blade summon is not permitted in town");}
        if(map.Width*2>65536 || map.Height*2>65536 || destination.X<=0 || destination.Y<=0
            || destination.X>=map.Width || destination.Y>=map.Height || Math.Abs(destination.Z-owner.Z)>=16)
        {throw new NotSupportedException("Blade summon destination/room is not mapped");}
        uint x=checked((uint)(owner.X*2))<<16|32768,y=checked((uint)(owner.Y*2))<<16|32768;
        var motion=new ActorMotionState(x,y,[],new(destination.X*2,destination.Y*2),0,0,0,0,0,new(0,0),new(0,0),20,0x800,0,[],0);
        var body=BladeSentinelBody.CreatePrepared(owner,capture.Creation,motion,owner.Z,capture.Context);
        try
        {
            body.StartPreparedController(new(destination.X*2,destination.Y*2),capture.Context.Speed,SourceRandom.InitLow(SeedLow()),at);
            AssassinTrapRoster.Register(owner,body,checked((int)SkillMechanics.Values(owner,CombatAbility.BladeSentinel,capture.Creation.Rank).Eval("petmax")));
            return body;
        }
        catch{body.Delete();throw;}
    }
}
