// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DemonicMasteryChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DemonicMasteryChecks));
    private const CombatAbility Ability=CombatAbility.DemonicMastery;
    private sealed record Pet(uint Serial,string Birth,int Life,NpcEquipmentSeed Random);
    private sealed record Owner(uint Serial,uint Helm,bool Female,Pet[] Pets);
    private sealed record Saved(int Generation,Owner[] Owners);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"demonic-mastery-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Birth(SkeletonGuardian b)=>JsonSerializer.Serialize(b switch
    {WarlockGoatmanBody g=>(object)g.Prepared.Snapshot,WarlockTaintedBody t=>t.Prepared.Snapshot,WarlockDefilerBody d=>d.Prepared.Snapshot,_=>throw new NotSupportedException()});
    private static Pet Capture(SkeletonGuardian b)=>new(b.Serial.Value,Birth(b),SkillHealth.Balance256(b),NpcEquipmentRandom.State(b));
    private static int Curve(int rank)=>rank<=0?0:5+34*Math.Min(100,110*rank/(rank+6))/100;
    private static void Consumers(SkeletonGuardian b)
    {
        int mastery,rank,attack,velocity,hard;
        if(b is WarlockGoatmanBody g)
        {
            var s=g.Prepared.Snapshot;var v=s.Birth.Suppliers;mastery=s.MasteryRank;rank=v.Rank;attack=v.AttackSpeed;velocity=v.VelocityPercent;hard=s.HardMastery;
            Require(v.AttackBonus==140+40*(rank-1)+25*mastery&&g.CombatProfile.AttackRating==s.Birth.ProjectedAttack,"Goatman flat Mastery AR did not reach actual NPC profile");
            Require(v.DamagePercent==10*(rank+mastery)&&((ISourceUnarmedNpc)g).UnarmedPhysical.DamagePercent==v.DamagePercent,"Captured Mastery ED did not reach actual unarmed combat");
            Require(GoatmanNpcBodyActions.BaseAttackRate(g)==100+attack,"Captured IAS did not reach Goatman action clock");
        }
        else if(b is WarlockTaintedBody t)
        {
            var s=t.Prepared.Snapshot;var v=s.Birth.Suppliers;mastery=s.MasteryRank;rank=v.Rank;attack=v.AttackSpeed;velocity=v.VelocityPercent;hard=s.HardMastery;
            Require(v.FireMasteryPercent==10*mastery,"Mastery did not reach Tainted captured fire mastery");
            var raw=TaintedNpcSkills.FireBall(v with{FireMasteryPercent=0});var actual=TaintedNpcSkills.FireBall(v);
            Require(actual.Minimum256==raw.Minimum256+(int)((long)raw.Minimum256*v.FireMasteryPercent/100)
                &&actual.Maximum256==raw.Maximum256+(int)((long)raw.Maximum256*v.FireMasteryPercent/100),"Actual Tainted launch packet lost captured mastery");
            int speed=BalanceCatalog.Read<TaintedCastClock>("tainted-fireball-cast").Single().Speed;
            Require(TaintedFireBallCasting.Timing(t).Speed==SourceAttackRate.Speed(speed,0,100+attack),"Mastery IAS did not reach Tainted A2 clock");
        }
        else if(b is WarlockDefilerBody d)
        {
            var s=d.Prepared.Snapshot;var v=s.Birth.Suppliers;mastery=s.MasteryRank;rank=v.Rank;attack=v.AttackSpeed;velocity=v.VelocityPercent;hard=s.HardMastery;
            Require(DefilerLinkCasting.Timing(d).Speed==128,"Stored Mastery attackrate leaked into Defiler Other-mode S1");
        }
        else{throw new NotSupportedException();}
        int own=b is WarlockDefilerBody?0:Math.Min(10+rank-1,25),bonus=mastery<=0?0:Math.Min(mastery+4,25);
        Require(attack==own+bonus&&velocity==Curve(mastery),"Captured Mastery attack/movement curves differ");
        int basis=b is WarlockGoatmanBody?8:6,animation=b is WarlockGoatmanBody?GoatmanNpcActionProposal.WalkAnimation.Speed:b is WarlockTaintedBody?176:256;
        Require(DemonPathWorld.Speed(b,TimedEffects.Now)==SourceActorMotion.Speed(basis,animation,100+velocity,0),"Captured Mastery did not reach actual source movement consumer");
        int cap=hard>=10?3:hard>=5?2:1;
        Require(b switch{WarlockGoatmanBody x=>x.Prepared.Snapshot.Birth.Suppliers.PetMaximum==cap,
            WarlockTaintedBody x=>x.Prepared.Snapshot.Birth.Suppliers.PetMaximum==cap,WarlockDefilerBody x=>x.Prepared.Snapshot.Birth.Suppliers.PetMaximum==cap,_=>false},"Captured hard-only cap differs");
    }
    private static SkeletonGuardian Cast(PlayerMobile p,CombatAbility ability,int offset)
    {
        ClassCombat.Stop(p);var session=ClassCombat.For(p);session.NextAction=session.RecoveryUntil=Core.TickCount;
        var fire=FireGround.Snapshot();FireGround.Restore(fire with{Cooldowns=fire.Cooldowns.Where(c=>c.Owner!=p.Serial.Value).ToArray()});NecromancerEffects.SetMana256(p,p.ManaMax*256);
        var before=WarlockGoatmanWorld.Demons(p).Select(b=>b.Serial).ToArray();var point=new Point3D(p.X+offset,p.Y,p.Z);
        Require(ClassCombat.Perform(p,ability,null,point,Core.TickCount)&&ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Actual learned summon route refused Mastery scene");
        var b=WarlockGoatmanWorld.Demons(p).Single(x=>!before.Contains(x.Serial));Consumers(b);return b;
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"demonic-mastery-reload-check":"demonic-mastery-check";
            ConsoleInputHandler.RegisterCommand(name,"Existing Mastery consumers,hard caps,real gear,snapshots and original saves",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Check(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));
        }
    }
    private static void Passive(PlayerMobile p)
    {
        Require(WarlockCombat.Implemented(Ability)&&!WarlockCombat.Handles(Ability)&&!ClassCombat.Available(p,Ability)
            &&SkillMechanics.For(Ability)is{SourceId:374,Passive:true,Left:false,Right:false}&&SkillMechanics.Values(p,Ability).Mana256==0,"Mastery became an active/mana-consuming skill");
        int mana=NecromancerEffects.ManaBalance256(p);
        Require(!SkillBindings.Instance.Assign(p,true,Ability)&&!SkillBindings.Instance.Assign(p,false,Ability)&&!SkillBindings.Instance.AssignQuick(p,7,Ability)
            &&!ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==mana,"Passive bound/cast or spent mana");
    }
    private static void Check(NetState network)
    {
        var owners=new System.Collections.Generic.List<Owner>();
        foreach(bool female in new[]{false,true})
        {
            var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.Female=female;p.AddItem(new Backpack());ClassSkillChecks.Train(p,"warlock.demonicmastery",4);
            foreach(string id in new[]{"warlock.summongoatman","warlock.summontainted","warlock.summondefiler"}){ClassSkillChecks.Train(p,id,6);}
            FixtureSpace.Corridor(p,6900,28,female?3600:3500);var account=FixtureNetwork.Authenticate(network,p,"demonic-mastery-");bool kept=false;
            try
            {
                var helm=PoisonDaggerChecks.Named(p,"Tarnhelm");ClassGear.Refresh(p);Passive(p);
                Require(SkillMechanics.Rank(p,Ability)==5&&WarlockGoatmanWorld.Limit(p)==1,"Soft rank5 incorrectly granted second demon");
                var a=Cast(p,CombatAbility.SummonGoatman,2);string first=Birth(a);
                ClassSkillChecks.Train(p,"warlock.demonicmastery",5);Require(WarlockGoatmanWorld.Limit(p)==2,"Hard5 did not grant second demon");
                var b=Cast(p,CombatAbility.SummonTainted,4);string second=Birth(b);Require(!a.Deleted&&Birth(a)==first,"Training mastery rewrote existing birth");
                ClassSkillChecks.Train(p,"warlock.demonicmastery",9);Require(SkillMechanics.Rank(p,Ability)==10&&WarlockGoatmanWorld.Limit(p)==2,"Soft10 incorrectly granted third demon");
                var c=Cast(p,CombatAbility.SummonDefiler,6);Require(a.Deleted&&!b.Deleted&&Birth(b)==second,"Hard9 cap/replacement or captured Mastery changed");
                ClassSkillChecks.Train(p,"warlock.demonicmastery",10);Require(WarlockGoatmanWorld.Limit(p)==3,"Hard10 did not grant third demon");var d=Cast(p,CombatAbility.SummonGoatman,8);
                var before=WarlockGoatmanWorld.Demons(p).ToDictionary(x=>x.Serial,Birth);p.AddToBackpack(helm);ClassGear.Refresh(p);
                Require(SkillMechanics.Rank(p,Ability)==10&&WarlockGoatmanWorld.Limit(p)==3&&WarlockGoatmanWorld.Demons(p).All(x=>Birth(x)==before[x.Serial]),"Gear removal recaptured stats or changed hard cap");
                var e=Cast(p,CombatAbility.SummonTainted,10);Require(b.Deleted&&!c.Deleted&&!d.Deleted&&e is WarlockTaintedBody{Prepared.Snapshot.MasteryRank:10},"New summon did not capture gear removal independently");
                before=WarlockGoatmanWorld.Demons(p).ToDictionary(x=>x.Serial,Birth);Require(p.EquipItem(helm),"Reequip Mastery gear");ClassGear.Refresh(p);
                Require(SkillMechanics.Rank(p,Ability)==11&&WarlockGoatmanWorld.Demons(p).All(x=>Birth(x)==before[x.Serial]),"Reequip rewrote existing birth");
                p.AddToBackpack(helm);ClassGear.Refresh(p);
                foreach(var body in WarlockGoatmanWorld.Demons(p)){Consumers(body);SkillHealth.SetBalance256(body,SkillHealth.Maximum256(body)-513);}
                PersistentMinions.Suspend(p);owners.Add(new(p.Serial.Value,helm.Serial.Value,female,WarlockGoatmanWorld.Demons(p).Select(Capture).ToArray()));kept=true;
            }
            finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){foreach(var b in WarlockGoatmanWorld.Demons(p)){b.Delete();}p.Delete();}}
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,owners.ToArray())));
        Log.Information("Demonic Mastery:Warlock/Witch real learned three-species casts;soft5/10 do not unlock caps,hard5/10 do;own combat/packet/clock/movement consumers;gear/training snapshots and no active binding/mana writer");
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Too many Mastery reloads");
        foreach(var entry in saved.Owners)
        {
            var p=(PlayerMobile)World.FindMobile((Serial)entry.Serial);var helm=World.FindItem((Serial)entry.Helm);
            Require(p.Female==entry.Female&&helm.IsChildOf(p.Backpack)&&SkillMechanics.Rank(p,Ability)==10&&WarlockGoatmanWorld.Limit(p)==3,"Original saved owner/gear/hard cap changed");
            var pets=entry.Pets.Select(x=>(SkeletonGuardian)World.FindMobile((Serial)x.Serial)).ToArray();
            for(int i=0;i<pets.Length;i++){Require(pets[i].Map==Map.Internal&&Birth(pets[i])==entry.Pets[i].Birth&&SkillHealth.Balance256(pets[i])==entry.Pets[i].Life
                &&NpcEquipmentRandom.State(pets[i])==entry.Pets[i].Random&&DemonNativeState.Controller(pets[i])is{Fault:null},"Saved Mastery body recaptured/healed/rerolled");}
            var account=FixtureNetwork.Authenticate(network,p,"demonic-mastery-reload-");
            try
            {
                PersistentMinions.Resume(p);WarlockGoatmanWorld.Tick(TimedEffects.Now);Passive(p);
                for(int i=0;i<pets.Length;i++){Consumers(pets[i]);Require(Birth(pets[i])==entry.Pets[i].Birth&&SkillHealth.Balance256(pets[i])==entry.Pets[i].Life,"Login rewrote/healed captured body");}
                Require(p.EquipItem(helm),"Saved Mastery helm cannot reequip");ClassGear.Refresh(p);Require(SkillMechanics.Rank(p,Ability)==11,"Saved +skill gear stopped supplying Mastery");
                for(int i=0;i<pets.Length;i++){Require(Birth(pets[i])==entry.Pets[i].Birth,"Saved snapshot changed with live gear");}
                p.AddToBackpack(helm);ClassGear.Refresh(p);PersistentMinions.Suspend(p);
            }
            finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
        Log.Information("Demonic Mastery:original Warlock/Witch three-species snapshots,wounds,RNG,controllers and effective/hard ranks retain across actual reload and gear recheck");
    }
}
