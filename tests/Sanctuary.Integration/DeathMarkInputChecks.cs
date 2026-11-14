// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DeathMarkInputChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DeathMarkInputChecks));
    private const CombatAbility Ability=CombatAbility.DeathMark;
    private static bool Creating;
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Rates(){var old=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>Creating||p.Name=="DeathMarkInputFixture"?TimeSpan.FromHours(1):old?.Invoke(p)??Mobile.DefaultManaRate;}
    public static void Configure()
    {
        Rates();foreach(var value in new[]{"left","right","quick"})
        {string kind=value,label="death-mark-input-"+kind;ConsoleInputHandler.RegisterCommand(label+"-check","Actual learned Death Mark mouse/quick input",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Label}-check: passed",label);FixtureNetwork.Connected(label+"-result",n=>Input(n,kind));}));}
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Input(NetState network,string kind)
    {
        PlayerMobile p;Creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Warlock);}finally{Creating=false;}
        p.Name="DeathMarkInputFixture";p.Female=kind=="quick";p.AddItem(new Backpack());ClassSkillChecks.Train(p,"warlock.deathmark",6);
        if(p.Female){PoisonDaggerChecks.Named(p,"Tarnhelm");ClassGear.Refresh(p);}
        FixtureSpace.Corridor(p,6800,32,3500);var account=FixtureNetwork.Authenticate(network,p,"death-mark-input-");
        Require(SkillBindings.Instance.Assign(p,kind=="left",Ability)&&SkillBindings.Instance.AssignQuick(p,7,Ability),"Death Mark real-input binding failed");
        NecromancerEffects.SetMana256(p,p.ManaMax*256);var values=SkillMechanics.Values(p,Ability);int mana=NecromancerEffects.ManaBalance256(p);
        var body=(WarlockDefilerBody)WarlockGoatmanWorld.Create(p,new(p.X+2,p.Y,p.Z),TimedEffects.Now,CombatAbility.SummonDefiler);
        WarlockGoatmanWorld.Admit(body);SkillHealth.SetBalance256(body,body.MaximumLife256-8192);
        var birth=body.Prepared.Snapshot;var origin=body.Location;int life=SkillHealth.Balance256(body),firstFrame=body.Prepared.Controller.Clock.Frame;
        var target=new GoatmanNpcContactChecks.Recipient{Frozen=true,CantWalk=true,NoKillAwards=true};
        target.SetHits(50000);target.Hits=target.HitsMax;target.MoveToWorld(new(p.X+10,p.Y,p.Z),p.Map);target.AIObject?.AITimer.Stop();
        var point=target.Location;string label="death-mark-input-"+kind;long deadline=Core.TickCount+8000;bool began=false,finished=false;
        DeathMarkCasting.Pending pending=null;
        void Cleanup(){ClassCombat.Stop(p);target.Delete();body.Delete();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();p.Delete();network.Disconnect("Death Mark input fixture complete");}
        void Inspect()
        {
            if(finished){return;}try
            {
                if(Core.TickCount>=deadline){throw new InvalidOperationException($"Death Mark input timeout,began={began},pending={ClassCombat.For(p).PendingAbility},fault={pending?.Fault}");}
                if(ClassCombat.For(p).DeathMark is{} current&&!began)
                {pending=current;began=true;Require(ClassCombat.For(p).Timing==WarlockCastClock.Calculate(CombatTiming.Fcr(p))&&NecromancerEffects.ManaBalance256(p)==mana,"Real SC windup paid early or used wrong clock");Log.Information("{Label}: windup",label);}
                if(pending is{Released:true})
                {
                    // The production host admits natural regeneration each
                    // active source frame. A relocation observation can skip
                    // a clock frame without healing,so elapsed frames give an
                    // upper bound;the synchronous release test checks exact
                    // unchanged wounds across the warp itself.
                    int frames=body.Prepared.Controller.Clock.Frame-firstFrame;
                    int expectedLife=(int)Math.Min(body.MaximumLife256,life+(long)frames*BattleOrders.Regeneration256(body));
                    Require(pending is{Fault:null,Result:{Result:1,EligibleDemons:1}}&&pending.ManaPaid256==values.Mana256&&NecromancerEffects.ManaBalance256(p)==mana-values.Mana256
                        &&body.Prepared.Snapshot==birth&&SkillHealth.Balance256(body)>=life&&SkillHealth.Balance256(body)<=expectedLife&&body.Prepared.Controller is{Fault:null}&&body.Location!=origin&&body.GetDistanceToSqrt(target)<=4,
                        $"Real input mismatch:result={pending.Result},fault={pending.Fault},cost={pending.ManaPaid256}/{values.Mana256},mana={NecromancerEffects.ManaBalance256(p)}/{mana-values.Mana256},life={SkillHealth.Balance256(body)}/{expectedLife},frames={frames},birth={body.Prepared.Snapshot==birth},controller={body.Prepared.Controller?.Fault},position={body.Location}/{origin},distance={body.GetDistanceToSqrt(target)}");
                    Require(kind=="right"?DeathMarkDebuffs.State(target)==null:DeathMarkDebuffs.State(target)is{} mark&&mark.Rank==values.Rank&&mark.CastId==0,"Ground/target or native metadata0 binding differs");
                    Require(FireGround.Snapshot().Cooldowns.Any(c=>c.Owner==p.Serial.Value&&c.Ability==Ability),"Real input missing skill-local cooldown");
                    Log.Information("{Label}: actual learned Death Mark relocated original source-host Defiler;rank{Rank}/cost{Cost}/ground{Ground}",label,values.Rank,values.Mana256,kind=="right");
                    finished=true;Cleanup();Log.Information("{Label}-result: passed",label);return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){finished=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
        {Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)Ability,(int)DiabloClass.Warlock,kind=="right"?0:target.Serial.Value,point.X,point.Y,point.Z);Inspect();}));
    }
}
