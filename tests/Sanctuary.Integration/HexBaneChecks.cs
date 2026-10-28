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
public static class HexBaneChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(HexBaneChecks));
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,uint Target,bool Female,HexBaneBuff Buff,HexBaneDebuff Debuff);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"hex-bane-fixtures.json");
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Register(string n,Action a)=>ConsoleInputHandler.RegisterCommand(n,"Prepared Hex Bane passive/debuff and saved native consumers",_=>Core.LoopContext.Post(()=>{try{a();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));
    public static void Configure(){Register("hex-bane-check",Check);Register("hex-bane-reload-check",Reload);}
    private static int Tier(int rank,int start,int[] steps)
    {int remaining=rank-1;int value=start;for(int i=0;i<5;i++){int n=i==4?remaining:Math.Min(remaining,i==1?8:i==0?7:6);value+=n*steps[i];remaining-=n;}return value;}
    private static SkillValues Values(int rank,int consume=0,int purge=0,int blades=0,int blast=0)=>new(SkillMechanics.For(CombatAbility.HexBane),k=>k=="Hex Bane"?rank:255,k=>k=="Consume"?consume:k=="Hex Purge"?purge:k=="Mirrored Blades"?blades:k=="Eldritch Blast"?blast:0,99);
    private static void Model()
    {
        int count=0;for(int rank=1;rank<=255;rank++)foreach(int c in new[]{0,1,20})foreach(int p in new[]{0,20})foreach(int m in new[]{0,20})foreach(int e in new[]{0,20})
        {
            var v=HexBane.Capture(Values(rank,c,p,m,e));int synergy=35*(c+p+m);
            int min=Tier(rank,9,[3,7,11,15,19])*128,max=Tier(rank,16,[5,9,13,17,21])*128;
            Require(v.Minimum==(min+min*synergy/100)/256&&v.Maximum==(max+max*synergy/100)/256,"Whole-point passive endpoint/hard-only35percent synergy");
            Require(v.AttackPercent==20+9*(rank-1)&&v.BuffFrames==3600+300*(rank-1)&&v.DebuffFrames==150+5*e&&v.Mana256==(12+rank-1)*128&&v.DefensePercent==-Math.Min(30+rank-1,50)&&v.TargetAttackPercent==-25,"Bane source values");count++;
        }
        Require(count==6120&&HexBane.Capture(Values(1)) is{Minimum:4,Maximum:8,Mana256:1536,DefensePercent:-30,BuffFrames:3600,DebuffFrames:150},"Bane rank1 rounding/duration");
        Require(!WarlockCombat.Implemented(CombatAbility.HexBane),"Prepared Bane enabled learned cast");
        Log.Information("Hex Bane:6120current hard-synergy/whole-point controls;192source do23contexts pinned, event36delivery supplied");
    }
    private static void Train(PlayerMobile p,string id,int rank=1)
    {Require(TalentCatalog.TryGet(DiabloClass.Warlock,id,out var t),"Bane talent "+id);foreach(var pre in t.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,pre)==0){Train(p,pre);}}while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Bane training "+id);}}
    private static PlayerMobile Fresh(bool female=false)
    {
        var p=new PlayerMobile{Female=female,Body=female?0x191:0x190,Name="HexBaneFixture",Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);
        FixtureSpace.Place(p,new(6150,1650,0),Map.Trammel);while(p.RawStr<50){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Fixture Strength");}Train(p,"warlock.hexbane");Equip(p,new Broadsword());Mana(p);return p;
    }
    private static void Equip(PlayerMobile p,Item item)
    {if(item is BaseWeapon&&CombatTiming.Equipped(p) is{} old){p.AddToBackpack(old);}p.AddToBackpack(item);Require(p.EquipItem(item),"Bane fixture equip "+item);ClassGear.Refresh(p);}
    private static void Mana(PlayerMobile p)=>NecromancerEffects.SetMana256(p,p.ManaMax*256);
    private static ShockWaveTarget Enemy(PlayerMobile p)
    {
        var t=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(5000);SkillHealth.SetBalance256(t,t.HitsMax*256);
        foreach(var kind in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(kind,0);}return t;
    }
    private static void NativeDamage(PlayerMobile p,Mobile t,bool ranged)
    {
        SkillHealth.SetBalance256(t,t.HitsMax*256);int before=SkillHealth.Balance256(t);var b=HexBane.Buff(p);AOS.Damage(t,p,100,false,100,0,0,0,0,archer:ranged,weaponDamage:true);int loss=before-SkillHealth.Balance256(t);
        Require(loss>=(100+b.Values.Minimum)*256&&loss<=(100+b.Values.Maximum)*256,"Native AOS weapon omitted/doubled Bane magic: "+loss);
        before=SkillHealth.Balance256(t);SkillCombat.HitParts(p,t,new(100,0,0,0,0),true,weapon:true,ranged:ranged);loss=before-SkillHealth.Balance256(t);
        Require(loss>=(100+b.Values.Minimum)*256&&loss<=(100+b.Values.Maximum)*256,"Shared weapon receiver omitted/doubled Bane magic");
    }
    private static void Consumers()
    {
        var p=Fresh();var t=Enemy(p);try
        {
            int attack=ClassStats.AttackRating(p);int mana=NecromancerEffects.ManaBalance256(p);Require(HexBane.CastPrepared(p)&&mana-NecromancerEffects.ManaBalance256(p)==1536,"Prepared Bane once-only6mana payment");var b=HexBane.Buff(p);
            Require(Math.Abs((b.UntilUtc-TimedEffects.Now).TotalSeconds-144)<0.2,"Bane actual cast duration");
            Require(ClassStats.AttackRating(p)==ClassStats.PlayerAttackPercent(Math.Max(1,ClassStats.PlayerAttackBase(p)),ClassStats.PlayerItemAttackPercent(p)),"Bane AR pool");
            Require(ClassStats.AttackRating(p)>attack&&HexBane.AttackPercent(p)==20&&GearEffects.Damage(p,0).Magic==4&&GearEffects.Damage(p,1).Magic==8,"Weapon enchantment/AR endpoints");
            NativeDamage(p,t,false);
            var packet=GearEffects.Damage(p,0,t);int health=SkillHealth.Balance256(t);GearEffects.Strike(p,t,0,new Random(71),wearWeapon:false,preparedDamage:packet,projectileCritical:true);
            Require(health-SkillHealth.Balance256(t)==NamedElements.To256(packet.Physical*2+packet.Magic),"Prepared ordinary strike doubled or omitted Bane magic with physical critical");
            Require(HexBane.Debuff(t)==null,"Prepared Bane invented automatic event36delivery");
            int defense=ClassStats.Defense(t),rating=ClassStats.AttackRating(t);Require(!HexBane.ApplyPrepared(p,t,b,false,TimedEffects.Now)&&HexBane.Debuff(t)==null,"Ineligible Bane event");
            Require(HexBane.ApplyPrepared(p,t,b,true,TimedEffects.Now)&&HexBane.DefensePercent(t)==-30&&HexBane.TargetAttackPercent(t)==-25,"Supplied Bane debuff");
            Require(ClassStats.Defense(t)==ClassStats.ScaleDefense(CreatureProfiles.Defense(t),-30)&&ClassStats.AttackRating(t)==rating+rating*-25/100&&ClassStats.Defense(t)<=defense,"Bane actual target defense/AR pools");
            var d=HexBane.Debuff(t);Require(Math.Abs((d.UntilUtc-TimedEffects.Now).TotalSeconds-6)<0.2,"Bane debuff duration");
            NecromancerEffects.Profiles.Store(t,new(Curse:new(CombatAbility.AmplifyDamage,p.Serial.Value,1,TimedEffects.Now.AddMinutes(1),t.X,t.Y)));
            Require(HexBane.Debuff(t)==d&&NecromancerEffects.Curse(t)?.Ability==CombatAbility.AmplifyDamage,"Noncurse Bane erased ordinary curse");NecromancerEffects.Profiles.Clear(t);
            var w=CombatTiming.Equipped(p);p.AddToBackpack(w);ClassGear.Refresh(p);Require(HexBane.Buff(p)==b&&HexBane.AttackPercent(p)==0&&GearEffects.Damage(p,0).Magic==0&&!HexBane.CastPrepared(p),"Weapon condition removed buff or retained unarmed benefit");Equip(p,w);
            NecromancerEffects.SetMana256(p,1535);Require(!HexBane.CastPrepared(p)&&HexBane.Buff(p)==b,"Fractional low mana replaced buff");Mana(p);
            Equip(p,new Bow());NativeDamage(p,t,true);Require(HexBane.Buff(p)==b&&HexBane.AttackPercent(p)==20,"Bane weap condition excluded bow");
            Require(!SkillBindings.Instance.Assign(p,false,CombatAbility.HexBane)&&!SkillBindings.Instance.AssignQuick(p,0,CombatAbility.HexBane),"Incomplete learned Bane binding enabled");
            var snapshot=HexBane.Snapshot();HexBane.Restore(new(snapshot.Buffs.Select(x=>x.Owner==p.Serial.Value?x with{UntilUtc=TimedEffects.Now.AddMilliseconds(-1)}:x).ToArray(),snapshot.Debuffs.Select(x=>x.Target==t.Serial.Value?x with{UntilUtc=TimedEffects.Now.AddMilliseconds(-1)}:x).ToArray()));
            Require(HexBane.Buff(p)==null&&HexBane.Debuff(t)==null&&HexBane.AttackPercent(p)==0&&HexBane.DefensePercent(t)==0,"Expired Bane remained active");HexBane.Prune();
            Mana(p);Require(HexBane.CastPrepared(p),"Equal-rank refresh fixture");var captured=HexBane.Buff(p);Train(p,"warlock.consume");Mana(p);Require(HexBane.CastPrepared(p)&&HexBane.Buff(p).Rank==captured.Rank&&HexBane.Buff(p).Values.Minimum>captured.Values.Minimum&&captured.Values.Minimum==4,"Equal-rank cast failed to rewrite changed hard-synergy passives");
        }
        finally{p.Delete();t.Delete();HexBane.Prune();}
    }
    private static void Lifecycle()
    {
        var p=Fresh();HexBane.CastPrepared(p);p.Kill();p.Resurrect();Require(HexBane.Buff(p)==null,"Death retained Bane");p.Corpse?.Delete();p.Delete();
        p=Fresh();HexBane.CastPrepared(p);ClassCreation.Profiles.TryGet(p.Serial,out var c);ClassCreation.Profiles.ResetAllocatedPoints(p,c);Require(HexBane.Buff(p)==null,"Respec retained Bane");p.Delete();
        p=Fresh();HexBane.CastPrepared(p);p.AddToBackpack(CombatTiming.Equipped(p));ClassGear.Refresh(p);Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Necromancer)&&!HexBane.Snapshot().Buffs.Any(x=>x.Owner==p.Serial.Value),"History retained Bane");p.Delete();
    }
    private static void Check()
    {
        Model();Consumers();Lifecycle();var saved=new Saved[2];int i=0;
        foreach(bool female in new[]{false,true})
        {
            var p=Fresh(female);Train(p,"warlock.hexbane",20);foreach(string id in new[]{"warlock.consume","warlock.hexpurge","warlock.mirroredblades","warlock.eldritchblast"}){Train(p,id);}
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Equip(p,helm);Mana(p);Require(HexBane.CastPrepared(p)&&HexBane.Buff(p).Rank==21,"Effective Bane rank");var b=HexBane.Buff(p);
            Require(b.Values==HexBane.Capture(SkillMechanics.Values(p,CombatAbility.HexBane))&&b.Values.DebuffFrames==155,"Hard-only Bane capture");
            p.AddToBackpack(helm);ClassGear.Refresh(p);Mana(p);int before=NecromancerEffects.ManaBalance256(p);Require(!HexBane.CastPrepared(p)&&HexBane.Buff(p)==b&&before==NecromancerEffects.ManaBalance256(p),"Lower-rank cast replaced stronger Bane or spent mana");Equip(p,helm);
            Mana(p);Require(HexBane.CastPrepared(p),"Equal-rank prepared recast");b=HexBane.Buff(p);var t=Enemy(p);Require(HexBane.ApplyPrepared(p,t,b,true,TimedEffects.Now),"Saved Bane debuff");var d=HexBane.Debuff(t);
            // Park inspected values for the long retained suite; original cast
            // durations and expiry eligibility were checked before parking.
            var held=HexBane.Snapshot();var until=TimedEffects.Now+FixtureSpace.Retention;
            HexBane.Restore(new(held.Buffs.Select(x=>x.Owner==p.Serial.Value?x with{UntilUtc=until}:x).ToArray(),held.Debuffs.Select(x=>x.Target==t.Serial.Value?x with{UntilUtc=until}:x).ToArray()));b=HexBane.Buff(p);d=HexBane.Debuff(t);held=HexBane.Snapshot();
            try{HexBane.Restore(new([b with{Values=b.Values with{AttackPercent=999}}],[]));throw new InvalidOperationException("Malformed Bane accepted");}catch(InvalidDataException){}
            Require(HexBane.Snapshot().Buffs.SequenceEqual(held.Buffs)&&HexBane.Snapshot().Debuffs.SequenceEqual(held.Debuffs),"Malformed Bane restore changed live state");saved[i++]=new(p.Serial.Value,CombatTiming.Equipped(p).Serial.Value,helm.Serial.Value,t.Serial.Value,female,b,d);
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload()
    {
        Model();foreach(var s in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(ShockWaveTarget)World.FindMobile((Serial)s.Target);Require(p.Female==s.Female&&World.FindItem((Serial)s.Weapon).Parent==p&&World.FindItem((Serial)s.Helm).Parent==p&&HexBane.Buff(p)==s.Buff&&HexBane.Debuff(t)==s.Debuff,"Saved Bane actor/gear/values changed");
            Require(HexBane.AttackPercent(p)==s.Buff.Values.AttackPercent&&HexBane.DefensePercent(t)==s.Debuff.DefensePercent&&GearEffects.Damage(p,0).Magic==s.Buff.Values.Minimum,"Reload lost native Bane consumers");NativeDamage(p,t,false);
        }
    }
}
