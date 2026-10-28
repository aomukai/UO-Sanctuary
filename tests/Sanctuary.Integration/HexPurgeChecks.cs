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
public static class HexPurgeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(HexPurgeChecks));
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,uint Target,bool Female,HexPurgeBuff Buff,HexPurgeTarget TargetPlan);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"hex-purge-fixtures.json");
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Register(string n,Action a)=>ConsoleInputHandler.RegisterCommand(n,"Prepared Purge skill rate and explicit target plans",_=>Core.LoopContext.Post(()=>{try{a();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));
    public static void Configure(){Register("hex-purge-check",Check);Register("hex-purge-reload-check",Reload);}
    private static int Tier(int rank,int start,int[] steps)
    {int left=rank-1,value=start;for(int i=0;i<5;i++){int n=i==4?left:Math.Min(left,i==0?7:i==1?8:6);value+=n*steps[i];left-=n;}return value;}
    private static SkillValues Values(int rank,int bane=0,int blast=0,int sigil=0)=>new(SkillMechanics.For(CombatAbility.HexPurge),k=>k=="Hex Purge"?rank:255,k=>k=="Hex Bane"?bane:k=="Eldritch Blast"?blast:k=="Sigil Death"?sigil:0,99);
    private static void Model()
    {
        int count=0;for(int rank=1;rank<=255;rank++)foreach(int bane in new[]{0,1,20})foreach(int blast in new[]{0,1,20})foreach(int sigil in new[]{0,1,20})
        {
            var v=Values(rank,bane,blast,sigil);var buff=HexPurge.Capture(v);var plan=HexPurge.Plan(v);int min=Tier(rank,10,[4,8,10,14,18])*256,max=Tier(rank,15,[6,10,12,16,20])*256;
            Require(buff.AttackPercent==20+9*(rank-1)&&buff.AttackRate==Math.Min(30,10+rank-1)&&buff.BuffFrames==3600+300*(rank-1)&&buff.Mana256==(25+rank-1)*256,"Purge buff source values");
            Require(plan.Minimum256==min+min*10*(bane+blast)/100&&plan.Maximum256==max+max*10*(bane+blast)/100&&plan.DebuffFrames==150+5*blast&&plan.ChanceRaw==(6900+50*(rank-1)+100*sigil)/100&&plan.InitialCharges==Math.Min(1+rank/10,3)&&plan.RadiusRaw==4,"Purge independent raw suppliers/unused radius growth");count++;
        }
        Require(count==6885,"Purge source matrix");foreach(var (rank,chance,charges,rate) in new[]{(1,69,1,10),(2,69,1,11),(3,70,1,12),(10,73,2,19),(20,78,3,29),(21,79,3,30),(255,196,3,30)})
        {Require(HexPurge.Plan(Values(rank)) is{} p&&p.ChanceRaw==chance&&p.InitialCharges==charges&&HexPurge.Capture(Values(rank)).AttackRate==rate,"Purge rank boundary");}
        Require(HexPurge.Plan(Values(255,sigil:20)).ChanceRaw==216&&!WarlockCombat.Implemented(CombatAbility.HexPurge),"Raw chance was silently capped or incomplete handler enabled");
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"hex-purge-source-probe.json")));int cases=0;
        foreach(var row in doc.RootElement.EnumerateArray()){if(row[4].GetInt32()==1){var v=HexPurge.Capture(Values(row[1].GetInt32()));Require(row[7].GetInt32()==v.AttackRate&&row[8].GetInt32()==v.AttackRate&&row[9].GetInt32()==v.AttackPercent,"Native passive/source writer values");}cases++;}Require(cases==192,"Purge source contexts missing");
        Log.Information("Hex Purge:6885current supplier controls,192source buff contexts;raw chance/charges/radius remain separate from modern event36/39");
    }
    private static void Train(PlayerMobile p,string id,int rank=1)
    {Require(TalentCatalog.TryGet(DiabloClass.Warlock,id,out var t),"Purge talent "+id);foreach(var pre in t.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,pre)==0){Train(p,pre);}}while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Purge train "+id);}}
    private static void Mana(PlayerMobile p)=>NecromancerEffects.SetMana256(p,p.ManaMax*256);
    private static PlayerMobile Fresh(bool female=false)
    {
        var p=new PlayerMobile{Female=female,Body=female?0x191:0x190,Player=true,Name="HexPurgeFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);FixtureSpace.Place(p,new(6100,1580,0),Map.Trammel);
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Purge Strength");}while(p.Dex<50){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Purge Dexterity");}Train(p,"warlock.hexpurge");Equip(p,new Broadsword());Mana(p);return p;
    }
    private static void Equip(PlayerMobile p,Item item)
    {if(item is BaseWeapon&&CombatTiming.Equipped(p) is{} old){p.AddToBackpack(old);}p.AddToBackpack(item);Require(p.EquipItem(item),"Purge equip "+item);ClassGear.Refresh(p);}
    private static ShockWaveTarget Enemy(PlayerMobile p)
    {var t=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(5000);SkillHealth.SetBalance256(t,t.HitsMax*256);t.SetResistance(ResistanceType.Physical,0);return t;}
    private static void Timing(PlayerMobile p)
    {
        var b=HexPurge.Buff(p);int ias=CombatTiming.Ias(p),fcr=CombatTiming.Fcr(p),slow=GearEffects.AttackSlow(p),rate=PaladinOffense.AttackRate(p)+AssassinBuffs.AttackRate(p)+b.Values.AttackRate;var w=CombatTiming.Weapon(CombatTiming.Equipped(p));
        Require(CombatTiming.SkillAttackRate(p)==rate&&CombatTiming.For(p,CombatAbility.BasicAttack)==CombatTiming.Calculate(DiabloClass.Warlock,CombatAbility.BasicAttack,w,ias,fcr,slow,rate)&&CombatTiming.WeaponSequenceAdvance(p)==CombatTiming.FrameAdvance(w,ias,slow,rate),"Purge missing from direct/sequence skill-rate pool");
        var cast=CombatTiming.For(p,CombatAbility.HexPurge);int walk=p.ComputeMovementSpeed(Direction.East,false),run=p.ComputeMovementSpeed(Direction.East|Direction.Running,false);var held=HexPurge.Snapshot();var fast=CombatTiming.For(p,CombatAbility.BasicAttack);HexPurge.Clear(p);
        Require(CombatTiming.For(p,CombatAbility.BasicAttack).Frames>fast.Frames&&CombatTiming.For(p,CombatAbility.HexPurge)==cast&&p.ComputeMovementSpeed(Direction.East,false)==walk&&p.ComputeMovementSpeed(Direction.East|Direction.Running,false)==run,"Purge speed placement/SC or movement contamination");HexPurge.Restore(held);
    }
    private static void Consumers()
    {
        var p=Fresh();var t=Enemy(p);try
        {
            int mana=NecromancerEffects.ManaBalance256(p),magic=NamedElements.To256(GearEffects.Damage(p,0).Magic);Require(HexPurge.CastPrepared(p)&&mana-NecromancerEffects.ManaBalance256(p)==25*256&&HexPurge.AttackRate(p)==10&&HexPurge.AttackPercent(p)==20,"Prepared Purge payment/passive values");var b=HexPurge.Buff(p);
            Require(Math.Abs((b.UntilUtc-TimedEffects.Now).TotalSeconds-144)<0.2&&ClassStats.AttackRating(p)==ClassStats.PlayerAttackPercent(Math.Max(1,ClassStats.PlayerAttackBase(p)),20)&&NamedElements.To256(GearEffects.Damage(p,0).Magic)==magic,"Purge duration/AR or invented weapon magic");Timing(p);
            int hp=SkillHealth.Balance256(t);AOS.Damage(t,p,10,false,100,0,0,0,0,weaponDamage:true);Require(hp-SkillHealth.Balance256(t)==10*256&&HexPurge.Debuff(t)==null,"Purge invented automatic application/explosion");
            var plan=HexPurge.Plan(p);mana=NecromancerEffects.ManaBalance256(p);hp=SkillHealth.Balance256(t);Require(!HexPurge.ApplyPrepared(p,t,b,plan,false,TimedEffects.Now)&&HexPurge.ApplyPrepared(p,t,b,plan,true,TimedEffects.Now),"Explicit eligible Purge plan");var marked=HexPurge.Debuff(t);Require(marked.Plan==plan&&hp==SkillHealth.Balance256(t)&&mana==NecromancerEffects.ManaBalance256(p),"Storing a plan spent resources/detonated");
            try{HexPurge.ApplyPrepared(p,t,b,plan,true,TimedEffects.Now);throw new InvalidOperationException("Unverified Purge charge reset accepted");}catch(NotSupportedException){}Require(HexPurge.Debuff(t)==marked,"Rejected reapplication changed plan");
            AOS.Damage(t,p,10,false,100,0,0,0,0,weaponDamage:true);Require(hp-SkillHealth.Balance256(t)==10*256&&HexPurge.Debuff(t)==marked,"Ordinary hit consumed/detonated an unsupported plan");
            var weapon=CombatTiming.Equipped(p);p.AddToBackpack(weapon);ClassGear.Refresh(p);Require(HexPurge.Buff(p)==b&&HexPurge.AttackRate(p)==0&&HexPurge.AttackPercent(p)==0&&!HexPurge.CastPrepared(p),"Purge unarmed condition");Equip(p,weapon);NecromancerEffects.SetMana256(p,25*256-1);Require(!HexPurge.CastPrepared(p)&&HexPurge.Buff(p)==b,"Low mana replaced Purge");Mana(p);
            Train(p,"warlock.hexpurge",20);Require(HexPurge.CastPrepared(p)&&HexPurge.AttackRate(p)==29,"Purge rank20rate");
            var armor=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Twitchthroe").Id,99,new Random(173),true);var gloves=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Magefist").Id,99,new Random(173),true);Equip(p,armor);Equip(p,gloves);Require(CombatTiming.Ias(p)>=20&&CombatTiming.Fcr(p)>=20,"Real IAS/FCR fixture");Timing(p);
            var wt=CombatTiming.Weapon(CombatTiming.Equipped(p));Require(CombatTiming.WeaponSequenceAdvance(p)!=CombatTiming.FrameAdvance(wt,CombatTiming.Ias(p)+29,GearEffects.AttackSlow(p),0),"Purge treated as itemIAS");
            foreach(BaseWeapon w in new BaseWeapon[]{new Bow(),new NamedThrowingKnife(),new Halberd()}){Equip(p,w);Timing(p);}Equip(p,weapon);p.AddToBackpack(armor);p.AddToBackpack(gloves);armor.Delete();gloves.Delete();ClassGear.Refresh(p);
            Require(!SkillBindings.Instance.Assign(p,false,CombatAbility.HexPurge)&&!SkillBindings.Instance.AssignQuick(p,0,CombatAbility.HexPurge),"Incomplete Purge input enabled");
            var held=HexPurge.Snapshot();HexPurge.Restore(new(held.Buffs.Select(x=>x.Owner==p.Serial.Value?x with{UntilUtc=TimedEffects.Now.AddMilliseconds(-1)}:x).ToArray(),held.Targets.Select(x=>x.Target==t.Serial.Value?x with{UntilUtc=TimedEffects.Now.AddMilliseconds(-1)}:x).ToArray()));Require(HexPurge.Buff(p)==null&&HexPurge.Debuff(t)==null&&HexPurge.AttackRate(p)==0,"Expired Purge retained contribution/plan");HexPurge.Prune();
        }
        finally{p.Delete();t.Delete();HexPurge.Prune();}
    }
    private static void Clear(Mobile p){HexPurge.Clear(p);HexBane.Clear(p);HexSiphon.Clear(p);}
    private static bool Cast(PlayerMobile p,CombatAbility a){Mana(p);return a==CombatAbility.HexBane?HexBane.CastPrepared(p):a==CombatAbility.HexSiphon?HexSiphon.CastPrepared(p):HexPurge.CastPrepared(p);}
    private static bool Apply(PlayerMobile p,Mobile t,CombatAbility a)=>a==CombatAbility.HexBane?HexBane.ApplyPrepared(p,(BaseCreature)t,HexBane.Buff(p),true,TimedEffects.Now):a==CombatAbility.HexSiphon?HexSiphon.ApplyPrepared(p,(BaseCreature)t,HexSiphon.Buff(p),true,TimedEffects.Now):HexPurge.ApplyPrepared(p,(BaseCreature)t,HexPurge.Buff(p),HexPurge.Plan(p),true,TimedEffects.Now);
    private static void Refused(Action a){try{a();throw new InvalidOperationException("Unverified cross-hex replacement accepted");}catch(NotSupportedException){}}
    private static void CrossGroups()
    {
        var p=Fresh();var other=Fresh();Train(p,"warlock.hexsiphon");Train(other,"warlock.hexsiphon");var t=Enemy(p);try
        {
            foreach(var a in new[]{CombatAbility.HexBane,CombatAbility.HexSiphon})
            {
                Clear(p);Clear(other);Clear(t);Require(Cast(p,CombatAbility.HexPurge)&&Apply(p,t,CombatAbility.HexPurge)&&Cast(other,a),"Cross-hex Purge setup");var b=HexPurge.Buff(p);var target=HexPurge.Debuff(t);int mp=NecromancerEffects.ManaBalance256(p);Refused(()=>{if(a==CombatAbility.HexBane){HexBane.CastPrepared(p);}else{HexSiphon.CastPrepared(p);}});Refused(()=>Apply(other,t,a));Require(HexPurge.Buff(p)==b&&HexPurge.Debuff(t)==target&&NecromancerEffects.ManaBalance256(p)==mp,"Rejected Purge replacement changed state/mana");
                Clear(p);Clear(other);Clear(t);Require(Cast(p,a)&&Apply(p,t,a)&&Cast(other,CombatAbility.HexPurge),"Reverse cross-hex setup");mp=NecromancerEffects.ManaBalance256(p);Refused(()=>HexPurge.CastPrepared(p));Refused(()=>Apply(other,t,CombatAbility.HexPurge));Require(HexPurge.Buff(p)==null&&HexPurge.Debuff(t)==null&&NecromancerEffects.ManaBalance256(p)==mp,"Reverse rejection changed Purge/mana");
            }
        }
        finally{p.Delete();other.Delete();t.Delete();HexPurge.Prune();HexBane.Prune();HexSiphon.Prune();}
    }
    private static void Lifecycle()
    {
        var p=Fresh();HexPurge.CastPrepared(p);p.Kill();p.Resurrect();Require(HexPurge.Buff(p)==null,"Death retained Purge");p.Corpse?.Delete();p.Delete();p=Fresh();HexPurge.CastPrepared(p);ClassCreation.Profiles.TryGet(p.Serial,out var c);ClassCreation.Profiles.ResetAllocatedPoints(p,c);Require(HexPurge.Buff(p)==null,"Respec retained Purge");p.Delete();p=Fresh();HexPurge.CastPrepared(p);p.AddToBackpack(CombatTiming.Equipped(p));ClassGear.Refresh(p);Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Necromancer)&&!HexPurge.Snapshot().Buffs.Any(x=>x.Owner==p.Serial.Value),"History retained Purge");p.Delete();
    }
    private static void Check()
    {
        Model();Consumers();CrossGroups();Lifecycle();var saved=new Saved[2];int i=0;
        foreach(bool female in new[]{false,true})
        {
            var p=Fresh(female);Train(p,"warlock.hexpurge",20);Train(p,"warlock.eldritchblast");Train(p,"warlock.sigildeath");var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Equip(p,helm);Mana(p);Require(HexPurge.CastPrepared(p)&&HexPurge.Buff(p) is{Rank:21,Values.AttackRate:30,Values.AttackPercent:200},"Purge effective-rank capture");var b=HexPurge.Buff(p);var t=Enemy(p);var plan=HexPurge.Plan(p);Require(plan is{Rank:21,ChanceRaw:80,InitialCharges:3,RadiusRaw:4,DebuffFrames:155,Minimum256:46694,Maximum256:60518}&&HexPurge.ApplyPrepared(p,t,b,plan,true,TimedEffects.Now),"Explicit saved Purge suppliers");
            Train(p,"warlock.sigildeath",2);Require(HexPurge.Plan(p).ChanceRaw==81&&HexPurge.Debuff(t).Plan==plan,"Supplied plan changed with future hard ranks");p.AddToBackpack(helm);ClassGear.Refresh(p);Mana(p);int mp=NecromancerEffects.ManaBalance256(p);Require(!HexPurge.CastPrepared(p)&&HexPurge.Buff(p)==b&&NecromancerEffects.ManaBalance256(p)==mp,"Lower-rank Purge replaced/paid");Equip(p,helm);Mana(p);Require(HexPurge.CastPrepared(p),"Equal-rank Purge recast");
            var held=HexPurge.Snapshot();var until=TimedEffects.Now+FixtureSpace.Retention;HexPurge.Restore(new(held.Buffs.Select(x=>x.Owner==p.Serial.Value?x with{UntilUtc=until}:x).ToArray(),held.Targets.Select(x=>x.Target==t.Serial.Value?x with{UntilUtc=until}:x).ToArray()));b=HexPurge.Buff(p);var target=HexPurge.Debuff(t);held=HexPurge.Snapshot();
            try{HexPurge.Restore(new([b],[target with{Plan=plan with{InitialCharges=0}}]));throw new InvalidOperationException("Malformed Purge plan accepted");}catch(InvalidDataException){}Require(HexPurge.Snapshot().Buffs.SequenceEqual(held.Buffs)&&HexPurge.Snapshot().Targets.SequenceEqual(held.Targets),"Malformed Purge restore mutated state");saved[i++]=new(p.Serial.Value,CombatTiming.Equipped(p).Serial.Value,helm.Serial.Value,t.Serial.Value,female,b,target);
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload()
    {
        Model();foreach(var s in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var t=(ShockWaveTarget)World.FindMobile((Serial)s.Target);Require(p.Female==s.Female&&World.FindItem((Serial)s.Weapon).Parent==p&&World.FindItem((Serial)s.Helm).Parent==p&&HexPurge.Buff(p)==s.Buff&&HexPurge.Debuff(t)==s.TargetPlan,"Saved Purge actor/gear/payload changed");Require(HexPurge.Plan(p).ChanceRaw==81&&s.TargetPlan.Plan.ChanceRaw==80&&HexPurge.AttackRate(p)==30,"Saved plan/live supplier distinction");Timing(p);}
    }
}
