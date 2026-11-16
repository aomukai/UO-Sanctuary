// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class DruidWeaponPoisonChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DruidWeaponPoisonChecks));
    private sealed record Saved(uint Owner,uint Weapon,uint Gloves,uint Target,CombatAbility Ability,CombatAbility Form,Point3D Point,string Gear,string Shape,string Poison,int Life,int Mana,int TargetLife,int Generation);
    private static readonly (CombatAbility Ability,CombatAbility Form)[] Cases=[(CombatAbility.Maul,CombatAbility.Werebear),(CombatAbility.FeralRage,CombatAbility.Werewolf),(CombatAbility.FireClaws,CombatAbility.Werewolf),(CombatAbility.FireClaws,CombatAbility.Werebear),(CombatAbility.Hunger,CombatAbility.Werewolf),(CombatAbility.Hunger,CombatAbility.Werebear),(CombatAbility.Rabies,CombatAbility.Werewolf),(CombatAbility.Fury,CombatAbility.Werewolf)];
    private static string Manifest=>Path.Join(Core.BaseDirectory,"druid-weapon-poison-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static bool Captured(CombatAbility a)=>a is CombatAbility.Maul or CombatAbility.FeralRage or CombatAbility.FireClaws;
    private sealed class NoEffects:Random{public override int Next(int maximum)=>maximum-1;}
    private sealed class Rolls:IDisposable
    {
        private readonly Func<double> Charge=DruidChargedAttacks.HitRoll,Fire=DruidFireClaws.HitRoll,Hunger=DruidHunger.HitRoll,Rabies=DruidRabies.HitRoll,Fury=DruidFury.HitRoll;
        private readonly Random C=DruidChargedAttacks.Effects,F=DruidFireClaws.Effects,H=DruidHunger.Effects,R=DruidRabies.Effects,Y=DruidFury.Effects;
        private readonly Func<int,int> Poison=DruidWeaponContact.PoisonRoll,Block=SourceBlocking.BlockRoll,Choice=SourceBlocking.ChoiceRoll;
        internal Rolls(){Hit(true);DruidWeaponContact.PoisonRoll=_=>0;SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=_=>1;DruidChargedAttacks.Effects=DruidFireClaws.Effects=DruidHunger.Effects=DruidRabies.Effects=DruidFury.Effects=new NoEffects();}
        internal static void Hit(bool yes){DruidChargedAttacks.HitRoll=DruidFireClaws.HitRoll=DruidHunger.HitRoll=DruidRabies.HitRoll=DruidFury.HitRoll=()=>yes?0:1;}
        public void Dispose(){DruidChargedAttacks.HitRoll=Charge;DruidFireClaws.HitRoll=Fire;DruidHunger.HitRoll=Hunger;DruidRabies.HitRoll=Rabies;DruidFury.HitRoll=Fury;DruidChargedAttacks.Effects=C;DruidFireClaws.Effects=F;DruidHunger.Effects=H;DruidRabies.Effects=R;DruidFury.Effects=Y;DruidWeaponContact.PoisonRoll=Poison;SourceBlocking.BlockRoll=Block;SourceBlocking.ChoiceRoll=Choice;}
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"druid-weapon-poison-reload-check":"druid-weapon-poison-check";ConsoleInputHandler.RegisterCommand(name,"Real source item poison on all six form skills and saved clocks",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string skill in new[]{"maul","hunger","rabies","fury"})foreach(string mode in new[]{"right","quick"})
        {string label="druid-weapon-poison-"+skill+"-"+mode;CombatAbility ability=skill switch{"maul"=>CombatAbility.Maul,"hunger"=>CombatAbility.Hunger,"rabies"=>CombatAbility.Rabies,_=>CombatAbility.Fury};ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated real Blackbog/Venom Grip poison allocation",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label,ability));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(CombatAbility ability,CombatAbility form,int row,out Item weapon,out Item gloves)
    {
        var p=HellslayerChecks.Owner(DiabloClass.Druid,row);ClassSkillChecks.Train(p,SkillMechanics.For(ability).TalentId);ClassSkillChecks.Train(p,"druid.werewolf");ClassSkillChecks.Train(p,"druid.werebear");ClassSkillChecks.Train(p,"druid.lycanthropy",20);
        weapon=DruidSetupItemChecks.Named(p,"Blackbog's Sharp");gloves=DruidSetupItemChecks.Named(p,"Venom Grip");Require(DruidPlayerForms.ApplyPrepared(p,form,TimedEffects.Now),"Poison form entry");
        var raw=BowLaunch.RawPoison(p,CombatTiming.Equipped(p));Require(raw.PoisonMin==653&&raw.PoisonMax==653&&raw.PoisonFrames==350&&raw.PoisonCount==2,"Complete named poison pools changed");
        Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Bind poison skill");return p;
    }
    private static PoisonDaggerContactChecks.Recipient Target(PlayerMobile p,int poison)
    {
        var t=new PoisonDaggerContactChecks.Recipient();t.SetHits(60000);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Poison,poison);return t;
    }
    private static TimedEffectState Poison(Mobile t)=>TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value&&x.PoisonMs>0);
    private static void Ready(PlayerMobile p){ClassCombat.Stop(p);DruidAttackCharges.Clear(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void ClearRabies(PlayerMobile p)
    {var s=DruidRabiesInfections.Snapshot();DruidRabiesInfections.Restore(s with{Infections=s.Infections.Where(x=>x.Caster!=p.Serial.Value).ToArray(),Contagions=s.Contagions.Where(x=>x.Caster!=p.Serial.Value).ToArray(),Markers=s.Markers.Where(x=>x.Caster!=p.Serial.Value).ToArray(),Seeds=s.Seeds.Where(x=>x.Caster!=p.Serial.Value).ToArray()});}
    private static PoisonDaggerContactChecks.Recipient Cast(PlayerMobile p,Item gloves,CombatAbility ability,int resistance,bool change=false,bool miss=false,bool block=false,bool cancel=false,bool full=false)
    {
        Require(gloves.Parent==p,"Poison glove must be equipped at start");Ready(p);Rolls.Hit(true);SourceBlocking.BlockRoll=n=>n-1;var t=Target(p,resistance);int before=SkillHealth.Balance256(t),life=SkillHealth.Balance256(p),mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
        if(Captured(ability)){Rolls.Hit(!miss);if(block){SourceBlocking.BlockRoll=_=>0;}}
        Require(ClassCombat.Perform(p,ability,t,t.Location,Core.TickCount),"Poison skill start");var s=ClassCombat.For(p);long at=s.EffectAt;
        Require(SkillHealth.Balance256(t)==before&&Poison(t)==null&&!ClassCombat.ResolveAction(p,at-1),"Poison applied during windup");
        if(change){p.AddToBackpack(gloves);t.SetResistance(ResistanceType.Poison,0);}
        if(cancel)
        {int paid=NecromancerEffects.ManaBalance256(p);ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at)&&SkillHealth.Balance256(t)==before&&Poison(t)==null&&NecromancerEffects.ManaBalance256(p)==paid,"Cancelled poison packet committed/refunded");return t;}
        if(!Captured(ability)){Rolls.Hit(!miss);if(block){SourceBlocking.BlockRoll=_=>0;}}
        int releases=0;
        do{Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt),"Poison skill action");releases++;}while(full&&ability==CombatAbility.Fury&&ClassCombat.For(p).Fury is{Remaining:>0});
        ClassCombat.Stop(p);ClearRabies(p);int rate=change?(Captured(ability)?326:500):resistance>=100?0:resistance==50?326:653;int frames=change&&!Captured(ability)?250:175;
        if(miss||block){Require(SkillHealth.Balance256(t)==before,"Miss/block committed an initial weapon poison rate");if(ability!=CombatAbility.Rabies){Require(Poison(t)==null,"Miss/block committed weapon poison state");}}
        else
        {
            Require(before-SkillHealth.Balance256(t)==rate*releases&&SkillHealth.Balance256(p)==life,"Initial poison rate missing,duplicated,reduced as Hunger physical damage or leeched");
            var state=Poison(t);Require(rate==0?state==null:state is{PoisonClock:not null}&&state.PoisonClock.Rate256==rate&&state.PoisonMs==frames*40&&state.PoisonSource==p.Serial.Value,"Source poison rate/length/count/capture/defense or strongest-state replacement differs");
            if(full){Require(releases==2,"Rank-one Fury did not release both poisoned contacts");}
        }
        Require(NecromancerEffects.ManaBalance256(p)==mana-cost,"Poison changed payment timing/count");return t;
    }
    private static void CasesFor(PlayerMobile p,Item gloves,CombatAbility ability)
    {
        foreach(var c in new[]{(0,false,false,false,false),(50,true,false,false,false),(100,false,false,false,false),(0,false,true,false,false),(0,false,false,true,false),(0,false,false,false,true)})
        {
            var t=Cast(p,gloves,ability,c.Item1,c.Item2,c.Item3,c.Item4,c.Item5);
            t.Delete();if(gloves.Parent!=p){Require(p.EquipItem(gloves),"Restore real poison glove");ClassGear.Refresh(p);}Rolls.Hit(true);SourceBlocking.BlockRoll=n=>n-1;ClearRabies(p);
        }
        if(ability==CombatAbility.Fury){Cast(p,gloves,ability,0,full:true).Delete();}
    }
    private static void Check(bool reload)
    {
        using var rolls=new Rolls();var saved=reload?JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)):new Saved[Cases.Length];
        for(int i=0;i<Cases.Length;i++)
        {
            var c=Cases[i];PlayerMobile p;Item weapon,gloves;PoisonDaggerContactChecks.Recipient t;int generation=0;
            if(!reload)
            {
                p=Owner(c.Ability,c.Form,1800+i*36,out weapon,out gloves);CasesFor(p,gloves,c.Ability);t=Cast(p,gloves,c.Ability,50);PoisonDaggerChecks.Freeze(t);
            }
            else
            {
                var s=saved[i];generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);weapon=World.FindItem((Serial)s.Weapon);gloves=World.FindItem((Serial)s.Gloves);t=(PoisonDaggerContactChecks.Recipient)World.FindMobile((Serial)s.Target);
                Require(generation<=2&&s.Ability==c.Ability&&s.Form==c.Form&&weapon.Parent==p&&gloves.Parent==p&&Json(new[]{ClassGear.State(weapon),ClassGear.State(gloves)})==s.Gear&&Json(DruidPlayerForms.State(p))==s.Shape&&Json(Poison(t))==s.Poison&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(t)==s.TargetLife,"Saved poison clock/originals/form/resources/wounds changed");p.MoveToWorld(s.Point,Map.Trammel);Cast(p,gloves,c.Ability,0).Delete();
            }
            ClearRabies(p);var point=p.Location;p.Internalize();saved[i]=new(p.Serial.Value,weapon.Serial.Value,gloves.Serial.Value,t.Serial.Value,c.Ability,c.Form,point,Json(new[]{ClassGear.State(weapon),ClassGear.State(gloves)}),Json(DruidPlayerForms.State(p)),Json(Poison(t)),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(t),generation);
        }
        File.WriteAllText(Manifest,Json(saved));
    }
    private static void Input(NetState n,string label,CombatAbility ability)
    {
        var rolls=new Rolls();var p=Owner(ability,ability==CombatAbility.Maul?CombatAbility.Werebear:CombatAbility.Werewolf,2200,out var weapon,out var gloves);var t=Target(p,0);Ready(p);var account=FixtureNetwork.Authenticate(n,p,"druid-poison-");int before=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;bool began=false;long deadline=Core.TickCount+6000;
        void Cleanup(){ClassCombat.Stop(p);ClearRabies(p);rolls.Dispose();DruidPlayerForms.Clear(p);t.Delete();p.Delete();account.Delete();}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Weapon poison input timeout");var s=ClassCombat.For(p);
                if(!began&&s.PendingAbility==ability){Require(SkillHealth.Balance256(t)==before&&Poison(t)==null&&NecromancerEffects.ManaBalance256(p)==mana-(ability==CombatAbility.Hunger?0:cost),"Wire poison windup changed HP/status/payment");began=true;Log.Information("{Label}: windup",label);}
                var state=Poison(t);if(state==null){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(began&&state.PoisonClock?.Rate256==653&&state.PoisonMs is>0 and<=7000&&state.PoisonSource==p.Serial.Value&&SkillHealth.Balance256(t)<=before-653&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Wire weapon poison rate/count/length/source/initial hit or payment differs");Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Druid,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
