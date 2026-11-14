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

public static class DeathMarkActionChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DeathMarkActionChecks));
    private const CombatAbility Ability=CombatAbility.DeathMark;
    private sealed record Pet(uint Serial,int Life,int Maximum,string Birth,NpcEquipmentSeed Random);
    private sealed record Saved(uint Owner,uint Target,Pet[] Pets,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"death-mark-action-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"death-mark-action-reload-check":"death-mark-action-check";
            ConsoleInputHandler.RegisterCommand(name,"Prepared Death Mark player SC/payment and automatic original demons",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));
        }
    }
    private static string Birth(SkeletonGuardian b)=>JsonSerializer.Serialize(b switch
    {WarlockGoatmanBody g=>(object)g.Prepared.Snapshot,WarlockTaintedBody t=>t.Prepared.Snapshot,WarlockDefilerBody d=>d.Prepared.Snapshot,_=>throw new NotSupportedException()});
    private static void Ready(PlayerMobile p)
    {
        ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;
        var fire=FireGround.Snapshot();FireGround.Restore(fire with{Cooldowns=fire.Cooldowns.Where(c=>c.Owner!=p.Serial.Value||c.Ability!=Ability).ToArray()});
        NecromancerEffects.SetMana256(p,p.ManaMax*256);
    }
    private static DeathMarkCasting.Pending Start(PlayerMobile p,Serial target,Point3D point,int opaque=0)
    {
        var a=DeathMarkCasting.BeginPrepared(p,target,point,Core.TickCount,opaque);
        Require(a!=null,"Prepared Death Mark startup refused");return a;
    }
    private static bool Release(PlayerMobile p)=>ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt);
    private static void HoldRecovery(SkeletonGuardian b)
    {
        var at=TimedEffects.Now.AddHours(1);
        if(b is WarlockGoatmanBody g){g.RestorePrepared(g.Prepared with{NextRecoveryUtc=at});}
        else if(b is WarlockTaintedBody t){t.RestorePrepared(t.Prepared with{NextRecoveryUtc=at});}
        else if(b is WarlockDefilerBody d){d.RestorePrepared(d.Prepared with{NextRecoveryUtc=at});}
    }
    private static void Scene(NetState network)
    {
        var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.AddItem(new Backpack());ClassSkillChecks.Train(p,"warlock.demonicmastery",10);
        ClassSkillChecks.Train(p,"warlock.deathmark",6);FixtureSpace.Corridor(p,6720,48,3650);
        var account=FixtureNetwork.Authenticate(network,p,"death-mark-action-");bool kept=false;GoatmanNpcContactChecks.Recipient target=null;
        try
        {
            Ready(p);var aim=new Point3D(p.X+10,p.Y,p.Z);int mana=NecromancerEffects.ManaBalance256(p);
            Require(SkillBindings.Instance.Assign(p,true,Ability)&&SkillBindings.Instance.Assign(p,false,Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Approved Death Mark bindings unavailable");
            Require(DeathMarkCasting.BeginPrepared(p,Serial.Zero,aim,Core.TickCount,0)==null&&NecromancerEffects.ManaBalance256(p)==mana,"No-demon startup paid/animated");
            SkeletonGuardian[] pets=[WarlockGoatmanWorld.Create(p,new(p.X+1,p.Y,p.Z),TimedEffects.Now,CombatAbility.SummonGoatman),
                WarlockGoatmanWorld.Create(p,new(p.X+3,p.Y,p.Z),TimedEffects.Now,CombatAbility.SummonTainted),
                WarlockGoatmanWorld.Create(p,new(p.X+5,p.Y,p.Z),TimedEffects.Now,CombatAbility.SummonDefiler)];
            foreach(var b in pets){WarlockGoatmanWorld.Admit(b);HoldRecovery(b);SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)-333);}
            var births=pets.Select(Birth).ToArray();var life=pets.Select(SkillHealth.Balance256).ToArray();
            Ready(p);var cancel=Start(p,Serial.Zero,aim);ClassCombat.Stop(p);
            Require(cancel.Cancelled&&!Release(p)&&NecromancerEffects.ManaBalance256(p)==mana,"Cancelled SC paid/released");
            Ready(p);var early=Start(p,Serial.Zero,aim);var session=ClassCombat.For(p);var clock=CombatTiming.For(p,Ability);
            Require(session.Timing==clock&&!ClassCombat.ResolveAction(p,session.EffectAt-1)&&!early.Released&&NecromancerEffects.ManaBalance256(p)==mana,"SC timing or unpaid windup differs");
            Require(Release(p)&&early.Result is{Result:1,EligibleDemons:3}&&early.ManaPaid256==SkillMechanics.Values(p,Ability).Mana256
                &&NecromancerEffects.ManaBalance256(p)==mana-early.ManaPaid256&&!Release(p),"Ground release did not pay exactly once");
            Require(pets.Select(Birth).SequenceEqual(births)&&pets.Select(SkillHealth.Balance256).SequenceEqual(life),"Warp healed/replaced/recaptured original demons");
            var cooldown=FireGround.Snapshot().Cooldowns.Single(c=>c.Owner==p.Serial.Value&&c.Ability==Ability);
            Require(!FireGround.Ready(p,Ability,cooldown.UntilUtc.AddTicks(-1))&&FireGround.Ready(p,Ability,cooldown.UntilUtc),"20-frame cooldown boundary differs");
            session.NextAction=Core.TickCount;Require(DeathMarkCasting.BeginPrepared(p,Serial.Zero,aim,Core.TickCount,0)==null,"Active local delay admitted new SC");
            Ready(p);var poor=Start(p,Serial.Zero,new(p.X+12,p.Y,p.Z));var before=pets.Select(b=>b.Location).ToArray();NecromancerEffects.SetMana256(p,0);
            Require(!Release(p)&&poor.Released&&poor.Result==null&&pets.Select(b=>b.Location).SequenceEqual(before),"Late mana refusal moved/charged demons");
            Ready(p);var moved=Start(p,Serial.Zero,aim);p.MoveToWorld(new(p.X,p.Y+1,p.Z),p.Map);
            Require(moved.Cancelled&&!Release(p),"Player movement did not cancel pending SC");p.MoveToWorld(new(p.X,p.Y-1,p.Z),p.Map);
            target=new(){Frozen=true,CantWalk=true,NoKillAwards=true};target.SetHits(50000);target.Hits=target.HitsMax;
            target.MoveToWorld(new(p.X+12,p.Y,p.Z),p.Map);target.AIObject?.AITimer.Stop();
            // Let real source controllers choose their actions, then warp
            // those same actors through the player's source release.
            DateTime tick=TimedEffects.Now;bool pending=false;
            for(int i=0;i<150&&!pending;i++)
            {
                tick=tick.AddMilliseconds(40);WarlockGoatmanWorld.Tick(tick);
                pending=TaintedFireBallCasting.State((WarlockTaintedBody)pets[1])!=null||DefilerLinkCasting.State((WarlockDefilerBody)pets[2])!=null;
            }
            Require(pending,"Automatic demon scene never began an intrinsic action");
            Ready(p);var marked=Start(p,target.Serial,aim,98765);mana=NecromancerEffects.ManaBalance256(p);before=pets.Select(b=>b.Location).ToArray();
            Require(Release(p)&&marked.Fault==null&&marked.ManaPaid256>0&&DeathMarkDebuffs.State(target)is{Rank:6,CastId:98765},"Player release did not bind qualified mark metadata");
            Require(TaintedFireBallCasting.State((WarlockTaintedBody)pets[1])==null&&DefilerLinkCasting.State((WarlockDefilerBody)pets[2])==null
                &&pets.All(b=>DeathMarkWorld.State(b).Mode==1&&DeathMarkWorld.State(b).Target48==checked((int)target.Serial.Value)),"Relocation did not interrupt/command original active actors");
            var fly=DeathMarkWorld.Path(pets[2]);
            Require(DefilerFlyingWorld.Collision((WarlockDefilerBody)pets[2],(int)(fly.X>>16),(int)(fly.Y>>16))==0,
                "Source placement cross admitted a diagonally blocked native flying box");
            for(int i=0;i<70;i++)
            {
                tick=tick.AddMilliseconds(40);WarlockGoatmanWorld.Tick(tick);
                Require(pets.All(b=>DemonNativeState.Controller(b)is{Fault:null}),"Automatic host faulted after Death Mark:"+string.Join(";",pets.Select(b=>DemonNativeState.Controller(b)?.Fault)));
            }
            Require(pets.All(b=>DemonNativeState.Controller(b)is{Fault:null})&&pets.Select(Birth).SequenceEqual(births),"Automatic host faulted or rebirthed after Death Mark");
            // A stronger prior mark rejects allocation AFTER pet commands.
            // Preserve source partial mutation and leave mana/delay untouched.
            Require(DeathMarkDebuffs.ApplyPrepared(p,target,255,TimedEffects.Now),"Stronger mark control setup");
            target.MoveToWorld(new(p.X+16,p.Y,p.Z),p.Map);Ready(p);var partial=Start(p,target.Serial,target.Location);before=pets.Select(b=>b.Location).ToArray();mana=NecromancerEffects.ManaBalance256(p);
            Require(!Release(p)&&partial.Result is{Result:0,ServerDoRan:true}&&partial.ManaPaid256==0&&NecromancerEffects.ManaBalance256(p)==mana
                &&!pets.Select(b=>b.Location).SequenceEqual(before)&&DeathMarkDebuffs.State(target)is{Rank:255}&&FireGround.Ready(p,Ability),"Failed late allocation rolled back warps,paid or started delay");
            DeathMarkDebuffs.Clear(target);Ready(p);var qualified=Start(p,target.Serial,target.Location,0); // Explicit proposed native metadata value.
            Require(Release(p)&&qualified.ManaPaid256>0&&DeathMarkDebuffs.State(target)is{Rank:6,CastId:0},"Explicit native metadata0 did not survive actual recipient binding");
            Require(WarlockCombat.Implemented(Ability)&&ClassCombat.Available(p,Ability),"Approved learned Death Mark route unavailable");
            PersistentMinions.Suspend(p);
            var saved=pets.Select(b=>new Pet(b.Serial.Value,SkillHealth.Balance256(b),SkillHealth.Maximum256(b),Birth(b),NpcEquipmentRandom.State(b))).ToArray();
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,saved,0)));kept=true;
            Log.Information("Prepared Death Mark action:shared SC,required demon,start/release refusal,cancel/movement,once-only fractional success payment,20-frame cooldown,actual source-host interruption/command/recovery and no rollback after late allocation failure;approved learned route available");
        }
        finally
        {
            ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
            if(!kept){target?.Delete();foreach(var b in WarlockGoatmanWorld.Demons(p)){b.Delete();}p.Delete();}
        }
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Too many reloads");
        var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=(BaseCreature)World.FindMobile((Serial)saved.Target);
        var pets=saved.Pets.Select(s=>(SkeletonGuardian)World.FindMobile((Serial)s.Serial)).ToArray();
        Require(ClassCombat.Existing(p)?.DeathMark==null,"Transient player action replayed on reload");
        Require(SkillBindings.Instance.Get(p).Left==Ability&&SkillBindings.Instance.Get(p).Right==Ability
            &&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Warlock,7)==Ability,"Original learned Death Mark bindings changed on reload");
        for(int i=0;i<pets.Length;i++)
        {Require(Birth(pets[i])==saved.Pets[i].Birth&&SkillHealth.Balance256(pets[i])==saved.Pets[i].Life&&SkillHealth.Maximum256(pets[i])==saved.Pets[i].Maximum
            &&NpcEquipmentRandom.State(pets[i])==saved.Pets[i].Random&&DemonNativeState.Controller(pets[i])is{Fault:null},"Original saved demon birth/wounds/RNG/controller changed");}
        var account=FixtureNetwork.Authenticate(network,p,"death-mark-action-reload-");
        try
        {
            PersistentMinions.Resume(p);WarlockGoatmanWorld.Tick(TimedEffects.Now);Ready(p);
            var pending=Start(p,Serial.Zero,new(p.X+8,p.Y,p.Z));
            Require(Release(p)&&pending.ManaPaid256>0&&pending.Result is{Result:1,EligibleDemons:3},"Reloaded original source roster could not complete player ground cast");
            for(int i=0;i<20;i++){WarlockGoatmanWorld.Tick(TimedEffects.Now.AddMilliseconds(40*i));}
            Require(pets.All(b=>DemonNativeState.Controller(b)is{Fault:null}),"Reloaded source host faulted after player warp");
            PersistentMinions.Suspend(p);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1,
                Pets=pets.Select(b=>new Pet(b.Serial.Value,SkillHealth.Balance256(b),SkillHealth.Maximum256(b),Birth(b),NpcEquipmentRandom.State(b))).ToArray()}));
            Log.Information("Prepared Death Mark action:original three-species saved hosts resumed,cast again and retained wounds/birth without replay");
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
