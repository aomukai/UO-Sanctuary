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
public static class BladeWarpChecks
{
    private const CombatAbility Ability=CombatAbility.BladeWarp;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BladeWarpChecks));
    private sealed record Saved(uint Owner,uint Weapon,bool Female,int Mana,DateTime Cooldown);
    private sealed record Manifest(int Generation,Saved[] Owners);
    private static string FileName=>Path.Join(Core.BaseDirectory,"blade-warp-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Approved Blade Warp adaptation",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Register("blade-warp-check",()=>Check(false));Register("blade-warp-reload-check",()=>Check(true));
        foreach(string choice in new[]{"left","right","quick"}){string mode=choice;Register("blade-warp-"+mode+"-check",()=>FixtureNetwork.Connected("blade-warp-"+mode+"-result",n=>Input(n,mode)));}
    }
    public static void Initialize()
    {var mana=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name!="BladeWarpSaved"&&mana?.Invoke(p)!=false;}
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;FireGround.Delay(p,Ability,TimedEffects.Now.AddSeconds(-10));}
    private static PlayerMobile Fresh(bool female,int x)
    {
        var p=new PlayerMobile{Player=true,Female=female,Body=female?0x191:0x190,Name="BladeWarpFixture"};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"warlock.bladewarp");
        while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Warp strength");}
        FixtureSpace.Corridor(p,x,24,3500);var w=new Broadsword();p.AddToBackpack(w);Require(p.EquipItem(w),"Warp melee equip");ClassGear.Refresh(p);
        NecromancerEffects.SetMana256(p,p.ManaMax*256);Ready(p);return p;
    }
    private static ShockWaveTarget Enemy(PlayerMobile p,int dx,int dy=0)
    {
        var t=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};t.MoveToWorld(new(p.X+dx,p.Y+dy,p.Z),p.Map);t.AIObject.AITimer.Stop();
        t.SetHits(10000);SkillHealth.SetBalance256(t,t.HitsMax*256);return t;
    }
    private static void Cast(PlayerMobile p,Point3D at,Serial selected=default)
    {
        Ready(p);Require(ClassCombat.Perform(p,Ability,World.FindMobile(selected),at,Core.TickCount),"Warp cast start");var s=ClassCombat.For(p);
        Require(s.Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p)),"Warp ordinary SC clock");
        Require(ClassCombat.ResolveAction(p,s.EffectAt)&&!ClassCombat.ResolveAction(p,s.EffectAt),"Warp one release");
    }
    private static void Controls(PlayerMobile p)
    {
        Require(WarlockCombat.Implemented(Ability)&&ClassCombat.Available(p,Ability)&&SkillBindings.Instance.Assign(p,true,Ability)
            &&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Warp learned/mouse/quick availability");
        Require(!SkillBindings.MouseAllowed(DiabloClass.Warlock,true,CombatAbility.PsychicWard)&&WarlockCombat.Handles(CombatAbility.PsychicWard),"Ward binding/registration regression");
        int mana=NecromancerEffects.ManaBalance256(p);var origin=p.Location;var aim=new Point3D(p.X+8,p.Y,p.Z);
        Require(ClassCombat.Perform(p,Ability,null,aim,Core.TickCount),"Warp windup start");var s=ClassCombat.For(p);long at=s.EffectAt;
        Require(!ClassCombat.ResolveAction(p,at-1)&&BladeWarp.Snapshot().Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Warp early payment/launch");
        ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at)&&BladeWarp.Snapshot().Length==0,"Warp Stop release");Ready(p);
        Require(ClassCombat.Perform(p,Ability,null,aim,Core.TickCount),"Warp late mana start");at=s.EffectAt;NecromancerEffects.SetMana256(p,3839);
        Require(!ClassCombat.ResolveAction(p,at)&&BladeWarp.Snapshot().Length==0&&NecromancerEffects.ManaBalance256(p)==3839,"Warp late mana debited/launched");
        Ready(p);NecromancerEffects.SetMana256(p,mana);Require(ClassCombat.Perform(p,Ability,null,aim,Core.TickCount),"Warp move windup");at=s.EffectAt;p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);
        Require(!ClassCombat.ResolveAction(p,at)&&BladeWarp.Snapshot().Length==0,"Warp movement windup survived");p.MoveToWorld(origin,p.Map);Ready(p);
        var allocate=BladeWarp.Allocate;
        try{Require(ClassCombat.Perform(p,Ability,null,aim,Core.TickCount),"Warp allocation start");BladeWarp.Allocate=()=>false;
            Require(!ClassCombat.ResolveAction(p,s.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana&&BladeWarp.Snapshot().Length==0,"Warp failed allocation paid");}
        finally{BladeWarp.Allocate=allocate;}
        var weapon=CombatTiming.Equipped(p);p.AddToBackpack(weapon);Ready(p);Require(!ClassCombat.Perform(p,Ability,null,aim,Core.TickCount),"Unarmed Warp accepted");
        var bow=new Bow();p.AddToBackpack(bow);Require(p.EquipItem(bow),"Warp bow equip");Ready(p);Require(!ClassCombat.Perform(p,Ability,null,aim,Core.TickCount),"Ranged Warp accepted");
        p.AddToBackpack(bow);bow.Delete();Require(p.EquipItem(weapon),"Warp sword restore");ClassGear.Refresh(p);Ready(p);
        Cast(p,aim);var shot=BladeWarp.Snapshot().Single();Require(shot.Minimum256==SkillMechanics.Values(p,Ability).Damage256(false)&&shot.Maximum256==SkillMechanics.Values(p,Ability).Damage256(true)
            &&shot.Remaining==20&&shot.Aim==aim&&p.Location==origin&&NecromancerEffects.ManaBalance256(p)==mana-3840&&!FireGround.Ready(p,Ability),"Warp snapshot/payment/cooldown");
        // Frozen birth damage survives hard-point changes after release.
        ClassSkillChecks.Train(p,"warlock.echoingstrike",2);Require(BladeWarp.Snapshot().Single()==shot,"Warp release snapshot changed with training");
        BladeWarp.Tick(shot.NextUtc.AddSeconds(1),true);Require(BladeWarp.Snapshot().Length==0&&p.Location==aim,"Empty-ground Warp missed its clicked landing");
        Require(NecromancerEffects.ManaBalance256(p)==mana-3840,"Warp landing paid again");p.MoveToWorld(origin,p.Map);
        Ready(p);NecromancerEffects.SetMana256(p,mana);
        Log.Information("Blade Warp controls:SC cancellation,late mana/allocation failure,melee restriction,one3840FP payment,20frame birth snapshot,cooldown and empty-ground landing pass.");
    }
    private static void Combat(PlayerMobile p)
    {
        var origin=p.Location;var first=Enemy(p,4);var splash=Enemy(p,4,1);var far=Enemy(p,10);var last=BladeWarp.Roll;BladeWarp.Roll=_=>0;
        try
        {
            int a=SkillHealth.Balance256(first),b=SkillHealth.Balance256(splash),c=SkillHealth.Balance256(far);var aim=new Point3D(p.X+12,p.Y,p.Z);
            Cast(p,aim);var shot=BladeWarp.Snapshot().Single();p.AddToBackpack(CombatTiming.Equipped(p));BladeWarp.Tick(shot.NextUtc.AddSeconds(1),true);
            Require(SkillHealth.Balance256(first)<a&&SkillHealth.Balance256(splash)<b&&SkillHealth.Balance256(far)==c,"Warp first-contact/radius damage");
            Require(p.Location!=origin&&p.Location!=aim&&p.GetDistanceToSqrt(first)<=3&&Teleportation.Space(p.Map,p.Location,p),"Warp impact free landing");
            int hit=SkillHealth.Balance256(first);BladeWarp.Tick(shot.NextUtc.AddSeconds(2),true);Require(SkillHealth.Balance256(first)==hit,"Warp impact replayed");
            var weapon=p.Backpack.FindItemByType<Broadsword>();Require(p.EquipItem(weapon),"Warp sword reequip after released flight");ClassGear.Refresh(p);
            first.Delete();splash.Delete();far.Delete();p.MoveToWorld(origin,p.Map);Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);
            Cast(p,new(p.X+8,p.Y,p.Z));shot=BladeWarp.Snapshot().Single();BladeWarp.Tick(shot.NextUtc,false);Require(BladeWarp.Snapshot().Length==0&&p.Location==origin,"Offline Warp moved owner");
            Ready(p);Cast(p,new(p.X+8,p.Y,p.Z));shot=BladeWarp.Snapshot().Single();var map=p.Map;p.MoveToWorld(p.Location,Map.Felucca);
            BladeWarp.Tick(shot.NextUtc,true);Require(BladeWarp.Snapshot().Length==0,"Facet Warp survived");p.MoveToWorld(origin,map);
        }
        finally{BladeWarp.Roll=last;first.Delete();splash.Delete();far.Delete();BladeWarp.Clear(p);Ready(p);}
        Log.Information("Blade Warp combat:first hostile collision,magic splash once,free impact landing,birth survives gear change,offline/facet cancellation pass.");
    }
    private static void Geometry(PlayerMobile p)
    {
        var origin=p.Location;var aim=new Point3D(p.X+8,p.Y,p.Z);
        int wallId=Enumerable.Range(1,TileData.MaxItemValue).First(id=>(TileData.ItemTable[id].Flags&(TileFlag.Wall|TileFlag.NoShoot))!=0&&TileData.ItemTable[id].Height>=16);
        var wall=new Static(wallId);wall.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);
        try{Ready(p);NecromancerEffects.SetMana256(p,p.ManaMax*256);Cast(p,aim);var shot=BladeWarp.Snapshot().Single();BladeWarp.Tick(shot.NextUtc.AddSeconds(1),true);
            Require(p.X>origin.X&&p.X<wall.X&&p.Location!=aim&&BladeWarp.Snapshot().Length==0,"Warp crossed missile wall");}
        finally{wall.Delete();p.MoveToWorld(origin,p.Map);BladeWarp.Clear(p);Ready(p);}
        var blockers=new System.Collections.Generic.List<ShockWaveTarget>();
        try{for(int x=-1;x<=1;x++)for(int y=-1;y<=1;y++){blockers.Add(Enemy(p,8+x,y));}
            Require(!BladeWarp.Landing(p,aim,out _),"Warp accepted wholly occupied landing neighborhood");}
        finally{foreach(var b in blockers){b.Delete();}}
        var doomed=Fresh(false,6720);
        try{var at=new Point3D(doomed.X+6,doomed.Y,doomed.Z);Cast(doomed,at);var shot=BladeWarp.Snapshot().Single();doomed.Kill();BladeWarp.Tick(shot.NextUtc,true);
            Require(BladeWarp.Snapshot().All(x=>x.Owner!=doomed.Serial.Value),"Dead owner retained Warp");}
        finally{BladeWarp.Clear(doomed);doomed.Delete();}
        Log.Information("Blade Warp geometry:actual wall stops flight,fully occupied landing refuses,owner death cancels.");
    }
    private static void Check(bool reload)
    {
        Manifest manifest;
        if(!reload)
        {
            var rows=new Saved[2];int i=0;
            foreach(bool female in new[]{false,true})
            {
                var p=Fresh(female,female?6520:6480);Controls(p);Combat(p);Geometry(p);p.Name="BladeWarpSaved";NecromancerEffects.SetMana256(p,p.ManaMax*256-517-i);
                var until=TimedEffects.Now.Add(FixtureSpace.Retention);var save=FireGround.Snapshot();FireGround.Restore(save with{Cooldowns=save.Cooldowns.Where(x=>x.Owner!=p.Serial.Value||x.Ability!=Ability).Append(new(p.Serial.Value,Ability,until)).ToArray()});
                rows[i++]=new(p.Serial.Value,CombatTiming.Equipped(p).Serial.Value,female,NecromancerEffects.ManaBalance256(p),until);
            }
            manifest=new(0,rows);
        }
        else
        {
            var old=JsonSerializer.Deserialize<Manifest>(File.ReadAllText(FileName));Require(old.Generation is 0 or 1,"Warp reload generation");Require(BladeWarp.Snapshot().Length==0,"Warp transient replayed on process restart");
            foreach(var row in old.Owners)
            {
                var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);Require(p.Female==row.Female&&CombatTiming.Equipped(p)?.Serial.Value==row.Weapon&&NecromancerEffects.ManaBalance256(p)==row.Mana
                    &&FireGround.Snapshot().Cooldowns.Any(c=>c.Owner==row.Owner&&c.Ability==Ability&&c.UntilUtc==row.Cooldown),"Warp original saved gear/mana/cooldown");
                FixtureSpace.Corridor(p,row.Female?6520:6480,24,3500);Combat(p);NecromancerEffects.SetMana256(p,row.Mana);
                var save=FireGround.Snapshot();FireGround.Restore(save with{Cooldowns=save.Cooldowns.Where(x=>x.Owner!=row.Owner||x.Ability!=Ability).Append(new(row.Owner,Ability,row.Cooldown)).ToArray()});
            }
            manifest=old with{Generation=old.Generation+1};
        }
        File.WriteAllText(FileName,JsonSerializer.Serialize(manifest));Log.Information("Blade Warp original:Warlock/Witch original gear,mana and saved shared cooldown persist across two reloads;no transient flight replay.");
    }
    private static void Input(NetState network,string mode)
    {
        var p=Fresh(mode=="quick",6640);var aim=new Point3D(p.X+6,p.Y,p.Z);var origin=p.Location;int mana=NecromancerEffects.ManaBalance256(p);bool began=false,done=false;long deadline=Core.TickCount+6000;
        Require(SkillBindings.Instance.Assign(p,mode=="left",Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Warp wire bindings");var account=FixtureNetwork.Authenticate(network,p,"WarpWire");Ready(p);string label="blade-warp-"+mode;
        void Cleanup(){ClassCombat.Stop(p);BladeWarp.Clear(p);p.Delete();account.Delete();}
        void Inspect()
        {
            if(done){return;}try
            {
                if(Core.TickCount>=deadline){throw new InvalidOperationException("Warp wire timeout");}var s=ClassCombat.For(p);
                if(!began&&s.PendingAbility==Ability){Require(p.Location==origin&&NecromancerEffects.ManaBalance256(p)==mana,"Warp wire paid during windup");began=true;Log.Information("{Label}: windup",label);}
                if(p.Location!=origin)
                {Require(began&&p.Location==aim&&NecromancerEffects.ManaBalance256(p)==mana-3840&&!FireGround.Ready(p,Ability),"Warp wire natural impact/payment");done=true;Cleanup();Log.Information("{Label}-result: passed",label);return;}
                Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Warlock,0,aim.X,aim.Y,aim.Z);Inspect();}));
    }
}
