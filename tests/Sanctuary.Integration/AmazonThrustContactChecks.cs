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

public static class AmazonThrustContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(AmazonThrustContactChecks));
    private static readonly CombatAbility[] Abilities=[CombatAbility.PowerStrike,CombatAbility.ChargedStrike,CombatAbility.LightningStrike];
    private sealed record Saved(uint Owner,uint Weapon,uint Gloves,uint Helm,Point3D Point,CombatAbility Ability,string Gear,string Profile,int Life,int Mana,int Durability,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"amazon-thrust-contact-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private sealed class High:Random{internal int Calls;public override int Next(int n){Calls++;return n-1;}}
    private sealed class Rolls:IDisposable
    {
        private readonly Func<double> Hit=AmazonMelee.HitRoll,Element=AmazonMelee.ElementRoll;
        private readonly Func<int,int> Physical=AmazonMelee.PhysicalRoll,Poison=AmazonMelee.PoisonRoll,Block=SourceBlocking.BlockRoll,Choice=SourceBlocking.ChoiceRoll;
        private readonly Random Effects=AmazonMelee.Effects,Wear=AmazonMelee.WearRoll;
        internal readonly High WearCounter=new();
        private readonly Func<int,int> Lightning=AmazonMelee.LightningRoll;
        internal int LightningCalls;
        internal Rolls(){AmazonMelee.HitRoll=AmazonMelee.ElementRoll=()=>0;AmazonMelee.PhysicalRoll=AmazonMelee.PoisonRoll=_=>0;AmazonMelee.Effects=new High();AmazonMelee.LightningRoll=_=>{LightningCalls++;return 0;};AmazonMelee.WearRoll=WearCounter;SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=_=>1;}
        public void Dispose(){AmazonMelee.HitRoll=Hit;AmazonMelee.ElementRoll=Element;AmazonMelee.PhysicalRoll=Physical;AmazonMelee.PoisonRoll=Poison;AmazonMelee.Effects=Effects;AmazonMelee.LightningRoll=Lightning;AmazonMelee.WearRoll=Wear;SourceBlocking.BlockRoll=Block;SourceBlocking.ChoiceRoll=Choice;}
    }
    private static PlayerProcSave Own(PlayerMobile p,Item weapon)
    {var s=PlayerItemProcs.Capture();return s with{Items=s.Items.Where(x=>x.Item==weapon.Serial.Value).ToArray(),Owners=s.Owners.Where(x=>x.Actor==p.Serial.Value).ToArray()};}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"amazon-thrust-contact-reload-check":"amazon-thrust-contact-check";ConsoleInputHandler.RegisterCommand(name,"Thrust captured contact,post-wear poison,ordered item/wear events and originals",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(var a in Abilities)foreach(string mode in new[]{"right","quick"})
        {var ability=a;string label="amazon-thrust-contact-"+a.ToString().ToLowerInvariant()+"-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated captured Thrust contact and equipped proc",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label,ability));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(CombatAbility ability,int row,out Item weapon,out Item gloves,out Item helm)
    {
        var p=HellslayerChecks.Owner(DiabloClass.Amazon,row);ClassSkillChecks.Train(p,SkillMechanics.For(ability).TalentId);weapon=PlayerItemProcWorldChecks.Carrier(p,new Spear());gloves=DruidSetupItemChecks.Named(p,"Venom Grip");helm=DruidSetupItemChecks.Named(p,"Blackhorn's Face");ClassGear.Refresh(p);
        Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Thrust bindings");return p;
    }
    private static void Ready(PlayerMobile p)
    {AmazonMelee.Cancel(p);ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static BaseCreature Target(PlayerMobile p)
    {var t=new PoisonDaggerContactChecks.Recipient();t.SetHits(60000);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Poison,0);return t;}
    private static void Clean(PlayerMobile p,BaseCreature t)
    {AmazonMelee.Cancel(p);ClassCombat.Stop(p);SorceressSpellMissiles.Cancel(p);ChargedStrikeBolts.Cancel(p);ChainLightning.Cancel(p);SourceKnockbacks.Clear(t);t.Delete();}
    private static int Children(PlayerMobile p,CombatAbility ability)=>ability==CombatAbility.ChargedStrike?ChargedStrikeBolts.Snapshot().Count(x=>x.Owner==p.Serial.Value):ability==CombatAbility.LightningStrike?ChainLightning.Snapshot().Count(x=>x.Owner==p.Serial.Value&&x.StrikeContext!=null):0;
    private static void Scenarios(PlayerMobile p,Item weapon,Item gloves,CombatAbility ability,Rolls rolls)
    {
        using var proof=PlayerItemProcWorld.Candidate(p);var observed=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);Require(Children(p,ability)==0,"Child emitted before melee events");if(v.Phase=="domeleedamage"){Require(rolls.WearCounter.Calls==0,"Wear preceded damage events");}else if((v.Packet.ResultFlags&1)!=0){Require(rolls.WearCounter.Calls==1,"Wear missing before attack event");}}};
            foreach(string scenario in new[]{"hit","change","immune","range","cancel","miss","block","low-hit","low-miss","low-block","lethal"})
            {
                Ready(p);var t=Target(p);var neighbor=Target(p);neighbor.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);bool basic=scenario.StartsWith("low-");
                if(scenario=="immune"){t.SetResistance(ResistanceType.Poison,100);}if(scenario=="lethal"){t.SetResistance(ResistanceType.Physical,0);SkillHealth.SetBalance256(t,256);}if(basic){NecromancerEffects.SetMana256(p,17);}
                int hitRolls=0;AmazonMelee.HitRoll=()=>{hitRolls++;return scenario.EndsWith("miss")?1:0;};SourceBlocking.BlockRoll=n=>{Require(TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value).HealBlockedUntilUtc>TimedEffects.Now,"PMH did not precede shield");return scenario.EndsWith("block")?0:n-1;};
                visits.Clear();rolls.LightningCalls=rolls.WearCounter.Calls=0;int before=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p),cost=basic?0:SkillMechanics.Values(p,ability).Mana256;string profile=Json(Own(p,weapon));var world=NpcWorldAllocation.Capture();
                try
                {
                    Require(ClassCombat.Perform(p,ability,t,t.Location,Core.TickCount),"Thrust entry "+scenario);ClassCombat.ReleaseCast(p);var a=ClassCombat.For(p).AmazonAttack;var c=a.Contact;bool hit=!scenario.EndsWith("miss")&&!scenario.EndsWith("block");
                    Require(c!=null&&a.Basic==basic&&a.Hit==hit&&hitRolls==1&&rolls.LightningCalls==(!basic&&hit?1:0)&&visits.Count==0&&Children(p,ability)==0&&SkillHealth.Balance256(t)==before&&Json(Own(p,weapon))==profile&&NpcWorldAllocation.Capture()==world&&NecromancerEffects.ManaBalance256(p)==mana-cost
                        &&rolls.WearCounter.Calls==0&&!ClassCombat.ResolveAction(p,a.EffectAt-1),"Thrust precomputation,proc timing,shield,wear or payment");
                    if(scenario=="change"){p.AddToBackpack(gloves);ClassGear.Refresh(p);t.SetResistance(ResistanceType.Poison,100);}
                    if(scenario=="range"){t.MoveToWorld(new(p.X+10,p.Y,p.Z),p.Map);Require(!ClassCombat.ResolveAction(p,a.EffectAt)&&visits.Count==0&&Children(p,ability)==0&&SkillHealth.Balance256(t)==before&&rolls.WearCounter.Calls==0,"Range-rejected thrust emitted contact");continue;}
                    if(scenario=="cancel"){p.Frozen=true;Require(!ClassCombat.ResolveAction(p,a.EffectAt)&&visits.Count==0&&Children(p,ability)==0&&SkillHealth.Balance256(t)==before,"Invalid source replayed contact");continue;}
                    Require(ClassCombat.ResolveAction(p,a.EffectAt),"Thrust release");
                    Require(visits.Count==(hit?2:1)&&visits[^1].Phase=="domeleeattack"&&visits[^1].Packet.HitFlags==(hit?32u:0u)&&rolls.WearCounter.Calls==(hit?1:0),"Captured thrust phase/flags/wear");
                    Require(before-SkillHealth.Balance256(t)==(hit?Math.Min(before,c.Impact.LifeDamage256.Value):0)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Thrust reread resistance/gear,replicated damage or repaid mana");
                    if(scenario!="lethal")
                    {int rate=hit&&scenario!="immune"?153:0;var poison=TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value&&x.PoisonMs>0);Require(rate==0?poison==null:poison?.PoisonClock?.Rate256==153&&poison.PoisonMs==4000,"Captured initial poison/clock");}
                    Require(hit?visits.All(v=>v.Outcomes.Single() is{CastAttempts:1,CastResults:1}):visits.All(v=>v.Outcomes.All(o=>o.CastAttempts==0)),"Thrust item proc success/avoidance gates");
                    int children=basic?0:ability==CombatAbility.ChargedStrike?(int)SkillMechanics.Values(p,ability,a.Rank).Eval("calc1"):ability==CombatAbility.LightningStrike?1:0;
                    Require(Children(p,ability)==children,"Miss/block/lethal child emission changed");Require(!ClassCombat.ResolveAction(p,a.EffectAt),"Thrust replayed contact");
                }
                finally{p.Frozen=false;Clean(p,t);neighbor.Delete();if(gloves.Parent!=p){Require(p.EquipItem(gloves),"Restore gloves");ClassGear.Refresh(p);}SourceBlocking.BlockRoll=n=>n-1;}
            }
        }
        finally{PlayerItemProcWorld.Observed=observed;}
    }
    private static void Check(bool reload)
    {
        using var rolls=new Rolls();var saved=reload?JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)):new Saved[3];
        for(int i=0;i<3;i++)
        {
            var ability=Abilities[i];PlayerMobile p;Item weapon,gloves,helm;int generation=0;
            if(!reload){p=Owner(ability,1700+i*36,out weapon,out gloves,out helm);}
            else
            {var s=saved[i];generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);weapon=World.FindItem((Serial)s.Weapon);gloves=World.FindItem((Serial)s.Gloves);helm=World.FindItem((Serial)s.Helm);
                Require(generation<=2&&s.Ability==ability&&weapon.Parent==p&&gloves.Parent==p&&helm.Parent==p&&Json(new[]{ClassGear.State(weapon),ClassGear.State(gloves),ClassGear.State(helm)})==s.Gear&&Json(Own(p,weapon))==s.Profile&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&((BaseWeapon)weapon).HitPoints==s.Durability,"Saved thrust originals,streams,wounds,resource or wear changed");p.MoveToWorld(s.Point,Map.Trammel);}
            Scenarios(p,weapon,gloves,ability,rolls);SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-17);NecromancerEffects.SetMana256(p,17);var point=p.Location;p.Internalize();saved[i]=new(p.Serial.Value,weapon.Serial.Value,gloves.Serial.Value,helm.Serial.Value,point,ability,Json(new[]{ClassGear.State(weapon),ClassGear.State(gloves),ClassGear.State(helm)}),Json(Own(p,weapon)),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),((BaseWeapon)weapon).HitPoints,generation);
        }
        File.WriteAllText(Manifest,Json(saved));
    }
    private static void Input(NetState n,string label,CombatAbility ability)
    {
        var rolls=new Rolls();var p=Owner(ability,2300,out _,out _,out _);var t=Target(p);Ready(p);var account=FixtureNetwork.Authenticate(n,p,"amazon-thrust-contact-");var observer=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;long deadline=Core.TickCount+6000;bool began=false;PlayerWeaponContactPrepared c=null;
        void Close(){Clean(p,t);PlayerItemProcWorld.Observed=observer;rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try
            {Require(Core.TickCount<deadline,"Thrust wire timeout");var a=ClassCombat.For(p).AmazonAttack;if(!began&&a!=null){began=true;c=a.Contact;Require(c!=null&&visits.Count==0&&rolls.WearCounter.Calls==0&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Wire captured/start wear phase");Log.Information("{Label}: windup",label);}
                if(visits.Count<2){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(began&&visits.Count==2&&visits[0].Packet.Poison256==153&&visits[1].Phase=="domeleeattack"&&visits.All(v=>v.Outcomes.Single() is{CastAttempts:1,CastResults:1})&&rolls.WearCounter.Calls==1&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Wire captured damage/proc,slow,wear or payment");Close();Log.Information("{Label}-result: passed",label);}
            catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Amazon,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
