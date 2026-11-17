// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class BarbarianContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BarbarianContactChecks));
    private static readonly (CombatAbility Ability,string Label)[] Cases=[(CombatAbility.Bash,"bash"),(CombatAbility.Stun,"stun"),(CombatAbility.Concentrate,"concentrate"),(CombatAbility.Berserk,"berserk")];
    private sealed record Saved(uint Owner,uint Weapon,uint Gloves,uint Helm,Point3D Point,CombatAbility Ability,string Gear,string Profile,int Life,int Mana,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"barbarian-contact-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private sealed class NoEffects:Random{public override int Next(int maximum)=>maximum-1;}
    private sealed class Rolls:IDisposable
    {
        private readonly Func<double> Hit=BarbarianMelee.HitRoll,Element=BarbarianMelee.ElementRoll,Basic=PlayerBasicContact.HitRoll;
        private readonly Func<int,int> Physical=BarbarianMelee.PhysicalRoll,Poison=BarbarianMelee.PoisonRoll,Block=SourceBlocking.BlockRoll,Choice=SourceBlocking.ChoiceRoll;
        private readonly Random Effects=BarbarianMelee.Effects;
        internal Rolls(){BarbarianMelee.HitRoll=BarbarianMelee.ElementRoll=PlayerBasicContact.HitRoll=()=>0;BarbarianMelee.PhysicalRoll=BarbarianMelee.PoisonRoll=_=>0;BarbarianMelee.Effects=new NoEffects();SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=_=>1;}
        public void Dispose(){BarbarianMelee.HitRoll=Hit;BarbarianMelee.ElementRoll=Element;PlayerBasicContact.HitRoll=Basic;BarbarianMelee.PhysicalRoll=Physical;BarbarianMelee.PoisonRoll=Poison;BarbarianMelee.Effects=Effects;SourceBlocking.BlockRoll=Block;SourceBlocking.ChoiceRoll=Choice;}
    }
    private static PlayerProcSave Own(PlayerMobile p,Item item)
    {var s=PlayerItemProcs.Capture();return s with{Items=s.Items.Where(x=>x.Item==item.Serial.Value).ToArray(),Owners=s.Owners.Where(x=>x.Actor==p.Serial.Value).ToArray()};}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"barbarian-contact-reload-check":"barbarian-contact-check";ConsoleInputHandler.RegisterCommand(name,"Captured four-skill contact/procs/source poison and original saves",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(var c in Cases)foreach(string mode in new[]{"right","quick"})
        {var ability=c.Ability;string label="barbarian-contact-"+c.Label+"-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated whole-item captured Barbarian contact",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label,ability));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerMobile Owner(CombatAbility ability,int row,out Item weapon,out Item gloves,out Item helm)
    {
        var p=HellslayerChecks.Owner(DiabloClass.Barbarian,row);foreach(var c in Cases){ClassSkillChecks.Train(p,SkillMechanics.For(c.Ability).TalentId);}
        weapon=HellslayerChecks.Original(p);p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Equip complete Hellslayer");gloves=DruidSetupItemChecks.Named(p,"Venom Grip");helm=DruidSetupItemChecks.Named(p,"Blackhorn's Face");ClassGear.Refresh(p);
        Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Barbarian contact bindings");SkillHealth.SetBalance256(p,p.HitsMax*128);return p;
    }
    private static void Ready(PlayerMobile p)
    {
        var s=ClassCombat.For(p);if(s.Concentrate!=null){ClassCombat.UpdateCombat(p,s.NextAction+1);}ClassCombat.Stop(p);s.NextAction=s.RecoveryUntil=Core.TickCount;p.Frozen=p.Paralyzed=false;NecromancerEffects.SetMana256(p,p.ManaMax*256);
    }
    private static PoisonDaggerContactChecks.Recipient Target(PlayerMobile p,int resistance)
    {var t=new PoisonDaggerContactChecks.Recipient();t.SetHits(60000);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Poison,resistance);Require(SourceBlocking.TryRate(t,out int chance,out _)&&chance>0,"Missing source shield control");return t;}
    private static void Cleanup(PlayerMobile p,BaseCreature t)
    {var s=ClassCombat.For(p);if(s.Concentrate!=null){ClassCombat.UpdateCombat(p,s.NextAction+1);}ClassCombat.Stop(p);SorceressSpellMissiles.Cancel(p);SourceKnockbacks.Clear(t);t.Delete();}
    private static void Scenarios(PlayerMobile p,Item weapon,Item gloves,CombatAbility ability)
    {
        using var proof=PlayerItemProcWorld.Candidate(p);var observed=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
            foreach(string scenario in new[]{"hit","change","miss","block","immune","cancel","low"})
            {
                Ready(p);BarbarianMelee.HitRoll=()=>scenario=="miss"?1:0;var t=Target(p,scenario=="immune"?100:0);int before=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);visits.Clear();var profile=Json(Own(p,weapon));var world=NpcWorldAllocation.Capture();
                SourceBlocking.BlockRoll=n=>{Require(TimedEffects.Snapshot().Single(x=>x.Owner==t.Serial.Value).HealBlockedUntilUtc>TimedEffects.Now,"PMH did not precede shield selection");return scenario=="block"?0:n-1;};
                try
                {
                    if(scenario=="low"){NecromancerEffects.SetMana256(p,0);mana=0;SourceBlocking.BlockRoll=n=>n-1;}
                    Require(ClassCombat.Perform(p,ability,t,t.Location,Core.TickCount),"Captured Barbarian entry "+scenario);ClassCombat.ReleaseCast(p);var s=ClassCombat.For(p);long at=s.EffectAt;
                    if(scenario=="low")
                    {Require(s.PendingAbility==CombatAbility.BasicAttack&&s.BarbarianAttack==null&&NecromancerEffects.ManaBalance256(p)==0,"Underfunded skill did not use free basic attack");Require(ClassCombat.ResolveAction(p,at)&&NecromancerEffects.ManaBalance256(p)==0,"Basic fallback payment/release");continue;}
                    var saved=s.BarbarianAttack;var contact=saved?.Contact;int cost=SkillMechanics.Values(p,ability).Mana256;bool hit=scenario is not("miss" or "block");
                    Require(contact!=null&&saved.Hit==hit&&SkillHealth.Balance256(t)==before&&visits.Count==0&&Json(Own(p,weapon))==profile&&NpcWorldAllocation.Capture()==world&&NecromancerEffects.ManaBalance256(p)==mana-cost&&!ClassCombat.ResolveAction(p,at-1),"Windup changed health,proc streams,payment or packet");
                    if(scenario=="cancel")
                    {p.Frozen=true;ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at)&&visits.Count==0&&SkillHealth.Balance256(t)==before&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Invalid-owner cancellation replayed/refunded contact");p.Frozen=false;continue;}
                    if(scenario=="change"){p.AddToBackpack(gloves);ClassGear.Refresh(p);t.SetResistance(ResistanceType.Poison,100);t.SetResistance(ResistanceType.Physical,0);BarbarianMelee.HitRoll=()=>1;}
                    Require(ClassCombat.ResolveAction(p,at),"Captured Barbarian action");
                    Require(visits.Count==(hit?2:1)&&visits[^1].Phase=="domeleeattack"&&visits[^1].Packet.HitFlags==(hit?32u:0u),"Captured attack attempt/proc flags changed");
                    Require(before-SkillHealth.Balance256(t)==(hit?contact.Impact.LifeDamage256:0)&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Captured life budget rerolled,resisted,duplicated or repaid");
                    var poison=TimedEffects.Snapshot().SingleOrDefault(x=>x.Owner==t.Serial.Value&&x.PoisonMs>0);int rate=hit&&scenario!="immune"?153:0;
                    Require(rate==0?poison==null:poison?.PoisonClock?.Rate256==rate&&poison.PoisonMs==4000,"Captured source poison initial rate/clock differs");
                    if(hit)
                    {
                        Require(visits[0].Phase=="domeleedamage"&&visits[0].Packet.Poison256==rate&&visits[0].Packet.Physical256==NamedElements.To256(contact.Impact.Parts.Physical),"Damage phase lost poison or allocated physical field");
                        if(ability==CombatAbility.Stun){Require(CombatStuns.Remaining(t)>0,"Captured Stun did not reach recipient");}
                        if(ability==CombatAbility.Bash){Require((visits[0].Packet.ResultFlags&8)!=0&&visits[0].Packet.HitClass==112,"Bash source knockback/hit class lost");}
                    }
                    else{Require(visits[0].Outcomes.All(x=>x.CastAttempts==0),"Miss/block emitted proc");}
                    int count=visits.Count;Require(!ClassCombat.ResolveAction(p,at)&&visits.Count==count,"Captured contact replayed");
                }
                finally{p.Frozen=false;Cleanup(p,t);if(gloves.Parent!=p){Require(p.EquipItem(gloves),"Restore complete Venom Grip");ClassGear.Refresh(p);}SourceBlocking.BlockRoll=n=>n-1;}
            }
        }
        finally{PlayerItemProcWorld.Observed=observed;}
    }
    private static void Check(bool reload)
    {
        using var rolls=new Rolls();var saved=reload?JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)):new Saved[Cases.Length];
        for(int i=0;i<Cases.Length;i++)
        {
            var ability=Cases[i].Ability;PlayerMobile p;Item weapon,gloves,helm;int generation=0;
            if(!reload){p=Owner(ability,1600+i*36,out weapon,out gloves,out helm);}
            else
            {
                var s=saved[i];generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);weapon=World.FindItem((Serial)s.Weapon);gloves=World.FindItem((Serial)s.Gloves);helm=World.FindItem((Serial)s.Helm);
                Require(generation<=2&&s.Ability==ability&&weapon.Parent==p&&gloves.Parent==p&&helm.Parent==p&&Json(new[]{ClassGear.State(weapon),ClassGear.State(gloves),ClassGear.State(helm)})==s.Gear&&Json(Own(p,weapon))==s.Profile&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Saved originals,proc stream or wounds changed");p.MoveToWorld(s.Point,Map.Trammel);
            }
            Scenarios(p,weapon,gloves,ability);SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-17);var point=p.Location;p.Internalize();saved[i]=new(p.Serial.Value,weapon.Serial.Value,gloves.Serial.Value,helm.Serial.Value,point,ability,Json(new[]{ClassGear.State(weapon),ClassGear.State(gloves),ClassGear.State(helm)}),Json(Own(p,weapon)),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),generation);
        }
        File.WriteAllText(Manifest,Json(saved));
    }
    private static void Input(NetState n,string label,CombatAbility ability)
    {
        var rolls=new Rolls();var p=Owner(ability,2200,out var weapon,out var gloves,out var helm);var t=Target(p,0);Ready(p);var account=FixtureNetwork.Authenticate(n,p,"barbarian-contact-");var observed=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
        int mana=NecromancerEffects.ManaBalance256(p),before=SkillHealth.Balance256(t),cost=SkillMechanics.Values(p,ability).Mana256;long deadline=Core.TickCount+6000;bool began=false;BarbarianMelee.Prepared captured=null;
        void Close(){Cleanup(p,t);rolls.Dispose();PlayerItemProcWorld.Observed=observed;p.Delete();account.Delete();}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Captured Barbarian wire timeout");var s=ClassCombat.For(p);
                if(!began&&s.BarbarianAttack!=null){captured=s.BarbarianAttack;Require(SkillHealth.Balance256(t)==before&&visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Wire windup changed health,events or payment");began=true;Log.Information("{Label}: windup",label);}
                if(visits.Count<2){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(began&&visits.Count==2&&visits[0].Phase=="domeleedamage"&&visits[0].Packet.Poison256==153&&visits[1].Phase=="domeleeattack"&&SkillHealth.Balance256(t)<=before-captured.Impact.LifeDamage256&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Wire captured contact,poison,proc flags,health or payment differs");Close();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Close();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Barbarian,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
