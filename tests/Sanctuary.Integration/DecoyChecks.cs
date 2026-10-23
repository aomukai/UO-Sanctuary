// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Spells;

namespace Sanctuary.Integration;

public static class DecoyChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DecoyChecks));
    private sealed record Saved(uint Owner,uint Pet,uint Helm,Point3D Origin,Point3D Point,DecoyStats Stats,DateTime Until,int Health,int Mana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"decoy-checks.json");
    private static bool _creating;
    private static int? _loadedHealth;
    private static PlayerMobile _connected;
    public static void Configure(){Rates();Register("decoy-check",Check);Register("decoy-reload-check",Reload);Register("decoy-disconnect-check",Disconnect);}
    [CallPriority(int.MaxValue)] public static void Initialize()
    {
        Rates();if(File.Exists(Manifest)){var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));if(World.FindMobile((Serial)s.Pet) is AmazonDecoy pet){_loadedHealth=SkillHealth.Balance256(pet);}}
    }
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=a=>_creating || a.Name?.StartsWith("DecoyCast",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):hp?.Invoke(a)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=a=>_creating || a.Name?.StartsWith("DecoyCast",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):mp?.Invoke(a)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Decoy paid casting, NPC damage, resources and owner lifecycle",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{try{action();}catch(Exception e){Log.Error(e,"decoy-result: failed");}});
    private static PlayerMobile Fresh(DiabloClass cls=DiabloClass.Amazon)
    {
        PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x191,Female=true,Name="DecoyCastOwner",Player=true};}finally{_creating=false;}
        owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,cls);ClassCreation.Profiles.AdvanceLevel(owner,99);owner.MoveToWorld(new(2200,1000,0),Map.Trammel);bool found=false;
        for(int x=2400;x<2900 && !found;x+=4)for(int y=1000;y<1450 && !found;y+=4)
        {
            int z=Map.Trammel.GetAverageZ(x,y);var p=new Point3D(x,y,z);
            if(!Enumerable.Range(0,19).All(i=>Map.Trammel.GetAverageZ(x+i,y)==z && Map.Trammel.CanSpawnMobile(x+i,y,z))
                || !Map.Trammel.LineOfSight(new Point3D(x,y,z+12),new Point3D(x+18,y,z+12))){continue;}
            bool occupied=false;foreach(var other in Map.Trammel.GetMobilesInRange<Mobile>(p,22)){if(other!=owner){occupied=true;break;}}if(occupied){continue;}
            owner.MoveToWorld(p,Map.Trammel);if(Enumerable.Range(0,19).Any(i=>SpellHelper.IsTown(new Point3D(x+i,y,z),owner))){continue;}found=true;
        }
        Require(found,"No independent Decoy cast corridor");Log.Information("Decoy cast fixture corridor: {Point}",owner.Location);Mana(owner);return owner;
    }
    private static Point3D Point(PlayerMobile owner,int x=3)=>new(owner.X+x,owner.Y,owner.Z);
    private static void Train(PlayerMobile owner,CombatAbility ability)
    {
        var row=SkillMechanics.For(ability);if(ClassCreation.Profiles.GetRank(owner.Serial,row.TalentId)>0){return;}
        Require(TalentCatalog.TryGet(row.CharacterClass,row.TalentId,out var talent),"Talent "+row.TalentId);
        foreach(var prerequisite in talent.Prerequisites){Train(owner,SkillMechanics.All.Single(x=>x.TalentId==prerequisite).Ability);}
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,row.TalentId,out var why),"Train "+why);
    }
    private static void Mana(PlayerMobile owner)=>NecromancerEffects.SetMana256(owner,Math.Min(owner.ManaMax*256,50*256+17));
    private static void Ready(PlayerMobile owner){ClassCombat.Stop(owner);var s=ClassCombat.For(owner);s.NextAction=s.RecoveryUntil=Core.TickCount;}
    private static AmazonDecoy Cast(PlayerMobile owner,Point3D point,bool held=false)
    {
        Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner),cost=SkillMechanics.Values(owner,CombatAbility.Decoy).Mana256;
        Require(held?ClassCombat.ContinueCast(owner,CombatAbility.Decoy,null,point,Core.TickCount):ClassCombat.Perform(owner,CombatAbility.Decoy,null,point,Core.TickCount),"Decoy cast input");
        var s=ClassCombat.For(owner);Require(s.PendingPoint==point && s.PendingTarget==Serial.Zero && s.Animation==16 && NecromancerEffects.ManaBalance256(owner)==mana,"Ground SC/start payment");
        ClassCombat.ReleaseCast(owner);Require(!ClassCombat.ResolveAction(owner,s.EffectAt-1) && ClassCombat.ResolveAction(owner,s.EffectAt) && !ClassCombat.ResolveAction(owner,s.EffectAt),"Single SC do event");
        var pet=AmazonDecoys.For(owner).Single();Require(pet.Location==point && NecromancerEffects.ManaBalance256(owner)==mana-cost,"Once-only release mana/point");return pet;
    }
    private static Item Helm(PlayerMobile owner)
    {
        var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Require(owner.EquipItem(item),"Actual Tarnhelm");ClassGear.Refresh(owner);return item;
    }
    private static void Damage(PlayerMobile owner,AmazonDecoy pet)
    {
        pet.Pause();var enemy=new GraveyardZombie{CantWalk=true,Frozen=true,NoKillAwards=true};enemy.MoveToWorld(Point(owner,8),owner.Map);enemy.AIObject.AITimer.Stop();
        try
        {
            enemy.NextReacquireTime=Core.TickCount;
            Require(enemy.AIObject.AcquireFocusMob(12,FightMode.Closest,false,false,true) && enemy.FocusMob==pet,"Native enemy AI did not select the nearer Decoy");
            SkillHealth.SetBalance256(pet,SkillHealth.Maximum256(pet));int before=SkillHealth.Balance256(pet);
            AOS.Damage(pet,enemy,1,false,0,100,0,0,0);AOS.Damage(pet,enemy,1,false,0,100,0,0,0);
            Require(SkillHealth.Balance256(pet)==before-490,"Real NPC AOS subpoint fire packets lost fractional damage");
            before=SkillHealth.Balance256(pet);AOS.Damage(pet,enemy,1,false,50,50,0,0,0,weaponDamage:true);
            Require(SkillHealth.Balance256(pet)==before-250,"Real NPC mixed physical/fire weapon packet");
            before=SkillHealth.Balance256(pet);AOS.Damage(pet,owner,10,false,100,0,0,0,0,weaponDamage:true);
            Require(SkillHealth.Balance256(pet)==before,"Friendly native AOS damaged Decoy");
        }
        finally{enemy.Delete();pet.Start();}
    }
    private static void Buffs(PlayerMobile owner,AmazonDecoy pet)
    {
        var supplier=Fresh(DiabloClass.Barbarian);Train(supplier,CombatAbility.BattleOrders);pet.Pause();
        try
        {
            SkillHealth.SetBalance256(pet,pet.Snapshot.Life256-529);int oldMax=SkillHealth.Maximum256(pet),oldLife=SkillHealth.Balance256(pet);
            BattleOrders.Apply(supplier,pet,1,TimedEffects.Now);int max=SkillHealth.Maximum256(pet);
            Require(max==oldMax+(int)(oldMax*.35) && SkillHealth.Balance256(pet)==BattleOrders.Rescale256(oldLife,oldMax,max),"Decoy BO used rounded display maximum for proportional life");
            Require(SkillHealth.Balance256(pet)!=BattleOrders.Rescale256(oldLife,(oldMax+255)/256*256,(max+255)/256*256),"Fractional BO fixture failed to distinguish rounded maxima");
            int regenerationBefore=SkillHealth.Balance256(pet);pet.Start();pet.Pause();pet.Advance(TimedEffects.Now,Core.TickCount+40);
            Require(SkillHealth.Balance256(pet)==regenerationBefore+((15*(max>>8))>>4),"BO regeneration retained creation-time maximum");
            int injured=SkillHealth.Balance256(pet);BattleOrders.Apply(supplier,pet,1,TimedEffects.Now);Require(SkillHealth.Balance256(pet)==injured,"Equal BO healed Decoy");
            BattleOrders.Clear(pet);Require(SkillHealth.Maximum256(pet)==oldMax && SkillHealth.Balance256(pet)==BattleOrders.Rescale256(injured,max,oldMax),"BO removal used rounded maxima");
            int life=SkillHealth.Balance256(pet);BattleCommand.Apply(supplier,pet,1,TimedEffects.Now);Require(pet.Snapshot.Rank==1 && SkillHealth.Balance256(pet)==life,"Recipient Battle Command rewrote Decoy snapshot");BattleCommand.Clear(pet);
            BattleCommand.Apply(supplier,owner,1,TimedEffects.Now);Require(pet.Snapshot.Rank==1 && SkillMechanics.Rank(owner,CombatAbility.Decoy)==2,"Caster BC retroactively changed existing Decoy");BattleCommand.Clear(owner);
        }
        finally{BattleOrders.Clear(pet);BattleCommand.Clear(pet);BattleCommand.Clear(owner);supplier.Delete();pet.Start();}
    }
    private static void Cases(PlayerMobile owner)
    {
        Require(!ClassCombat.Available(owner,CombatAbility.Decoy),"Unlearned Decoy available");Train(owner,CombatAbility.Decoy);
        Require(!ClassAbilities.SelfTarget(CombatAbility.Decoy) && !SkillBindings.Instance.Assign(owner,true,CombatAbility.Decoy)
            && SkillBindings.Instance.Assign(owner,false,CombatAbility.Decoy) && SkillBindings.Instance.AssignQuick(owner,3,CombatAbility.Decoy),"Decoy right/quick/ground metadata");
        var pet=Cast(owner,Point(owner));Damage(owner,pet);Buffs(owner,pet);
        Ready(owner);NecromancerEffects.SetMana256(owner,19*256-1);int before=NecromancerEffects.ManaBalance256(owner);
        Require(!ClassCombat.Perform(owner,CombatAbility.Decoy,null,Point(owner,5),Core.TickCount) && !pet.Deleted && NecromancerEffects.ManaBalance256(owner)==before,"Low mana created/replaced/debited Decoy");
        Mana(owner);Ready(owner);before=NecromancerEffects.ManaBalance256(owner);Require(ClassCombat.Perform(owner,CombatAbility.Decoy,null,Point(owner,5),Core.TickCount),"Cancellation begin");long effect=ClassCombat.For(owner).EffectAt;ClassCombat.Stop(owner);
        Require(!ClassCombat.ResolveAction(owner,effect) && NecromancerEffects.ManaBalance256(owner)==before && !pet.Deleted,"Cancelled cast charged or replaced");
        Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.Decoy,null,Point(owner,5),Core.TickCount),"Mana loss begin");effect=ClassCombat.For(owner).EffectAt;NecromancerEffects.SetMana256(owner,0);
        Require(!ClassCombat.ResolveAction(owner,effect) && !pet.Deleted,"Unsupported mana-loss release created a free Decoy");
        Mana(owner);Ready(owner);var origin=owner.Location;Require(ClassCombat.Perform(owner,CombatAbility.Decoy,null,Point(owner,5),Core.TickCount),"Movement cancellation begin");effect=ClassCombat.For(owner).EffectAt;owner.MoveToWorld(Point(owner,1),owner.Map);
        Require(!ClassCombat.ResolveAction(owner,effect) && !pet.Deleted,"Moved SC cast completed");owner.MoveToWorld(origin,owner.Map);
        Ready(owner);before=NecromancerEffects.ManaBalance256(owner);var blocked=new Static(0x80);
        try
        {
            Require(ClassCombat.Perform(owner,CombatAbility.Decoy,null,Point(owner,5),Core.TickCount),"Late obstacle begin");effect=ClassCombat.For(owner).EffectAt;blocked.MoveToWorld(Point(owner,5),owner.Map);
            Require(!ClassCombat.ResolveAction(owner,effect) && !pet.Deleted && NecromancerEffects.ManaBalance256(owner)==before,"Failed release placement charged/replaced");
            Ready(owner);Require(!ClassCombat.Perform(owner,CombatAbility.Decoy,null,blocked.Location,Core.TickCount),"Unsupported blocked-ground fallback invented");
        }
        finally{blocked.Delete();}
        Ready(owner);Require(!ClassCombat.Perform(owner,CombatAbility.Decoy,null,new(owner.X+17,owner.Y,owner.Z),Core.TickCount),"Shared ground range exceeded");
        var helm=Helm(owner);try{Mana(owner);var next=Cast(owner,Point(owner,5),true);Require(pet.Deleted && next.Snapshot.Rank==2 && SkillMechanics.Values(owner,CombatAbility.Decoy).Mana256==4672,"Soft-rank paid replacement");pet=next;owner.AddToBackpack(helm);ClassGear.Refresh(owner);Require(pet.Snapshot.Rank==2,"Removed helm mutated existing pet");}finally{helm.Delete();ClassGear.Refresh(owner);}
        AmazonDecoys.Cancel(owner);Require(pet.Deleted,"Explicit idempotent cancellation");AmazonDecoys.Cancel(owner);
        var other=ClassCreation.Profiles.SelectHistory(owner,DiabloClass.Sorceress);ClassCreation.Profiles.ApplyActive(owner);Ready(owner);
        Require(!ClassCombat.Available(owner,CombatAbility.Decoy),"Wrong-class Decoy available");ClassCreation.Profiles.UndoSelection(owner,other);ClassCreation.Profiles.ApplyActive(owner);Mana(owner);
        pet=Cast(owner,Point(owner));other=ClassCreation.Profiles.SelectHistory(owner,DiabloClass.Sorceress);pet.Advance(TimedEffects.Now,Core.TickCount);Require(pet.Deleted,"Class-loss Decoy retained");ClassCreation.Profiles.UndoSelection(owner,other);ClassCreation.Profiles.ApplyActive(owner);Mana(owner);
        pet=Cast(owner,Point(owner));ClassCreation.Profiles.TryGet(owner.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(owner,profile);Require(pet.Deleted && !ClassCombat.Available(owner,CombatAbility.Decoy),"Respec retained summon or skill");Train(owner,CombatAbility.Decoy);Mana(owner);
        pet=Cast(owner,Point(owner));var point=pet.Location;owner.MoveToWorld(Point(owner,1),owner.Map);pet.Advance(TimedEffects.Now,Core.TickCount);Require(!pet.Deleted && pet.Location==point,"Same-map movement made Decoy follow");owner.MoveToWorld(origin,Map.Felucca);pet.Advance(TimedEffects.Now,Core.TickCount);Require(pet.Deleted,"Map-loss Decoy retained");owner.MoveToWorld(origin,Map.Trammel);Mana(owner);
        pet=Cast(owner,Point(owner));owner.Kill();Require(pet.Deleted,"Immediate owner death cleanup retained Decoy");owner.Resurrect();pet.Advance(TimedEffects.Now,Core.TickCount);Require(pet.Deleted,"Immediate resurrection restored Decoy");owner.MoveToWorld(origin,Map.Trammel);Mana(owner);Ready(owner);
        owner.MoveToWorld(new(1428,1695,0),Map.Trammel);Require(SpellHelper.IsTown(owner.Location,owner) && !ClassCombat.Perform(owner,CombatAbility.Decoy,null,Point(owner),Core.TickCount),"Town Decoy accepted");owner.MoveToWorld(origin,Map.Trammel);Mana(owner);Ready(owner);
    }
    private static void Attach(PlayerMobile owner)
    {
        var network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));network.Seeded=true;network.CompressionEnabled=false;network.Mobile=owner;owner.NetState=network;
        var state=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);Require(state!=null,"Protocol fixture bootstrap changed");state.SetValue(network,Enum.Parse(state.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(owner);_connected=owner;
    }
    private static void Persist()
    {
        var owner=Fresh();Train(owner,CombatAbility.Decoy);Require(SkillBindings.Instance.Assign(owner,false,CombatAbility.Decoy) && SkillBindings.Instance.AssignQuick(owner,3,CombatAbility.Decoy),"Saved bindings");var helm=Helm(owner);Mana(owner);var pet=Cast(owner,Point(owner));owner.AddToBackpack(helm);ClassGear.Refresh(owner);pet.HoldUntil(TimedEffects.Now.AddMinutes(10));SkillHealth.Damage(owner,pet,100*256+257);pet.Pause();
        // An unpaid pending windup must not replay after restart.
        Ready(owner);Require(ClassCombat.Perform(owner,CombatAbility.Decoy,null,Point(owner,5),Core.TickCount),"Saved unpaid pending cast");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,pet.Serial.Value,helm.Serial.Value,owner.Location,pet.Location,pet.Snapshot,pet.ExpiresUtc,SkillHealth.Balance256(pet),NecromancerEffects.ManaBalance256(owner))));
    }
    private static void Natural(PlayerMobile owner)
    {
        AmazonDecoys.Cancel(owner);Ready(owner);Mana(owner);int before=NecromancerEffects.ManaBalance256(owner);Attach(owner);var point=Point(owner);long deadline=Core.TickCount+13000;
        // The runner sends a real version7 Cast packet after the ready marker.
        void Wait()
        {
            var pets=AmazonDecoys.For(owner);if(pets.Length==0 && Core.TickCount<deadline){Later(Wait);return;}
            Require(pets.Length==1 && pets[0].Location==point && NecromancerEffects.ManaBalance256(owner)==before-4864,"Natural40ms SC/payment/one entity");
            var pet=pets[0];void Expire()
            {
                if(!pet.Deleted && Core.TickCount<deadline){Require(pet.Location==point,"Natural stationary Decoy moved");Later(Expire);return;}
                Require(pet.Deleted,"Natural cast Decoy did not expire");Mana(owner);Cast(owner,Point(owner));Persist();Log.Information("decoy-result: passed");
            }
            Later(Expire);
        }
        Later(()=>{Log.Information("decoy-input: ready ability={Ability} x={X} y={Y} z={Z}",(int)CombatAbility.Decoy,point.X,point.Y,point.Z);Later(Wait);});
    }
    private static void Disconnect()
    {Require(_connected!=null && _connected.NetState==null && AmazonDecoys.For(_connected).Length==0 && (ClassCombat.Existing(_connected) is not{} action || action.PendingAbility==CombatAbility.None && action.HeldAbility==CombatAbility.None),"Actual disconnect retained summon/pending cast");}
    private static void Check(){var owner=Fresh();Cases(owner);Natural(owner);}
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;var pet=World.FindMobile((Serial)saved.Pet) as AmazonDecoy;
        Require(owner!=null && pet!=null && pet.SummonMaster==owner && pet.Snapshot==saved.Stats && pet.ExpiresUtc==saved.Until && pet.Location==saved.Point && _loadedHealth==saved.Health,"Saved paid Decoy identity/snapshot/life/deadline");
        Require(NecromancerEffects.ManaBalance256(owner)==saved.Mana && ClassCombat.For(owner).PendingAbility==CombatAbility.None && SkillBindings.Instance.Get(owner).Right==CombatAbility.Decoy && SkillBindings.Instance.GetQuick(owner.Serial,DiabloClass.Amazon,3)==CombatAbility.Decoy && World.FindItem((Serial)saved.Helm) is Item{Parent:Backpack},"Unpaid action replay or saved gear/bindings/mana loss");
        owner.MoveToWorld(saved.Origin,Map.Trammel);pet.Delete();ClassCreation.Profiles.TryGet(owner.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(owner,profile);Mana(owner);Cases(owner);Natural(owner);
    }
}
