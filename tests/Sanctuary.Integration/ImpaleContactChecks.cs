// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class ImpaleContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ImpaleContactChecks));
    private sealed record Saved(uint Owner,uint Weapon,uint Gloves,uint Helm,Point3D Point,string Gear,string Profile,int Life,int Mana,int Durability,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"impale-contact-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private sealed class High:Random{internal int Calls;public override int Next(int n){Calls++;return n-1;}}
    private sealed class Rolls:IDisposable
    {
        private readonly Func<double> Hit=AmazonImpale.HitRoll,Element=AmazonImpale.ElementRoll;
        private readonly Func<int,int> Physical=AmazonImpale.PhysicalRoll,Poison=AmazonImpale.PoisonRoll,Block=SourceBlocking.BlockRoll,Choice=SourceBlocking.ChoiceRoll;
        private readonly Random Effects=AmazonImpale.Effects,Special=AmazonImpale.SpecialWearRoll,Wear=AmazonImpale.WearRoll;
        internal readonly High SpecialCounter=new(),WearCounter=new();
        internal Rolls(){AmazonImpale.HitRoll=AmazonImpale.ElementRoll=()=>0;AmazonImpale.PhysicalRoll=AmazonImpale.PoisonRoll=_=>0;AmazonImpale.Effects=new High();AmazonImpale.SpecialWearRoll=SpecialCounter;AmazonImpale.WearRoll=WearCounter;SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=_=>1;}
        public void Dispose(){AmazonImpale.HitRoll=Hit;AmazonImpale.ElementRoll=Element;AmazonImpale.PhysicalRoll=Physical;AmazonImpale.PoisonRoll=Poison;AmazonImpale.Effects=Effects;AmazonImpale.SpecialWearRoll=Special;AmazonImpale.WearRoll=Wear;SourceBlocking.BlockRoll=Block;SourceBlocking.ChoiceRoll=Choice;}
    }
    private static PlayerProcSave Own(PlayerMobile p,Item weapon)
    {var s=PlayerItemProcs.Capture();return s with{Items=s.Items.Where(x=>x.Item==weapon.Serial.Value).ToArray(),Owners=s.Owners.Where(x=>x.Actor==p.Serial.Value).ToArray()};}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"impale-contact-reload-check":"impale-contact-check";ConsoleInputHandler.RegisterCommand(name,"Impale captured contact,post-wear poison,ordered item/wear events and originals",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(string mode in new[]{"right","quick"})
        {string label="impale-contact-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated captured Impale contact and equipped proc",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(int row,out Item weapon,out Item gloves,out Item helm)
    {
        var p=HellslayerChecks.Owner(DiabloClass.Amazon,row);ClassSkillChecks.Train(p,"amazon.impale");weapon=PlayerItemProcWorldChecks.Carrier(p,new Spear());gloves=DruidSetupItemChecks.Named(p,"Venom Grip");helm=DruidSetupItemChecks.Named(p,"Blackhorn's Face");ClassGear.Refresh(p);
        Require(SkillBindings.Instance.Assign(p,false,CombatAbility.Impale)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Impale),"Impale bindings");return p;
    }
    private static void Ready(PlayerMobile p)
    {AmazonImpale.Cancel(p);ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static BaseCreature Target(PlayerMobile p)
    {var t=new PoisonDaggerContactChecks.Recipient();t.SetHits(60000);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Poison,0);return t;}
    private static void Clean(PlayerMobile p,BaseCreature t)
    {AmazonImpale.Cancel(p);ClassCombat.Stop(p);SorceressSpellMissiles.Cancel(p);SourceKnockbacks.Clear(t);t.Delete();}
    private static void Scenarios(PlayerMobile p,Item weapon,Item gloves,Rolls rolls)
    {
        using var proof=PlayerItemProcWorld.Candidate(p);var observed=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);if(v.Phase=="domeleedamage"){Require(rolls.WearCounter.Calls==0,"Ordinary wear preceded damage events");}else if((v.Packet.ResultFlags&1)!=0){Require(rolls.WearCounter.Calls==1,"Ordinary wear missing before attack event");}}};
            foreach(string scenario in new[]{"hit","change","immune","range","cancel","low-hit","low-miss","low-block"})
            {
                Ready(p);var t=Target(p);bool basic=scenario.StartsWith("low-");if(scenario=="immune"){t.SetResistance(ResistanceType.Poison,100);}if(basic){NecromancerEffects.SetMana256(p,17);}
                int hitRolls=0;AmazonImpale.HitRoll=()=>{hitRolls++;return scenario=="low-miss"?1:0;};SourceBlocking.BlockRoll=n=>scenario=="low-block"?0:n-1;
                visits.Clear();rolls.SpecialCounter.Calls=rolls.WearCounter.Calls=0;int before=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p),cost=basic?0:768;string profile=Json(Own(p,weapon));var world=NpcWorldAllocation.Capture();
                try
                {
                    Require(ClassCombat.Perform(p,CombatAbility.Impale,t,t.Location,Core.TickCount),"Impale entry "+scenario);ClassCombat.ReleaseCast(p);var a=ClassCombat.For(p).Impale;var c=a.Contact;
                    Require(c!=null&&a.Basic==basic&&hitRolls==(basic?1:0)&&visits.Count==0&&SkillHealth.Balance256(t)==before&&Json(Own(p,weapon))==profile&&NpcWorldAllocation.Capture()==world&&NecromancerEffects.ManaBalance256(p)==mana-cost
                        &&rolls.SpecialCounter.Calls==(basic?0:1)&&rolls.WearCounter.Calls==0&&!ClassCombat.ResolveAction(p,a.EffectAt-1),"Impale precomputation,proc timing,wear or payment");
                    if(!basic){Require(c.HitFlags==1&&TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value).HealBlockedUntilUtc>TimedEffects.Now,"Paid hitflag1/PMH capture lost");}
                    if(scenario=="change"){p.AddToBackpack(gloves);ClassGear.Refresh(p);t.SetResistance(ResistanceType.Poison,100);}
                    if(scenario=="range"){t.MoveToWorld(new(p.X+10,p.Y,p.Z),p.Map);}
                    if(scenario=="cancel"){p.Frozen=true;Require(!ClassCombat.ResolveAction(p,a.EffectAt)&&visits.Count==0&&SkillHealth.Balance256(t)==before,"Invalid source replayed contact");continue;}
                    Require(ClassCombat.ResolveAction(p,a.EffectAt),"Impale release");
                    if(scenario=="range"){Require(visits.Count==0&&SkillHealth.Balance256(t)==before&&ImpaleSlow.Effect(t)!=null&&rolls.WearCounter.Calls==0,"Slow/range phase or range-rejected damage/events");continue;}
                    bool hit=scenario is not("low-miss" or "low-block");int rate=hit&&scenario!="immune"?153:0;
                    Require(visits.Count==(hit?2:1)&&visits[^1].Phase=="domeleeattack"&&visits[^1].Packet.HitFlags==(hit?32u:0u)&&rolls.WearCounter.Calls==(hit?1:0),"Captured Impale phase/flags/wear");
                    Require(before-SkillHealth.Balance256(t)==(hit?c.Impact.LifeDamage256:0)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Impale reread resistance/gear,replicated damage or repaid mana");
                    var poison=TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value&&x.PoisonMs>0);Require(rate==0?poison==null:poison?.PoisonClock?.Rate256==153&&poison.PoisonMs==4000,"Captured initial poison/clock");
                    Require(hit?visits.All(v=>v.Outcomes.Single() is{CastAttempts:1,CastResults:1}):visits.All(v=>v.Outcomes.All(o=>o.CastAttempts==0)),"Impale item proc success/avoidance gates");
                    Require(basic?ImpaleSlow.Effect(t)==null:ImpaleSlow.Effect(t)!=null,"Basic fallback inherited skill slow");Require(!ClassCombat.ResolveAction(p,a.EffectAt),"Impale replayed contact");
                }
                finally{p.Frozen=false;Clean(p,t);if(gloves.Parent!=p){Require(p.EquipItem(gloves),"Restore gloves");ClassGear.Refresh(p);}}
            }
        }
        finally{PlayerItemProcWorld.Observed=observed;}
    }
    private static void Check(bool reload)
    {
        using var rolls=new Rolls();PlayerMobile p;Item weapon,gloves,helm;int generation=0;
        if(!reload){p=Owner(1700,out weapon,out gloves,out helm);}
        else
        {var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);weapon=World.FindItem((Serial)s.Weapon);gloves=World.FindItem((Serial)s.Gloves);helm=World.FindItem((Serial)s.Helm);
            Require(generation<=2&&weapon.Parent==p&&gloves.Parent==p&&helm.Parent==p&&Json(new[]{ClassGear.State(weapon),ClassGear.State(gloves),ClassGear.State(helm)})==s.Gear&&Json(Own(p,weapon))==s.Profile&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&((BaseWeapon)weapon).HitPoints==s.Durability,"Saved Impale originals,streams,wounds,resource or wear changed");p.MoveToWorld(s.Point,Map.Trammel);}
        Scenarios(p,weapon,gloves,rolls);SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-17);var point=p.Location;p.Internalize();File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,weapon.Serial.Value,gloves.Serial.Value,helm.Serial.Value,point,Json(new[]{ClassGear.State(weapon),ClassGear.State(gloves),ClassGear.State(helm)}),Json(Own(p,weapon)),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),((BaseWeapon)weapon).HitPoints,generation)));
    }
    private static void Input(NetState n,string label)
    {
        var rolls=new Rolls();var p=Owner(2300,out _,out _,out _);var t=Target(p);Ready(p);var account=FixtureNetwork.Authenticate(n,p,"impale-contact-");var observer=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
        int mana=NecromancerEffects.ManaBalance256(p);long deadline=Core.TickCount+6000;bool began=false;PlayerWeaponContactPrepared c=null;
        void Close(){Clean(p,t);PlayerItemProcWorld.Observed=observer;rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try
            {Require(Core.TickCount<deadline,"Impale wire timeout");var a=ClassCombat.For(p).Impale;if(!began&&a!=null){began=true;c=a.Contact;Require(c!=null&&visits.Count==0&&rolls.SpecialCounter.Calls==1&&rolls.WearCounter.Calls==0&&NecromancerEffects.ManaBalance256(p)==mana-768,"Wire captured/start wear phase");Log.Information("{Label}: windup",label);}
                if(visits.Count<2){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(began&&visits.Count==2&&visits[0].Packet.Poison256==153&&visits[1].Phase=="domeleeattack"&&visits.All(v=>v.Outcomes.Single() is{CastAttempts:1,CastResults:1})&&rolls.SpecialCounter.Calls==1&&rolls.WearCounter.Calls==1&&ImpaleSlow.Effect(t)!=null&&NecromancerEffects.ManaBalance256(p)==mana-768,"Wire captured damage/proc,slow,wear or payment");Close();Log.Information("{Label}-result: passed",label);}
            catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.Impale,(int)DiabloClass.Amazon,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
