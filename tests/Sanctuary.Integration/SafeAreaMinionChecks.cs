// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using Server;
using Server.Collections;
using Server.ContextMenus;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
using Server.Regions;
namespace Sanctuary.Integration;

public static class SafeAreaMinionChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SafeAreaMinionChecks));
    private sealed record Saved(uint Owner,uint Mage,uint Companion,Point3D Origin,Point3D Town,int Map,int Life,int Maximum,int Mana,int Rank,int Mastery,long Order,MageCriticalSeed Seed,int Slots,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"safe-area-minion-fixtures.json");
    public static void Configure()
    {
        Register("safe-area-minion-check",Check);Register("safe-area-minion-reload-check",Reload);
        ConsoleInputHandler.RegisterCommand("safe-area-minion-live","Natural safe-region entry and return",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("safe-area-minion-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Approved safe-area armies/slots/effects/healer service",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static PlayerMobile Owner()=>MagePetBuffChecks.Owner(20,6660,2800);
    private static GuardedRegion Town(PlayerMobile p,Point3D point)
    {var r=new GuardedRegion("ParkFixture"+p.Serial.Value,p.Map,1000,new Rectangle2D(point.X-2,point.Y-2,5,5));r.Register();return r;}
    private static Point3D Destination(PlayerMobile p)=>new(p.X+16,p.Y,p.Z);
    private static void Parked(PlayerMobile p,BaseCreature m)
    {Require(m.Alive&&!m.Deleted&&m.ControlMaster==p&&m.Map==Map.Internal&&MinionSafeAreas.Parked(m)&&m.ControlSlots==0&&m.Combatant==null&&m.ControlTarget==null,"Army not parked with original ownership/released slots");}
    private static void TownMageRefusal(PlayerMobile owner)
    {
        var dead=new GraveyardZombie{NoKillAwards=true};dead.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);dead.Damage(100000,owner);if(dead.Alive){dead.Kill();}
        var corpse=dead.Corpse as Corpse;
        try
        {
            Require(SkeletonPets.Usable(owner,corpse)&&SkillMechanics.For(CombatAbility.RaiseSkeletalMage).Number("InTown")==0,"Town Mage refusal fixture");
            int mana=NecromancerEffects.ManaBalance256(owner),count=NecromancerSkeletons.For(owner,CombatAbility.RaiseSkeletalMage).Count();
            Require(!NecromancerCombat.Release(owner,CombatAbility.RaiseSkeletalMage,corpse.Serial,corpse.Location)
                &&NecromancerEffects.ManaBalance256(owner)==mana&&!corpse.SanctuaryConsumed&&!corpse.Animated
                &&NecromancerSkeletons.For(owner,CombatAbility.RaiseSkeletalMage).Count()==count,"Town Mage cast spent mana,consumed a corpse or admitted a summon");
        }
        finally{corpse?.Delete();dead.Delete();}
    }
    private static void Controls()
    {
        var p=Owner();var m=MagePetBuffChecks.Pet(p,2);var born=MagePetBuffChecks.Pet(p,3,3,1);var origin=p.Location;var destination=Destination(p);var town=Town(p,destination);var walls=new List<Item>();
        var merc=MercenaryCareChecks.Companion(p);Require(MercenaryCare.Register(p,merc,20),"Named companion registration");
        m.ControlSlots=2;p.Followers+=2;PersistentMinions.Profiles.Track(m);int slots=p.Followers;int life=SkillHealth.Maximum256(m)-1025;SkillHealth.SetBalance256(m,life);m.SetMana(100);m.Mana=20;
        var seed=NecromancerMageMissiles.ActorSeed(m);long order=BattleCommand.PetOrder(m);int rank=m.Rank,mastery=m.Mastery;
        try
        {
            DateTime at=TimedEffects.Now;Require(TimedEffects.AddRestoration(m,true,10,1000,at)&&TimedEffects.AddRestoration(m,false,10,1000,at),"Queued restoration fixture");TimedEffects.Chill(m,50,1000,at);
            p.MoveToWorld(destination,p.Map);Parked(p,m);Require(p.Followers==slots-2&&merc.Map!=Map.Internal&&merc.ControlMaster==p&&MercenaryCare.IsCompanion(p,merc),"Parking touched companion or wrong slot count");
            // Mages are cast outside town,as required by the pinned table.
            // Entering safety parks both existing and newly created pets.
            Parked(p,born);TownMageRefusal(p);born.Delete();
            p.MoveToWorld(new(destination.X+1,destination.Y,destination.Z),p.Map);PersistentMinions.Resume(p);Parked(p,m);Require(p.Followers==slots-2,"Repeated entry released/restored slots twice");
            TimedEffects.Settle(m,at.AddMilliseconds(1001));Require(SkillHealth.Balance256(m)==life&&m.Mana==20&&TimedEffects.Chill(m,at.AddMilliseconds(1001))==0,"Parking healed/refilled or froze status/restoration time");
            NpcEquipmentRegeneration.Apply(m,256);Require(SkillHealth.Balance256(m)==life,"Parked direct regeneration paid");
            Require(MinionSafeAreas.RecoveryRate(m,20)==0&&MinionSafeAreas.RecoveryRate(m,-20)==-20,"Signed recovery mask changed harmful rate");
            TimedEffects.Clear(m);
            var holder=NpcEquipmentRegenerationChecks.Pet(p,NpcEquipmentRegenerationChecks.Named("unique/134"));
            try
            {
                MinionSafeAreas.Enforce(holder);Parked(p,holder);Require(NpcEquipmentRegeneration.ItemRate(holder)==20,"Actual replenish original");
                foreach(bool wound in new[]{false,true})
                {
                    TimedEffects.Clear(holder);int before=SkillHealth.Maximum256(holder)-1024;SkillHealth.SetBalance256(holder,before);DateTime edge=TimedEffects.Now;
                    Require(TimedEffects.PoisonFrames(p,holder,16,20,edge),"Parked source poison");if(wound){Require(TimedEffects.PreparedOpenWounds(m,holder,20,p.Serial.Value,edge),"Parked wound");}
                    TimedEffects.Settle(holder,edge.AddMilliseconds(40));Require(SkillHealth.Balance256(holder)==before-16-(wound?20:0),"Positive item replenish cancelled parked poison/wound damage");
                }
            }finally{holder.Delete();}
            ClassProfessions.Profiles.Confirm(p,SkillName.Alchemy);var healer=new Healer{CantWalk=true};healer.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);healer.AIObject.AITimer.Stop();
            try
            {
                foreach(var info in p.Aggressed.ToArray()){p.RemoveAggressed(info.Defender);info.Defender.RemoveAggressor(p);}
                foreach(var info in p.Aggressors.ToArray()){p.RemoveAggressor(info.Attacker);info.Attacker.RemoveAggressed(p);}
                Require(MercenaryCare.HealFollowers(p,healer)==0&&SkillHealth.Balance256(m)==life,"Automatic conversation/vendor path healed parked army");
                var dying=new SkeletonGuardian();dying.Initialize(p,1,0);Require(dying.SetControlMaster(p),"Healer lethal-entry owner");dying.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);PersistentMinions.Profiles.Track(dying);MinionSafeAreas.Enforce(dying);SkillHealth.SetBalance256(dying,10);Require(TimedEffects.PoisonFrames(p,dying,100,20,TimedEffects.Now.AddMilliseconds(-100)),"Healer overdue poison");
                var entries=PooledRefList<ContextMenuEntry>.Create();try{ClassMercenaryHooks.ContextEntries(healer,p,ref entries);Require(entries.Count>0,"Missing healer menu");entries[0].OnClick(p,healer);}finally{entries.Dispose();}
                Require(dying.Deleted&&SkillHealth.Full(m)&&m.Map==Map.Internal,"Explicit healer menu did not settle death and heal remaining parked roster in place");SkillHealth.SetBalance256(m,life);
            }finally{healer.Delete();}
            DateTime fractional=TimedEffects.Now;Require(TimedEffects.AddRestoration(m,true,1,3000,fractional),"Fractional parked dose");TimedEffects.Settle(m,fractional.AddMilliseconds(1000));
            // Every landing ring is obstructed. Slot restoration must wait for
            // successful placement, including map/location-hook retries.
            for(int x=-4;x<=4;x++)for(int y=-4;y<=4;y++)if(x!=0||y!=0){var wall=new Static(0x80);wall.MoveToWorld(new(origin.X+x,origin.Y+y,origin.Z),p.Map);walls.Add(wall);}
            p.MoveToWorld(origin,p.Map);Require(!PersistentMinions.FindPosition(p,out _),"Blocked landing fixture has an open tile");PersistentMinions.Resume(p);Parked(p,m);Require(p.Followers==slots-2,"Failed landing consumed follower slots");
            foreach(var wall in walls){wall.Delete();}walls.Clear();PersistentMinions.Resume(p);TimedEffects.Settle(m,fractional.AddMilliseconds(3000));
            Require(m.Map==p.Map&&m.InRange(p,4)&&!MinionSafeAreas.Parked(m)&&m.ControlSlots==2&&p.Followers==slots&&SkillHealth.Balance256(m)==life&&m.Mana==20&&m.Rank==rank&&m.Mastery==mastery&&BattleCommand.PetOrder(m)==order&&NecromancerMageMissiles.ActorSeed(m)==seed,"Return reset/replaced/healed actor or lost slots/capture/seed/order");
            p.MoveToWorld(destination,p.Map);Parked(p,m);town.GuardsDisabled=true;p.MoveToWorld(new(destination.X+1,destination.Y,destination.Z),p.Map);Require(!MinionSafeAreas.Contains(p),"Disabled guards still classified safe");PersistentMinions.Resume(p);Require(m.Map==p.Map&&p.Followers==slots,"Disabled safe-region return");
            var safePoint=new Point3D(origin.X+8,origin.Y,origin.Z);var safeZone=new Server.Engines.ConPVP.SafeZone(new Rectangle2D(safePoint.X-1,safePoint.Y-1,3,3),safePoint,p.Map,false);
            try{p.MoveToWorld(safePoint,p.Map);Parked(p,m);PersistentMinions.Resume(p);Parked(p,m);}finally{safeZone.Unregister();}
            p.MoveToWorld(origin,p.Map);PersistentMinions.Resume(p);var roomPoint=new Point3D(origin.X+6,origin.Y,origin.Z);var room=new BaseRegion("Parking dungeon building",p.Map,1000,new Rectangle2D(roomPoint.X-1,roomPoint.Y-1,3,3));room.Register();
            try{p.MoveToWorld(roomPoint,p.Map);Require(!MinionSafeAreas.Contains(p)&&!MinionSafeAreas.Parked(m)&&m.Map==p.Map,"Ordinary dungeon building parked army");}finally{room.Unregister();}
            town.GuardsDisabled=false;p.MoveToWorld(destination,p.Map);Parked(p,m);p.Kill();PersistentMinions.Profiles.Sweep(TimedEffects.Now);Require(!p.Alive&&m.Deleted,"Parked army escaped owner-death cleanup");
        }
        finally{foreach(var w in walls){w.Delete();}town.Unregister();m.Delete();born.Delete();merc.Delete();p.Delete();}
    }
    private static void LethalEntry()
    {
        var p=Owner();var m=MagePetBuffChecks.Pet(p,1);var doomed=new SkeletonGuardian();doomed.Initialize(p,1,0);Require(doomed.SetControlMaster(p),"Lethal-entry owner");doomed.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);PersistentMinions.Profiles.Track(doomed);SkillHealth.SetBalance256(doomed,10);var town=Town(p,Destination(p));
        try{Require(TimedEffects.PoisonFrames(p,doomed,100,20,TimedEffects.Now.AddMilliseconds(-100)),"Overdue lethal poison");p.MoveToWorld(Destination(p),p.Map);Require(doomed.Deleted||!doomed.Alive,"Entry failed to settle lethal harmful effect");Parked(p,m);}
        finally{town.Unregister();doomed.Delete();m.Delete();p.Delete();}
    }
    private static void NativeRevive()
    {
        var p=ReviveNativeActivationChecks.Owner();var corpse=ReviveNativeActivationChecks.Body(p,false,out var original);var actor=ReviveNativeActivationChecks.Activate(p,corpse,original);var destination=Destination(p);var town=Town(p,destination);var state=ReviveNativeActivation.State(actor);int hp=SkillHealth.Balance256(actor);
        try{p.MoveToWorld(destination,p.Map);Parked(p,actor);Require(ReviveNativeActivation.State(actor)==state&&SkillHealth.Balance256(actor)==hp,"Parking changed original Revive/birth state");PersistentMinions.Resume(p);Parked(p,actor);}
        finally{town.Unregister();actor.Delete();corpse.Delete();p.Delete();}
    }
    private static void Check()
    {
        Controls();LethalEntry();NativeRevive();var p=Owner();var m=MagePetBuffChecks.Pet(p,1);var merc=MercenaryCareChecks.Companion(p);Require(MercenaryCare.Register(p,merc,20),"Saved companion registration");
        var origin=p.Location;var destination=Destination(p);var town=Town(p,destination);m.ControlSlots=2;p.Followers+=2;PersistentMinions.Profiles.Track(m);SkillHealth.SetBalance256(m,SkillHealth.Maximum256(m)-257);m.SetMana(100);m.Mana=17;
        p.MoveToWorld(destination,p.Map);Parked(p,m);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,m.Serial.Value,merc.Serial.Value,origin,destination,p.Map.MapID,SkillHealth.Balance256(m),SkillHealth.Maximum256(m),m.Mana,m.Rank,m.Mastery,BattleCommand.PetOrder(m),NecromancerMageMissiles.ActorSeed(m),p.Followers,0)));town.Unregister();
        Log.Information("Safe-area minions:actual region entry/repeated notifications,original wounds/mana/ranks/order/seed,slot release/blocked return,live deadlines/queued recovery,signed poison/wound item regeneration,explicit healer menu/automatic refusal,named companion/native Revive and owner-death cleanup pass");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var m=(MageWorldBody)World.FindMobile((Serial)s.Mage);var merc=(BaseCreature)World.FindMobile((Serial)s.Companion);p.MoveToWorld(s.Town,Map.Maps[s.Map]);var town=Town(p,s.Town);
        try
        {
            Parked(p,m);
            if(merc.ControlMaster!=p){Require(merc.IsStabled&&merc.StabledBy==p&&p.AutoStabled?.Contains(merc)==true&&MercenaryCare.States.TryGetValue(merc.Serial,out var contract)&&contract.Owner==p.Serial&&!contract.Retired,"Named companion lost native autostable/contract identity");}
            p.ClaimAutoStabledPets();Parked(p,m);
            Require(s.Generation<2&&m.Rank==s.Rank&&m.Mastery==s.Mastery&&SkillHealth.Balance256(m)==s.Life&&SkillHealth.Maximum256(m)==s.Maximum&&m.Mana==s.Mana&&BattleCommand.PetOrder(m)==s.Order&&NecromancerMageMissiles.ActorSeed(m)==s.Seed&&p.Followers==s.Slots&&merc.Map!=Map.Internal&&MercenaryCare.IsCompanion(p,merc),$"Saved parked resources/identity/slots/companion changed:actual={JsonSerializer.Serialize(new{Life=SkillHealth.Balance256(m),Maximum=SkillHealth.Maximum256(m),Mana=m.Mana,Rank=m.Rank,Mastery=m.Mastery,Order=BattleCommand.PetOrder(m),Seed=NecromancerMageMissiles.ActorSeed(m),Slots=p.Followers,MercMap=merc.Map?.MapID,Companion=MercenaryCare.IsCompanion(p,merc),MercControlled=merc.Controlled,MercBonded=merc.IsBonded,MercOwner=merc.ControlMaster?.Serial.Value})};expected={JsonSerializer.Serialize(s)}");
            PersistentMinions.Resume(p);Parked(p,m);p.MoveToWorld(s.Origin,Map.Maps[s.Map]);PersistentMinions.Resume(p);Require(m.Map==p.Map&&m.ControlSlots==2&&p.Followers==s.Slots+2&&SkillHealth.Balance256(m)==s.Life,"Loaded return reset life/slots");p.MoveToWorld(s.Town,p.Map);Parked(p,m);File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
        }finally{town.Unregister();}
    }
    private static void Live(NetState network)
    {
        var p=Owner();FixtureNetwork.Authenticate(network,p,"safe-army");var m=MagePetBuffChecks.Pet(p,2);var target=PoisonDaggerChecks.Target(p);target.MoveToWorld(new(m.X+5,m.Y,m.Z),p.Map);int hp=SkillHealth.Balance256(target);var origin=p.Location;var town=Town(p,Destination(p));int life=SkillHealth.Maximum256(m)/2+17;SkillHealth.SetBalance256(m,life);m.Ground.Store(m.WorldState.Controller with{NextUtc=TimedEffects.Now.AddMilliseconds(40)});long deadline=Core.TickCount+8000;bool parked=false;
        void Clean(){town.Unregister();target.Delete();m.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(network.Running&&Core.TickCount<deadline&&m.WorldState.Controller.Fault==null,"Natural safe-area transition timeout/fault");var c=m.WorldState.Controller;
                if(!parked&&c is{Mode:4}&&c.ActionAt>c.Frame)
                {
                    life=SkillHealth.Balance256(m);Require(life<SkillHealth.Maximum256(m),"Natural fixture healed fully before parking");p.MoveToWorld(Destination(p),p.Map);Parked(p,m);parked=true;
                    Timer.StartTimer(TimeSpan.FromMilliseconds(1000),()=>
                    {try{Parked(p,m);Require(SkillHealth.Balance256(target)==hp&&!NecromancerMageMissiles.Snapshot().Bolts.Any(x=>x.Actor==m.Serial.Value)&&!FireGround.Snapshot().Meteors.Any(x=>x.NpcCaster==m.Serial.Value)&&SkillHealth.Balance256(m)==life,"Parked army fought/replayed windup/regenerated");p.MoveToWorld(origin,p.Map);Require(m.Map==p.Map&&m.InRange(p,4)&&SkillHealth.Balance256(m)==life,"Natural location hook did not return same wounds");Clean();Log.Information("safe-area-minion-live-result: passed");}catch(Exception e){Clean();Log.Error(e,"safe-area-minion-live-result: failed");}});
                    return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(10),Wait);
            }catch(Exception e){Clean();Log.Error(e,"safe-area-minion-live-result: failed");}
        }
        Log.Information("safe-area-minion-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Wait);
    }
}
