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

namespace Sanctuary.Integration;

public static class WeaponBlockChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(WeaponBlockChecks));
    private sealed record Saved(uint Owner,uint Primary,uint Secondary,uint Helm,uint Amulet,int Mana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"weapon-block-fixtures.json");
    private static bool _creating;
    private static PlayerMobile _owner;
    private static BaseWeapon _first,_second;
    private static BaseCreature _source;
    private static Func<int,int> _prior;
    private static int _phase,_calls,_before;
    private static long _deadline;
    public sealed class RejectPlayer:PlayerMobile
    {
        public bool Reject;
        [Constructible]public RejectPlayer(){}
        public RejectPlayer(Serial serial):base(serial){}
        public override bool CheckEquip(Item item)=>!Reject && base.CheckEquip(item);
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);reader.ReadInt();}
    }
    public static void Configure(){Rates();Register("weapon-block-check",Check);Register("weapon-block-reload-check",Reload);Register("weapon-block-input-check",Input);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=p=>_creating || p.Name?.StartsWith("WeaponBlockFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):hp?.Invoke(p)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=p=>_creating || p.Name?.StartsWith("WeaponBlockFixture",StringComparison.Ordinal)==true?TimeSpan.FromHours(1):mp?.Invoke(p)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Weapon Block real claw equip, source defense and saved identity",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Cleanup();Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Cleanup(){_source?.Delete();_source=null;if(_prior!=null){WeaponBlocks.Roll=_prior;_prior=null;}}
    private static void Later(int ms,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>{try{action();}catch(Exception e){Cleanup();Log.Error(e,"weapon-block-result: failed");}});
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1){while(ClassCreation.Profiles.GetRank(p.Serial,SkillMechanics.For(ability).TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,SkillMechanics.For(ability).TalentId,out _),"Train "+ability);}}
    private static PlayerMobile Fresh(bool reject=false)
    {
        PlayerMobile p;_creating=true;try{p=reject?new RejectPlayer():new PlayerMobile();}finally{_creating=false;}
        p.Body=0x191;p.Name="WeaponBlockFixture";p.Player=true;p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Assassin);ClassCreation.Profiles.AdvanceLevel(p,99);
        while(p.RawStr<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Strength");}while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Dexterity");}
        for(int x=2350;x<2390;x++)for(int y=1200;y<1240;y++)
        {
            var a=CombatPractice.FindFloor(Map.Trammel,x,y);var b=CombatPractice.FindFloor(Map.Trammel,x+4,y);
            if(a.Z!=b.Z || !Teleportation.Space(Map.Trammel,a,p) || !Map.Trammel.CanSpawnMobile(b) || !Map.Trammel.LineOfSight(new Point3D(x,y,a.Z+12),new Point3D(x+4,y,b.Z+12))){continue;}
            p.MoveToWorld(a,Map.Trammel);p.Hits=p.HitsMax;p.Mana=0;Train(p,CombatAbility.ClawMastery);return p;
        }
        throw new InvalidOperationException("No claw fixture corridor");
    }
    private static Tekagi Claw(PlayerMobile p)
    {var claw=new Tekagi();ClassGear.Profiles.Convert(claw,99,GearRarity.Normal,new Random(173));p.AddToBackpack(claw);return claw;}
    private static Item Named(PlayerMobile p,string key)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Random(173),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Equip "+key);ClassGear.Refresh(p);return item;}
    private static void Still(PlayerMobile p)
    {ClassCombat.Stop(p);var s=ClassCombat.For(p);s.RunningStep=false;s.RunningUntil=s.RecoveryUntil=s.NextAction=Core.TickCount-1;p.Frozen=p.Paralyzed=false;}
    private static void Mode(PlayerMobile p,int mode){var s=ClassCombat.For(p);s.RunningStep=mode==2;s.RunningUntil=Core.TickCount+(mode>0?1000:-1);s.StepMap=p.Map;s.StepPosition=p.Location;}
    private static void Pair(PlayerMobile p,BaseWeapon first,BaseWeapon second)
    {Require(WeaponHands.Pair(p)==new WeaponPair(first,second) && DualEquip.Primary(p)==first.Serial && CombatTiming.Equipped(p)==first && ClassGear.Active(p,first) && ClassGear.Active(p,second),"Real claw pair/primary/active contribution");}
    private static void Equipment()
    {
        var p=(RejectPlayer)Fresh(true);var a=Claw(p);var b=Claw(p);Require(p.EquipItem(a),"Initial claw");var original=b.Layer;var clock=p.NextCombatTime;p.Reject=true;Require(!p.EquipItem(b),"Claw equip veto ignored");p.Reject=false;
        Require(b.Parent==p.Backpack && b.Layer==original && a.Layer==Layer.OneHanded && p.NextCombatTime==clock && !DualEquip.Deferred(p) && DualEquip.Primary(p)==a.Serial,"Claw transaction rollback");
        Require(p.EquipItem(b),"Offhand claw equip");Pair(p,a,b);var c=Claw(p);Require(!p.EquipItem(c),"Third claw equipped");
        p.AddToBackpack(a);Require(b.Layer==Layer.OneHanded && DualEquip.Primary(p)==b.Serial,"Primary removal/promotion");Require(p.EquipItem(a),"Reequip older claw");Pair(p,b,a);
        p.AddToBackpack(a);var sword=new Longsword();ClassGear.Profiles.Convert(sword,99,GearRarity.Normal,new Random(173));p.AddToBackpack(sword);Require(!p.EquipItem(sword),"Assassin mixed weapon pair accepted");
        var state=ClassGear.State(a);a.Resource=CraftResource.Valorite;a.PlayerConstructed=true;ClassGear.Profiles.Store(a,state with{CraftedEquipment=true,MaterialRolls=[new("damage-fire",10)]});Require(p.EquipItem(a),"Valid material/crafted claw pair rejected");Pair(p,b,a);
        var damage=GearEffects.Damage(p,0);using(WeaponHands.Use(p,a)){Require(GearEffects.Damage(p,0).Fire>damage.Fire,"Selected crafted-claw fire did not follow hand");}
        p.Delete();
    }
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.WeaponBlock);Require(row.Passive && row.SourceId==263 && row.Text("passiveitype")=="h2h" && row.Text("passivestat1")=="passive_weaponblock","Pinned passive row");
        int[] golden=[26,32,36,39,42,44,46,47,49,50,51,52,53,54,55,56,56,56,57,57];
        for(int i=0;i<golden.Length;i++){int rank=i+1;Require(new SkillValues(row,_=>rank,_=>0,99).Eval("passivecalc1")==golden[i],"Independent Weapon Block rank "+rank);}
        Require(new SkillValues(row,_=>22,_=>0,99).Eval("passivecalc1")==58,"Current user rank22=58");
    }
    private static void Differential(PlayerMobile p)
    {
        var old=WeaponBlocks.Roll;int count=0;
        try
        {
            using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"weapon-block-source-probe.json")));
            foreach(var r in doc.RootElement.EnumerateArray())
            {
                int V(int i)=>r[i].GetInt32();if(V(0)!=1 || V(1)!=3 || V(2)!=3 || V(3)!=1 || V(5)!=13 || V(7)!=0 || V(8)!=0){continue;}
                int calls=0;Still(p);Mode(p,V(4));WeaponBlocks.Roll=max=>{Require(max==100,"Block RNG range");calls++;return V(9);};
                Require(WeaponBlocks.Attempt(p,Core.TickCount,reaction:false)==(V(11)==16) && calls==V(12),"Full source stationary learned-claw dispatch comparison");count++;
            }
            Require(count==60,"Learned layer native/source inventory");
        }
        finally{WeaponBlocks.Roll=old;Still(p);}
    }
    private static void Consumers(PlayerMobile p,BaseWeapon second)
    {
        var enemy=new Ogre{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.AIObject.AITimer.Stop();enemy.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);var old=WeaponBlocks.Roll;
        try
        {
            int calls=0;WeaponBlocks.Roll=_=>{calls++;return 0;};Still(p);int mana=NecromancerEffects.ManaBalance256(p);Require(ClassStats.CheckHit(enemy,p,0,()=>0)==false && calls==1 && HitRecovery.Active(p,Core.TickCount),"Ordinary melee did not block/react once");
            Still(p);calls=0;Require(ClassStats.CheckHit(enemy,p,0,()=>.999)==false && calls==0,"AR miss rolled weapon block");
            int hp=SkillHealth.Balance256(p);calls=0;Still(p);Require(SkillCombat.Hit(enemy,p,10,"fire",missileDefense:true)==0 && calls==1 && SkillHealth.Balance256(p)==hp && NecromancerEffects.ManaBalance256(p)==mana,"Traced spell block or mana");
            WeaponBlocks.Roll=_=>{calls++;return 99;};Still(p);calls=0;Require(SkillCombat.Hit(enemy,p,10,"fire",missileDefense:true)>0 && calls==1,"Failed spell block incorrectly prevented damage");
            WeaponBlocks.Roll=_=>{calls++;return 0;};Still(p);calls=0;hp=SkillHealth.Balance256(p);SkillCombat.HitParts(enemy,p,new(4,4,0,0,0),false,spell:true);Require(calls==0 && SkillHealth.Balance256(p)<hp,"Direct CE-style area gained global weapon block");
            calls=0;hp=SkillHealth.Balance256(p);SkillHealth.Damage(enemy,p,256);Require(calls==0 && SkillHealth.Balance256(p)==hp-256,"Direct health/DoT gained block");
            Train(p,CombatAbility.BurstOfSpeed);Still(p);NecromancerEffects.SetMana256(p,20*256);Require(ClassCombat.Perform(p,CombatAbility.BurstOfSpeed,null,p.Location,Core.TickCount),"Pending SC begin");var s=ClassCombat.For(p);long end=s.NextAction,effect=s.EffectAt,recovery=s.RecoveryUntil;mana=NecromancerEffects.ManaBalance256(p);calls=0;
            Require(ClassAvoidance.Missile(p,reaction:true) && calls==1 && s.PendingAbility==CombatAbility.BurstOfSpeed && s.NextAction==end && s.EffectAt==effect && s.RecoveryUntil==recovery && NecromancerEffects.ManaBalance256(p)==mana,"Weapon Block interrupted/delayed/paid pending cast");Still(p);
            foreach(int mode in new[]{1,2}){Mode(p,mode);calls=0;Require(!ClassAvoidance.Missile(p,reaction:true) && calls==0,"Moving claw block");ClassCombat.For(p).RunningUntil=Core.TickCount-1;Require(ClassAvoidance.Missile(p,reaction:false) && calls==1,"Movement lease did not expire");}
            Still(p);calls=0;Require(ClassAvoidance.Missile(p,reaction:false) && !HitRecovery.Active(p,Core.TickCount),"Soft-hit suppression did not preserve idle state");
            int durability=second.HitPoints;second.HitPoints=0;Require(WeaponBlocks.Chance(p)==0,"Broken offhand still blocks");second.HitPoints=durability;Require(WeaponBlocks.Chance(p)==26,"Repaired pair not restored");
            var armor=Named(p,"Twitchthroe");Require(WeaponBlocks.Chance(p)==26,"Ordinary increased block changed Weapon Block");p.AddToBackpack(armor);armor=Named(p,"Guardian Angel");Still(p);Require(ClassAvoidance.Missile(p,reaction:true) && ClassCombat.For(p).RecoveryUntil-Core.TickCount==HitRecovery.BlockFrames(DiabloClass.Assassin,30)*40 && WeaponBlocks.Chance(p)==26,"FBR reaction/passive chance");p.AddToBackpack(armor);
            p.AddToBackpack(second);var bartuc=Named(p,"Cutthroat1");Require(WeaponBlocks.Chance(p)==36 && SkillMechanics.Rank(p,CombatAbility.WeaponBlock)==3,"Real offhand class-skill bonus");p.AddToBackpack(bartuc);Require(p.EquipItem(second),"Ordinary claw restore");Still(p);p.Hits=p.HitsMax;p.Mana=0;
        }
        finally{WeaponBlocks.Roll=old;enemy.Delete();}
    }
    private static void Lifecycle()
    {
        var p=Fresh();var a=Claw(p);var b=Claw(p);Require(p.EquipItem(a) && p.EquipItem(b),"Lifecycle pair");Train(p,CombatAbility.WeaponBlock);Require(WeaponBlocks.Chance(p)==26,"Lifecycle learned chance");
        p.AddToBackpack(a);p.AddToBackpack(b);Still(p);Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Paladin) && WeaponBlocks.Chance(p)==0 && ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Assassin),"Wrong class retained chance");Require(p.EquipItem(a) && p.EquipItem(b) && WeaponBlocks.Chance(p)==26,"History pair restore");
        ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(WeaponBlocks.Chance(p)==0,"Respec retained passive");Train(p,CombatAbility.ClawMastery);Train(p,CombatAbility.WeaponBlock);while(p.RawStr<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Restore Strength");}while(p.RawDex<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Dexterity),"Restore Dexterity");}p.AddToBackpack(a);p.AddToBackpack(b);Require(p.EquipItem(a) && p.EquipItem(b) && WeaponBlocks.Chance(p)==26,"Respec/retrain pair restore");p.Kill();Require(WeaponBlocks.Chance(p)==0 && ClassCreation.Profiles.GetRank(p.Serial,"assassin.weaponblock")==1,"Dead owner blocks or loses rank");p.Resurrect();p.AddToBackpack(a);p.AddToBackpack(b);Require(p.EquipItem(a) && p.EquipItem(b) && WeaponBlocks.Chance(p)==26,"Resurrection/gear recovery lost passive");p.Corpse?.Delete();p.Delete();
    }
    private static void Check()
    {
        Goldens();Equipment();Lifecycle();var p=Fresh();var a=Claw(p);var b=Claw(p);Require(p.EquipItem(a) && p.EquipItem(b) && WeaponBlocks.Chance(p)==0,"Unlearned pair");Train(p,CombatAbility.WeaponBlock);Pair(p,a,b);
        Require(!ClassCombat.Available(p,CombatAbility.WeaponBlock) && !SkillBindings.Instance.Assign(p,true,CombatAbility.WeaponBlock) && !SkillBindings.Instance.Assign(p,false,CombatAbility.WeaponBlock) && !SkillBindings.Instance.AssignQuick(p,8,CombatAbility.WeaponBlock),"Passive accepted active binding");
        Differential(p);Consumers(p,b);_owner=p;_first=a;_second=b;_phase=0;_prior=WeaponBlocks.Roll;WeaponBlocks.Roll=max=>{Require(max==100,"Natural Weapon Block RNG range");_calls++;return _phase<3?0:99;};Next();
    }
    private static void Next()
    {
        Still(_owner);_owner.Hits=_owner.HitsMax;_owner.Mana=0;_calls=0;_before=SkillHealth.Balance256(_owner);_deadline=Core.TickCount+6000;
        var point=new Point3D(_owner.X+4,_owner.Y,_owner.Z);
        if(_phase<2)
        {
            var lich=new GraveyardLich{CantWalk=true,NoKillAwards=true,Name="WeaponBlockFixtureCaster"};lich.AIObject.AITimer.Stop();lich.MoveToWorld(point,_owner.Map);_source=lich;Require(lich.BeginFirebolt(_owner),"Natural firebolt begin");
            if(_phase==0){_owner.AddToBackpack(_second);}else{Require(_owner.EquipItem(_second),"Live offhand re-equip");}
        }
        else
        {var enemy=new EncounterCreature{CantWalk=true,NoKillAwards=true};enemy.AIObject.AITimer.Stop();enemy.MoveToWorld(point,_owner.Map);_source=enemy;Require(EliteCombat.Enemy(enemy,_owner,18),"Natural charged-bolt enemy");EliteBolts.Lightning(enemy);}
        Later(30,Observe);
    }
    private static void Observe()
    {
        bool blocked=_phase is 1 or 2;
        if((blocked?_calls==0:SkillHealth.Balance256(_owner)==_before) && Core.TickCount<_deadline){Later(30,Observe);return;}
        Require(_calls==(_phase==0?0:1) && (blocked?SkillHealth.Balance256(_owner)==_before:SkillHealth.Balance256(_owner)<_before) && NecromancerEffects.ManaBalance256(_owner)==0,"Natural projectile/block/live-claw phase "+_phase+" calls="+_calls);
        if(_source is EncounterCreature e){EliteBolts.Cancel(e);}_source.Delete();_source=null;if(++_phase<4){Next();return;}
        Train(_owner,CombatAbility.WeaponBlock,20);var helm=Named(_owner,"Tarnhelm");var amulet=Named(_owner,"Highlord's Wrath");Require(WeaponBlocks.Chance(_owner)==58 && !ClassCreation.Profiles.TryTrain(_owner.Serial,"assassin.weaponblock",out _),"Current effective22/hard20 cap");Still(_owner);Pair(_owner,_first,_second);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(_owner.Serial.Value,_first.Serial.Value,_second.Serial.Value,helm.Serial.Value,amulet.Serial.Value,NecromancerEffects.ManaBalance256(_owner))));Cleanup();Log.Information("weapon-block-result: passed");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var first=(BaseWeapon)World.FindItem((Serial)s.Primary);var second=(BaseWeapon)World.FindItem((Serial)s.Secondary);
        Pair(p,first,second);Require(WeaponBlocks.Chance(p)==58 && SkillMechanics.Rank(p,CombatAbility.WeaponBlock)==22 && World.FindItem((Serial)s.Helm)?.Parent==p && World.FindItem((Serial)s.Amulet)?.Parent==p && NecromancerEffects.ManaBalance256(p)==s.Mana && !ClassMovement.IsMoving(p,Core.TickCount),"Saved learned chance/gear/mana/movement");
        var old=WeaponBlocks.Roll;try{int calls=0;WeaponBlocks.Roll=_=>{calls++;return 57;};Require(ClassAvoidance.Missile(p,reaction:false) && calls==1,"Reloaded live passive boundary");WeaponBlocks.Roll=_=>58;Require(!ClassAvoidance.Missile(p,reaction:false),"Reloaded equality boundary");}finally{WeaponBlocks.Roll=old;}
    }
    private static void Input()
    {
        var p=Fresh();Train(p,CombatAbility.WeaponBlock);var a=Claw(p);var b=Claw(p);Require(p.EquipItem(a),"Network primary");
        var net=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));net.Seeded=true;net.CompressionEnabled=false;net.Mobile=p;p.NetState=net;var field=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);field.SetValue(net,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));net.ResetMovementState();net.SendMobileUpdate(p);
        p.Lift(b,1,out bool rejected,out var reason);Require(!rejected && p.Holding==b,"Network claw lift: "+reason);long deadline=Core.TickCount+5000;Log.Information("weapon-block-input: ready owner={Owner} item={Item}",p.Serial.Value,b.Serial.Value);
        void Wait(){try{if(b.Parent!=p){Require(Core.TickCount<deadline,"Network claw equip timeout");Timer.StartTimer(TimeSpan.FromMilliseconds(30),Wait);return;}Pair(p,a,b);Require(p.Holding==null && b.HeldBy==null && b.Layer==Layer.TwoHanded && WeaponBlocks.Chance(p)==26 && NecromancerEffects.ManaBalance256(p)==0,"Network dual-claw eligibility");p.Delete();Log.Information("weapon-block-input-result: passed");}catch(Exception ex){p.Delete();Log.Error(ex,"weapon-block-input-result: failed");}}
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),Wait);
    }
}
