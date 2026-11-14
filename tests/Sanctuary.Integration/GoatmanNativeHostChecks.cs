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

public static class GoatmanNativeHostChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GoatmanNativeHostChecks));
    private sealed record Saved(int Generation,uint Owner,uint Body,string Birth,DeathMarkSavedActor Controller,NpcEquipmentSeed Random,int Life,GoatmanHostSnapshot Host);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"goatman-native-host-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true}){bool reload=again;string name=reload?"goatman-native-host-reload-check":"goatman-native-host-check";
            ConsoleInputHandler.RegisterCommand(name,"Connected complete Goatman native AI/path/combat host candidate",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>Scene(n,!reload,name+"-result"));}
                catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static void Scene(NetState network,bool retain,string result)
    {
        var p=new PlayerMobile{Name="Goatman full host candidate fixture",Player=true};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,47);
        ClassSkillChecks.Train(p,"warlock.summongoatman",6);ClassSkillChecks.Train(p,"warlock.demonicmastery",16);
        FixtureSpace.Corridor(p,6560,28,3500);var account=FixtureNetwork.Authenticate(network,p,"goatman-host-");
        WarlockGoatmanBody b=null;GoatmanNpcContactChecks.Recipient t=null;BaseDoor door=null;bool kept=false;var stuns=CombatStuns.Snapshot();
        try
        {
            DateTime now=TimedEffects.Now;b=GoatmanNativeHostCandidate.Create(p,new(p.X+1,p.Y,p.Z),now);NpcEquipmentRandom.Set(b,SourceRandom.InitLow(173));
            t=new(){Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(50000);t.Hits=t.HitsMax;t.MoveToWorld(new(b.X+6,b.Y,b.Z),b.Map);t.AIObject?.AITimer.Stop();
            var host=new GoatmanNativeHostCandidate(b,now);var origin=b.Location;int before=SkillHealth.Balance256(t),mana=p.Mana;
            void Tick(int n){for(int i=0;i<n;i++){now=now.AddTicks(SourceFrameTimeline.IntervalTicks);host.Advance(now);}}
            for(int n=0;n<250&&t.Notifications<2;n++){Tick(1);}
            Require(host.Decisions>=2&&host.Movement.Paths>0&&host.Movement.Steps>0&&b.Location!=origin&&t.Notifications>=2
                &&SkillHealth.Balance256(t)<before&&p.Mana==mana,"Source AI entry did not acquire/approach/attack a real distant enemy through the complete candidate loop");
            int combatDecisions=host.Decisions,paths=host.Movement.Paths;var fighting=b.Location;t.Internalize();
            Tick(100);Require(host.Movement.Paths>paths&&host.Decisions>combatDecisions&&b.Location!=fighting,"Empty combat did not enter actual source follow/movement execution");
            p.MoveToWorld(new(6586,p.Y,p.Z),p.Map);Tick(1);
            for(int n=0;n<100&&host.Movement.Warps==0;n++){Tick(1);}
            Require(host.Movement.Warps>0&&b.GetDistanceToSqrt(p)<=4,"Over-leash source follow did not warp to actual owner space");
            var member=DeathMarkWorld.Capture().Worlds.Single(w=>w.Map==b.Map.MapID).Members.Single(m=>m.Handle==(int)b.Serial.Value);
            Require((member.Flags128&0x10000)!=0&&(member.PetFlags&0x10000)!=0,"Source warp flag0x128 was not published on the actual saved actor");
            var flags=new GoatmanNativeSearch(b,p);int bid=checked((int)b.Serial.Value);
            Require(flags.Search.Unit(bid).Flags128==member.Flags128&&flags.Traversal.Unit(bid).Flags128==member.Flags128
                &&flags.World.Unit(bid).Flags128==member.Flags128,"Native search/traversal readback lost the saved warp flags");
            // A real UO door runs through the source preprocessing pause/use.
            door=new MetalDoor(DoorFacing.NorthCW);door.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);
            for(int n=0;n<100&&!door.Open;n++){Tick(1);}
            Require(door.Open,"Source preprocessing did not open the actual adjacent native door");door.Delete();door=null;
            // A source stun pauses root decisions;native freeze cancels the
            // action/motion then re-arms future AI rather than losing its tick.
            CombatStuns.ApplyPreparedDuration(b,20,TimedEffects.Now);int decisions=host.Decisions;Tick(10);
            Require(host.Decisions==decisions,"Stunned native Goatman entered the default root");CombatStuns.Clear(b);Tick(10);
            b.Frozen=true;decisions=host.Decisions;Tick(10);Require(host.Decisions==decisions&&host.Attacks.Current==null,"Frozen native host kept acting");b.Frozen=false;Tick(20);
            Require(host.Decisions>decisions,"Thawed native host lost all future AI events");
            // Existing registered placement stays authoritative;the host must
            // discard its stale motion without replacing Death Mark's command.
            var point=new Point2D((b.X-2)*2,b.Y*2);var directive=new DeathMarkDirective{X=point.X,Y=point.Y,Remaining=1};
            Require(DeathMarkWorld.ApplyPrepared(b,directive,now),"Actual source/native Death Mark placement control failed");var placed=DeathMarkWorld.Path(b);
            Tick(1);Require(host.Movement.Motion.X==placed.X&&host.Movement.Motion.Y==placed.Y,"External registered placement retained stale motion");
            // Original wounds/Frenzy/RNG do not recover or reroll during an
            // offline interval. Reconnection goes through the real roster and
            // atomic source-room rebind before admitting a future AI frame.
            SkillHealth.SetBalance256(b,b.MaximumLife256-257);int life=SkillHealth.Balance256(b);var frenzy=b.Prepared.Frenzy;var seed=NpcEquipmentRandom.State(b);
            network.Mobile=null;p.NetState=null;now=now.AddHours(4);host.Advance(now);
            Require(b.Map==Map.Internal&&SkillHealth.Balance256(b)==life&&b.Prepared.Frenzy==frenzy&&NpcEquipmentRandom.State(b)==seed,"Inactive skip healed/reset/replayed the original body");
            network.Mobile=p;p.NetState=network;now=now.AddTicks(SourceFrameTimeline.IntervalTicks);host.Advance(now);
            Require(b.Map==p.Map&&SkillHealth.Balance256(b)==life&&b.Prepared.Frenzy==frenzy&&NpcEquipmentRandom.State(b)==seed
                &&DeathMarkWorld.State(b).Map==p.Map.MapID&&ReviveMeleeGeometry.Unit(b).HasRoom,"Actual roster return/source-room rebind changed combat state or failed geometry");
            Require(WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Learned Goatman route missing after approved host integration");
            if(retain)
            {
                PersistentMinions.Suspend(p);b.RestorePrepared(b.Prepared with{NextRecoveryUtc=host.Capture().Clock.NextUtc});
                File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,b.Serial.Value,b.SavedText,DeathMarkWorld.State(b),NpcEquipmentRandom.State(b),life,host.Capture())));kept=true;
            }
            Log.Information("Goatman host candidate:{Decisions} actual AI-entry/root decisions,{Paths} paths,{Steps} source moves,{Warps} source warps;real combat/follow/doors/stun/freeze/external-placement/offline/roster-rebind;no learned route or timer installed",host.Decisions,host.Movement.Paths,host.Movement.Steps,host.Movement.Warps);
            Log.Information("{Result}: passed",result);
        }
        catch(Exception e){Log.Error(e,"{Result}: failed",result);}
        finally
        {
            CombatStuns.Restore(stuns);door?.Delete();t?.Delete();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
            if(!kept){b?.Delete();p.Delete();}
        }
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var b=Server.World.FindMobile((Serial)s.Body)as WarlockGoatmanBody;
        Require(b!=null&&b.Map==Map.Internal&&b.ControlMaster?.Serial.Value==s.Owner&&b.SavedText==s.Birth&&DeathMarkWorld.State(b)==s.Controller
            &&NpcEquipmentRandom.State(b)==s.Random&&SkillHealth.Balance256(b)==s.Life,"Original host body/control/birth/Frenzy/wounds/randomness changed on load");
        var host=new GoatmanNativeHostCandidate(b,TimedEffects.Now,s.Host);host.Advance(s.Host.Clock.NextUtc.AddHours(1));
        Require(b.Map==Map.Internal&&host.Decisions==0&&host.Movement.Steps==0&&SkillHealth.Balance256(b)==s.Life&&NpcEquipmentRandom.State(b)==s.Random,"Loaded clock replayed offline actions or regeneration");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1,Birth=b.SavedText,Controller=DeathMarkWorld.State(b),Host=host.Capture()}));
    }
}
