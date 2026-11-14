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

public static class GoatmanNativeCombatChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GoatmanNativeCombatChecks));
    private sealed record Saved(int Generation,uint Owner,uint Body,string Birth,DeathMarkSavedActor Controller,NpcEquipmentSeed Random,int Life);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"goatman-native-combat-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true}){bool reload=again;string name=reload?"goatman-native-combat-reload-check":"goatman-native-combat-check";
            ConsoleInputHandler.RegisterCommand(name,"Connected original Goatman timed combat executor",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>Scene(n,!reload,name+"-result"));}
                catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
    }
    private static GenericPetBirthPath Fixture(SkeletonGuardian b)
    {
        // Explicit walking-qualified fixture input,not a production constructor.
        var path=new GenericPetCommitPath{X=((uint)(b.X*2)<<16)|32768,Y=((uint)(b.Y*2)<<16)|32768,Flags=1,
            Room=GenericPetNativePlacement.RoomHandle(b.Map,b.X*2,b.Y*2),Size5c=2,Mask60=0x100,Mask64=0x3401,Actor=new(checked((int)b.Serial.Value),1)};
        return new(new(new(742,0),true,1,742,2,2,0),path,1,1,TimedEffects.Now);
    }
    private static ulong HitSeed()
    {
        for(int i=0;i<10000;i++){ulong seed=SourceRandom.InitLow(i),next=seed;if(SourceRandom.Limited(ref next,100)==0){return seed;}}
        throw new InvalidOperationException("No hit control seed");
    }
    private static void Scene(NetState network,bool retain,string result)
    {
        var p=new PlayerMobile{Name="Goatman timed combat fixture",Player=true};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,47);
        ClassSkillChecks.Train(p,"warlock.summongoatman",6);ClassSkillChecks.Train(p,"warlock.demonicmastery",16);
        FixtureSpace.Corridor(p,6600,24,3560);var account=FixtureNetwork.Authenticate(network,p,"goatman-timing-");
        WarlockGoatmanBody b=null;GoatmanNpcContactChecks.Recipient t=null;bool kept=false;var stuns=CombatStuns.Snapshot();
        try
        {
            DateTime at=TimedEffects.Now;b=WarlockGoatmanBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),at,new(1,Fixture));
            t=new(){Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(50000);t.Hits=t.HitsMax;t.MoveToWorld(new(b.X+1,b.Y,b.Z),b.Map);t.AIObject?.AITimer.Stop();
            var host=new GoatmanNativeCombat(b,p);int frame=20000000,executed=0;
            DateTime Time(int f)=>at.AddTicks((long)(f-20000000)*SourceFrameTimeline.IntervalTicks);
            var leaves=new GenericPetActionServices((_,_,_)=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),
                (_,_)=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),
                ()=>throw new InvalidOperationException(),(_,_,_,_,_)=>throw new InvalidOperationException(),()=>throw new InvalidOperationException(),
                _=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),(_,_,_,_,_,_)=>throw new InvalidOperationException(),
                _=>throw new InvalidOperationException(),_=>throw new InvalidOperationException(),(_,_)=>throw new InvalidOperationException(),
                _=>throw new InvalidOperationException(),_=>throw new InvalidOperationException());
            var bound=host.Bind(leaves,()=>frame,()=>Time(frame));
            foreach(int skill in new[]{0,416,418,417,419})
            {
                CombatStuns.Restore(stuns);SkillHealth.SetBalance256(t,20000*256);t.Notifications=0;NpcEquipmentRandom.Set(b,HitSeed());
                int before=SkillHealth.Balance256(t),mana=p.Mana;ulong random=NpcEquipmentRandom.State(b).Seed;
                bool selected=skill==0?bound.ExecuteMode(bound.InitializeMode(4) with{Target=checked((int)t.Serial.Value)})
                    :bound.ExecuteSkill((byte)(skill==417?5:4),skill,checked((int)t.Serial.Value),0,0,true);
                var a=host.Current;Require(selected&&a!=null,"Source choice did not bind to timed executor");
                Require(host.Current==a&&DeathMarkWorld.State(b).Mode==a.Mode&&a.ReleaseFrame>frame&&a.EndFrame>a.ReleaseFrame
                    &&t.Notifications==0&&SkillHealth.Balance256(t)==before,"Timed start damaged target or lost captured mode/clock");
                Require((skill is 417 or 419)==(a.Packet==null)&&(skill is 417 or 419)==(NpcEquipmentRandom.State(b).Seed==random),"Allocation boundary differs from startup/do2 and approved release rules");
                var prepared=a.Packet?.Impact.Packet;var entryRandom=NpcEquipmentRandom.State(b).Seed;
                for(int f=frame;f<a.ReleaseFrame;f++){host.Advance(f,Time(f));}
                Require(t.Notifications==0&&SkillHealth.Balance256(t)==before&&NpcEquipmentRandom.State(b).Seed==entryRandom,"Windup dealt damage or rerolled a packet");
                host.Advance(a.ReleaseFrame,Time(a.ReleaseFrame));int after=SkillHealth.Balance256(t);ulong releasedRandom=NpcEquipmentRandom.State(b).Seed;
                Require(a.Released&&a.Contacts==1&&after<before&&t.Notifications==1&&p.Mana==mana,"Contact did not deliver one mana-free actual hit");
                if(prepared!=null){Require(a.Packet.Consumed&&a.Result.Packet.Physical256==prepared.Physical256&&a.Result.Packet.Magic256==prepared.Magic256,"do2 reallocated the prepared packet");}
                if(skill==416){Require(CombatStuns.Remaining(t)>0,"Timed Stun did not reach native recipient");}
                if(skill==418){Require(a.Result.Packet.Physical256==0&&a.Result.Packet.Magic256>0,"Timed Berserk lost conversion");}
                if(skill==417){Require(b.Prepared.Frenzy is{Charges:1},"Timed successful Frenzy did not persist its own charge");}
                host.Advance(a.ReleaseFrame,Time(a.ReleaseFrame));host.Advance(a.EndFrame,Time(a.EndFrame));host.Advance(a.EndFrame+1,Time(a.EndFrame+1));
                Require(host.Current==null&&a.Finished&&!a.Cancelled&&DeathMarkWorld.State(b).Mode==1&&t.Notifications==1
                    &&NpcEquipmentRandom.State(b).Seed==releasedRandom&&SkillHealth.Balance256(t)==after,"Repeated/end delivery replayed a hit or left mode active");
                frame=a.EndFrame+2;executed++;
            }
            CombatStuns.Restore(stuns);
            // A movement smaller than a source cell/native tile still cancels.
            var moved=host.Open(t,416,frame,Time(frame));var path=DeathMarkWorld.Path(b);var point=b.Location;int hp=SkillHealth.Balance256(t);
            DeathMarkWorld.WalkPrepared(b,path.X+1,path.Y);Require(b.Location==point,"Fractional movement control changed native tile");
            host.Advance(frame+1,Time(frame+1));Require(host.Current==null&&moved.Cancelled&&moved.Packet.Consumed&&SkillHealth.Balance256(t)==hp,"Fractional source movement retained stale prepared contact");frame+=2;
            var frenzy=host.Open(t,417,frame,Time(frame));GoatmanNpcActionProposal.Advance(frenzy.Sweep,frame, new GoatmanNpcContact.World(b,p,()=>Time(frame)));
            DeathMarkWorld.WalkPrepared(b,path.X+1,path.Y);host.Advance(frame+1,Time(frame+1));Require(frenzy.Cancelled&&frenzy.Sweep.Cancelled,"Fractional movement did not cancel approved action");frame+=2;
            // The approved fan itself uses the exact source origin/locked aim.
            DeathMarkWorld.WalkPrepared(b,path.X-32771,path.Y);
            var cleave=host.Open(t,419,frame,Time(frame));var fan=GoatmanNpcActionProposal.Sweep(cleave.Sweep,ReviveMeleeGeometry.Unit(b).RawRange);
            Require(fan.X==(int)(path.X>>16)&&fan.Y==(int)(path.Y>>16)&&fan.X!=b.X*2,"Cleave reconstructed its origin from native tiles");host.Cancel();frame+=2;
            var mode=host.Open(t,0,frame,Time(frame));DeathMarkWorld.ChangeMode(b,8);host.Advance(frame+1,Time(frame+1));
            Require(mode.Cancelled&&DeathMarkWorld.State(b).Mode==8,"Cancel overwrote external mode transition");DeathMarkWorld.ChangeMode(b,1);frame+=2;
            var phase=host.Open(t,416,frame,Time(frame));ulong phaseSeed=NpcEquipmentRandom.State(b).Seed;bool refused=false;
            try{host.Advance(frame+1,Time(frame+1).AddTicks(1));}catch(ArgumentException){refused=true;}
            Require(refused&&host.Current==phase&&NpcEquipmentRandom.State(b).Seed==phaseSeed,"Mismatched clock phase partially delivered attack");host.Cancel();
            // Disconnect legitimately parks the roster. Run this final control
            // after active actions;reattaching NetState alone is not a source
            // path/reactivation transaction and must not pretend to resume it.
            frame+=2;var offline=host.Open(t,418,frame,Time(frame));network.Mobile=null;p.NetState=null;
            host.Advance(frame+1,Time(frame+1));Require(offline.Cancelled&&offline.Packet.Consumed&&SkillHealth.Balance256(t)==hp,"Offline attack delivered or leaked its prepared contact");
            network.Mobile=p;p.NetState=network;
            Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman),"Timed combat enabled incomplete summon AI");
            if(retain)
            {
                SkillHealth.SetBalance256(b,b.MaximumLife256-257);PersistentMinions.Suspend(p);b.RestorePrepared(b.Prepared with{NextRecoveryUtc=TimedEffects.Now.AddDays(2)});
                File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,b.Serial.Value,b.SavedText,DeathMarkWorld.State(b),NpcEquipmentRandom.State(b),SkillHealth.Balance256(b))));kept=true;
            }
            Log.Information("Goatman timed combat:{Executed} actual base/Stun/Berserk/Frenzy/Cleave actions;startup/release allocation,no windup HP or mana,once-only native damage/finish,source movement/offline/mode/clock cancellation;full AI host remains unfinished",executed);
            Log.Information("{Result}: passed",result);
        }
        catch(Exception e){Log.Error(e,"{Result}: failed",result);}
        finally
        {
            CombatStuns.Restore(stuns);t?.Delete();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
            if(!kept){b?.Delete();p.Delete();}
        }
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var b=World.FindMobile((Serial)s.Body)as WarlockGoatmanBody;
        Require(b!=null&&b.ControlMaster?.Serial.Value==s.Owner&&b.SavedText==s.Birth&&DeathMarkWorld.State(b)==s.Controller
            &&DeathMarkWorld.State(b).Mode==1&&NpcEquipmentRandom.State(b)==s.Random&&SkillHealth.Balance256(b)==s.Life,"Original idle actor/controller/Frenzy/wounds/RNG changed on reload");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
}
