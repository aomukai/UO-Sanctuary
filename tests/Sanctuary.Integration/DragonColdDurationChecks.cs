// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DragonColdDurationChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DragonColdDurationChecks));
    private static readonly CombatAbility[] Cases=[CombatAbility.DragonClaw,CombatAbility.DragonTail,CombatAbility.DragonTalon,CombatAbility.DragonFlight];
    private sealed record Saved(uint Owner,uint Target,uint Ring,CombatAbility Ability,string Gear,string State,string Charges,int Life,int Mana,int TargetLife,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"dragon-cold-duration-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private sealed class Rolls:IDisposable
    {
        private readonly Func<double>[] Hit=[DragonClaws.HitRoll,DragonTails.HitRoll,DragonTalons.HitRoll,DragonFlights.HitRoll];
        private readonly Func<int,int> Block=SourceBlocking.BlockRoll,Choice=SourceBlocking.ChoiceRoll;
        internal Rolls(){DragonClaws.HitRoll=DragonTails.HitRoll=DragonTalons.HitRoll=DragonFlights.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=_=>1;}
        public void Dispose(){DragonClaws.HitRoll=Hit[0];DragonTails.HitRoll=Hit[1];DragonTalons.HitRoll=Hit[2];DragonFlights.HitRoll=Hit[3];SourceBlocking.BlockRoll=Block;SourceBlocking.ChoiceRoll=Choice;}
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"dragon-cold-duration-reload-check":"dragon-cold-duration-check";ConsoleInputHandler.RegisterCommand(name,"Real Raven Frost and Ice replacement on four learned Dragon finishers",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(var ability in Cases)foreach(string mode in ability==CombatAbility.DragonClaw?new[]{"right","quick"}:new[]{"right"})
        {var a=ability;string label="dragon-cold-"+a.ToString().ToLowerInvariant()+"-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated Raven/Ice duration through learned finisher",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label,a));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(CombatAbility a,int row,out Item ring)
    {
        var p=DragonClawChecks.Fresh(6640,row);foreach(var ability in Cases){ClassSkillChecks.Train(p,SkillMechanics.For(ability).TalentId);}ClassSkillChecks.Train(p,"assassin.bladesofice");
        ring=DruidSetupItemChecks.Named(p,"Raven Frost");Require(NamedGear.ColdDuration(p)==4000,"Real Raven Frost four-second item length");Require(SkillBindings.Instance.Assign(p,false,a)&&SkillBindings.Instance.AssignQuick(p,7,a),"Cold finisher bindings");return p;
    }
    private static TimedEffectState State(Mobile t)=>TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value);
    private static void Ready(PlayerMobile p,int count)
    {
        ClassCombat.Stop(p);AssassinCharges.Clear(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);
        if(count>0){DragonClawChecks.Hold(p,CombatAbility.BladesOfIce,count);}
    }
    private static int Packets(CombatAbility ability)=>ability==CombatAbility.DragonTail?2:1;
    private static int Frames(PlayerMobile p,int count,int resistance)=>resistance==100?0:(count==0?100:SkillMechanics.Values(p,CombatAbility.BladesOfIce).DurationFrames)*(100-resistance)/100;
    private static void StateCheck(PlayerMobile p,BaseCreature t,int count,int resistance)
    {
        int frames=Frames(p,count,resistance);var state=State(t);
        if(frames==0){Require(state?.Cold==null&&state?.Control?.Freeze==null,"Cold immunity gained duration");return;}
        Require(state?.Cold!=null&&Math.Abs((state.ChillUntilUtc-TimedEffects.Now).TotalMilliseconds-frames*40)<80,"Cold duration added item length or mitigated twice");
        Require((state.Control?.Freeze!=null)==(count==3),"Freeze charge threshold changed");
        if(count==3){Require(Math.Abs((state.Control.FreezeUntil-TimedEffects.Now).TotalMilliseconds-frames*40)<80,"Freeze included replaced item length");}
        Require(AssassinCharges.State(p,CombatAbility.BladesOfIce)?.Count==(count>1?count-1:null),"First finisher contact did not consume one Ice charge");
    }
    private static void First(PlayerMobile p,BaseCreature t,CombatAbility ability,int count,int resistance)
    {
        Ready(p,count);t.SetResistance(ResistanceType.Cold,resistance);int mana=NecromancerEffects.ManaBalance256(p),packets=NativeMonsterReturn.Session(t).Packets;Require(ClassCombat.Perform(p,ability,t,t.Location,Core.TickCount),"Cold finisher entry");ClassCombat.ReleaseCast(p);
        for(int i=0;i<4&&NativeMonsterReturn.Session(t).Packets==packets;i++){Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Cold finisher stage");}
        Require(NativeMonsterReturn.Session(t).Packets==packets+Packets(ability)&&NecromancerEffects.ManaBalance256(p)==mana-SkillMechanics.Values(p,ability).Mana256,$"First-contact count/payment changed {ability}/{count}/{resistance}:packets={NativeMonsterReturn.Session(t).Packets-packets},mana={NecromancerEffects.ManaBalance256(p)}/{mana-SkillMechanics.Values(p,ability).Mana256}");ClassCombat.Stop(p);StateCheck(p,t,count,resistance);
    }
    private static void Check(bool reload)
    {
        using var rolls=new Rolls();var saved=reload?JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)):new Saved[Cases.Length];
        for(int i=0;i<Cases.Length;i++)
        {
            PlayerMobile p;Item ring;BaseCreature t;int generation=0;var ability=Cases[i];
            if(!reload)
            {
                p=Owner(ability,2700+i*40,out ring);
                foreach(int count in new[]{0,1,3})foreach(int resistance in new[]{0,50,100}){var target=DragonClawChecks.Target(p);try{First(p,target,ability,count,resistance);}finally{target.Delete();}}
                t=DragonClawChecks.Target(p);First(p,t,ability,3,0);var state=State(t);var until=TimedEffects.Now.AddHours(1);
                TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==t.Serial.Value?x with{UpdatedUtc=until,ChillUntilUtc=until,Control=x.Control with{FreezeUntil=until,Freeze=x.Control.Freeze with{AiUntilUtc=until}}}:x).ToArray());
                SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-257);p.Internalize();
            }
            else
            {
                var s=saved[i];generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);ring=World.FindItem((Serial)s.Ring);t=(BaseCreature)World.FindMobile((Serial)s.Target);
                Require(generation<=2&&s.Ability==ability&&ring.Parent==p&&Json(ClassGear.State(ring))==s.Gear&&Json(State(t))==s.State&&Json(AssassinCharges.Finisher(p))==s.Charges&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(t)==s.TargetLife,"Saved cold/freeze,original,charges,resources or wounds changed");
                FixtureSpace.Corridor(p,6640,30,3200+i*40);var fresh=DragonClawChecks.Target(p);try{First(p,fresh,ability,3,50);}finally{fresh.Delete();}p.Internalize();
            }
            saved[i]=new(p.Serial.Value,t.Serial.Value,ring.Serial.Value,ability,Json(ClassGear.State(ring)),Json(State(t)),Json(AssassinCharges.Finisher(p)),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(t),generation);
        }
        File.WriteAllText(Manifest,Json(saved));
    }
    private static void Input(NetState n,string label,CombatAbility ability)
    {
        var rolls=new Rolls();var p=Owner(ability,3150,out var ring);var t=DragonClawChecks.Target(p);Ready(p,3);var account=FixtureNetwork.Authenticate(n,p,"dragon-cold-");int packets=NativeMonsterReturn.Session(t).Packets;long deadline=Core.TickCount+6000;bool began=false;
        void Cleanup(){ClassCombat.Stop(p);AssassinCharges.Clear(p);rolls.Dispose();t.Delete();p.Delete();account.Delete();}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Cold finisher wire timeout");var s=ClassCombat.For(p);if(!began&&s.PendingAbility==ability){began=true;Log.Information("{Label}: windup",label);}
                if(NativeMonsterReturn.Session(t).Packets==packets){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(began&&NativeMonsterReturn.Session(t).Packets==packets+Packets(ability),"Cold wire duplicated first contact");ClassCombat.Stop(p);StateCheck(p,t,3,0);Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Assassin,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
