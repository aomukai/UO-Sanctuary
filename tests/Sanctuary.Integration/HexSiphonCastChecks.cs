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
public static class HexSiphonCastChecks
{
    private const CombatAbility Ability=CombatAbility.HexSiphon;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(HexSiphonCastChecks));
    private sealed record Saved(uint Owner,uint Target,uint Weapon,bool Female,int Life,int Mana,HexSiphonBuff Buff,HexSiphonDebuff Debuff);
    private sealed record Manifest(int Generation,Saved[] Rows);
    private static string FileName=>Path.Join(Core.BaseDirectory,"siphon-cast-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Learned Siphon and automatic weapon Hex events",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("siphon-cast-check",()=>Check(false));Register("siphon-cast-reload-check",()=>Check(true));
        foreach(string choice in new[]{"right","quick"}){string mode=choice;Register("siphon-cast-"+mode+"-check",()=>FixtureNetwork.Connected("siphon-cast-"+mode+"-result",n=>Input(n,mode)));}
    }
    public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mana=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=p=>p.Name!="SiphonCastSaved"&&hp?.Invoke(p)!=false;
        ClassGearHooks.CanRegenerateMana=p=>p.Name!="SiphonCastSaved"&&mana?.Invoke(p)!=false;
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;}
    private static PlayerMobile Fresh(bool female,int x)
    {
        var p=new PlayerMobile{Player=true,Female=female,Body=female?0x191:0x190,Name="SiphonCastFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"warlock.hexsiphon");
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Siphon strength");}FixtureSpace.Corridor(p,x,24,3500);
        var sword=new Broadsword();p.AddToBackpack(sword);Require(p.EquipItem(sword),"Siphon sword");ClassGear.Refresh(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);Ready(p);return p;
    }
    private static ShockWaveTarget Enemy(PlayerMobile p,bool rewards=false)
    {var t=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=!rewards};t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(5000);SkillHealth.SetBalance256(t,t.HitsMax*256);t.SetResistance(ResistanceType.Physical,0);t.SetResistance(ResistanceType.Fire,0);return t;}
    private static void Cast(PlayerMobile p)
    {
        Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);Require(ClassCombat.Perform(p,Ability,null,new(-20,-20,0),Core.TickCount),"Siphon learned start");var s=ClassCombat.For(p);
        Require(s.PendingTarget==Serial.Zero&&s.PendingPoint==p.Location&&s.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p)),"Siphon self SC context");
        int cost=SkillMechanics.Values(p,Ability).Mana256;Require(ClassCombat.ResolveAction(p,s.EffectAt)&&!ClassCombat.ResolveAction(p,s.EffectAt)&&NecromancerEffects.ManaBalance256(p)==p.ManaMax*256-cost,"Siphon one release/debit");Ready(p);
    }
    private static void Casting(PlayerMobile p)
    {
        Require(WeaponHexGroups.Approved&&ClassAbilities.SelfTarget(Ability)&&ClassCombat.Available(p,Ability)&&!SkillBindings.Instance.Assign(p,true,Ability)
            &&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Siphon approved/right/quick/self metadata");
        int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Siphon unpaid start");var s=ClassCombat.For(p);long effect=s.EffectAt;
        Require(!ClassCombat.ResolveAction(p,effect-1)&&HexSiphon.Buff(p)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Siphon early debit/effect");ClassCombat.Stop(p);
        Require(!ClassCombat.ResolveAction(p,effect)&&HexSiphon.Buff(p)==null,"Siphon Stop cast");Cast(p);var old=HexSiphon.Buff(p);
        Ready(p);Require(ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Siphon late-mana start");effect=s.EffectAt;NecromancerEffects.SetMana256(p,SkillMechanics.Values(p,Ability).Mana256-1);
        Require(!ClassCombat.ResolveAction(p,effect)&&HexSiphon.Buff(p)==old,"Siphon late mana replaced buff");
        Ready(p);NecromancerEffects.SetMana256(p,mana);Require(ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Siphon move start");effect=s.EffectAt;var origin=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);
        Require(!ClassCombat.ResolveAction(p,effect)&&HexSiphon.Buff(p)==old,"Siphon moved cast released");p.MoveToWorld(origin,p.Map);Ready(p);
        var sword=CombatTiming.Equipped(p);p.AddToBackpack(sword);Require(!ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Unarmed Siphon cast accepted");Require(p.EquipItem(sword),"Siphon sword restore");
        // Actual +all-skills gear captures rank2;removing it must not rewrite registration.
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Tarnhelm").Id,99,new Random(173),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Siphon rank helm");ClassGear.Refresh(p);Cast(p);
        var captured=HexSiphon.Buff(p);Require(captured.Rank==2,"Siphon effective rank");p.AddToBackpack(helm);ClassGear.Refresh(p);helm.Delete();
        Require(SkillMechanics.Rank(p,Ability)==1&&HexSiphon.Buff(p)==captured&&HexSiphon.AttackPercent(p)==29,"Siphon registration rewritten by gear");
        Log.Information("Siphon learned:self SC/right/quick,early/Stop/movement/late-mana/unarmed controls,one fractional table payment and effective-rank registration snapshot pass.");
    }
    private static void Events(PlayerMobile p,ShockWaveTarget t)
    {
        var saved=HexSiphon.Buff(p);Require(saved!=null,"Siphon event registration missing");var sword=CombatTiming.Equipped(p);var bow=new Bow();p.AddToBackpack(bow);
        var prepared=ClassCombatHooks.PreparedWeaponDamage;int before=0;bool observed=false;
        try
        {
            HexSiphon.ClearDebuff(t);int hp=SkillHealth.Balance256(t);sword.OnMiss(p,t);
            Require(SkillHealth.Balance256(t)==hp&&HexSiphon.Debuff(t)?.Rank==saved.Rank,"Actual native melee miss did not Hex");
            HexSiphon.ClearDebuff(t);using(PaladinThorns.Attempt(p,t,false)){Require(ClassStats.CheckHit(p,t,0,()=>1)==false,"Siphon forced miss");}
            Require(HexSiphon.Debuff(t)?.Rank==saved.Rank&&SkillHealth.Balance256(t)==hp,"Class melee miss did not Hex");
            var origin=t.Location;t.MoveToWorld(new(p.X+12,p.Y,p.Z),p.Map);HexSiphon.ClearDebuff(t);sword.OnMiss(p,t);Require(HexSiphon.Debuff(t)==null,"Out-of-reach melee applied Siphon");t.MoveToWorld(origin,p.Map);
            p.AddToBackpack(sword);Require(p.EquipItem(bow),"Siphon bow");ClassGear.Refresh(p);bow.OnMiss(p,t);Require(HexSiphon.Debuff(t)==null,"Ranged miss applied Siphon");
            ClassCombatHooks.PreparedWeaponDamage=(a,b,amount,ranged)=>{prepared(a,b,amount,ranged);if(a==p&&b==t&&ranged){observed=HexSiphon.Debuff(t)!=null&&SkillHealth.Balance256(t)==before;}};
            foreach(var c in new[]{(Physical:100,Weapon:true,Immune:false,Apply:true),(Physical:0,Weapon:true,Immune:false,Apply:true),(Physical:100,Weapon:true,Immune:true,Apply:true),(Physical:100,Weapon:false,Immune:false,Apply:false)})
            {
                HexSiphon.ClearDebuff(t);before=SkillHealth.Balance256(t);observed=false;t.SetResistance(ResistanceType.Physical,c.Immune?100:0);
                AOS.Damage(t,p,3,false,c.Physical,100-c.Physical,0,0,0,archer:true,weaponDamage:c.Weapon,spellDamage:!c.Weapon);
                Require((HexSiphon.Debuff(t)!=null)==c.Apply&&(!c.Apply||observed),"Siphon native ranged event/order/exclusions");
                if(c.Immune){Require(SkillHealth.Balance256(t)==before,"Siphon fully mitigated arrow damaged target");}
            }
            t.SetResistance(ResistanceType.Physical,0);HexSiphon.ClearDebuff(t);
            Require(!HexSiphonEvents.QualifiedHexTrigger(p,t,false)&&HexSiphon.Debuff(t)==null&&HexSiphonEvents.QualifiedHexTrigger(p,t,true),"Siphon qualified trigger boundary");
            // Hex is a separate noncurse -33STAT_DAMAGEPERCENT component.
            SkillHealth.SetBalance256(p,p.HitsMax*256);int health=SkillHealth.Balance256(p);AOS.Damage(p,t,20,false,100,0,0,0,0,weaponDamage:true);
            Require(health-SkillHealth.Balance256(p)==NamedElements.To256(20*.67),"Siphon did not reduce native outgoing weapon damage");
            SkillHealth.SetBalance256(p,p.HitsMax*256);health=SkillHealth.Balance256(p);AOS.Damage(p,t,20,false,100,0,0,0,0,spellDamage:true);
            Require(health-SkillHealth.Balance256(p)==5120,"Siphon reduced an unrelated physical spell");
            p.AddToBackpack(bow);Require(p.EquipItem(sword),"Siphon melee restore");ClassGear.Refresh(p);
        }
        finally{ClassCombatHooks.PreparedWeaponDamage=prepared;if(bow.Parent==p){p.AddToBackpack(bow);}bow.Delete();if(sword.Parent!=p){Require(p.EquipItem(sword),"Siphon original weapon final restore");}ClassGear.Refresh(p);}
        Log.Information("Siphon events:native/class melee misses,ranged miss refusal,4native ranged packets including full mitigation,pre-HP application,qualified trigger and native -33weapon-only damage pass.");
    }
    private static void Kill(PlayerMobile p)
    {
        var t=Enemy(p,true);t.SetHits(1);t.Hits=1;SkillHealth.SetBalance256(p,p.HitsMax*256-1000-73);NecromancerEffects.SetMana256(p,p.ManaMax*256-1000-91);
        int hp=SkillHealth.Balance256(p),mana=NecromancerEffects.ManaBalance256(p);var gain=HexSiphon.AfterKill(p);
        try{SkillCombat.Hit(p,t,10000,"magic");Require(!t.Alive&&t.LastKiller==p&&SkillHealth.Balance256(p)==hp+gain.Life*256&&NecromancerEffects.ManaBalance256(p)==mana+gain.Mana*256,"Siphon actual direct-kill returns");}
        finally{t.Corpse?.Delete();t.Delete();}
    }
    private static void Check(bool reload)
    {
        Manifest manifest;
        if(!reload)
        {
            var rows=new Saved[2];int i=0;
            foreach(bool female in new[]{false,true})
            {
                var p=Fresh(female,female?6880:6800);Casting(p);var t=Enemy(p);Events(p,t);Kill(p);var snapshot=HexSiphon.Snapshot();var until=TimedEffects.Now.Add(FixtureSpace.Retention);
                HexSiphon.Restore(new(snapshot.Buffs.Select(b=>b.Owner==p.Serial.Value?b with{UntilUtc=until}:b).ToArray(),snapshot.Debuffs.Select(d=>d.Target==t.Serial.Value?d with{UntilUtc=until}:d).ToArray()));p.Name="SiphonCastSaved";
                rows[i++]=new(p.Serial.Value,t.Serial.Value,CombatTiming.Equipped(p).Serial.Value,female,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),HexSiphon.Buff(p),HexSiphon.Debuff(t));
            }
            manifest=new(0,rows);
        }
        else
        {
            var old=JsonSerializer.Deserialize<Manifest>(File.ReadAllText(FileName));Require(old.Generation is 0 or 1,"Siphon reload generation");
            foreach(var row in old.Rows)
            {
                var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var t=(ShockWaveTarget)World.FindMobile((Serial)row.Target);
                Require(p.Female==row.Female&&CombatTiming.Equipped(p)?.Serial.Value==row.Weapon&&HexSiphon.Buff(p)==row.Buff&&HexSiphon.Debuff(t)==row.Debuff
                    &&SkillHealth.Balance256(p)==row.Life&&NecromancerEffects.ManaBalance256(p)==row.Mana&&ClassCombat.For(p).PendingAbility==CombatAbility.None,"Siphon original saved state/resources/gear");
                FixtureSpace.Corridor(p,row.Female?6880:6800,24,3500);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);Events(p,t);Kill(p);
                var snapshot=HexSiphon.Snapshot();HexSiphon.Restore(new(snapshot.Buffs.Select(b=>b.Owner==row.Owner?row.Buff:b).ToArray(),snapshot.Debuffs.Select(d=>d.Target==row.Target?row.Debuff:d).ToArray()));SkillHealth.SetBalance256(p,row.Life);NecromancerEffects.SetMana256(p,row.Mana);Ready(p);
            }
            manifest=old with{Generation=old.Generation+1};
        }
        File.WriteAllText(FileName,JsonSerializer.Serialize(manifest));Log.Information("Siphon original:Warlock/Witch registered rank2 buff,target state,original weapon and fractional life/mana survive two reloads;automatic events/direct kill returns resume.");
    }
    private static void Input(NetState network,string mode)
    {
        var p=Fresh(mode=="quick",7000);Require(SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Siphon wire bindings");var account=FixtureNetwork.Authenticate(network,p,"SiphonWire");Ready(p);
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,Ability).Mana256;bool began=false,done=false;long deadline=Core.TickCount+6000;string label="siphon-cast-"+mode;
        void Cleanup(){ClassCombat.Stop(p);HexSiphon.Clear(p);p.Delete();account.Delete();}
        void Inspect()
        {
            if(done){return;}try
            {
                if(Core.TickCount>=deadline){throw new InvalidOperationException("Siphon wire timeout");}var s=ClassCombat.For(p);
                if(!began&&s.PendingAbility==Ability){Require(s.PendingTarget==Serial.Zero&&s.PendingPoint==p.Location&&NecromancerEffects.ManaBalance256(p)==mana,"Siphon wire self/unpaid context");began=true;Log.Information("{Label}: windup",label);}
                if(HexSiphon.Buff(p)==null){Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Siphon wire one debit");var t=Enemy(p);
                try{CombatTiming.Equipped(p).OnMiss(p,t);Require(HexSiphon.Debuff(t)!=null,"Wire-created Siphon did not apply on native miss");}finally{HexSiphon.Clear(t);t.Delete();}
                done=true;Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Warlock,0,p.X,p.Y,p.Z);Inspect();}));
    }
}
