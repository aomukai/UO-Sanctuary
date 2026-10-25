// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

public static class SentryCastChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SentryCastChecks));
    private sealed record Saved(uint Owner,uint Actor,Point3D Origin,int Map,SentryBodyState State,int Life256,int Mana256);
    private static bool _creating;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"sentry-cast-fixture.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Rates()
    {
        var previous=Mobile.ManaRegenRateHandler;
        Mobile.ManaRegenRateHandler=x=>_creating||x.Name?.StartsWith("SentryCastFixture",StringComparison.Ordinal)==true
            ?TimeSpan.FromHours(1):previous?.Invoke(x)??Mobile.DefaultManaRate;
    }
    public static void Configure(){Rates();Register("sentry-cast-check",Check);Register("sentry-cast-reload-check",Reload);}
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Unregistered Sentry free-cell do45 release, payment and real saved S1 actor",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Clear(PlayerMobile p)
    {foreach(var e in AssassinTrapRoster.For(p)){World.FindMobile((Serial)e.Actor)?.Delete();}}
    private static AssassinSentryBody Last(PlayerMobile p)=>(AssassinSentryBody)World.FindMobile((Serial)AssassinTrapRoster.For(p).Last().Actor);
    private static bool Cast(PlayerMobile p,Serial selected=default)
        =>AssassinSentryCasts.Release(p,selected,TimedEffects.Now.AddDays(1));
    private static void Check()
    {
        PlayerMobile p;_creating=true;try{p=BladeBodyChecks.Fresh(6480,30);}finally{_creating=false;}
        p.Name="SentryCastFixture";
        try
        {
            Require(Mobile.GetManaRegenRate(p)==TimeSpan.FromHours(1),"Fractional-payment fixture regeneration rate was replaced");
            var row=SkillMechanics.For(CombatAbility.ChargedBoltSentry);var values=SkillMechanics.Values(p,CombatAbility.ChargedBoltSentry);
            Require(values.Rank==1&&values.Mana256==3328&&row.Number("srvdofunc")==45&&row.Number("srvstfunc")==0
                &&row.Number("usemanaondo")==0&&row.Number("localdelay")==0&&row.Number("delay")==0
                &&AssassinSentryCasts.Timing(p)==LobProjectiles.Timing(p),"Pinned do45/resource/no-delay/shared S2 contract");
            var session=ClassCombat.For(p);var aim=new Point3D(p.X+6,p.Y,p.Z);session.PendingPoint=aim;
            int mana=NecromancerEffects.ManaBalance256(p);
            Require(!AssassinSentryCasts.Begin(p,Serial.Zero,aim,Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==mana
                &&session.PendingAbility==CombatAbility.None&&!ClassCombat.Available(p,CombatAbility.ChargedBoltSentry),"Unregistered casting exposed/paid a learned windup");
            NecromancerEffects.SetMana256(p,3327);
            Require(!Cast(p)&&NecromancerEffects.ManaBalance256(p)==3327&&AssassinTrapRoster.For(p).Length==0,"Unaffordable release created/paid a trap");
            NecromancerEffects.SetMana256(p,3328+127);
            foreach(var bad in new[]{new Point3D(0,aim.Y,aim.Z),new Point3D(aim.X,0,aim.Z),new Point3D(aim.X,aim.Y,p.Z+16),new Point3D(p.X+100,p.Y,p.Z)})
            {session.PendingPoint=bad;Require(!Cast(p)&&NecromancerEffects.ManaBalance256(p)==3455&&AssassinTrapRoster.For(p).Length==0,"Invalid point spent mana or fell back to caster");}
            session.PendingPoint=aim;var wall=new Static(0x80);wall.MoveToWorld(aim,p.Map);
            try
            {Require(!p.Map.CanSpawnMobile(aim)&&!Cast(p)&&NecromancerEffects.ManaBalance256(p)==3455&&AssassinTrapRoster.For(p).Length==0,"Obstructed unsupported fallback created/paid a trap");}
            finally{wall.Delete();}
            Require(Cast(p)&&NecromancerEffects.ManaBalance256(p)==127,"Rank1 did not pay current thirteen mana exactly once");
            var first=Last(p);
            Require(first.Location==aim&&first.SummonMaster==p&&first.Prepared.Control is{Mode:8,Frame:0,NextThink:25}
                &&first.Prepared.Control.EmergenceProfile==AssassinSentryEmergence.Profile&&first.Prepared.Creation.Rank==1
                &&FireGround.Ready(p,CombatAbility.ChargedBoltSentry),"Successful do45 did not create S1 at the resolved point or added a local delay");
            // No local delay: separate release requests are separate casts.
            // Shared player scheduling must dispatch each request only once.
            for(int i=0;i<4;i++){NecromancerEffects.SetMana256(p,3328);Require(Cast(p)&&NecromancerEffects.ManaBalance256(p)==0,"Independent no-delay cast failed/debited twice");}
            var order=AssassinTrapRoster.For(p).Select(x=>x.Actor).ToArray();Require(order.Length==5&&!first.Deleted,"Cap before replacement");
            wall=new Static(0x80);wall.MoveToWorld(aim,p.Map);NecromancerEffects.SetMana256(p,3328);
            try{Require(!Cast(p)&&AssassinTrapRoster.For(p).Select(x=>x.Actor).SequenceEqual(order)&&!first.Deleted
                &&NecromancerEffects.ManaBalance256(p)==3328,"Failed placement changed the full roster or paid mana");}
            finally{wall.Delete();}
            Require(Cast(p)&&first.Deleted&&AssassinTrapRoster.For(p).Length==5&&NecromancerEffects.ManaBalance256(p)==0,"Successful sixth cast did not evict oldest/pay once");Clear(p);
            Targets(p,aim);Persist(p,aim);p=null;
        }
        finally{if(p!=null){Clear(p);p.Delete();}}
    }
    private static void Targets(PlayerMobile p,Point3D cached)
    {
        // A hidden GM has no native blocking footprint, allowing explicit
        // free-cell targeting without claiming monster-overlap placement.
        var target=new PlayerMobile{Body=0x190,Player=true,Hidden=true,AccessLevel=AccessLevel.GameMaster};
        try
        {
            target.MoveToWorld(new(p.X+8,p.Y,p.Z),p.Map);ClassCombat.For(p).PendingPoint=cached;NecromancerEffects.SetMana256(p,3328);
            Require(Cast(p,target.Serial)&&Last(p).Location==target.Location,"Present target did not override cached point");Clear(p);
            target.MoveToWorld(target.Location,Map.Felucca);NecromancerEffects.SetMana256(p,3328);
            Require(!Cast(p,target.Serial)&&AssassinTrapRoster.For(p).Length==0&&NecromancerEffects.ManaBalance256(p)==3328,"Cross-map target silently used cached point");
            var serial=target.Serial;target.Delete();
            Require(Cast(p,serial)&&Last(p).Location==cached&&NecromancerEffects.ManaBalance256(p)==0,"Deleted target did not retain action aim");Clear(p);
        }
        finally{target.Delete();Clear(p);}
    }
    private static void Persist(PlayerMobile p,Point3D aim)
    {
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);
        p.AddToBackpack(helm);Require(p.EquipItem(helm),"Actual cast-rank equipment");ClassGear.Refresh(p);
        Require(SkillMechanics.Rank(p,CombatAbility.ChargedBoltSentry)==2,"Tarnhelm cast rank");
        ClassCombat.For(p).PendingPoint=aim;NecromancerEffects.SetMana256(p,3455);Require(Cast(p),"Saved real cast failed");var b=Last(p);
        p.AddToBackpack(helm);ClassGear.Refresh(p);
        Require(b.Prepared.Creation.Rank==2&&SkillMechanics.Rank(p,CombatAbility.ChargedBoltSentry)==1,"Later gear rewrote cast rank");
        for(int i=0;i<7;i++){b.AdvanceController(b.Prepared.NextFrameUtc,false);}
        SkillHealth.SetBalance256(b,12850);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,b.Serial.Value,p.Location,p.Map.MapID,b.Prepared,SkillHealth.Balance256(b),NecromancerEffects.ManaBalance256(p))));
        Require(!AssassinCombat.Implemented(CombatAbility.ChargedBoltSentry),"Prepared release enabled unfinished learned input");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var b=(AssassinSentryBody)World.FindMobile((Serial)saved.Actor);
        Require(JsonSerializer.Serialize(b.Prepared)==JsonSerializer.Serialize(saved.State)&&SkillHealth.Balance256(b)==saved.Life256
            &&NecromancerEffects.ManaBalance256(p)==saved.Mana256&&saved.Mana256==127&&b.Prepared.Creation.Rank==2
            &&b.Prepared.Control is{Mode:8,Frame:7,NextThink:25},$"Actual paid cast changed rank/phase/injury/resources on reload: stateEqual={JsonSerializer.Serialize(b.Prepared)==JsonSerializer.Serialize(saved.State)} life={SkillHealth.Balance256(b)}/{saved.Life256} mana={NecromancerEffects.ManaBalance256(p)}/{saved.Mana256} rank={b.Prepared.Creation.Rank} phase={b.Prepared.Control.Mode}/{b.Prepared.Control.Frame}/{b.Prepared.Control.NextThink}");
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);
        b.AdvanceController(b.Prepared.NextFrameUtc.AddMilliseconds(17*40),false);
        Require(b.Prepared.Control is{Mode:1,Frame:25}&&NecromancerEffects.ManaBalance256(p)==saved.Mana256,"Offline cast resume replayed payment or froze phase");
        Clear(p);Persist(p,saved.State.Position);
    }
}
