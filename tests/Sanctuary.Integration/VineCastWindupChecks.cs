// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class VineCastWindupChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(VineCastWindupChecks));
    private static readonly CombatAbility[] Skills=[CombatAbility.PoisonCreeper,CombatAbility.CarrionVine,CombatAbility.SolarCreeper];
    private sealed record Actor(uint Owner,uint Body,CombatAbility Ability,string State,int Life,int Mana);
    private sealed record Saved(Actor[] Actors,uint PendingOwner,int PendingMana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"vine-cast-windup-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("vine-cast-windup-check",Check);Register("vine-cast-windup-reload-check",Reload);
        Register("vine-cast-windup-live-check",()=>FixtureClock.NextLoop("vine-cast-windup-live-result",()=>FixtureNetwork.Connected("vine-cast-windup-live-result",Live)));
    }
    private static void Register(string name,Action run)=>ConsoleInputHandler.RegisterCommand(name,"Prepared shared do115 SC/FCR windup and retained public gate",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static PlayerMobile Fresh()
    {
        var p=RecyclerVineChecks.Fresh();p.Name="VineWindupFixture";FixtureSpace.Corridor(p,6940,24,1800);
        while(p.RawStr<60){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Vine gear Strength");}
        NecromancerEffects.SetMana256(p,p.ManaMax*256);return p;
    }
    private static RecyclerRecoveryModel? Model(CombatAbility ability)=>ability==CombatAbility.PoisonCreeper?null:ability==CombatAbility.CarrionVine?RecyclerRecoveryModel.LegacySource:RecyclerRecoveryModel.CurrentTableSolarProvisional;
    private static SkeletonGuardian[] Own(PlayerMobile p)=>(p.AllFollowers??[]).OfType<SkeletonGuardian>().Where(x=>!x.Deleted&&DruidVines.Source(x.SourceMonsterId)).ToArray();
    private static string State(SkeletonGuardian b)=>b is DruidCreeperBody c?c.SavedText:((DruidRecyclerBody)b).SavedText;
    private static void Hold(SkeletonGuardian b)
    {
        DateTime at=TimedEffects.Now.AddDays(2);
        if(b is DruidCreeperBody c){c.RestorePrepared(c.Prepared with{Controller=c.Prepared.Controller with{NextUtc=at},NextRecoveryUtc=at});}
        else{var r=(DruidRecyclerBody)b;r.RestorePrepared(r.Prepared with{Controller=r.Prepared.Controller with{NextUtc=at},NextRecoveryUtc=at});}
    }
    private static void End(PlayerMobile p){ClassCombat.Stop(p);DruidCreeperGround.Cancel(p);DruidRecyclerRecovery.Cancel(p);PersistentMinions.Dismiss(p);p.Delete();}
    private static bool Begin(PlayerMobile p,CombatAbility a,Serial selected,Point3D point,long? now=null)
        =>DruidVineCasting.BeginPrepared(p,a,selected,point,now??Core.TickCount,Model(a));
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);ClassCombat.For(p).NextAction=Core.TickCount-1;}
    private static void ClockAndTransaction()
    {
        foreach(var a in Skills)
        {
            var p=Fresh();var target=new GraveyardZombie{CantWalk=true,NoKillAwards=true};target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);target.AIObject?.AITimer.Stop();
            try
            {
                Require(!DruidCombat.Implemented(a)&&!ClassCombat.Available(p,a)&&!SkillBindings.Instance.Assign(p,false,a),"Prepared vine escaped public availability gate");
                int mana=NecromancerEffects.ManaBalance256(p);Require(Begin(p,a,target.Serial,target.Location),"Prepared vine begin");var action=ClassCombat.For(p);
                Require(action.Timing==new ActionTiming(19,10)&&action.VineCast?.Ability==a&&action.PendingAbility==a&&Own(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Source human SCclock or early mutation");
                Require(!ClassCombat.ResolveAction(p,action.EffectAt-1)&&Own(p).Length==0,"Early vine release");
                target.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);Require(ClassCombat.ResolveAction(p,action.EffectAt),"Prepared vine effect callback");var body=Own(p).Single();Hold(body);
                int cost=SkillMechanics.Values(p,a).Mana256;
                Require(body.Location==target.Location&&NecromancerEffects.ManaBalance256(p)==mana-cost&&action.VineCast==null&&action.PendingAbility==CombatAbility.None,"Release aim/payment/single consumption");
                Require(!ClassCombat.ResolveAction(p,action.EffectAt)&&!Begin(p,a,Serial.Zero,p.Location,action.NextAction-1)&&Own(p).Single()==body&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Duplicate effect or cycle-tail reentry");
                Ready(p);var glove=PoisonDaggerChecks.Named(p,"Magefist");Require(CombatTiming.Fcr(p)==20&&Begin(p,a,Serial.Zero,p.Location),"Real20FCR vine begin");
                Require(ClassCombat.For(p).Timing==new ActionTiming(16,9),"Real FCR source cast clock");ClassCombat.Stop(p);glove.Delete();ClassGear.Refresh(p);
                foreach(var form in new[]{CombatAbility.Werewolf,CombatAbility.Werebear})
                {
                    ClassSkillChecks.Train(p,SkillMechanics.For(form).TalentId);Require(DruidPlayerForms.ApplyPrepared(p,form,TimedEffects.Now),"Prepared existing form");Ready(p);
                    Require(Begin(p,a,Serial.Zero,p.Location)&&ClassCombat.For(p).Timing==(form==CombatAbility.Werewolf?new ActionTiming(17,8):new ActionTiming(17,9)),"Vine cast did not use current form SC");
                    DruidPlayerForms.Clear(p);Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&ClassCombat.For(p).VineCast==null,"Changed form retained vine windup");
                }
            }
            finally{target.Delete();End(p);}
        }
    }
    private static void Cancellation()
    {
        foreach(var a in Skills)foreach(int mode in new[]{0,1,2,3,4})
        {
            var p=Fresh();try
            {
                Require(Begin(p,a,Serial.Zero,p.Location),"Cancellation begin");var action=ClassCombat.For(p);int mana=NecromancerEffects.ManaBalance256(p);long at=action.EffectAt;
                if(mode==0){ClassCombat.Stop(p);}else if(mode==1){p.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);}
                else if(mode==2){p.Frozen=true;}else if(mode==3){PoisonDaggerChecks.Dagger(p);}else{ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);}
                Require(!ClassCombat.ResolveAction(p,at)&&action.VineCast==null&&Own(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Canceled vine paid or allocated: "+a+"/"+mode);
            }
            finally{p.Frozen=false;End(p);}
        }
    }
    private static void ReleaseFailure()
    {
        foreach(var a in Skills)
        {
            var p=Fresh();try
            {
                var point=new Point3D(p.X+3,p.Y,p.Z);Require(Begin(p,a,Serial.Zero,point)&&ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Original paid vine");var old=Own(p).Single();Hold(old);SkillHealth.SetBalance256(old,1111);Ready(p);
                Require(Begin(p,a,Serial.Zero,point),"Lost-mana begin");int cost=SkillMechanics.Values(p,a).Mana256;NecromancerEffects.SetMana256(p,cost-1);
                Require(!ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&ClassCombat.For(p).VineCast==null&&Own(p).Single()==old&&SkillHealth.Balance256(old)==1111&&NecromancerEffects.ManaBalance256(p)==cost-1,"Lost mana paid/replaced/healed original");
                NecromancerEffects.SetMana256(p,p.ManaMax*256);Ready(p);Require(Begin(p,a,Serial.Zero,point),"Live-rank begin");int mana=NecromancerEffects.ManaBalance256(p);PoisonDaggerChecks.Named(p,"Tarnhelm");
                int live=SkillMechanics.Values(p,a).Mana256;Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&old.Deleted&&NecromancerEffects.ManaBalance256(p)==mana-live,"Current release rank or effective-rank Solar price");Hold(Own(p).Single());
            }
            finally{End(p);}
        }
        var owner=Fresh();try
        {
            int mana=NecromancerEffects.ManaBalance256(owner);bool refused=false;
            try{DruidVineCasting.BeginPrepared(owner,CombatAbility.SolarCreeper,Serial.Zero,owner.Location,Core.TickCount,null);}catch(NotSupportedException){refused=true;}
            Require(refused&&ClassCombat.For(owner).VineCast==null&&NecromancerEffects.ManaBalance256(owner)==mana,"Missing Solar recovery profile guessed");
        }
        finally{End(owner);}
    }
    private static void Check()
    {
        ClockAndTransaction();Cancellation();ReleaseFailure();var saved=new Actor[3];
        for(int i=0;i<3;i++)
        {
            var p=Fresh();var a=Skills[i];Require(Begin(p,a,Serial.Zero,p.Location)&&ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Saved windup cast");var body=Own(p).Single();Hold(body);SkillHealth.SetBalance256(body,1111);PersistentMinions.Suspend(p);
            saved[i]=new(p.Serial.Value,body.Serial.Value,a,State(body),SkillHealth.Balance256(body),NecromancerEffects.ManaBalance256(p));
        }
        var pending=Fresh();Require(Begin(pending,CombatAbility.CarrionVine,Serial.Zero,pending.Location,Core.TickCount+300000),"Staged unpaid transient windup");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(saved,pending.Serial.Value,NecromancerEffects.ManaBalance256(pending))));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        foreach(var s in saved.Actors)
        {
            var p=World.FindMobile((Serial)s.Owner) as PlayerMobile;var b=World.FindMobile((Serial)s.Body) as SkeletonGuardian;
            Require(p!=null&&b!=null&&b.ControlMaster==p&&State(b)==s.State&&SkillHealth.Balance256(b)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana
                &&ClassCombat.For(p).VineCast==null&&ClassCombat.For(p).PendingAbility==CombatAbility.None&&!ClassCombat.Available(p,s.Ability),"Original paid vine/birth/wounds/resource/gate changed");
        }
        var pending=World.FindMobile((Serial)saved.PendingOwner) as PlayerMobile;Require(pending!=null&&ClassCombat.For(pending).VineCast==null&&ClassCombat.For(pending).PendingAbility==CombatAbility.None
            &&Own(pending).Length==0&&NecromancerEffects.ManaBalance256(pending)==saved.PendingMana,"Unpaid transient cast resumed/charged after reload");
    }
    private static void Live(NetState network)
    {
        var p=Fresh();var account=FixtureNetwork.Authenticate(network,p,"vine-windup-");int index=0,mana=0;long effect=0,deadline=Core.TickCount+12000;
        void End(){network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();VineCastWindupChecks.End(p);}
        void Start()
        {
            var a=Skills[index];ClassCombat.For(p).NextAction=Core.TickCount-1;mana=NecromancerEffects.ManaBalance256(p);
            Require(!ClassCombat.Perform(p,a,null,p.Location,Core.TickCount),"Public unresolved vine cast became available");
            Require(Begin(p,a,Serial.Zero,p.Location),"Connected prepared windup");effect=ClassCombat.For(p).EffectAt;
            Require(NecromancerEffects.ManaBalance256(p)==mana&&ClassCombat.For(p).VineCast!=null,"Connected windup charged early");
        }
        void Poll()
        {
            try
            {
                if(ClassCombat.For(p).VineCast==null&&Core.TickCount>=effect)
                {
                    var a=Skills[index];var b=Own(p).Single();Require(NecromancerEffects.ManaBalance256(p)==mana-SkillMechanics.Values(p,a).Mana256,"Natural prepared callback payment");Hold(b);
                    Require((a==CombatAbility.PoisonCreeper&&b is DruidCreeperBody)||(b is DruidRecyclerBody r&&r.VineAbility==a),"Natural prepared wrong body");
                    if(++index==3){End();Log.Information("vine-cast-windup-live-result: passed");return;}Start();
                }
                Require(Core.TickCount<deadline,"Natural vine windup timeout");Timer.StartTimer(TimeSpan.FromMilliseconds(20),Poll);
            }
            catch(Exception e){End();Log.Error(e,"vine-cast-windup-live-result: failed");}
        }
        try{Start();Timer.StartTimer(TimeSpan.FromMilliseconds(20),Poll);}catch{End();throw;}
    }
}
