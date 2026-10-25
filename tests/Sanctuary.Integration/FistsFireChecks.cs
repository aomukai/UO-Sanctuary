// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class FistsFireChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(FistsFireChecks));
    private sealed class FixedRandom:Random{public override int Next(int max)=>Math.Max(0,max-1);}
    private sealed record Saved(uint Owner,uint Target,MartialCharge[] Charges,FirePatch[] Patches,Point3D Origin,uint Primary,uint Secondary);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"fists-fire-fixtures.json");
    public static void Configure(){Register("fists-fire-check",Check);Register("fists-fire-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Prepared Fists finisher conversion/area/field and persistence",_=>Core.LoopContext.Post(()=>
    {try{Controlled(action);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Controlled(Action action)
    {
        var hit=TigerStrikes.HitRoll;var element=TigerStrikes.ElementRoll;var physical=TigerStrikes.PhysicalRoll;var effects=TigerStrikes.Effects;
        var fire=FistsFireRelease.DamageRoll;var seed=FistsFireRelease.SeedLow;var block=SourceBlocking.BlockRoll;var field=WeaponMissiles.DamageRoll;
        try{TigerStrikes.HitRoll=()=>0;TigerStrikes.ElementRoll=()=>0;TigerStrikes.PhysicalRoll=_=>0;TigerStrikes.Effects=new FixedRandom();FistsFireRelease.DamageRoll=_=>0;FistsFireRelease.SeedLow=()=>7;SourceBlocking.BlockRoll=_=>99;WeaponMissiles.DamageRoll=_=>0;action();}
        finally{TigerStrikes.HitRoll=hit;TigerStrikes.ElementRoll=element;TigerStrikes.PhysicalRoll=physical;TigerStrikes.Effects=effects;FistsFireRelease.DamageRoll=fire;FistsFireRelease.SeedLow=seed;SourceBlocking.BlockRoll=block;WeaponMissiles.DamageRoll=field;}
    }
    private static SkillValues Values(int rank,int synergy=0)=>new(SkillMechanics.For(CombatAbility.FistsOfFire),k=>k=="Fists of Fire"?rank:0,k=>k=="Royal Strike"?synergy:0,99);
    private static MartialCharge Charge(PlayerMobile p,int count=3,int rank=1,CombatAbility ability=CombatAbility.FistsOfFire)=>new(p.Serial.Value,ability,count,rank,count*50,TimedEffects.Now.AddHours(1));
    private static void Hold(PlayerMobile p,params MartialCharge[] states)=>AssassinCharges.Restore(AssassinCharges.Snapshot().Where(x=>x.Owner!=p.Serial.Value).Concat(states).ToArray());
    private static FirePatch[] Patches(PlayerMobile p)=>FireGround.Snapshot().Patches.Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static PlayerMobile Fresh(int column=6000)
    {var p=BladeBodyChecks.Fresh(column,10,3400);p.Name="FistsFixtureOwner";while(p.RawStr<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}return p;}
    private static BaseCreature Target(PlayerMobile p,int x=1,int y=0)
    {
        var t=new CobraPassiveTarget{Name="FistsFixtureTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.AIObject?.AITimer.Stop();t.MoveToWorld(new(p.X+x,p.Y+y,p.Z),p.Map);t.SetHits(50000);t.Hits=t.HitsMax;
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(type,0);}return t;
    }
    private static void Delete(BaseCreature t){t.Corpse?.Delete();t.Delete();}
    private static Item Named(PlayerMobile p,string key)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new FixedRandom(),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Equip "+key);ClassGear.Refresh(p);return item;}
    private static void Differential(PlayerMobile p,BaseCreature t)
    {
        using var conversion=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"fists-fire-conversion-source-probe.json")));int fireRows=0;
        foreach(var r in conversion.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();if(V(8)!=0 || V(0)>3){continue;}
            bool applies=V(0)>0 && V(3)!=0 && V(4)!=0 && V(5)!=0 && V(6)!=0 && Math.Max(V(1),V(2))>0;
            var packet=GearEffects.PrepareWeaponImpact(p,t,0,new FixedRandom(),new(V(7)/256.0,17/256.0,0,0,0),
                skillElements:applies?new GearDamage(0,Math.Max(V(1),V(2))*100/256.0,0,0,0,ColdMs:0):null,
                projectileCritical:false,physicalToFirePercent:applies?Math.Max(V(1),V(2))*3:0);
            Require(NamedElements.To256(packet.Parts.Physical)==V(9) && NamedElements.To256(packet.Parts.Fire)==V(10),"Complete source consumer/conversion packet");fireRows++;
        }
        using var builder=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"fists-fire-builder-source-probe.json")));int builds=0;
        foreach(var r in builder.RootElement.EnumerateArray())
        {
            int V(int i)=>r[i].GetInt32();if(V(4)==0 || V(5)==0 || V(6)==0){continue;}
            MartialCharge old=V(2)>0?new(1,CombatAbility.FistsOfFire,V(2),9,V(2)*50,DateTime.UnixEpoch.AddMilliseconds(321*40)):null;
            var next=AssassinCharges.Build(old,1,Values(V(3)),DateTime.UnixEpoch.AddMilliseconds(100*40));
            Require(next.Count==V(10) && next.InitialRank==V(11) && next.AttackPercent==V(12) && next.UntilUtc==DateTime.UnixEpoch.AddMilliseconds(V(13)*40),"Complete do35/do34 successful gain");builds++;
        }
        var one=FistsFireRelease.Capture(Values(1),1,0,_=>0);var synergized=FistsFireRelease.Capture(Values(1,1),3,0,_=>0);var mastery=FistsFireRelease.Capture(Values(1,1),3,15,_=>0);
        Require(one.Inline256==1536 && one.Max256==2560 && one.FieldMin256==48 && one.FieldMax256==80 && one.ConversionPercent==3 && one.Radius==4 && one.ScatterRadius==4,"Pinned rank1 skill and distinct ground missile endpoints");
        Require(synergized.Inline256==1720 && synergized.Max256==2867 && synergized.FieldMin256==48 && synergized.FieldMax256==80 && mastery.Inline256==1978 && mastery.FieldMin256==55,"Skill12 versus missile6 hard-point synergy and mastery/truncation");
        for(int rank=1;rank<=255;rank++)
        {
            var v=Values(rank);var a=FistsFireRelease.Capture(v,1,0,_=>0);var b=FistsFireRelease.Capture(v,3,0,_=>0);
            Require(a.Inline256==b.Inline256 && a.ConversionPercent==Math.Min(3*rank,100) && v.Mana256==512 && v.Formula("toht")==25+10*(rank-1),"255 ranks; count must not multiply inline fire/conversion");
        }
        Require(fireRows==6144 && builds==512,"Incomplete source/native selected matrices: "+fireRows+"/"+builds);
        var expired=Charge(p,3,20) with{UntilUtc=DateTime.UnixEpoch};var rebuilt=AssassinCharges.Build(expired,p.Serial.Value,Values(3),DateTime.UnixEpoch.AddSeconds(1));
        Require(rebuilt.Count==1 && rebuilt.InitialRank==3 && rebuilt.AttackPercent==50,"Expired Fists slot retained old rank/count");
        Log.Information("Fists release:6144 complete source fire conversion controls,512 builder gains and255 numeric/count controls pass; cold/count4 controls stay outside this component");
    }
    private static void Conversion(PlayerMobile p,BaseCreature t)
    {
        var caster=ClassSkillChecks.Player(DiabloClass.Sorceress);caster.AddItem(new Backpack());var orb=Named(caster,"Eschuta's temper");int mastery=NamedElements.MasteryPercent(caster,"fire");Require(mastery>0,"Actual item fire mastery");
        var damage=new GearDamage(100,10,0,0,0);
        var normal=GearEffects.PrepareWeaponImpact(caster,t,0,new FixedRandom(),damage,projectileCritical:false);
        var converted=GearEffects.PrepareWeaponImpact(caster,t,0,new FixedRandom(),damage,projectileCritical:true,physicalToFirePercent:60);
        Require(converted.Parts.Physical==80 && NamedElements.To256(converted.Parts.Fire-normal.Parts.Fire)==120*256,"Critical precedes conversion; converted fire must bypass item mastery");
        var high=GearEffects.PrepareWeaponImpact(caster,t,0,new FixedRandom(),new(2000099/256.0,0,0,0,0),projectileCritical:false,physicalToFirePercent:33);
        Require(NamedElements.To256(high.Parts.Physical)==1340099 && NamedElements.To256(high.Parts.Fire)==660000,"Legacy large-value source percentage truncation");
        t.SetResistance(ResistanceType.Physical,100);t.SetResistance(ResistanceType.Fire,50);
        converted=GearEffects.PrepareWeaponImpact(caster,t,0,new FixedRandom(),damage,projectileCritical:true,physicalToFirePercent:60);
        Require(converted.Parts.Physical==0 && NamedElements.To256(converted.Parts.Fire)==NamedElements.To256((normal.Parts.Fire+120)/2),"Typed resistance follows conversion");
        t.SetResistance(ResistanceType.Fire,100);Require(GearEffects.PrepareWeaponImpact(caster,t,0,new FixedRandom(),damage,projectileCritical:false,physicalToFirePercent:100).Parts.Total==0,"Fire immune after full conversion");
        t.SetResistance(ResistanceType.Physical,0);t.SetResistance(ResistanceType.Fire,0);caster.Delete();
        var raw=new GearDamage(20000,0,0,0,0);CreatureProfiles.Session(t).RecoveryUntil=0;
        GearEffects.Strike(p,t,0,new FixedRandom(),wearWeapon:false,preparedDamage:raw,softHit:true,sourceDrain:default(PlayerWeaponDrain));Require(CreatureHitRecovery.Remaining(t)==0,"SOFTHIT main contact triggered hit recovery");
        t.Hits=t.HitsMax;SkillCombat.FireImpact(p,t,20000*256);Require(CreatureHitRecovery.Remaining(t)>0,"Area hit lost its independent recovery");CreatureProfiles.Session(t).RecoveryUntil=0;t.Hits=t.HitsMax;
        var elite=new EncounterCreature();var profile=EncounterProfiles.Profile("crypt-sentinel-90");
        elite.Assign(profile,profile.MinimumLevel,MonsterDifficulty.Hell,EncounterGrade.Unique,ChampionKind.Champion,[EliteModifier.LightningEnchanted],EliteAura.None,null,new Random(173));
        elite.CantWalk=true;elite.NoKillAwards=true;elite.MoveToWorld(new(t.X,t.Y+1,t.Z),p.Map);elite.AIObject?.AITimer.Stop();elite.SetResistance(ResistanceType.Physical,0);
        int bolts=EliteBolts.ActiveCount;GearEffects.Strike(p,elite,0,new FixedRandom(),wearWeapon:false,preparedDamage:new GearDamage(10,0,0,0,0),softHit:true,sourceDrain:default(PlayerWeaponDrain));
        Require(EliteBolts.ActiveCount==bolts+8,"SOFTHIT suppressed existing elite damage retaliation");EliteBolts.Cancel(elite);Delete(elite);
    }
    private static void Finish(PlayerMobile p,BaseCreature target)
    {
        ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;
        Require(ClassCombat.Perform(p,CombatAbility.BasicAttack,target,target.Location,Core.TickCount),"Ordinary finisher start");ClassCombat.ReleaseCast(p);long at=ClassCombat.For(p).EffectAt;
        Require(ClassCombat.ResolveAction(p,at) && !ClassCombat.ResolveAction(p,at),"Exactly one ordinary finisher");
    }
    private static void Delivery(PlayerMobile p,BaseCreature t)
    {
        var nearby=Target(p,2);var outside=Target(p,4);int hp=SkillHealth.Balance256(nearby),far=SkillHealth.Balance256(outside);
        Hold(p,Charge(p,1));Finish(p,t);Require(AssassinCharges.State(p,CombatAbility.FistsOfFire)==null && Patches(p).Length==0 && SkillHealth.Balance256(nearby)==hp,"One charge has only inline fire");
        Hold(p,Charge(p,2));Finish(p,t);Require(AssassinCharges.State(p,CombatAbility.FistsOfFire)?.Count==1 && SkillHealth.Balance256(nearby)<hp && SkillHealth.Balance256(outside)==far && Patches(p).Length==0,"Two charges stacked area/one consumed/no field/outside radius");
        Hold(p,Charge(p),Charge(p,2,3,CombatAbility.TigerStrike),Charge(p,3,1,CombatAbility.CobraStrike));var packet=FistsFireRelease.Capture(p,AssassinCharges.State(p,CombatAbility.FistsOfFire));hp=SkillHealth.Balance256(nearby);Finish(p,t);
        Require(AssassinCharges.Finisher(p).Length==3 && AssassinCharges.Finisher(p).All(x=>x.Count==(x.Ability==CombatAbility.TigerStrike?1:2)),"Independent modern consume-one families");
        var patches=Patches(p);Require(patches.Length==FistsFireRelease.Scatter(4,7).Length && patches.All(x=>x.Size==2 && x.DamageRate==0 && x.SkillRank==packet.Rank && x.Min256==packet.FieldMin256 && x.Max256==packet.FieldMax256 && (x.UntilUtc-x.NextUtc).TotalMilliseconds==63*40),"Three charges: source scatter/field payload/lifetime");
        Require(SkillHealth.Balance256(nearby)<hp && SkillHealth.Balance256(outside)==far,"Area recipient isolation");
        int before=SkillHealth.Balance256(t);FireGround.Tick(patches[0].NextUtc);Require(SkillHealth.Balance256(t)<before,"Actual field first-collider tick");
        FireGround.Tick(patches.Max(x=>x.UntilUtc));Require(Patches(p).Length==0,"Natural64frame expiration");
        //100% conversion has zero physical leech, including captured Cobra.
        Hold(p,Charge(p,1,34),Charge(p,3,1,CombatAbility.CobraStrike));SkillHealth.SetBalance256(p,10*256);NecromancerEffects.SetMana256(p,10*256);int life=SkillHealth.Balance256(p),mana=NecromancerEffects.ManaBalance256(p);Finish(p,t);
        Require(SkillHealth.Balance256(p)==life && NecromancerEffects.ManaBalance256(p)==mana,"Converted fire produced Cobra physical leech");
        // A failed hit must retain every charge and create no secondary effects.
        Hold(p,Charge(p));var held=AssassinCharges.State(p,CombatAbility.FistsOfFire);TigerStrikes.HitRoll=()=>1;Finish(p,t);TigerStrikes.HitRoll=()=>0;
        Require(AssassinCharges.State(p,CombatAbility.FistsOfFire)==held && Patches(p).Length==0,"Miss consumed or released charge");
        var dying=new CobraDeathTarget{Name="FistsFixtureDying",Frozen=true,CantWalk=true,NoKillAwards=true};dying.MoveToWorld(t.Location,p.Map);dying.AIObject?.AITimer.Stop();dying.SetHits(1);dying.Hits=1;
        bool died=false;hp=SkillHealth.Balance256(nearby);Hold(p,Charge(p));CobraDeathTarget.Died=()=>{Require(SkillHealth.Balance256(nearby)==hp && Patches(p).Length==0,"Secondary release preceded parent death commit");died=true;};
        try{Finish(p,dying);}finally{CobraDeathTarget.Died=null;}
        Require(died && SkillHealth.Balance256(nearby)<hp && Patches(p).Length>0,"Committed parent death lost stacked secondary release");Delete(dying);
        Delete(nearby);Delete(outside);FireGround.Cancel(p);
    }
    private static void FieldLifecycle(PlayerMobile p,BaseCreature t)
    {
        var at=TimedEffects.Now;FireGround.AddFists(p,1,t.X,t.Y,t.Z,256,256,at);FireGround.AddFists(p,1,t.X,t.Y,t.Z,256,256,at);var fields=Patches(p);int hp=SkillHealth.Balance256(t);
        FireGround.Tick(at.AddMilliseconds(40));Require(SkillHealth.Balance256(t)==hp-512,"Independent overlapping first-collider fields");
        var location=p.Location;p.Internalize();hp=SkillHealth.Balance256(t);FireGround.Tick(at.AddMilliseconds(80));Require(SkillHealth.Balance256(t)==hp,"Offline owner continued fighting");FireGround.Tick(at.AddSeconds(4));FireGround.Tick(at.AddSeconds(4.04));Require(Patches(p).Length==0,"Offline lifetime stopped");p.MoveToWorld(location,Map.Trammel);
        FireGround.AddFists(p,1,t.X,t.Y,t.Z,256,256,TimedEffects.Now);p.MoveToWorld(location,Map.Felucca);FireGround.Tick(TimedEffects.Now.AddMilliseconds(40));Require(Patches(p).Length==0,"Owner map change retained source fire");p.MoveToWorld(location,Map.Trammel);
        // Invalid footprint must fail closed without touching production saves.
        var save=FireGround.Snapshot();FireGround.AddFists(p,1,t.X,t.Y,t.Z,256,256,TimedEffects.Now);var invalid=Patches(p).Single() with{Size=1};bool failed=false;
        try{FireGround.Restore(save with{Patches=save.Patches.Append(invalid).ToArray()});}catch(InvalidDataException){failed=true;}finally{FireGround.Restore(save);}Require(failed,"Invalid Fists field footprint accepted");
    }
    private static void Check()
    {
        var p=Fresh();var t=Target(p);Differential(p,t);Conversion(p,t);
        var first=new Tekagi();ClassGear.Profiles.Convert(first,99,GearRarity.Normal,new Random(173));Require(p.EquipItem(first),"Primary claw");var second=(BaseWeapon)Named(p,"Cutthroat1");
        Delivery(p,t);FieldLifecycle(p,t);Hold(p,Charge(p),Charge(p,2,3,CombatAbility.TigerStrike),Charge(p,3,1,CombatAbility.CobraStrike));
        FistsFireRelease.AfterParent(p,t.Location,p.Map,FistsFireRelease.Capture(p,AssassinCharges.State(p,CombatAbility.FistsOfFire)),TimedEffects.Now);
        // Deliberately held reload payload; natural64frame duration tested above.
        var due=TimedEffects.Now.AddHours(1);var patches=Patches(p).Select(x=>x with{NextUtc=due,UntilUtc=due.AddMilliseconds(64*40)}).ToArray();var save=FireGround.Snapshot();FireGround.Restore(save with{Patches=save.Patches.Where(x=>x.Owner!=p.Serial.Value).Concat(patches).ToArray()});
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,t.Serial.Value,AssassinCharges.Finisher(p),patches,p.Location,first.Serial.Value,second.Serial.Value)));
        Require(AssassinCombat.Implemented(CombatAbility.FistsOfFire) && AssassinClaws.Handles(CombatAbility.FistsOfFire),"Learned claw action missing");
        Log.Information("Fists prepared finisher:actual Sorceress/Eschuta mastery/critical/conversion/immunity,SOFTHIT,ordinary attack release,stacked area/fields,Tiger/Cobra,miss,offline/expiry/map cleanup pass; learned action stays gated");
    }
    private static void Reload()
    {
        var save=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)save.Owner);var target=(BaseCreature)World.FindMobile((Serial)save.Target);
        Require(AssassinCharges.Finisher(p).SequenceEqual(save.Charges) && Patches(p).SequenceEqual(save.Patches),"Saved independent charges/source fields");
        Require(CombatTiming.Equipped(p)?.Serial.Value==save.Primary && WeaponHands.Pair(p)?.Secondary.Serial.Value==save.Secondary,"Saved selected claw equipment");
        p.MoveToWorld(save.Origin,Map.Trammel); // Saved offline players load on Map.Internal.
        var patches=Patches(p);var origin=p.Location;int hp=SkillHealth.Balance256(target);FireGround.Tick(patches[0].NextUtc);Require(SkillHealth.Balance256(target)<hp,"Restored source field resumed once due");
        FireGround.Cancel(p);Hold(p,save.Charges);Finish(p,target);Require(AssassinCharges.State(p,CombatAbility.FistsOfFire)?.Count==2 && Patches(p).Length>0,"Restored charges release through ordinary Attack");FireGround.Cancel(p);
        var all=FireGround.Snapshot();FireGround.Restore(all with{Patches=all.Patches.Concat(save.Patches).ToArray()});Hold(p,save.Charges);
        Require(AssassinCombat.Implemented(CombatAbility.FistsOfFire),"Reload lost learned Fists action");
    }
}
