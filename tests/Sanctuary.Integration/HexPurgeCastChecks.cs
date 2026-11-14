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

public static class HexPurgeCastChecks
{
    private const CombatAbility Ability=CombatAbility.HexPurge;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(HexPurgeCastChecks));
    private sealed record Row(uint Owner,uint Target,uint Weapon,bool Female,int X,int Y,int Life,int Mana,int TargetLife,HexPurgeBuff Buff,HexPurgeTarget Mark);
    private sealed record Saved(int Generation,Row[] Rows);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"purge-cast-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=p=>p.Name!="PurgeCastSaved"&&hp?.Invoke(p)!=false;
        ClassGearHooks.CanRegenerateMana=p=>p.Name!="PurgeCastSaved"&&mp?.Invoke(p)!=false;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Approved learned Purge and automatic shared events",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        foreach(bool again in new[]{false,true}){bool reload=again;string name=reload?"purge-cast-reload-check":"purge-cast-check";Register(name,()=>Core.LoopContext.Post(()=>FixtureNetwork.Connected(name+"-result",n=>
        {try{Check(n,reload);Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}})));}
        foreach(string choice in new[]{"right","quick"}){string mode=choice;Register("purge-cast-"+mode+"-check",()=>FixtureNetwork.Connected("purge-cast-"+mode+"-result",n=>Input(n,mode)));}
    }
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;}
    private static PlayerMobile Fresh(bool female,int column=6560)
    {
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.Female=female;p.Name="PurgeCastFixture";p.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(p,99);
        ClassSkillChecks.Train(p,"warlock.hexpurge",20);ClassSkillChecks.Train(p,"warlock.cleave");ClassSkillChecks.Train(p,"warlock.hexsiphon");
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Purge strength");}
        FixtureSpace.Corridor(p,column,24,3500);var w=new Broadsword();p.AddToBackpack(w);Require(p.EquipItem(w),"Purge sword");ClassGear.Refresh(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);Ready(p);return p;
    }
    private static CleaveActionTarget Enemy(PlayerMobile p,int dx)
    {
        var t=new CleaveActionTarget{NoKillAwards=true,Frozen=true,CantWalk=true};t.MoveToWorld(new(p.X+dx,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();t.SetHits(50000);SkillHealth.SetBalance256(t,t.HitsMax*256);
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy,ResistanceType.Poison}){t.SetResistance(type,0);}return t;
    }
    private static ulong Seed(int wanted)
    {for(int n=0;n<10000;n++){ulong s=SourceRandom.InitLow(n),copy=s;if(SourceRandom.Limited(ref copy,100)==wanted){return s;}}throw new InvalidOperationException("Purge seed");}
    private static void Cast(PlayerMobile p)
    {
        Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,Ability).Mana256;
        Require(ClassCombat.Perform(p,Ability,null,new(-20,-20,0),Core.TickCount),"Purge self start");var s=ClassCombat.For(p);
        Require(s.PendingTarget==Serial.Zero&&s.PendingPoint==p.Location&&s.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p))&&NecromancerEffects.ManaBalance256(p)==mana,"Purge self SC/unpaid windup");
        Require(ClassCombat.ResolveAction(p,s.EffectAt)&&!ClassCombat.ResolveAction(p,s.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana-cost&&HexPurge.Buff(p)?.Rank==20,"Purge one table debit");Ready(p);
    }
    private static void Casting(PlayerMobile p)
    {
        Require(HexPurgeReactionProposal.Approved&&WarlockCombat.Available(p,Ability)&&ClassAbilities.SelfTarget(Ability)&&!SkillBindings.Instance.Assign(p,true,Ability)
            &&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Purge availability/metadata/bindings");
        int mana=NecromancerEffects.ManaBalance256(p);Require(ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Purge cancel start");var s=ClassCombat.For(p);long at=s.EffectAt;
        Require(!ClassCombat.ResolveAction(p,at-1)&&HexPurge.Buff(p)==null&&NecromancerEffects.ManaBalance256(p)==mana,"Purge early payment");ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at)&&HexPurge.Buff(p)==null,"Purge canceled release");
        Cast(p);var old=HexPurge.Buff(p);Require(ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Purge late mana start");at=s.EffectAt;NecromancerEffects.SetMana256(p,SkillMechanics.Values(p,Ability).Mana256-1);
        Require(!ClassCombat.ResolveAction(p,at)&&HexPurge.Buff(p)==old,"Purge late mana replaced buff");Ready(p);NecromancerEffects.SetMana256(p,mana);
        Require(ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Purge move start");at=s.EffectAt;var position=p.Location;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);
        Require(!ClassCombat.ResolveAction(p,at)&&HexPurge.Buff(p)==old,"Purge moving windup released");p.MoveToWorld(position,p.Map);Ready(p);
        var weapon=CombatTiming.Equipped(p);p.AddToBackpack(weapon);Require(!ClassCombat.Perform(p,Ability,null,p.Location,Core.TickCount),"Unarmed Purge accepted");Require(p.EquipItem(weapon),"Purge restore weapon");ClassGear.Refresh(p);Cast(p);
    }
    private static void Events(PlayerMobile p,CleaveActionTarget target)
    {
        var near=Enemy(p,3);var hit=CleaveActionProposal.HitRoll;var block=SourceBlocking.BlockRoll;var seed=HexPurgeEvents.InitialSeed;
        int seedCalls=0,packets=0,packet256=0;HexPurgeEvents.InitialSeed=()=>{seedCalls++;return Seed(0);};CleaveActionProposal.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;
        void Observe(SkillDamageContact c){if(c.Target==target){packets++;packet256=c.Damage256;}}
        SkillDamageEvents.BeforeDirectDamage+=Observe;
        try
        {
            HexPurge.ClearDebuff(target);int hp=SkillHealth.Balance256(target),other=SkillHealth.Balance256(near);Ready(p);
            var a=CleaveActionProposal.BeginPrepared(p,target.Serial,target.Location,Core.TickCount);Require(a!=null,"Purge Cleave start");CleaveActionProposal.Advance(a,a.EffectAt);ClassCombat.Stop(p);
            Require(a.Fault==null&&a.Successful==1&&packets==1&&packet256>0&&hp-SkillHealth.Balance256(target)>packet256&&SkillHealth.Balance256(near)<other
                &&HexPurge.Debuff(target)?.Reaction.RemainingCharges==2&&seedCalls==1,"Automatic Cleave applying-hit reaction/order/one charge");
            var mark=HexPurge.Debuff(target);Require(mark.Plan.Rank==HexPurge.Buff(p).Rank&&mark.Reaction.Modifiers==DruidFirestorm.Modifiers(p)&&mark.Reaction.MagicMastery==NamedElements.MasteryPercent(p,"mag"),"Purge attachment capture");
            // Zero-life weapon contact refreshes without refilling or drawing a seed.
            Require(WeaponLandedEvents.Notify(p,target,false)&&HexPurge.Debuff(target).Reaction==mark.Reaction&&seedCalls==1,"Purge refresh changed snapshot/stream/charges");
            HexPurge.ClearDebuff(target);CleaveActionProposal.HitRoll=()=>1;Ready(p);a=CleaveActionProposal.BeginPrepared(p,target.Serial,target.Location,Core.TickCount);CleaveActionProposal.Advance(a,a.EffectAt);ClassCombat.Stop(p);
            Require(a.Successful==0&&HexPurge.Debuff(target)==null,"Missed Cleave attached Purge");
            var prepared=PlayerWeaponContact.PrepareOrdinary(p,target,0,0,()=>true,()=>0,()=>0,_=>0,_=>0,new Random(4));
            foreach(int flags in new[]{0,16,128,256,32768}){PlayerWeaponContact.Release(p,target,prepared with{Flags=flags},TimedEffects.Now);}
            Require(HexPurge.Debuff(target)==null,"Miss/block/dodge attached Purge");
            var zero=prepared with{Impact=prepared.Impact with{Parts=new(0,0,0,0,0),LifeDamage256=0,Absorbed256=0,AllocatedPoison=null}};
            PlayerWeaponContact.Release(p,target,zero,TimedEffects.Now);Require(HexPurge.Debuff(target)?.Reaction.RemainingCharges==3,"Fully absorbed landed hit spent a charge");
            HexPurge.ClearDebuff(target);using(CombatItemEvents.Suppress(p,target)){PlayerWeaponContact.Release(p,target,zero,TimedEffects.Now);}PlayerWeaponContact.Release(p,target,zero with{Flags=zero.Flags|32},TimedEffects.Now);
            Require(HexPurge.Debuff(target)==null,"Suppressed/flag32 contact attached Purge");
            SkillCombat.ResolveHitParts(p,target,new(1,0,0,0,0),false,spell:true,enhanceBase:false,addWeaponElements:false);Require(HexPurge.Debuff(target)==null,"Spell attached Purge");
            SkillCombat.ResolveHitParts(p,target,new(1,0,0,0,0),false,weapon:true,ranged:true,enhanceBase:false,addWeaponElements:false);Require(HexPurge.Debuff(target)?.Reaction.RemainingCharges==2,"Ranged weapon failed applying-hit reaction");
            HexPurge.ClearDebuff(target);Require(!HexPurgeEvents.QualifiedHexTrigger(p,target,false)&&HexPurgeEvents.QualifiedHexTrigger(p,target,true),"Purge explicit trigger qualification");
            mark=HexPurge.Debuff(target);int before=SkillHealth.Balance256(target);SkillHealth.Damage(p,target,129,SkillDamageKind.Periodic);Require(HexPurge.Debuff(target)==mark&&before-SkillHealth.Balance256(target)==129,"Periodic packet triggered Purge");
            // A direct native NPC armor-ignore hit uses the same pre-life bridge.
            var attacker=Enemy(p,10);try{before=SkillHealth.Balance256(target);AOS.Damage(target,attacker,1,true,100,0,0,0,0);Require(HexPurge.Debuff(target)?.Reaction.RemainingCharges==2&&before-SkillHealth.Balance256(target)>256,"Native armor-ignore incoming bypassed Purge");}finally{attacker.Delete();}
            mark=HexPurge.Debuff(target);var network=p.NetState;network.Mobile=null;p.NetState=null;SkillHealth.Damage(near,target,1);Require(HexPurge.Debuff(target)==mark,"Offline Purge reacted");network.Mobile=p;p.NetState=network;
            var position=p.Location;p.MoveToWorld(position,Map.Felucca);SkillHealth.Damage(near,target,1);Require(HexPurge.Debuff(target)==mark,"Cross-facet Purge reacted");p.MoveToWorld(position,Map.Trammel);
            Require(HexSiphon.CastPrepared(p,replaceGroup:true)&&HexPurge.Buff(p)==null&&HexPurge.Debuff(target)==mark,"Buff group swap erased recipient mark");Cast(p);
        }
        finally{SkillDamageEvents.BeforeDirectDamage-=Observe;HexPurgeEvents.InitialSeed=seed;CleaveActionProposal.HitRoll=hit;SourceBlocking.BlockRoll=block;ClassCombat.Stop(p);near.Delete();}
        Log.Information("Purge production:applying Cleave/ranged hit,one nonrecursive burst,miss/block/zero/suppression/spell exclusions,capture/refresh,qualified trigger,native incoming,offline/facet and independent groups pass.");
    }
    private static void Check(NetState network,bool reload)
    {
        var saved=reload?JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)):new Saved(0,new Row[2]);var rows=new Row[2];
        for(int i=0;i<2;i++)
        {
            var old=reload?saved.Rows[i]:null;var p=reload?(PlayerMobile)World.FindMobile((Serial)old.Owner):Fresh(i==1,i==0?6400:6480);
            var target=reload?(CleaveActionTarget)World.FindMobile((Serial)old.Target):Enemy(p,1);
            if(reload)
            {
                Require(p.Female==old.Female&&CombatTiming.Equipped(p)?.Serial.Value==old.Weapon&&HexPurge.Buff(p)==old.Buff&&HexPurge.Debuff(target)==old.Mark
                    &&SkillHealth.Balance256(p)==old.Life&&NecromancerEffects.ManaBalance256(p)==old.Mana&&SkillHealth.Balance256(target)==old.TargetLife,"Original Purge snapshot/stream/spent charges/resources changed");
                FixtureSpace.Place(p,new(old.X,old.Y,0),Map.Trammel);target.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);target.AIObject.AITimer.Stop();
            }
            FixtureNetwork.Authenticate(network,p,"PurgeSaved");
            if(reload)
            {
                ulong next=old.Mark.Reaction.Seed;bool burst=SourceRandom.Limited(ref next,100)<Math.Clamp(old.Mark.Plan.ChanceRaw,0,100);
                if(burst){_=SourceSpellPacketRoll.Range(ref next,HexPurgeReactionProposal.Enhanced(old.Mark.Plan.Minimum256,old.Mark.Reaction.MagicMastery),HexPurgeReactionProposal.Enhanced(old.Mark.Plan.Maximum256,old.Mark.Reaction.MagicMastery));}
                SkillHealth.Damage(p,target,1);var after=HexPurge.Debuff(target);Require(after?.Reaction.Seed==next&&after.Reaction.RemainingCharges==old.Mark.Reaction.RemainingCharges-(burst?1:0),"Original Purge next draw/charge replayed");Cast(p);
            }
            else{Casting(p);}
            Events(p,target);var state=HexPurge.Debuff(target);Require(state?.Reaction.RemainingCharges==2,"Persistence control is not actually spent");
            var until=TimedEffects.Now.Add(FixtureSpace.Retention);HexPurge.StoreReactionPrepared(state with{UntilUtc=until});var all=HexPurge.Snapshot();HexPurge.Restore(all with{Buffs=all.Buffs.Select(b=>b.Owner==p.Serial.Value?b with{UntilUtc=until}:b).ToArray()});
            p.Name="PurgeCastSaved";SkillHealth.SetBalance256(p,p.HitsMax*256-333);NecromancerEffects.SetMana256(p,p.ManaMax*256-517);SkillHealth.SetBalance256(target,target.HitsMax*256-129);
            target.Combatant=null;target.MoveToWorld(target.Location,Map.Internal);p.Combatant=null;Ready(p);
            rows[i]=new(p.Serial.Value,target.Serial.Value,CombatTiming.Equipped(p).Serial.Value,p.Female,p.X,p.Y,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(target),HexPurge.Buff(p),HexPurge.Debuff(target));network.Mobile=null;p.NetState=null;
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(saved.Generation+(reload?1:0),rows)));
    }
    private static void Input(NetState network,string mode)
    {
        var p=Fresh(mode=="quick");Require(SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Purge wire bindings");var account=FixtureNetwork.Authenticate(network,p,"PurgeWire");
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,Ability).Mana256;bool began=false,done=false;long deadline=Core.TickCount+6000;string label="purge-cast-"+mode;
        void Cleanup(){ClassCombat.Stop(p);HexPurge.Clear(p);p.Delete();account.Delete();}
        void Inspect()
        {
            if(done){return;}try
            {
                if(Core.TickCount>=deadline){throw new InvalidOperationException("Purge wire timeout");}var s=ClassCombat.For(p);
                if(!began&&s.PendingAbility==Ability){Require(s.PendingTarget==Serial.Zero&&s.PendingPoint==p.Location&&NecromancerEffects.ManaBalance256(p)==mana,"Purge wire unpaid self context");began=true;Log.Information("{Label}: windup",label);}
                if(HexPurge.Buff(p)==null){Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));return;}
                Require(began&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Purge wire one debit");var t=Enemy(p,1);try{Require(WeaponLandedEvents.Notify(p,t,false)&&HexPurge.Debuff(t)?.Reaction.RemainingCharges==3,"Wire-created Purge did not attach");}finally{HexPurge.Clear(t);t.Delete();}
                done=true;Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target=0 x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Warlock,p.X,p.Y,p.Z);Inspect();}));
    }
}
