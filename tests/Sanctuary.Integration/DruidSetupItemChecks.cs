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

public static class DruidSetupItemChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DruidSetupItemChecks));
    private sealed record Saved(uint Owner,uint Weapon,uint Helm,uint Target,CombatAbility Ability,CombatAbility Form,DateTime Until,int Loss,int Defense,int Life,string Gear,string Shape);
    private static readonly (CombatAbility Ability,CombatAbility Form)[] Cases=[
        (CombatAbility.Maul,CombatAbility.Werebear),(CombatAbility.FeralRage,CombatAbility.Werewolf),
        (CombatAbility.FireClaws,CombatAbility.Werewolf),(CombatAbility.FireClaws,CombatAbility.Werebear),
        (CombatAbility.Hunger,CombatAbility.Werewolf),(CombatAbility.Hunger,CombatAbility.Werebear),
        (CombatAbility.Rabies,CombatAbility.Werewolf),(CombatAbility.Fury,CombatAbility.Werewolf)];
    private static string Manifest=>Path.Join(Core.BaseDirectory,"druid-setup-items-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static DateTime Until(Mobile t)=>TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value)?.HealBlockedUntilUtc??default;
    private static bool Captured(CombatAbility a)=>a is CombatAbility.Maul or CombatAbility.FeralRage or CombatAbility.FireClaws;
    private sealed class Rolls:IDisposable
    {
        private readonly Func<double> Charge=DruidChargedAttacks.HitRoll,Fire=DruidFireClaws.HitRoll,Hunger=DruidHunger.HitRoll,Rabies=DruidRabies.HitRoll,Fury=DruidFury.HitRoll;
        private readonly Func<int,int> Block=SourceBlocking.BlockRoll,Choice=SourceBlocking.ChoiceRoll;
        internal static void Hit(bool yes){DruidChargedAttacks.HitRoll=DruidFireClaws.HitRoll=DruidHunger.HitRoll=DruidRabies.HitRoll=DruidFury.HitRoll=()=>yes?0:1;}
        internal Rolls(){Hit(true);SourceBlocking.ChoiceRoll=_=>1;SourceBlocking.BlockRoll=n=>n-1;}
        public void Dispose(){DruidChargedAttacks.HitRoll=Charge;DruidFireClaws.HitRoll=Fire;DruidHunger.HitRoll=Hunger;DruidRabies.HitRoll=Rabies;DruidFury.HitRoll=Fury;SourceBlocking.BlockRoll=Block;SourceBlocking.ChoiceRoll=Choice;}
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"druid-setup-items-reload-check":"druid-setup-items-check";ConsoleInputHandler.RegisterCommand(name,"Real form hit/setup item phases and exact saved state",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}else{Check();}Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string skill in new[]{"maul","hunger","rabies","fury"})foreach(string mode in new[]{"right","quick"})
        {string label="druid-setup-items-"+skill+"-"+mode;CombatAbility ability=skill switch{"maul"=>CombatAbility.Maul,"hunger"=>CombatAbility.Hunger,"rabies"=>CombatAbility.Rabies,_=>CombatAbility.Fury};ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated real Woestave/Blackhorn setup phases",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label,ability));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static Item Named(PlayerMobile p,string key)
    {var item=NamedGear.Create(NamedGear.Catalog.Items.Single(d=>d.SourceKey==key).Id,99,new Random(173),true);p.AddToBackpack(item);Require(p.EquipItem(item),"Equip complete "+key);ClassGear.Refresh(p);return item;}
    private static PlayerMobile Owner(CombatAbility ability,CombatAbility form,int row,out Item weapon,out Item helm)
    {
        var p=HellslayerChecks.Owner(DiabloClass.Druid,row);ClassSkillChecks.Train(p,SkillMechanics.For(ability).TalentId);ClassSkillChecks.Train(p,"druid.werewolf");ClassSkillChecks.Train(p,"druid.werebear");ClassSkillChecks.Train(p,"druid.lycanthropy",20);
        weapon=Named(p,"Woestave");helm=Named(p,"Blackhorn's Face");Require(ClassGear.Bonus(p,"dmg-ac")==-50&&ClassGear.Bonus(p,"noheal")==2,"Real setup property suppliers differ");
        Require(DruidPlayerForms.ApplyPrepared(p,form,TimedEffects.Now),"Enter setup form");Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Bind setup skill");return p;
    }
    private static GraveyardSkeleton Target(PlayerMobile p)
    {var t=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(60000);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}Require(SourceBlocking.TryRate(t,out int rate,out _)&&rate>0&&CreatureProfiles.Defense(t)>0,"Missing shield/defense control");return t;}
    private static void Ready(PlayerMobile p)
    {ClassCombat.Stop(p);DruidAttackCharges.Clear(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static void DiscardRabies(PlayerMobile p)
    {var s=DruidRabiesInfections.Snapshot();DruidRabiesInfections.Restore(s with{Infections=s.Infections.Where(x=>x.Caster!=p.Serial.Value).ToArray(),Contagions=s.Contagions.Where(x=>x.Caster!=p.Serial.Value).ToArray(),Markers=s.Markers.Where(x=>x.Caster!=p.Serial.Value).ToArray(),Seeds=s.Seeds.Where(x=>x.Caster!=p.Serial.Value).ToArray()});}
    private static void Drop(PlayerMobile p,BaseCreature t)
    {ClassCombat.Stop(p);DiscardRabies(p);DruidPlayerForms.Clear(p);t?.Delete();p.Delete();}
    private static void Case(PlayerMobile p,CombatAbility ability,bool hit,bool blocked)
    {
        var t=Target(p);Ready(p);Rolls.Hit(hit);int blocks=0,basis=CreatureProfiles.Defense(t)+NpcEquipmentCombat.Defense(t),expected=Math.Min(50,basis);int before=SkillHealth.Balance256(t);
        SourceBlocking.BlockRoll=_=>{blocks++;Require(Until(t)>TimedEffects.Now&&TimedEffects.DefenseLoss(t)==0,"PMH did not precede shield selection,or armor loss preceded damage allocation");return blocked?0:99;};
        try
        {
            bool begun=ClassCombat.Perform(p,ability,t,t.Location,Core.TickCount);var s=ClassCombat.For(p);
            if(ability==CombatAbility.Rabies&&!hit)
            {Require(!begun&&Until(t)<=TimedEffects.Now&&TimedEffects.DefenseLoss(t)==0,"Failed initial Rabies applied setup effects");return;}
            if(ability==CombatAbility.Rabies&&blocked)
            {Require(!begun&&Until(t)>TimedEffects.Now&&TimedEffects.DefenseLoss(t)==0&&blocks==1,"Initial Rabies block lost pre-block PMH or allocated armor loss");return;}
            Require(begun,"Setup skill start");bool early=Captured(ability)||ability==CombatAbility.Rabies;
            Require((Until(t)>TimedEffects.Now)==(early&&hit)&&TimedEffects.DefenseLoss(t)==(Captured(ability)&&hit&&!blocked?expected:0)&&SkillHealth.Balance256(t)==before,"Wrong start/allocation phase or early HP loss");
            DateTime retained=default;
            if(Captured(ability)&&hit)
            {
                var state=TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value);retained=state.HealBlockedUntilUtc.AddMinutes(-1);
                TimedEffects.Load(TimedEffects.Snapshot().Where(x=>x.Owner!=t.Serial.Value).Append(state with{HealBlockedUntilUtc=retained}).ToArray());
            }
            Require(!ClassCombat.ResolveAction(p,s.EffectAt-1)&&ClassCombat.ResolveAction(p,s.EffectAt),"Setup release clock");ClassCombat.Stop(p);
            Require((Until(t)>TimedEffects.Now)==hit&&TimedEffects.DefenseLoss(t)==(hit&&!blocked?expected:0),"Release omitted or fabricated setup effects");
            if(Captured(ability)&&hit){Require(Until(t)==retained,"Stored packet release refreshed a start-only no-heal deadline");}
            if(!hit||blocked){Require(SkillHealth.Balance256(t)==before,"Miss/block allocated ordinary damage");}
            var deadline=Until(t);int loss=TimedEffects.DefenseLoss(t);Require(!ClassCombat.ResolveAction(p,s.EffectAt)&&Until(t)==deadline&&TimedEffects.DefenseLoss(t)==loss,"Setup release replayed an item effect");
        }
        finally{ClassCombat.Stop(p);DiscardRabies(p);t.Delete();}
    }
    private static void Check()
    {
        using var rolls=new Rolls();var saved=new Saved[Cases.Length];
        for(int i=0;i<Cases.Length;i++)
        {
            var c=Cases[i];var p=Owner(c.Ability,c.Form,2600+i*36,out var weapon,out var helm);
            foreach(bool hit in new[]{false,true})foreach(bool block in new[]{false,true}){Case(p,c.Ability,hit,block);}
            Cancel(p,c.Ability);SourceBlocking.BlockRoll=n=>n-1;
            Rolls.Hit(true);SourceBlocking.BlockRoll=n=>n-1;Ready(p);var t=Target(p);Require(ClassCombat.Perform(p,c.Ability,t,t.Location,Core.TickCount),"Saved setup start");long at=ClassCombat.For(p).EffectAt;
            Require(ClassCombat.ResolveAction(p,at),"Saved setup release");ClassCombat.Stop(p);DiscardRabies(p);
            var point=p.Location;p.Internalize();PoisonDaggerChecks.Freeze(t);
            saved[i]=new(p.Serial.Value,weapon.Serial.Value,helm.Serial.Value,t.Serial.Value,c.Ability,c.Form,Until(t),TimedEffects.DefenseLoss(t),ClassStats.Defense(t),SkillHealth.Balance256(t),Json(new[]{ClassGear.State(weapon),ClassGear.State(helm)}),Json(DruidPlayerForms.State(p)));
        }
        File.WriteAllText(Manifest,Json(saved));
    }
    private static void Cancel(PlayerMobile p,CombatAbility ability)
    {
        SourceBlocking.BlockRoll=n=>n-1;Rolls.Hit(true);Ready(p);var t=Target(p);
        try
        {
            Require(ClassCombat.Perform(p,ability,t,t.Location,Core.TickCount),"Setup cancellation start");long at=ClassCombat.For(p).EffectAt;var until=Until(t);int loss=TimedEffects.DefenseLoss(t),mana=NecromancerEffects.ManaBalance256(p);
            ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at)&&Until(t)==until&&TimedEffects.DefenseLoss(t)==loss&&NecromancerEffects.ManaBalance256(p)==mana,"Stop reverted/replayed setup or refunded payment");
        }
        finally{DiscardRabies(p);t.Delete();}
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest));Require(saved.Length==Cases.Length,"Missing saved setup contexts");
        foreach(var s in saved)
        {
            var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var weapon=World.FindItem((Serial)s.Weapon);var helm=World.FindItem((Serial)s.Helm);var t=(BaseCreature)World.FindMobile((Serial)s.Target);
            Require(weapon.Parent==p&&helm.Parent==p&&Json(new[]{ClassGear.State(weapon),ClassGear.State(helm)})==s.Gear&&Json(DruidPlayerForms.State(p))==s.Shape&&Until(t)==s.Until&&TimedEffects.DefenseLoss(t)==s.Loss&&ClassStats.Defense(t)==s.Defense&&SkillHealth.Balance256(t)==s.Life&&!TimedEffects.CanRegenerateHits(t),"Saved original/no-heal deadline/armor/wound/form changed");
        }
    }
    private static bool Pending(PlayerMobile p,CombatAbility a)
    {var s=ClassCombat.For(p);return a switch{CombatAbility.Maul=>s.DruidChargeAttack!=null,CombatAbility.Hunger=>s.HungerAttack!=null,CombatAbility.Rabies=>s.RabiesAttack!=null,_=>s.Fury!=null};}
    private static void Input(NetState network,string label,CombatAbility ability)
    {
        var p=Owner(ability,ability==CombatAbility.Maul?CombatAbility.Werebear:CombatAbility.Werewolf,2500,out var weapon,out var helm);Ready(p);var rolls=new Rolls();var t=Target(p);var account=FixtureNetwork.Authenticate(network,p,"druid-setup-");bool began=false;long deadline=Core.TickCount+7000;
        int expected=Math.Min(50,CreatureProfiles.Defense(t)+NpcEquipmentCombat.Defense(t));DateTime early=default;
        void Cleanup(){rolls.Dispose();ClassCombat.Stop(p);Drop(p,t);weapon.Delete();helm.Delete();account.Delete();}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Setup input timeout");
                if(!began&&Pending(p,ability))
                {
                    bool initial=Captured(ability)||ability==CombatAbility.Rabies;Require((Until(t)>TimedEffects.Now)==initial&&TimedEffects.DefenseLoss(t)==(Captured(ability)?expected:0),"Wire windup used wrong setup phase");
                    if(Captured(ability)){var state=TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value);early=state.HealBlockedUntilUtc.AddMinutes(-1);TimedEffects.Load(TimedEffects.Snapshot().Where(x=>x.Owner!=t.Serial.Value).Append(state with{HealBlockedUntilUtc=early}).ToArray());}
                    began=true;Log.Information("{Label}: windup",label);
                }
                if(!began||Pending(p,ability)&&ability!=CombatAbility.Fury||ability==CombatAbility.Fury&&ClassCombat.For(p).Fury is{Released:0}){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(Until(t)>TimedEffects.Now&&TimedEffects.DefenseLoss(t)==expected&&(!Captured(ability)||Until(t)==early),"Wire contact omitted/duplicated setup effects");Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Druid,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
