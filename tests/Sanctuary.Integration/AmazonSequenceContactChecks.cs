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

public static class AmazonSequenceContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(AmazonSequenceContactChecks));
    private static readonly CombatAbility[] Abilities=[CombatAbility.Jab,CombatAbility.Fend];
    private sealed record Saved(uint Owner,uint Weapon,uint Gloves,uint Helm,Point3D Point,CombatAbility Ability,string Gear,string Profile,int Life,int Mana,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"amazon-sequence-contact-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private sealed class High:Random{public int Calls;public override int Next(int n){Calls++;return n-1;}}
    private sealed class Rolls:IDisposable
    {
        private readonly Func<double> JabHit=AmazonJab.HitRoll,FendHit=AmazonFend.HitRoll,JabElement=AmazonJab.ElementRoll,FendElement=AmazonFend.ElementRoll;
        private readonly Func<int,int> JabPhysical=AmazonJab.PhysicalRoll,FendPhysical=AmazonFend.PhysicalRoll,JabPoison=AmazonJab.PoisonRoll,FendPoison=AmazonFend.PoisonRoll,Block=SourceBlocking.BlockRoll,Choice=SourceBlocking.ChoiceRoll;
        private readonly Random JabEffects=AmazonJab.Effects,FendEffects=AmazonFend.Effects,JabWear=AmazonJab.WearRoll,FendWear=AmazonFend.WearRoll;
        internal readonly High Wear=new();
        internal Rolls(){AmazonJab.HitRoll=AmazonFend.HitRoll=AmazonJab.ElementRoll=AmazonFend.ElementRoll=()=>0;AmazonJab.PhysicalRoll=AmazonFend.PhysicalRoll=AmazonJab.PoisonRoll=AmazonFend.PoisonRoll=_=>0;AmazonJab.Effects=AmazonFend.Effects=new High();AmazonJab.WearRoll=AmazonFend.WearRoll=Wear;SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=_=>1;}
        public void Dispose(){AmazonJab.HitRoll=JabHit;AmazonFend.HitRoll=FendHit;AmazonJab.ElementRoll=JabElement;AmazonFend.ElementRoll=FendElement;AmazonJab.PhysicalRoll=JabPhysical;AmazonFend.PhysicalRoll=FendPhysical;AmazonJab.PoisonRoll=JabPoison;AmazonFend.PoisonRoll=FendPoison;AmazonJab.Effects=JabEffects;AmazonFend.Effects=FendEffects;AmazonJab.WearRoll=JabWear;AmazonFend.WearRoll=FendWear;SourceBlocking.BlockRoll=Block;SourceBlocking.ChoiceRoll=Choice;}
    }
    private static PlayerProcSave Own(PlayerMobile p,Item item)
    {var all=PlayerItemProcs.Capture();return all with{Items=all.Items.Where(x=>x.Item==item.Serial.Value).ToArray(),Owners=all.Owners.Where(x=>x.Actor==p.Serial.Value).ToArray()};}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"amazon-sequence-contact-reload-check":"amazon-sequence-contact-check";ConsoleInputHandler.RegisterCommand(name,"Jab/Fend ordinary contacts,source poison,proc phase and saved originals",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(var a in Abilities)foreach(string mode in new[]{"right","quick"})
        {var ability=a;string label="amazon-sequence-contact-"+(a==CombatAbility.Jab?"jab":"fend")+"-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated learned Amazon sequence contact",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label,ability));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(CombatAbility ability,int row,out Item weapon,out Item gloves,out Item helm)
    {
        var p=HellslayerChecks.Owner(DiabloClass.Amazon,row);ClassSkillChecks.Train(p,SkillMechanics.For(ability).TalentId);
        weapon=PlayerItemProcWorldChecks.Carrier(p,new Spear());gloves=DruidSetupItemChecks.Named(p,"Venom Grip");helm=DruidSetupItemChecks.Named(p,"Blackhorn's Face");ClassGear.Refresh(p);
        Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Sequence bindings");return p;
    }
    private static void Ready(PlayerMobile p)
    {AmazonJab.Cancel(p);AmazonFend.Cancel(p);ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;NecromancerEffects.SetMana256(p,p.ManaMax*256);}
    private static BaseCreature Target(PlayerMobile p,int dy=0)
    {
        var t=new PoisonDaggerContactChecks.Recipient();t.SetHits(60000);t.MoveToWorld(new(p.X+1,p.Y+dy,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Poison,0);
        Require(SourceBlocking.TryRate(t,out int chance,out _)&&chance>0,"Real recipient shield control");return t;
    }
    private static void Clean(PlayerMobile p,params BaseCreature[] targets)
    {AmazonJab.Cancel(p);AmazonFend.Cancel(p);ClassCombat.Stop(p);SorceressSpellMissiles.Cancel(p);foreach(var t in targets){if(t!=null){SourceKnockbacks.Clear(t);t.Delete();}}}
    private static long Next(PlayerMobile p,CombatAbility a)=>a==CombatAbility.Jab?ClassCombat.For(p).Jab.Started+ClassCombat.For(p).Jab.Events[ClassCombat.For(p).Jab.Released]:ClassCombat.For(p).Fend.EffectAt;
    private static void Start(PlayerMobile p,CombatAbility a,BaseCreature t)
    {Require(ClassCombat.Perform(p,a,t,t.Location,Core.TickCount),"Sequence start");ClassCombat.ReleaseCast(p);}
    private static void Scenarios(PlayerMobile p,Item weapon,Item gloves,CombatAbility ability,High wear)
    {
        using var proof=PlayerItemProcWorld.Candidate(p);var observed=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);if(v.Phase=="domeleedamage"){Require(wear.Calls==0,"Weapon wear preceded damage events");}else if((v.Packet.ResultFlags&1)!=0){Require(wear.Calls==1,"Expansion wear did not occur once before attack events");}}};
            foreach(string scenario in new[]{"hit","change","miss","block","immune","cancel","low"})
            {
                Ready(p);var t=Target(p);if(scenario=="immune"){t.SetResistance(ResistanceType.Poison,100);}AmazonJab.HitRoll=AmazonFend.HitRoll=()=>scenario=="miss"?1:0;
                SourceBlocking.BlockRoll=n=>{Require(TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value).HealBlockedUntilUtc>TimedEffects.Now,"PMH did not precede shield");return scenario=="block"?0:n-1;};
                visits.Clear();wear.Calls=0;int life=SkillHealth.Balance256(t);string profile=Json(Own(p,weapon));var world=NpcWorldAllocation.Capture();
                try
                {
                    if(scenario=="low"){NecromancerEffects.SetMana256(p,0);}int mana=NecromancerEffects.ManaBalance256(p),cost=scenario=="low"?0:SkillMechanics.Values(p,ability).Mana256;
                    Start(p,ability,t);long at=Next(p,ability);Require(visits.Count==0&&SkillHealth.Balance256(t)==life&&Json(Own(p,weapon))==profile&&NpcWorldAllocation.Capture()==world&&NecromancerEffects.ManaBalance256(p)==mana-cost&&!ClassCombat.ResolveAction(p,at-1),"Sequence windup emitted,reread or repaid contact");
                    if(scenario=="cancel"){p.Frozen=true;Require(!ClassCombat.ResolveAction(p,at)&&visits.Count==0&&SkillHealth.Balance256(t)==life,"Invalid source replayed contact");continue;}
                    if(scenario=="change"){p.AddToBackpack(gloves);ClassGear.Refresh(p);}
                    bool hit=scenario is not("miss" or "block");int rate=hit&&scenario is not("immune" or "change")?153:0;
                    Require(ClassCombat.ResolveAction(p,at),"Actual do-event");
                    Require(visits.Count==(hit?2:1)&&visits[^1].Phase=="domeleeattack"&&visits[^1].Packet.HitFlags==(hit?32u:0u)&&wear.Calls==(hit?1:0),"Sequence result flags,proc phases or wear");
                    Require(life-SkillHealth.Balance256(t)==rate&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Action-time poison/HP/payment changed");
                    var poison=TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value&&x.PoisonMs>0);Require(rate==0?poison==null:poison?.PoisonClock?.Rate256==153&&poison.PoisonMs==4000,"Sequence source poison clock");
                    Require(visits.All(v=>v.Outcomes.Single() is{CastAttempts:1,CastResults:1})==hit&&visits.All(v=>hit||v.Outcomes.All(o=>o.CastAttempts==0)),"Miss/block or landed proc dispatch");
                    Require(!ClassCombat.ResolveAction(p,at),"Sequence replayed released event");
                }
                finally{p.Frozen=false;Clean(p,t);if(gloves.Parent!=p){Require(p.EquipItem(gloves),"Restore gloves");ClassGear.Refresh(p);}SourceBlocking.BlockRoll=n=>n-1;}
            }
            Ready(p);var first=Target(p);var second=Target(p,1);visits.Clear();wear.Calls=0;AmazonJab.HitRoll=AmazonFend.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;
            // Per-contact observer verifies the callback boundary independently.
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
            try
            {
                Start(p,ability,first);long at=Next(p,ability);Require(ClassCombat.ResolveAction(p,at),"First complete-series event");first.Delete();int otherLife=SkillHealth.Balance256(second);at=Next(p,ability);
                Require(ClassCombat.ResolveAction(p,at),"Second complete-series event");
                Require(ability==CombatAbility.Jab?SkillHealth.Balance256(second)==otherLife&&visits.Count==2:otherLife-SkillHealth.Balance256(second)==153&&visits.Count==4,"Jab retargeted or Fend lost cyclic target");
            }
            finally{Clean(p,first,second);}
            Ready(p);var a=Target(p);var b=Target(p,1);visits.Clear();wear.Calls=0;
            try
            {
                AmazonJab.HitRoll=AmazonFend.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;
                int ah=SkillHealth.Balance256(a),bh=SkillHealth.Balance256(b),mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
                ulong actor=Own(p,weapon).Owners.Single().Seed,world=NpcWorldAllocation.Capture().Seed;int count=ability==CombatAbility.Jab?3:2;
                Start(p,ability,a);
                for(int i=0;i<count;i++)
                {
                    visits.Clear();wear.Calls=0;Require(ClassCombat.ResolveAction(p,Next(p,ability)),"Complete sequence event");
                    SourceRandom.Roll(ref actor);SourceRandom.Roll(ref actor);SourceRandom.Roll(ref world);SourceRandom.Roll(ref world);
                    Require(visits.Count==2&&visits[0].Packet.Poison256==153&&visits[1].Phase=="domeleeattack"&&wear.Calls==1
                        &&visits.All(v=>v.Outcomes.Single() is{CastAttempts:1,CastResults:1})&&Own(p,weapon).Owners.Single().Seed==actor&&NpcWorldAllocation.Capture().Seed==world
                        &&NecromancerEffects.ManaBalance256(p)==mana-cost,"Later strike lost proc streams,poison,wear or once-only payment");
                }
                Require(ah-SkillHealth.Balance256(a)==(ability==CombatAbility.Jab?459:153)&&bh-SkillHealth.Balance256(b)==(ability==CombatAbility.Jab?0:153),"Jab retargeted within its series or Fend failed to cycle");
            }
            finally{Clean(p,a,b);}
        }
        finally{PlayerItemProcWorld.Observed=observed;}
    }
    private static void Check(bool reload)
    {
        using var rolls=new Rolls();var saved=reload?JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)):new Saved[2];
        for(int i=0;i<2;i++)
        {
            var ability=Abilities[i];PlayerMobile p;Item weapon,gloves,helm;int generation=0;
            if(!reload){p=Owner(ability,1700+i*36,out weapon,out gloves,out helm);}
            else
            {
                var s=saved[i];generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);weapon=World.FindItem((Serial)s.Weapon);gloves=World.FindItem((Serial)s.Gloves);helm=World.FindItem((Serial)s.Helm);
                Require(generation<=2&&s.Ability==ability&&weapon.Parent==p&&gloves.Parent==p&&helm.Parent==p&&Json(new[]{ClassGear.State(weapon),ClassGear.State(gloves),ClassGear.State(helm)})==s.Gear&&Json(Own(p,weapon))==s.Profile&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Saved originals,proc streams,wounds or mana changed");p.MoveToWorld(s.Point,Map.Trammel);
            }
            Scenarios(p,weapon,gloves,ability,rolls.Wear);SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-17);var point=p.Location;p.Internalize();saved[i]=new(p.Serial.Value,weapon.Serial.Value,gloves.Serial.Value,helm.Serial.Value,point,ability,Json(new[]{ClassGear.State(weapon),ClassGear.State(gloves),ClassGear.State(helm)}),Json(Own(p,weapon)),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),generation);
        }
        File.WriteAllText(Manifest,Json(saved));
    }
    private static void Input(NetState n,string label,CombatAbility ability)
    {
        var rolls=new Rolls();var p=Owner(ability,2200,out _,out _,out _);var t=Target(p);Ready(p);var account=FixtureNetwork.Authenticate(n,p,"amazon-sequence-contact-");var observed=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
        int mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;long deadline=Core.TickCount+6000;bool began=false;
        void Close(){Clean(p,t);PlayerItemProcWorld.Observed=observed;rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Amazon sequence wire timeout");var s=ClassCombat.For(p);if(!began&&(s.Jab!=null||s.Fend!=null)){began=true;Require(visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Wire startup phase/payment");Log.Information("{Label}: windup",label);}
                if(visits.Count<2){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(began&&visits.Count==2&&visits[0].Packet.Poison256==153&&visits[1].Phase=="domeleeattack"&&visits.All(v=>v.Outcomes.Single() is{CastAttempts:1,CastResults:1})&&rolls.Wear.Calls==1&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Wire per-event poison,proc,wear or payment");Close();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Amazon,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
