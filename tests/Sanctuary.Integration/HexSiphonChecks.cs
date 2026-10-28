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
public static class HexSiphonChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(HexSiphonChecks));
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,uint Target,bool Female,HexSiphonBuff Buff,HexSiphonDebuff Debuff);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"hex-siphon-fixtures.json");
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Register(string n,Action a)=>ConsoleInputHandler.RegisterCommand(n,"Prepared Siphon enchant/debuff and actual direct-kill resources",_=>Core.LoopContext.Post(()=>{try{a();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));
    public static void Configure(){Register("hex-siphon-check",Check);Register("hex-siphon-reload-check",Reload);}
    private static SkillValues Values(int rank,int engorge=0,int blast=0)=>new(SkillMechanics.For(CombatAbility.HexSiphon),k=>k=="Hex Siphon"?rank:255,k=>k=="Engorge"?engorge:k=="Eldritch Blast"?blast:0,99);
    private static void Model()
    {
        int count=0;for(int rank=1;rank<=255;rank++)foreach(int engorge in new[]{0,1,20})foreach(int blast in new[]{0,1,20})
        {var v=HexSiphon.Capture(Values(rank,engorge,blast));Require(v.LifeAfterKill==rank+engorge&&v.ManaAfterKill==rank+engorge&&v.AttackPercent==20+9*(rank-1)&&v.BuffFrames==3600+300*(rank-1)&&v.DebuffFrames==150+5*blast&&v.DamagePercent==-33&&v.Mana256==(25+rank-1)*128,"Current Siphon values/hard-only synergies/constant target damage");count++;}
        Require(count==2295&&HexSiphon.Capture(Values(1)) is{LifeAfterKill:1,ManaAfterKill:1,Mana256:3200},"Siphon rank1 values");Require(!WarlockCombat.Implemented(CombatAbility.HexSiphon),"Incomplete learned Siphon enabled");
        Log.Information("Hex Siphon:2295current formula controls;192source buff/560source kill contexts pinned,event36delivery supplied");
    }
    private static void Train(PlayerMobile p,string id,int rank=1)
    {Require(TalentCatalog.TryGet(DiabloClass.Warlock,id,out var t),"Siphon talent "+id);foreach(var pre in t.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,pre)==0){Train(p,pre);}}while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Siphon train "+id);}}
    private static void Mana(PlayerMobile p)=>NecromancerEffects.SetMana256(p,p.ManaMax*256);
    private static PlayerMobile Fresh(bool female=false)
    {
        var p=new PlayerMobile{Female=female,Body=female?0x191:0x190,Player=true,Name="HexSiphonFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);FixtureSpace.Place(p,new(6100,1580,0),Map.Trammel);
        while(p.RawStr<70){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Siphon fixture Strength");}Train(p,"warlock.hexsiphon");Equip(p,new Broadsword());Mana(p);return p;
    }
    private static void Equip(PlayerMobile p,Item item)
    {if(item is BaseWeapon&&CombatTiming.Equipped(p) is{} old){p.AddToBackpack(old);}p.AddToBackpack(item);Require(p.EquipItem(item),"Siphon fixture equip");ClassGear.Refresh(p);}
    private static ShockWaveTarget Enemy(PlayerMobile p,bool rewards=false)
    {var t=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=!rewards};t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(5000);SkillHealth.SetBalance256(t,t.HitsMax*256);foreach(var r in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Poison,ResistanceType.Energy}){t.SetResistance(r,0);}return t;}
    private static void ResourceVectors(PlayerMobile p)
    {
        using var d=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"hex-siphon-source-probe.json")));int count=0;
        foreach(var row in d.RootElement.GetProperty("kill").EnumerateArray())
        {
            int V(int i)=>row[i].GetInt32();if(V(1)!=0||V(2)!=1||V(3)!=1||V(5)>V(6)){continue;}
            Require(V(6)==(V(0)==17?p.ManaMax*256:SkillHealth.Maximum256(p)),"Source resource-vector fixture maximum");
            if(V(0)==17){NecromancerEffects.SetMana256(p,V(5));NecromancerEffects.AddMana(p,V(4)*256);Require(NecromancerEffects.ManaBalance256(p)==V(8),"Native mana helper/source kill callback fraction/cap");}
            else{SkillHealth.SetBalance256(p,V(5));SkillHealth.Heal(p,V(4)*256);Require(SkillHealth.Balance256(p)==V(8),"Native life helper/source kill callback fraction/cap");}count++;
        }
        Require(count==60,"Incomplete admitted native kill-resource comparisons");SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p));Mana(p);
    }
    private static void Injure(PlayerMobile p,int missing=4000)
    {SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-missing-73);NecromancerEffects.SetMana256(p,p.ManaMax*256-missing-91);}
    private static void KillCase(PlayerMobile p,string kind,bool rewards=true)
    {
        var t=Enemy(p,rewards);t.SetHits(1);t.Hits=1;Mobile actor=p;BaseCreature pet=null;Injure(p);int hp=SkillHealth.Balance256(p),mana=NecromancerEffects.ManaBalance256(p);var bonus=HexSiphon.AfterKill(p);
        int life=bonus.Life+ClassGear.Bonus(p,"heal-kill")+(NamedCombat.Demon(t)?ClassGear.Bonus(p,"demon-heal"):0),mp=bonus.Mana+ClassGear.Bonus(p,"mana-kill");
        if(kind=="pet"){pet=Enemy(p);pet.ControlMaster=p;pet.Controlled=true;pet.Summoned=true;actor=pet;}
        try
        {
            if(kind=="weapon"){GearEffects.Strike(p,t,0,new Random(71),wearWeapon:false,preparedDamage:new(10000,0,0,0,0));}
            else if(kind=="suppressed"){using(CombatItemEvents.Suppress(p,t)){SkillCombat.Hit(p,t,10000,"magic");}}
            else{SkillCombat.Hit(actor,t,10000,"magic");}
            Require(!t.Alive&&t.LastKiller==actor&&ClassLoot.Killer(t)==p,"Actual kill/direct-versus-owner credit");bool gain=rewards&&kind is not("pet" or "suppressed");
            Require(SkillHealth.Balance256(p)==Math.Min(SkillHealth.Maximum256(p),hp+(gain?life*256:0))&&NecromancerEffects.ManaBalance256(p)==Math.Min(p.ManaMax*256,mana+(gain?mp*256:0)),"Actual "+kind+" kill restored wrong fractional resources");
            if(t.Corpse is Corpse corpse){hp=SkillHealth.Balance256(p);mana=NecromancerEffects.ManaBalance256(p);Require(!RewardLife.Process(t,corpse)&&hp==SkillHealth.Balance256(p)&&mana==NecromancerEffects.ManaBalance256(p),"Duplicate reward restored Siphon twice");corpse.Delete();}
        }
        finally{pet?.Delete();t.Delete();}
    }
    private static void Consumers()
    {
        var p=Fresh();var t=Enemy(p);try
        {
            ResourceVectors(p);int before=NecromancerEffects.ManaBalance256(p);Require(HexSiphon.CastPrepared(p)&&before-NecromancerEffects.ManaBalance256(p)==3200,"Siphon fractional12.5mana payment");var b=HexSiphon.Buff(p);
            Require(Math.Abs((b.UntilUtc-TimedEffects.Now).TotalSeconds-144)<0.2&&HexSiphon.AttackPercent(p)==20&&ClassStats.AttackRating(p)==ClassStats.PlayerAttackPercent(Math.Max(1,ClassStats.PlayerAttackBase(p)),20),"Siphon duration/AR consumer");
            Require(!HexSiphon.ApplyPrepared(p,t,b,false,TimedEffects.Now)&&HexSiphon.Debuff(t)==null,"Ineligible Siphon applied");Require(HexSiphon.ApplyPrepared(p,t,b,true,TimedEffects.Now)&&NecromancerEffects.DamageStatPercent(t)==-33,"Supplied target damagepercent");
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p));int hp=SkillHealth.Balance256(p);AOS.Damage(p,t,100,false,100,0,0,0,0,weaponDamage:true);Require(hp-SkillHealth.Balance256(p)==67*256,"Native weapon damage missed Siphon penalty");
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p));hp=SkillHealth.Balance256(p);SkillCombat.Hit(t,p,100,"physical");Require(hp-SkillHealth.Balance256(p)==100*256,"Siphon incorrectly scaled a physical spell");
            NecromancerEffects.Profiles.Store(t,new(Curse:new(CombatAbility.Weaken,p.Serial.Value,1,TimedEffects.Now.AddMinutes(1),t.X,t.Y)));Require(HexSiphon.Debuff(t)!=null&&NecromancerEffects.DamageStatPercent(t)==-66,"Siphon did not add to ordinary curse weapon ED");NecromancerEffects.Profiles.Clear(t);
            foreach(string kind in new[]{"weapon","spell","pet","suppressed"}){KillCase(p,kind);}KillCase(p,"spell",false);
            var wall=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Lidless Wall").Id,99,new Random(173),true);var blade=CombatTiming.Equipped(p);var wand=NamedGear.Create("unique/299",99,new Random(173),true);Equip(p,wall);Equip(p,wand);
            Require(ClassGear.Bonus(p,"mana-kill")>0&&ClassGear.Bonus(p,"heal-kill")>0,"Actual item kill-bonus fixtures");KillCase(p,"spell");
            // A final sub-point below max must cap exactly,clearing fractions.
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-1);NecromancerEffects.SetMana256(p,p.ManaMax*256-1);var cap=Enemy(p,true);cap.SetHits(1);cap.Hits=1;SkillCombat.Hit(p,cap,10000,"magic");Require(SkillHealth.Balance256(p)==SkillHealth.Maximum256(p)&&NecromancerEffects.ManaBalance256(p)==p.ManaMax*256,"Kill restoration exceeded/came short of exact maximum");cap.Corpse?.Delete();cap.Delete();p.AddToBackpack(wall);p.AddToBackpack(wand);wall.Delete();wand.Delete();ClassGear.Refresh(p);Equip(p,blade);
            var w=CombatTiming.Equipped(p);p.AddToBackpack(w);ClassGear.Refresh(p);Require(HexSiphon.Buff(p)==b&&HexSiphon.AttackPercent(p)==0&&HexSiphon.AfterKill(p)==(0,0)&&!HexSiphon.CastPrepared(p),"Siphon unarmed condition");Equip(p,w);
            NecromancerEffects.SetMana256(p,3199);Require(!HexSiphon.CastPrepared(p)&&HexSiphon.Buff(p)==b,"Low mana replaced Siphon");Mana(p);Equip(p,new Bow());Require(HexSiphon.AttackPercent(p)==20&&HexSiphon.AfterKill(p)==(1,1),"Siphon excluded bow");
            Require(!SkillBindings.Instance.Assign(p,false,CombatAbility.HexSiphon)&&!SkillBindings.Instance.AssignQuick(p,0,CombatAbility.HexSiphon),"Incomplete Siphon input accepted");
            try{HexBane.CastPrepared(p);throw new InvalidOperationException("Unverified cross-hex buff replacement accepted");}catch(NotSupportedException){}Require(HexSiphon.Buff(p)==b&&HexBane.Buff(p)==null,"Rejected cross-hex changed state");
            HexSiphon.Clear(p);Mana(p);Require(HexBane.CastPrepared(p),"Bane cross-hex control");var bane=HexBane.Buff(p);try{HexSiphon.CastPrepared(p);throw new InvalidOperationException("Reverse cross-hex accepted");}catch(NotSupportedException){}Require(HexBane.Buff(p)==bane&&HexSiphon.Buff(p)==null,"Reverse rejection changed state");
            try{HexBane.ApplyPrepared(p,t,bane,true,TimedEffects.Now);throw new InvalidOperationException("Unverified cross-hex target replacement accepted");}catch(NotSupportedException){}HexSiphon.Clear(t);Require(HexBane.ApplyPrepared(p,t,bane,true,TimedEffects.Now),"Bane target control");
            try{HexSiphon.ApplyPrepared(p,t,b,true,TimedEffects.Now);throw new InvalidOperationException("Reverse cross-hex target accepted");}catch(NotSupportedException){}HexBane.Clear(t);HexBane.Clear(p);
            Mana(p);Require(HexSiphon.CastPrepared(p)&&HexSiphon.ApplyPrepared(p,t,HexSiphon.Buff(p),true,TimedEffects.Now),"Siphon expiry control");var held=HexSiphon.Snapshot();HexSiphon.Restore(new(held.Buffs.Select(x=>x.Owner==p.Serial.Value?x with{UntilUtc=TimedEffects.Now.AddMilliseconds(-1)}:x).ToArray(),held.Debuffs.Select(x=>x.Target==t.Serial.Value?x with{UntilUtc=TimedEffects.Now.AddMilliseconds(-1)}:x).ToArray()));Require(HexSiphon.Buff(p)==null&&HexSiphon.AfterKill(p)==(0,0)&&HexSiphon.Debuff(t)==null&&NecromancerEffects.DamageStatPercent(t)==0,"Expired Siphon still active");HexSiphon.Prune();
        }
        finally{p.Delete();t.Delete();HexSiphon.Prune();HexBane.Prune();}
    }
    private static void Lifecycle()
    {
        var p=Fresh();HexSiphon.CastPrepared(p);p.Kill();p.Resurrect();Require(HexSiphon.Buff(p)==null,"Death retained Siphon");p.Corpse?.Delete();p.Delete();
        p=Fresh();HexSiphon.CastPrepared(p);ClassCreation.Profiles.TryGet(p.Serial,out var c);ClassCreation.Profiles.ResetAllocatedPoints(p,c);Require(HexSiphon.Buff(p)==null,"Respec retained Siphon");p.Delete();
        p=Fresh();HexSiphon.CastPrepared(p);p.AddToBackpack(CombatTiming.Equipped(p));ClassGear.Refresh(p);Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Necromancer)&&!HexSiphon.Snapshot().Buffs.Any(x=>x.Owner==p.Serial.Value),"History retained Siphon");p.Delete();
    }
    private static void Check()
    {
        Model();Consumers();Lifecycle();var saved=new Saved[2];int i=0;
        foreach(bool female in new[]{false,true})
        {
            var p=Fresh(female);Train(p,"warlock.hexsiphon",20);Train(p,"warlock.engorge");Train(p,"warlock.eldritchblast");var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Equip(p,helm);Mana(p);Require(HexSiphon.CastPrepared(p)&&HexSiphon.Buff(p) is{Rank:21,Values.LifeAfterKill:22,Values.ManaAfterKill:22,Values.DebuffFrames:155},"Siphon effective rank/hard synergies");var b=HexSiphon.Buff(p);
            p.AddToBackpack(helm);ClassGear.Refresh(p);Mana(p);int mp=NecromancerEffects.ManaBalance256(p);Require(!HexSiphon.CastPrepared(p)&&HexSiphon.Buff(p)==b&&mp==NecromancerEffects.ManaBalance256(p),"Lower rank replaced Siphon");Equip(p,helm);Mana(p);Require(HexSiphon.CastPrepared(p),"Equal rank Siphon refresh");b=HexSiphon.Buff(p);Train(p,"warlock.engorge",2);Mana(p);Require(HexSiphon.CastPrepared(p)&&HexSiphon.Buff(p).Values.LifeAfterKill==23&&b.Values.LifeAfterKill==22,"Equal-rank Engorge rewrite/capture independence");
            b=HexSiphon.Buff(p);var t=Enemy(p);Require(HexSiphon.ApplyPrepared(p,t,b,true,TimedEffects.Now),"Saved Siphon target");var held=HexSiphon.Snapshot();var until=TimedEffects.Now+FixtureSpace.Retention;HexSiphon.Restore(new(held.Buffs.Select(x=>x.Owner==p.Serial.Value?x with{UntilUtc=until}:x).ToArray(),held.Debuffs.Select(x=>x.Target==t.Serial.Value?x with{UntilUtc=until}:x).ToArray()));b=HexSiphon.Buff(p);var d=HexSiphon.Debuff(t);held=HexSiphon.Snapshot();
            try{HexSiphon.Restore(new([b with{Values=b.Values with{ManaAfterKill=999}}],[]));throw new InvalidOperationException("Malformed Siphon accepted");}catch(InvalidDataException){}Require(HexSiphon.Snapshot().Buffs.SequenceEqual(held.Buffs)&&HexSiphon.Snapshot().Debuffs.SequenceEqual(held.Debuffs),"Malformed Siphon mutated live state");
            saved[i++]=new(p.Serial.Value,CombatTiming.Equipped(p).Serial.Value,helm.Serial.Value,t.Serial.Value,female,b,d);
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload()
    {
        Model();foreach(var s in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(ShockWaveTarget)World.FindMobile((Serial)s.Target);Require(p.Female==s.Female&&World.FindItem((Serial)s.Weapon).Parent==p&&World.FindItem((Serial)s.Helm).Parent==p&&HexSiphon.Buff(p)==s.Buff&&HexSiphon.Debuff(t)==s.Debuff,"Saved Siphon actor/gear/capture changed");Require(HexSiphon.AttackPercent(p)==200&&HexSiphon.AfterKill(p)==(23,23)&&NecromancerEffects.DamageStatPercent(t)==-33,"Reload lost Siphon native consumers");KillCase(p,"spell");KillCase(p,"pet");}
    }
}
