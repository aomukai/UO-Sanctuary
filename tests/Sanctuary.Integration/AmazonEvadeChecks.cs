// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using Server.Network;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class AmazonEvadeChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(AmazonEvadeChecks));
    private sealed record Saved(uint Owner,uint[] Items,Point3D Origin,int Mana);
    private static bool _creating;
    private static PlayerMobile _owner;
    private static Item[] _items;
    private static Point3D _origin;
    private static BaseCreature _source;
    private static NetState _network;
    private static Func<int,int> _original;
    private static int _phase,_calls,_draw,_before,_mana;
    private static bool _walkingAtDraw,_stepped,_runningStep;
    private static long _deadline;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"amazon-evade-fixtures.json");
    public static void Configure(){Freeze();Register("amazon-evade-check",Check);Register("amazon-evade-reload-check",Reload);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Freeze();
    private static void Freeze()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        Mobile.HitsRegenRateHandler=a=>_creating || a.Name=="AmazonEvadeFixture"?TimeSpan.FromHours(1):hp?.Invoke(a)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=a=>_creating || a.Name=="AmazonEvadeFixture"?TimeSpan.FromHours(1):mp?.Invoke(a)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Evade actual walking, incoming damage, gear and persistence",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Cleanup();Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Cleanup()
    {
        if(_source is EncounterCreature encounter){EliteBolts.Cancel(encounter);}
        _source?.Delete();_source=null;
        if(_network!=null){_network.Mobile=null;if(_owner!=null){_owner.NetState=null;}_network=null;}
        if(_original!=null){AmazonAvoidance.Roll=_original;_original=null;}
    }
    private static void Later(TimeSpan delay,Action action)=>Timer.StartTimer(delay,()=>
    {try{action();}catch(Exception e){Cleanup();Log.Error(e,"amazon-evade-result: failed");}});
    private static PlayerMobile Fresh()
    {
        PlayerMobile owner;_creating=true;try{owner=new PlayerMobile{Body=0x190,Name="AmazonEvadeFixture",Player=true};}finally{_creating=false;}
        owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(owner,99);
        while(owner.RawStr<110){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Strength");}
        while(owner.RawDex<100){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Dexterity");}
        var map=Map.Trammel;
        for(int x=2500;x<2800;x++)for(int y=1000;y<1300;y++)
        {
            int z=map.GetAverageZ(x,y);var point=new Point3D(x+1,y,z);
            if(!Enumerable.Range(0,7).All(i=>map.CanSpawnMobile(x+i,y,z)) || !FixtureSpace.ClearOfAllies(owner,point,map)
                || !map.LineOfSight(new Point3D(x,y,z+12),new Point3D(x+6,y,z+12))){continue;}
            owner.MoveToWorld(point,map);owner.Hits=owner.HitsMax;owner.Mana=0;return owner;
        }
        throw new InvalidOperationException("No independent Evade walking corridor");
    }
    private static void Train(PlayerMobile owner,string id,int rank=1)
    {
        while(ClassCreation.Profiles.GetRank(owner.Serial,id)<rank)
        {
            Require(TalentCatalog.TryGet(DiabloClass.Amazon,id,out var talent),"Talent "+id);
            foreach(var prerequisite in talent.Prerequisites){Train(owner,prerequisite);}
            Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Training "+id);
        }
    }
    private static void Standing(PlayerMobile owner)
    {var s=ClassCombat.For(owner);s.RunningUntil=Core.TickCount-1;s.RunningStep=false;s.NextAction=s.RecoveryUntil=Core.TickCount-1;}
    private static void Mode(PlayerMobile owner,int mode)
    {var s=ClassCombat.For(owner);s.RunningStep=mode==2;s.RunningUntil=Core.TickCount+(mode>0?1000:-1);s.StepPosition=owner.Location;s.StepMap=owner.Map;}
    private static BaseCreature Enemy(PlayerMobile owner,int dx=1)
    {
        var enemy=new Ogre{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.AIObject.AITimer.Stop();enemy.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);return enemy;
    }
    private static Item Named(PlayerMobile owner,string name)
    {
        var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==name).Id,99,new Random(173),true);
        Require(owner.EquipItem(item),"Equip "+name);ClassGear.Refresh(owner);return item;
    }
    private static void Goldens()
    {
        var row=SkillMechanics.For(CombatAbility.Evade);
        Require(row.Passive && row.Text("passivestat1")=="passive_evade" && row.Text("stsound")=="block_arrow_1" && row.Text("stsoundclass")=="amazon_dodge_1","Pinned Evade metadata");
        int[] chances=[18,24,29,34,37,40,42,44,46,47,49,50,51,52,52,54,54,55,55,56];
        for(int i=0;i<chances.Length;i++){int rank=i+1;Require(new SkillValues(row,_=>rank,_=>0,99).Eval("passivecalc1")==chances[i],"Evade rank golden "+rank);}
        Require(chances[5]==40,"User effective6=40");
    }
    private static void Differential()
    {
        var owner=Fresh();var enemy=Enemy(owner);var prior=AmazonAvoidance.Roll;int weapon=0,spell=0,running=0;
        try
        {
            Train(owner,"amazon.avoid");Require(AmazonAvoidance.DodgeChance(owner)==18 && AmazonAvoidance.AvoidChance(owner)==24,"Standing passive controls");
            using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"amazon-evade-source-probe.json")));
            foreach(int rank in new[]{0,1,3,6,20})
            {
                if(rank>0){Train(owner,"amazon.evade",rank);}int chance=rank==0?0:rank==1?18:rank==3?29:rank==6?40:56;
                Require(AmazonAvoidance.EvadeChance(owner)==chance,"Native Evade rank");
                foreach(var row in data.RootElement.GetProperty("dispatch").EnumerateArray())
                {
                    if(row[0].GetInt32()!=0 || row[3].GetInt32()!=chance || row[5].GetInt32()!=0 || row[6].GetInt32()!=1){continue;}
                    int mode=row[1].GetInt32(),calls=0,draw=row[4].GetInt32();bool projectile=row[2].GetInt32()!=0;
                    AmazonAvoidance.Roll=max=>{Require(max==100,"Evade RNG range");calls++;return draw;};Mode(owner,mode);
                    bool hit=ClassStats.CheckHit(enemy,owner,0,()=>0,projectile)==true;
                    if(mode==2){Require(hit && calls==0,"Approved running suppression changed");running++;}
                    else{Require(hit==(row[7].GetInt32()==0) && calls==row[8].GetInt32(),"Compiled standing/walking dispatch differential");weapon++;}
                    if(!projectile){continue;}
                    calls=0;Mode(owner,mode);bool avoided=AmazonAvoidance.Missile(owner);
                    if(mode==2){Require(!avoided && calls==0,"Running spell Evade enabled");running++;}
                    else{Require(avoided==(row[7].GetInt32()!=0) && calls==row[8].GetInt32(),"Compiled standing/walking spell differential");spell++;}
                }
            }
            Require(weapon==200 && spell==100 && running==150,"Incomplete Evade matrix");
        }
        finally{AmazonAvoidance.Roll=prior;enemy.Delete();owner.Delete();}
        Log.Information("Evade:300 native/source comparisons,150 approved-running cases and20 rank goldens passed");
    }
    private static void Movement(PlayerMobile owner,Item shield)
    {
        var origin=owner.Location;var prior=AmazonAvoidance.Roll;BaseCreature enemy=null;
        try
        {
            owner.AddToBackpack(shield);ClassGear.Refresh(owner);Standing(owner);var s=ClassCombat.For(owner);s.Running=true;
            Require(!ClassMovement.IsMoving(owner,Core.TickCount),"Selected Run created movement");
            owner.Direction=Direction.East;Require(owner.Move(Direction.East) && owner.Location!=origin,"Actual accepted walk");
            Require(ClassMovement.IsMoving(owner,Core.TickCount) && !ClassMovement.IsRunning(owner,Core.TickCount),"Walk lease did not reach shared defense");
            enemy=Enemy(owner,-1);enemy.Frozen=false;int calls=0;AmazonAvoidance.Roll=_=>{Require(ClassMovement.IsMoving(owner,Core.TickCount) && !ClassMovement.IsRunning(owner,Core.TickCount),"Melee roll left accepted walking mode");calls++;return 0;};
            int hp=SkillHealth.Balance256(owner),mana=NecromancerEffects.ManaBalance256(owner);long next=s.NextAction,recovery=s.RecoveryUntil;
            var fists=(BaseWeapon)enemy.Weapon;
            for(int i=0;i<1000 && calls<4;i++){fists.OnSwing(enemy,owner);}
            Require(calls>=4 && SkillHealth.Balance256(owner)==hp && NecromancerEffects.ManaBalance256(owner)==mana
                && s.NextAction==next && s.RecoveryUntil==recovery,"Native melee Evade damage/resource/recovery lock");
            var before=owner.Location;owner.Direction=Direction.East;
            Require(owner.Move(Direction.East) && owner.Location!=before,"Evade interrupted subsequent walking");enemy.Delete();enemy=null;
            owner.Direction=Direction.West;Require(owner.Move(Direction.West|Direction.Running),"Accepted run");
            Require(ClassMovement.IsRunning(owner,Core.TickCount),"Run lease missing");s.Running=false;calls=0;AmazonAvoidance.Roll=_=>{calls++;return 0;};
            Require(!AmazonAvoidance.Missile(owner) && calls==0 && ClassMovement.IsRunning(owner,Core.TickCount),"Selecting Walk erased accepted run penalty");
            enemy=Enemy(owner,-1);enemy.Frozen=false;hp=SkillHealth.Balance256(owner);
            ((BaseWeapon)enemy.Weapon).OnSwing(enemy,owner);Require(SkillHealth.Balance256(owner)<hp && calls==0,"Running received melee Evade");enemy.Delete();enemy=null;
            // These negative input controls start after an explicitly expired
            // fixture lease; natural expiry is observed separately below.
            Standing(owner);owner.Direction=Direction.North;before=owner.Location;
            Require(owner.Move(Direction.West|Direction.Running) && owner.Location==before && !ClassMovement.IsMoving(owner,Core.TickCount),"Direction-only turn created a walking/run lease");
            owner.Frozen=true;Require(!owner.Move(Direction.West|Direction.Running) && owner.Location==before && !ClassMovement.IsMoving(owner,Core.TickCount),"Failed step created movement");owner.Frozen=false;
            // Standing reverts to the separate Dodge/Avoid percentages.
            int evade=AmazonAvoidance.EvadeChance(owner),avoid=AmazonAvoidance.AvoidChance(owner);
            AmazonAvoidance.Roll=_=>avoid-1;Require(AmazonAvoidance.Missile(owner),"Standing did not restore Avoid");
            Mode(owner,1);calls=0;AmazonAvoidance.Roll=_=>{calls++;return evade;};
            Require(!AmazonAvoidance.Missile(owner) && calls==1,"Failed Evade fell back to standing Avoid");
        }
        finally
        {
            AmazonAvoidance.Roll=prior;owner.Frozen=false;enemy?.Delete();owner.MoveToWorld(origin,owner.Map);
            Require(owner.EquipItem(shield),"Restore named shield");ClassGear.Refresh(owner);Standing(owner);owner.Hits=owner.HitsMax;owner.Mana=0;
        }
    }
    private static void Check()
    {
        Goldens();Differential();var owner=Fresh();var helm=Named(owner,"Tarnhelm");Require(AmazonAvoidance.EvadeChance(owner)==0,"General allskills granted unlearned Evade");Train(owner,"amazon.evade");
        var items=new[]{helm,Named(owner,"Highlord's Wrath"),Named(owner,"Skin of the Vipermagi"),Named(owner,"Lidless Wall"),Named(owner,"The Stone of Jordan")};
        Require(SkillMechanics.Rank(owner,CombatAbility.Evade)==6 && AmazonAvoidance.EvadeChance(owner)==40,"Current hard1/item5/effective6=40");
        Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.Evade) && !SkillBindings.Instance.Assign(owner,false,CombatAbility.Evade) && !SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.Evade),"Passive active binding");
        Movement(owner,items[3]);
        var other=Fresh();Train(other,"amazon.evade");Require(AmazonAvoidance.EvadeChance(other)==18,"Base rank1");
        Require(ClassCreation.Profiles.TryActivateHistory(other,DiabloClass.Paladin) && AmazonAvoidance.EvadeChance(other)==0,"Wrong class Evade");
        Require(ClassCreation.Profiles.TryActivateHistory(other,DiabloClass.Amazon) && AmazonAvoidance.EvadeChance(other)==18,"History Evade");
        ClassCreation.Profiles.TryGet(other.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(other,profile);Require(AmazonAvoidance.EvadeChance(other)==0,"Respec Evade");other.Delete();
        Natural(owner,items);
    }
    private static void Natural(PlayerMobile owner,Item[] items)
    {
        _owner=owner;_items=items;_origin=owner.Location;_phase=0;_original=AmazonAvoidance.Roll;
        _network=NetState.Instances.Single(x=>x.Mobile==null && IPAddress.IsLoopback(x.Address));
        _network.Seeded=true;_network.CompressionEnabled=false;_network.Mobile=owner;owner.NetState=_network;
        var protocol=typeof(NetState).GetField("_protocolState",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
        Require(protocol!=null,"Connected fixture protocol");protocol.SetValue(_network,Enum.Parse(protocol.FieldType,"GameServer_LoggedIn"));_network.ResetMovementState();_network.SendMobileUpdate(owner);
        AmazonAvoidance.Roll=max=>{Require(max==100,"Natural Evade RNG bound");_calls++;_walkingAtDraw=ClassMovement.IsMoving(owner,Core.TickCount) && !ClassMovement.IsRunning(owner,Core.TickCount);Log.Information("Evade natural draw phase={Phase} now={Now} until={Until} location={Location} step={Step} run={Run}",_phase,Core.TickCount,ClassCombat.For(owner).RunningUntil,owner.Location,ClassCombat.For(owner).StepPosition,ClassCombat.For(owner).RunningStep);return _draw;};Next();
    }
    private static void Step(bool run)
    {
        var owner=_owner;var before=owner.Location;var direction=owner.X>_origin.X?Direction.West:Direction.East;owner.Direction=direction;
        Require(owner.Move(direction|(run?Direction.Running:0)) && owner.Location!=before,"Natural accepted movement");
        _stepped=true;_runningStep=ClassMovement.IsRunning(owner,Core.TickCount);
        Log.Information("Evade natural step phase={Phase} now={Now} until={Until} location={Location} delay={Delay}",_phase,Core.TickCount,ClassCombat.For(owner).RunningUntil,owner.Location,owner.ComputeMovementSpeed(Direction.East,false));
        Require(ClassMovement.IsMoving(owner,Core.TickCount) && _runningStep==run,"Natural movement mode");
    }
    private static void WalkUntilImpact()
    {
        // Timer service may release a newly loaded caster late. Keep taking
        // actual steps until its impact instead of extending a fixture lease.
        if(_phase!=0 || _calls!=0 || Core.TickCount>=_deadline){return;}
        Step(false);Later(TimeSpan.FromMilliseconds(200),WalkUntilImpact);
    }
    private static void Next()
    {
        var owner=_owner;owner.MoveToWorld(_origin,Map.Trammel);Standing(owner);owner.Hits=owner.HitsMax;owner.Mana=0;
        _before=SkillHealth.Balance256(owner);_mana=NecromancerEffects.ManaBalance256(owner);_calls=0;_walkingAtDraw=_stepped=_runningStep=false;_deadline=Core.TickCount+6000;_draw=_phase==1?99:0;
        var point=new Point3D(owner.X+4,owner.Y,owner.Z);
        if(_phase==0)
        {
            var source=new GraveyardLich{CantWalk=true,NoKillAwards=true,Name="EvadeFixtureCaster"};source.AIObject.AITimer.Stop();source.MoveToWorld(point,owner.Map);_source=source;
            Require(source.BeginFirebolt(owner),"Natural delayed firebolt");Later(TimeSpan.FromMilliseconds(500),WalkUntilImpact);
        }
        else
        {
            var source=new EncounterCreature{CantWalk=true,NoKillAwards=true};source.AIObject.AITimer.Stop();source.MoveToWorld(point,owner.Map);_source=source;
            Require(EliteCombat.Enemy(source,owner,18),"Natural lightning enemy");EliteBolts.Lightning(source);Step(_phase==2);
        }
        Later(TimeSpan.FromSeconds(1.1),Observe);
    }
    private static void Observe()
    {
        bool run=_phase==2;
        if(!run && _calls==0 && Core.TickCount<_deadline){Later(TimeSpan.FromMilliseconds(200),Observe);return;}
        if(!run && _calls>0 && ClassMovement.IsMoving(_owner,Core.TickCount) && Core.TickCount<_deadline)
        {Later(TimeSpan.FromMilliseconds(200),Observe);return;}
        bool avoided=_phase is 0 or 3;var owner=_owner;
        Require(_stepped && _runningStep==run && _calls==(run?0:1) && (run || _walkingAtDraw)
            && (avoided?SkillHealth.Balance256(owner)==_before:SkillHealth.Balance256(owner)<_before)
            && NecromancerEffects.ManaBalance256(owner)==_mana,"Natural Evade phase "+_phase+" calls="+_calls+" walking="+_walkingAtDraw);
        Require(!ClassMovement.IsMoving(owner,Core.TickCount),"Natural movement lease failed to expire");
        if(_source is EncounterCreature encounter){EliteBolts.Cancel(encounter);}_source.Delete();_source=null;
        if(++_phase<4){Next();return;}
        Train(owner,"amazon.evade",20);Require(SkillMechanics.Rank(owner,CombatAbility.Evade)==25 && AmazonAvoidance.EvadeChance(owner)==58
            && !ClassCreation.Profiles.TryTrain(owner.Serial,"amazon.evade",out _),"Saved hard20/soft25/cap");
        owner.MoveToWorld(_origin,Map.Trammel);Standing(owner);owner.Hits=owner.HitsMax;owner.Mana=0;
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,_items.Select(x=>x.Serial.Value).ToArray(),_origin,NecromancerEffects.ManaBalance256(owner))));
        Cleanup();Log.Information("amazon-evade-result: passed");
    }
    private static void Reload()
    {
        Goldens();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=World.FindMobile((Serial)saved.Owner) as PlayerMobile;
        Require(owner!=null && saved.Items.Length==5 && saved.Items.All(x=>World.FindItem((Serial)x)?.Parent==owner) && ClassCreation.Profiles.GetRank(owner.Serial,"amazon.evade")==20,"Saved Evade rank/gear");
        if(owner.Map==Map.Internal){owner.MoveToWorld(saved.Origin,Map.Trammel);}
        Require(AmazonAvoidance.EvadeChance(owner)==58 && NecromancerEffects.ManaBalance256(owner)==saved.Mana && !ClassMovement.IsMoving(owner,Core.TickCount),"Saved effective chance/resource/transient movement");
        var items=saved.Items.Select(x=>World.FindItem((Serial)x)).ToArray();Movement(owner,items[3]);Natural(owner,items);
    }
}
