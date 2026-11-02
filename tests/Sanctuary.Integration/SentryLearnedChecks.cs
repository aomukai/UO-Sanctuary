// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class SentryLearnedChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(SentryLearnedChecks));
    private sealed record Saved(uint Owner,uint Body,uint Target,uint Helm,Point3D Origin,int Map,int Mana,int Health,SentryBodyState Actor,SentryBoltState Bolt,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"sentry-learned-fixtures.json");
    private static bool _creating;
    private static void Rates()
    {
        var mp=Mobile.ManaRegenRateHandler;var hp=Mobile.HitsRegenRateHandler;
        Mobile.ManaRegenRateHandler=p=>_creating||p.Name?.StartsWith("SentryLearnedFixture")==true?TimeSpan.FromHours(1):mp?.Invoke(p)??Mobile.DefaultManaRate;
        Mobile.HitsRegenRateHandler=p=>_creating||p.Name?.StartsWith("SentryLearnedFixture")==true?TimeSpan.FromHours(1):hp?.Invoke(p)??Mobile.DefaultHitsRate;
    }
    [CallPriority(int.MaxValue)]public static void Initialize()=>Rates();
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Learned CBS right/quick S2, once-only payment and saved original actor/bolt",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    public static void Configure()
    {
        Rates();Register("sentry-learned-availability-check",Availability);Register("sentry-learned-mechanics-check",Mechanics);
        Register("sentry-learned-check",()=>FixtureNetwork.Connected("sentry-learned-result",Save));Register("sentry-learned-reload-check",()=>FixtureNetwork.Connected("sentry-learned-reload-result",Reload));
        foreach(string mode in new[]{"left","right","quick"}){string label="sentry-learned-"+mode;Register(label+"-check",()=>FixtureNetwork.Connected(label+"-result",n=>Input(n,label)));}
    }
    internal static PlayerMobile Fresh()
    {
        PlayerMobile p;_creating=true;try{p=BladeBodyChecks.Fresh(reach:20);}finally{_creating=false;}
        p.Name="SentryLearnedFixture";p.Mana=p.ManaMax;return p;
    }
    internal static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=Core.TickCount;s.RecoveryUntil=0;p.NextSkillTime=p.NextSpellTime=0;}
    private static Point3D Aim(PlayerMobile p)=>new(p.X+4,p.Y,p.Z);
    internal static void Clean(PlayerMobile p)
    {
        foreach(var body in AssassinSentryActors.Actors().Where(x=>x.Prepared.Owner==p.Serial.Value)){body.Delete();}
        ClassCombat.Stop(p);p.Delete();
    }
    internal static void Bind(PlayerMobile p,CombatAbility ability=CombatAbility.ChargedBoltSentry)
    {
        var left=SkillBindings.Instance.Get(p).Left;
        Require(!SkillBindings.Instance.Assign(p,true,ability)&&SkillBindings.Instance.Get(p).Left==left
            &&SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Pinned right-only/quick binding policy");
    }
    internal static AssassinSentryBody Cast(PlayerMobile p,CombatAbility ability=CombatAbility.ChargedBoltSentry)
    {
        Ready(p);int mana=NecromancerEffects.ManaBalance256(p),cost=ability==CombatAbility.LightningSentry?5120:3328;Require(ClassCombat.Perform(p,ability,null,Aim(p),Core.TickCount),"Learned CBS begin");
        var s=ClassCombat.For(p);long next=s.NextAction;
        Require(s.Animation==9&&s.Timing==AssassinSentryCasts.Timing(p)&&NecromancerEffects.ManaBalance256(p)==mana&&AssassinTrapRoster.For(p).Length==0,"S2 windup created/paid early");
        Require(!ClassCombat.ResolveAction(p,s.EffectAt-1)&&ClassCombat.ResolveAction(p,s.EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Trap release did not pay its pinned cost once");
        Require(!ClassCombat.ResolveAction(p,s.EffectAt)&&s.NextAction==next,"Duplicate release changed payment/cycle");
        return (AssassinSentryBody)World.FindMobile((Serial)AssassinTrapRoster.For(p).Single().Actor);
    }
    private static void Availability()
    {
        var p=Fresh();try
        {
            Require(ClassCombat.Available(p,CombatAbility.ChargedBoltSentry)&&AssassinSentryCasts.Supported(p),"Learned CBS unavailable");MousePolicy(p);Migration(p);Bind(p);
            Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Barbarian)&&!ClassCombat.Available(p,CombatAbility.ChargedBoltSentry),"Inactive Assassin retained CBS");
            Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Assassin)&&ClassCombat.Available(p,CombatAbility.ChargedBoltSentry)
                &&SkillBindings.Instance.Get(p).Right==CombatAbility.ChargedBoltSentry&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Assassin,7)==CombatAbility.ChargedBoltSentry,"Assassin history lost CBS/bindings");
        }
        finally{Clean(p);}
    }
    private static void MousePolicy(PlayerMobile p)
    {
        int count=0;
        foreach(var row in SkillMechanics.All.Where(x=>x.CharacterClass==DiabloClass.Assassin))
        {
            bool available=ClassCombat.Available(p,row.Ability);
            foreach(bool left in new[]{false,true})
            {
                bool source=(left?row.Number("leftskill"):row.Number("rightskill"))!=0;
                Require(SkillBindings.MouseAllowed(DiabloClass.Assassin,left,row.Ability)==source,"Assassin mouse policy differs from pinned source: "+row.Name);
                var before=SkillBindings.Instance.Get(p);int mana=NecromancerEffects.ManaBalance256(p);long next=ClassCombat.For(p).NextAction;
                Require(SkillBindings.Instance.Assign(p,left,row.Ability)==(source&&available),"Assassin slot assignment differs: "+row.Name);
                if(!source||!available){Require(SkillBindings.Instance.Get(p)==before&&NecromancerEffects.ManaBalance256(p)==mana&&ClassCombat.For(p).NextAction==next,"Rejected assignment changed bindings/resources/cycle");}
            }
            Require(SkillBindings.Instance.AssignQuick(p,7,row.Ability)==available,"Assassin quick incorrectly follows mouse flags");count++;
        }
        Require(count==30,"Incomplete Assassin policy scope");
        foreach(bool left in new[]{false,true})
        {
            Require(SkillBindings.MouseAllowed(DiabloClass.Assassin,left,CombatAbility.BasicAttack)&&SkillBindings.MouseAllowed(DiabloClass.Assassin,left,CombatAbility.None)
                &&!SkillBindings.MouseAllowed(DiabloClass.Assassin,left,CombatAbility.FireBolt)&&!SkillBindings.MouseAllowed(DiabloClass.Assassin,left,(CombatAbility)255),"Assassin basic/empty/foreign/invalid policy");
        }
    }
    private static void Migration(PlayerMobile p)
    {
        byte[] original=NecromancerBindingChecks.Encode();int count=0;
        var abilities=SkillMechanics.All.Where(x=>x.CharacterClass==DiabloClass.Assassin).Select(x=>x.Ability)
            .Concat(new[]{CombatAbility.BasicAttack,CombatAbility.None,CombatAbility.FireBolt}).ToArray();
        try
        {
            foreach(int version in new[]{0,1})foreach(bool left in new[]{false,true})foreach(var old in abilities)
            {
                var writer=new BufferWriter(new byte[1024],true);writer.Write(version);writer.Write(1);writer.Write(p.Serial.Value);
                if(version==1){writer.Write((byte)DiabloClass.Assassin);}
                writer.Write((byte)(left?old:CombatAbility.FireBlast));writer.Write((byte)(left?CombatAbility.ChargedBoltSentry:old));
                if(version==1){for(int slot=0;slot<SkillBindingProtocol.Slots;slot++){writer.Write((byte)(slot==7?CombatAbility.ChargedBoltSentry:CombatAbility.None));}}
                SkillBindings.Instance.Deserialize(new BufferReader(writer.Buffer[..(int)writer.Position]));
                bool allowed=old is CombatAbility.None or CombatAbility.BasicAttack;
                if(!allowed&&old!=CombatAbility.FireBolt){var row=SkillMechanics.For(old);allowed=left?row.Left:row.Right;}
                var expected=(left?(allowed?old:CombatAbility.BasicAttack):CombatAbility.FireBlast,left?CombatAbility.ChargedBoltSentry:(allowed?old:CombatAbility.None));
                if(version==1)
                {
                    byte[] raw=NecromancerBindingChecks.Encode();Require(SkillBindings.Instance.Get(p)==expected&&SkillBindings.Instance.Get(p)==expected,"Version1 Assassin read normalization");
                    Require(raw.SequenceEqual(NecromancerBindingChecks.Encode()),"Binding read mutated persisted state");
                }
                SkillBindings.Instance.PostDeserialize();Require(SkillBindings.Instance.Get(p)==expected,"Assassin saved-slot normalization");
                Require(SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Assassin,7)==(version==1?CombatAbility.ChargedBoltSentry:CombatAbility.None),"Mouse migration changed quick slots");
                byte[] first=NecromancerBindingChecks.Encode();SkillBindings.Instance.Deserialize(new BufferReader(first));SkillBindings.Instance.PostDeserialize();
                Require(first.SequenceEqual(NecromancerBindingChecks.Encode()),"Assassin migration not idempotent");count++;
            }
        }
        finally{SkillBindings.Instance.Deserialize(new BufferReader(original));SkillBindings.Instance.PostDeserialize();}
        Require(count==132,"Incomplete Assassin migration scope");
    }
    private static void Mechanics()
    {
        var p=Fresh();try
        {
            var timing=CombatTiming.Calculate(DiabloClass.Assassin,CombatAbility.ChargedBoltSentry,new(AttackFamily.Unarmed,0));
            Require(timing==new ActionTiming(15,8)&&CombatTiming.Calculate(DiabloClass.Assassin,CombatAbility.ChargedBoltSentry,new(AttackFamily.Unarmed,0),fcr:200)==timing
                &&CombatTiming.Calculate(DiabloClass.Assassin,CombatAbility.ChargedBoltSentry,new(AttackFamily.Unarmed,0),ias:200)==new ActionTiming(9,5),"Trap S2 IAS/no FCR policy");
            NecromancerEffects.SetMana256(p,3327);Ready(p);long next=ClassCombat.For(p).NextAction;
            Require(!ClassCombat.Perform(p,CombatAbility.ChargedBoltSentry,null,Aim(p),Core.TickCount)&&NecromancerEffects.ManaBalance256(p)==3327&&ClassCombat.For(p).NextAction==next,"Low-mana begin changed state");
            NecromancerEffects.SetMana256(p,3328);Ready(p);Require(ClassCombat.Perform(p,CombatAbility.ChargedBoltSentry,null,Aim(p),Core.TickCount),"Stop-control begin");var s=ClassCombat.For(p);long effect=s.EffectAt;next=s.NextAction;
            ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==3328&&AssassinTrapRoster.For(p).Length==0&&s.NextAction==next,"Stop paid/spawned/refunded cycle");
            Ready(p);Require(ClassCombat.Perform(p,CombatAbility.ChargedBoltSentry,null,Aim(p),Core.TickCount),"Windup mana-loss begin");NecromancerEffects.SetMana256(p,3327);
            Require(!ClassCombat.ResolveAction(p,s.EffectAt)&&NecromancerEffects.ManaBalance256(p)==3327&&AssassinTrapRoster.For(p).Length==0,"Unsupported legacy free-trap mana-loss edge enabled");
            NecromancerEffects.SetMana256(p,3328);var seed=BladeSentinelSummons.SeedLow;int draws=0;
            try
            {
                BladeSentinelSummons.SeedLow=()=>{draws++;return 19;};var body=Cast(p);
                Require(draws==1&&body.Prepared.Control is{Mode:8,Frame:0,NextThink:25}&&FireGround.Ready(p,CombatAbility.ChargedBoltSentry),"Creation seed/S1/no independent delay");
            }
            finally{BladeSentinelSummons.SeedLow=seed;}
        }
        finally{Clean(p);}
        p=Fresh();try
        {
            Ready(p);int mana=NecromancerEffects.ManaBalance256(p);var origin=p.Location;
            Require(ClassCombat.Perform(p,CombatAbility.ChargedBoltSentry,null,Aim(p),Core.TickCount),"Move-control begin");long effect=ClassCombat.For(p).EffectAt;
            p.Direction=Direction.West;Require(p.Move(Direction.West)&&p.Location!=origin,"Accepted cancellation step");
            Require(!ClassCombat.ResolveAction(p,effect)&&NecromancerEffects.ManaBalance256(p)==mana&&AssassinTrapRoster.For(p).Length==0,"Movement cancellation paid/created trap");
        }
        finally{Clean(p);}
    }
    internal static void Clock(AssassinSentryBody body,DateTime at)
    {
        typeof(AssassinSentryBody).GetMethod("Store",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(body,[body.Prepared with{NextFrameUtc=at}]);
        if(body.Prepared.Creation.Ability==CombatAbility.LightningSentry)
        {var pool=AssassinSentryLightning.Snapshot();AssassinSentryLightning.Restore(pool with{Bolts=pool.Bolts.Select(x=>x.Actor==body.Serial.Value?x with{NextUtc=at}:x).ToArray()});}
        else
        {var pool=AssassinSentryBolts.Snapshot();AssassinSentryBolts.Restore(pool with{Bolts=pool.Bolts.Select(x=>x.Actor==body.Serial.Value?x with{NextUtc=at}:x).ToArray()});}
    }
    private static void Hold(PlayerMobile p,AssassinSentryBody body,BladeMissileTarget target,Item helm,long bolt,Point3D origin,int map,int generation)
    {
        Clock(body,TimedEffects.Now.Add(FixtureSpace.Retention));p.Internalize();
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,body.Serial.Value,target.Serial.Value,helm.Serial.Value,origin,map,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(target),body.Prepared,AssassinSentryBolts.State(bolt),generation)));
    }
    private static void Save(NetState network)
    {
        var p=Fresh();while(ClassCreation.Profiles.GetRank(p.Serial,"assassin.chargedboltsentry")<20){Require(ClassCreation.Profiles.TryTrain(p.Serial,"assassin.chargedboltsentry",out _),"Train CBS");}
        var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);Require(p.EquipItem(helm),"Real soft-rank helm");ClassGear.Refresh(p);Bind(p);
        FixtureNetwork.Authenticate(network,p,"sentry-learned-");var body=Cast(p);var target=new BladeMissileTarget{NoKillAwards=true,Frozen=true,CantWalk=true};target.MoveToWorld(new(body.X+4,body.Y,body.Z),body.Map);target.AIObject?.AITimer.Stop();target.SetHits(50000);target.Hits=target.HitsMax;target.SetResistance(ResistanceType.Energy,0);
        long end=Core.TickCount+5000;int initial=SkillHealth.Balance256(target);
        void Observe()
        {
            try
            {
                Require(body is{Alive:true,Deleted:false}&&body.Prepared.ControllerFault==null,"Paid CBS died/faulted before observation");
                var bolt=AssassinSentryBolts.Snapshot().Bolts.FirstOrDefault(x=>x.Actor==body.Serial.Value&&x.Fault==null);
                if(SkillHealth.Balance256(target)==initial||bolt==null){Require(Core.TickCount<end,"Paid CBS did not naturally emit/damage");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(body.Prepared.Creation.Rank==21&&body.Prepared.Control.Dispatches==1,"Effective rank/first actual volley");
                // Move the wounded receiver off this held flight's path, so
                // reload inspection can observe its original identity safely.
                target.MoveToWorld(new(target.X,target.Y+6,target.Z),target.Map);SkillHealth.SetBalance256(body,12850);
                Hold(p,body,target,helm,bolt.Id,p.Location,p.Map.MapID,0);Log.Information("sentry-learned-result: passed");
            }
            catch(Exception e){Log.Error(e,"sentry-learned-result: failed");}
        }
        Observe();
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var body=(AssassinSentryBody)World.FindMobile((Serial)saved.Body);var target=(BladeMissileTarget)World.FindMobile((Serial)saved.Target);var helm=World.FindItem((Serial)saved.Helm);
        Require(saved.Generation<2&&helm?.Parent==p&&JsonSerializer.Serialize(body.Prepared)==JsonSerializer.Serialize(saved.Actor)
            &&JsonSerializer.Serialize(AssassinSentryBolts.State(saved.Bolt.Id))==JsonSerializer.Serialize(saved.Bolt)&&SkillHealth.Balance256(body)==12850
            &&SkillHealth.Balance256(target)==saved.Health&&NecromancerEffects.ManaBalance256(p)==saved.Mana,"Original paid actor/bolt/gear/wounds/mana changed on reload");
        Require(SkillBindings.Instance.Get(p).Right==CombatAbility.ChargedBoltSentry&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Assassin,7)==CombatAbility.ChargedBoltSentry,"CBS right/quick binding reload");
        p.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);FixtureNetwork.Authenticate(network,p,"sentry-learned-resume-");Clock(body,TimedEffects.Now.AddMilliseconds(40));long end=Core.TickCount+3000;
        void Observe()
        {
            try
            {
                var bolt=AssassinSentryBolts.State(saved.Bolt.Id);Require(body.Prepared.ControllerFault==null&&bolt!=null&&bolt.Fault==null,"Original paid actor/bolt resume fault");
                if(body.Prepared.Control.Frame<saved.Actor.Control.Frame+2||bolt.Frame<saved.Bolt.Frame+2){Require(Core.TickCount<end,"Original paid actor/bolt failed natural resume");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(bolt.Actor==body.Serial.Value&&bolt.Owner==p.Serial.Value&&bolt.Minimum256==saved.Bolt.Minimum256&&bolt.Maximum256==saved.Bolt.Maximum256
                    &&body.Prepared.Control.Dispatches==saved.Actor.Control.Dispatches&&NecromancerEffects.ManaBalance256(p)==saved.Mana,"Resume recast/recollected/repaid");
                Hold(p,body,target,helm,bolt.Id,saved.Origin,saved.Map,saved.Generation+1);Log.Information("sentry-learned-reload-result: passed");
            }
            catch(Exception e){Log.Error(e,"sentry-learned-reload-result: failed");}
        }
        Observe();
    }
    internal static void Input(NetState network,string label,CombatAbility ability=CombatAbility.ChargedBoltSentry)
    {
        var p=Fresh();Bind(p,ability);FixtureNetwork.Authenticate(network,p,"sentry-learned-input-");Ready(p);var aim=Aim(p);int mana=NecromancerEffects.ManaBalance256(p);bool windup=false;long end=Core.TickCount+5000;
        void Observe()
        {
            try
            {
                var s=ClassCombat.For(p);
                if(label.EndsWith("left"))
                {
                    if(s.Sequence==0){Require(Core.TickCount<end,"Forged left-slot request never arrived");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                    Require(s.PendingAbility==CombatAbility.None&&AssassinTrapRoster.For(p).Length==0&&NecromancerEffects.ManaBalance256(p)==mana,"Right-only CBS accepted left-slot request");
                    Log.Information("{Label}: acknowledged",label);Clean(p);Log.Information("{Label}-result: passed",label);return;
                }
                if(!windup&&s.PendingAbility==ability)
                {Require(s.Animation==9&&s.Timing==AssassinSentryCasts.Timing(p)&&NecromancerEffects.ManaBalance256(p)==mana&&AssassinTrapRoster.For(p).Length==0,"Input created/paid before release");windup=true;Log.Information("{Label}: windup",label);}
                var traps=AssassinTrapRoster.For(p);
                if(traps.Length==0){Require(Core.TickCount<end,"Input did not create trap");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Observe);return;}
                Require(windup&&traps.Length==1&&NecromancerEffects.ManaBalance256(p)==mana-(ability==CombatAbility.LightningSentry?5120:3328)&&FireGround.Ready(p,ability),"Input count/payment/local delay");
                Clean(p);Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Clean(p);Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Assassin,aim.X,aim.Y,aim.Z);Observe();});
    }
}
