// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DruidFormCastingChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(DruidFormCastingChecks));
    private sealed record Saved(uint Owner,DruidPlayerForm Form,int Life,int Mana,int Body,DateTime Delay,uint Pending,int PendingMana);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"druid-form-casting-fixtures.json");
    private static bool _creating;private static int _scene;
    public static void Configure()
    {
        Rates();Register("druid-form-casting-check",Check);Register("druid-form-casting-reload-check",Reload);
        Register("druid-form-delay-live-check",DelayLive);
        foreach(var label in new[]{"form-wolf-right","form-bear-quick","form-wolf-unshift-quick","form-bear-unshift-right"})
        {Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Rates()
    {
        var hp=Mobile.HitsRegenRateHandler;var mp=Mobile.ManaRegenRateHandler;
        bool Held(Mobile p)=>_creating||p.Name?.StartsWith("DruidFormCastFixture",StringComparison.Ordinal)==true;
        Mobile.HitsRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):hp?.Invoke(p)??Mobile.DefaultHitsRate;
        Mobile.ManaRegenRateHandler=p=>Held(p)?TimeSpan.FromHours(1):mp?.Invoke(p)??Mobile.DefaultManaRate;
    }
    private static void Register(string name,Action check)=>ConsoleInputHandler.RegisterCommand(name,"Learned form casting,payment,delay,input and reload",_=>Core.LoopContext.Post(()=>
    {try{check();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,CombatAbility ability,int rank=1)
    {
        var row=SkillMechanics.For(ability);TalentCatalog.TryGet(DiabloClass.Druid,row.TalentId,out var talent);
        foreach(var prior in talent.Prerequisites)
        {if(ClassCreation.Profiles.GetRank(p.Serial,prior)==0){Train(p,SkillMechanics.All.Single(x=>x.TalentId==prior).Ability);}}
        while(ClassCreation.Profiles.GetRank(p.Serial,row.TalentId)<rank)
        {Require(ClassCreation.Profiles.TryTrain(p.Serial,row.TalentId,out _),"Train "+row.TalentId);}
    }
    private static PlayerMobile Fresh(bool trained=true)
    {
        PlayerMobile p;_creating=true;try{p=new PlayerMobile{Body=0x190,Player=true,Name="DruidFormCastFixture"};}finally{_creating=false;}
        p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Druid);ClassCreation.Profiles.AdvanceLevel(p,99);
        if(trained){foreach(var ability in new[]{CombatAbility.Werewolf,CombatAbility.Lycanthropy,CombatAbility.Werebear}){Train(p,ability);}}
        // These self-cast fixtures need no travelling corridor. Reuse the four
        // verified bands; the eastern map has no fit above the earlier2324start.
        // FixtureSpace.Place still requires an unoccupied valid destination.
        FixtureSpace.Corridor(p,6140,20,2100+32*(_scene++%4));Mana(p,7000);Ready(p);return p;
    }
    private static void Mana(PlayerMobile p,int value)=>NecromancerEffects.SetMana256(p,value);
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);p.Frozen=p.Paralyzed=false;ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;}
    private static void ClearDelay(PlayerMobile p)
    {var all=FireGround.Snapshot();FireGround.Restore(all with{Cooldowns=all.Cooldowns.Where(x=>x.Owner!=p.Serial.Value||!DruidFormCasting.Handles(x.Ability)).ToArray()});}
    private static void Drop(PlayerMobile p)
    {ClassCombat.Stop(p);DruidPlayerForms.Clear(p);ClearDelay(p);p.Corpse?.Delete();p.Delete();}
    private static void Ratio(PlayerMobile p,BattleOrders.Pools before)
    {
        long error=Math.Abs((long)SkillHealth.Balance256(p)*before.LifeMax*256-(long)before.Life256*p.HitsMax*256);
        Require(error<=(long)before.LifeMax*256,"Cast changed relative life beyond one projected FPquantum");
    }
    private static void Cast(PlayerMobile p,CombatAbility ability)
    {
        Ready(p);var before=DruidPlayerForms.State(p);int mana=NecromancerEffects.ManaBalance256(p),body=p.Body;var pools=BattleOrders.Capture(p);
        Require(ClassCombat.Perform(p,ability,p,new(65530,65530,0),Core.TickCount),"Form cast start "+ability);
        var action=ClassCombat.For(p);long at=action.EffectAt;
        Require(action.PendingPoint==p.Location&&action.PendingTarget==Serial.Zero&&action.Transform!=null
            &&action.Timing==DruidFormCastClock.Calculate(before?.Form.Ability??CombatAbility.None,CombatTiming.Fcr(p))
            &&DruidPlayerForms.State(p)==before&&NecromancerEffects.ManaBalance256(p)==mana,"Self canonicalization,SCclock or unpaid windup");
        Require(!DruidFormCasting.Release(p,ability,at-1)&&action.Transform!=null,"Early direct release consumed context");
        Require(ClassCombat.ResolveAction(p,at)&&!ClassCombat.ResolveAction(p,at),"Form cast once-only release");
        Require((before==null?DruidPlayerForms.State(p)?.Form.Ability==ability:DruidPlayerForms.State(p)==null)
            &&NecromancerEffects.ManaBalance256(p)==mana-(before==null?3840:0)&&p.Body==body,"Entry/unshift/payment/placeholder body");
        Ratio(p,pools);Require(!FireGround.Ready(p,CombatAbility.Werewolf)&&!FireGround.Ready(p,CombatAbility.Werebear),"Shared25frame form delay missing");
        var delays=FireGround.Snapshot().Cooldowns.Where(x=>x.Owner==p.Serial.Value&&DruidFormCasting.Handles(x.Ability)).ToArray();
        Require(delays.Length==2&&delays[0].UntilUtc==delays[1].UntilUtc&&delays[0].UntilUtc.Kind==DateTimeKind.Utc,"Form delay identity/UTC");
    }
    private static void Clock()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"druid-form-cast-clock-source-probe.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int index=row[0].GetInt32();var actual=DruidFormCastClock.Calculate(index==0?CombatAbility.Werewolf:index==9?CombatAbility.Werebear:CombatAbility.None,row[1].GetInt32());
            Require(actual.EffectFrame==row[3][0][1].GetInt32()&&actual.Frames==row[3][1][1].GetInt32(),"Complete source SCclock mismatch");count++;
        }
        Require(count==2590,"Incomplete SCclock source matrix");Log.Information("Learned form SC:2590native action/end comparisons match ten licensed records and complete source FCRclock");
    }
    private static void Transactions()
    {
        var p=Fresh();var untrained=Fresh(false);var oldApply=DruidFormCasting.Apply;
        try
        {
            Require(!ClassCombat.Available(untrained,CombatAbility.Werewolf)&&!ClassCombat.Perform(untrained,CombatAbility.Werewolf,null,untrained.Location,Core.TickCount),"Untrained form cast");
            Require(DruidCombat.Implemented(CombatAbility.Lycanthropy)&&!ClassCombat.Available(p,CombatAbility.Lycanthropy)
                &&!SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Lycanthropy),"Passive Lycanthropy cast/binding");
            foreach(var ability in new[]{CombatAbility.Werewolf,CombatAbility.Werebear})
            {
                Require(!SkillBindings.Instance.Assign(p,true,ability)&&SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Form right/quick/left policy");
                Ready(p);Mana(p,3839);Require(!ClassCombat.Perform(p,ability,null,p.Location,Core.TickCount)&&DruidPlayerForms.State(p)==null,"Fractional unaffordable entry");
                Ready(p);Mana(p,7000);Require(ClassCombat.Perform(p,ability,null,p.Location,Core.TickCount),"Cancelled form start");long at=ClassCombat.For(p).EffectAt;
                ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at)&&DruidPlayerForms.State(p)==null&&NecromancerEffects.ManaBalance256(p)==7000,"Cancelled form paid/applied");
                Ready(p);Require(ClassCombat.Perform(p,ability,null,p.Location,Core.TickCount),"Moved form start");at=ClassCombat.For(p).EffectAt;var origin=p.Location;
                p.MoveToWorld(new(p.X-1,p.Y,p.Z),p.Map);p.MoveToWorld(origin,p.Map);
                Require(!ClassCombat.ResolveAction(p,at)&&DruidPlayerForms.State(p)==null,"Out-and-back form windup escaped cancellation");
                Ready(p);Require(ClassCombat.Perform(p,ability,null,p.Location,Core.TickCount),"Lost-mana start");at=ClassCombat.For(p).EffectAt;Mana(p,3839);
                Require(!ClassCombat.ResolveAction(p,at)&&DruidPlayerForms.State(p)==null&&NecromancerEffects.ManaBalance256(p)==3839&&FireGround.Ready(p,ability),"Explicitly unsupported lost-mana branch committed");
                Ready(p);Mana(p,7000);DruidFormCasting.Apply=(_,_,_)=>false;
                Require(ClassCombat.Perform(p,ability,null,p.Location,Core.TickCount),"Allocation-failure start");at=ClassCombat.For(p).EffectAt;
                Require(!ClassCombat.ResolveAction(p,at)&&DruidPlayerForms.State(p)==null&&NecromancerEffects.ManaBalance256(p)==7000&&FireGround.Ready(p,ability),"Failed allocation changed resources/delay");DruidFormCasting.Apply=oldApply;
                Ready(p);SkillHealth.SetBalance256(p,p.HitsMax*256-20007);Cast(p,ability);
                var other=ability==CombatAbility.Werewolf?CombatAbility.Werebear:CombatAbility.Werewolf;
                Require(!ClassCombat.Available(p,other)&&!ClassCombat.Perform(p,other,null,p.Location,Core.TickCount),"Unverified direct switching silently enabled");
                Ready(p);Require(!ClassCombat.Perform(p,ability,null,p.Location,Core.TickCount),"Shared cooldown allowed immediate unshift");
                Require(ClassCombat.Available(p,CombatAbility.BasicAttack)&&ClassCombat.Perform(p,CombatAbility.BasicAttack,null,p.Location,Core.TickCount),"Form delay blocked ordinary zero-delay attack");
                Ready(p);ClearDelay(p);Mana(p,0);Cast(p,ability);
                ClearDelay(p);Ready(p);Mana(p,7000);
            }
        }
        finally{DruidFormCasting.Apply=oldApply;Drop(p);Drop(untrained);}
    }
    private static void Check()
    {
        Clock();Transactions();var p=Fresh();Train(p,CombatAbility.Werebear,20);Train(p,CombatAbility.Lycanthropy,20);Cast(p,CombatAbility.Werebear);
        SkillHealth.SetBalance256(p,p.HitsMax*256-20007);Mana(p,1000);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Werebear)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Werebear),"Saved form bindings");
        var all=FireGround.Snapshot();var held=TimedEffects.Now.AddMinutes(10);
        FireGround.Restore(all with{Cooldowns=all.Cooldowns.Select(x=>x.Owner==p.Serial.Value&&DruidFormCasting.Handles(x.Ability)?x with{UntilUtc=held}:x).ToArray()});
        var pending=Fresh();Require(ClassCombat.Perform(pending,CombatAbility.Werewolf,null,pending.Location,Core.TickCount),"Saved unpaid form windup");
        ClassCombat.For(pending).EffectAt=Core.TickCount+3600000;ClassCombat.For(pending).NextAction=Core.TickCount+3601000;
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,DruidPlayerForms.State(p),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),p.Body,held,pending.Serial.Value,NecromancerEffects.ManaBalance256(pending))));
        Log.Information("Learned forms:15mana entry,free same-form return,relative life,once-only allocation/payment,shared25frame delay,passive Lycanthropy,bindings and cancellation pass; direct switch/lost-mana remain explicit unsupported branches");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var pending=(PlayerMobile)World.FindMobile((Serial)saved.Pending);
        Require(DruidPlayerForms.State(p)==saved.Form&&SkillHealth.Balance256(p)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana&&p.Body==saved.Body,"Learned form/deadline/injury/mana/body changed on reload");
        var delays=FireGround.Snapshot().Cooldowns.Where(x=>x.Owner==saved.Owner&&DruidFormCasting.Handles(x.Ability)).ToArray();
        Require(delays.Length==2&&delays.All(x=>x.UntilUtc==saved.Delay)&&!FireGround.Ready(p,CombatAbility.Werewolf)&&!FireGround.Ready(p,CombatAbility.Werebear),"Saved shared cooldown refreshed/lost");
        Require(DruidPlayerForms.State(pending)==null&&ClassCombat.For(pending).PendingAbility==CombatAbility.None&&NecromancerEffects.ManaBalance256(pending)==saved.PendingMana,"Saved unpaid form replayed");
        Require(SkillBindings.Instance.Get(p).Right==CombatAbility.Werebear&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Druid,7)==CombatAbility.Werebear,"Form bindings changed on reload");
    }
    private static void DelayLive()
    {
        var p=Fresh();Cast(p,CombatAbility.Werewolf);var state=DruidPlayerForms.State(p);
        Timer.StartTimer(TimeSpan.FromMilliseconds(1150),()=>
        {try{Require(FireGround.Ready(p,CombatAbility.Werewolf)&&FireGround.Ready(p,CombatAbility.Werebear)&&DruidPlayerForms.State(p)==state,"Natural shared cooldown expiry changed form");Drop(p);Log.Information("druid-form-delay-result: passed");}
            catch(Exception e){Drop(p);Log.Error(e,"druid-form-delay-result: failed");}});
    }
    private static void Input(NetState network,string label)
    {
        var p=Fresh();bool unshift=label.Contains("unshift",StringComparison.Ordinal),quick=!label.EndsWith("right",StringComparison.Ordinal);
        var ability=label.Contains("wolf",StringComparison.Ordinal)?CombatAbility.Werewolf:CombatAbility.Werebear;
        if(unshift){Require(DruidPlayerForms.ApplyPrepared(p,ability,TimedEffects.Now),"Input prepared starting form");Mana(p,0);}Ready(p);
        int mana=NecromancerEffects.ManaBalance256(p);network.Seeded=network.SentFirstPacket=true;network.CompressionEnabled=false;network.Mobile=p;p.NetState=network;
        var field=typeof(NetState).GetField("_protocolState",BindingFlags.Instance|BindingFlags.NonPublic);Require(field!=null,"Protocol bootstrap");
        field.SetValue(network,Enum.Parse(field.FieldType,"GameServer_LoggedIn"));network.ResetMovementState();network.SendMobileUpdate(p);
        Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Input form bindings");
        bool began=false;long deadline=Core.TickCount+5000;
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Form input timeout");var action=ClassCombat.For(p);
                if(action.PendingAbility==ability)
                {Require(NecromancerEffects.ManaBalance256(p)==mana,"Input paid during windup");if(!began){began=true;Log.Information("{Label}: windup",label);}}
                bool applied=unshift?DruidPlayerForms.State(p)==null:DruidPlayerForms.State(p)?.Form.Ability==ability;
                if(!applied||!began){Timer.StartTimer(TimeSpan.FromMilliseconds(20),Inspect);return;}
                Require((quick||action.HeldAbility==CombatAbility.None)&&NecromancerEffects.ManaBalance256(p)==mana-(unshift?0:3840)
                    &&!FireGround.Ready(p,ability),"Real form input payment/delay/release");Drop(p);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Drop(p);Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Druid,p.X+2,p.Y,p.Z);Inspect();});
    }
}
