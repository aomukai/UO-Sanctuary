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

public static class AssassinWeaponContactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(AssassinWeaponContactChecks));
    private static readonly (CombatAbility Ability,string Label)[] Cases=[(CombatAbility.TigerStrike,"tiger"),(CombatAbility.CobraStrike,"cobra"),(CombatAbility.PhoenixStrike,"phoenix"),(CombatAbility.FistsOfFire,"fire"),(CombatAbility.ClawsOfThunder,"thunder"),(CombatAbility.BladesOfIce,"ice")];
    private sealed record Saved(uint Owner,uint Item,uint Gloves,Point3D Point,string Gear,string Profile,string Charges,int Life,int Mana,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"assassin-weapon-contact-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private sealed class NoEffects:Random{public override int Next(int maximum)=>maximum-1;}
    private sealed class Rolls:IDisposable
    {
        private readonly Func<double> Hit=TigerStrikes.HitRoll,Element=TigerStrikes.ElementRoll;private readonly Func<int,int> Physical=TigerStrikes.PhysicalRoll,Poison=TigerStrikes.PoisonRoll,Block=SourceBlocking.BlockRoll,Choice=SourceBlocking.ChoiceRoll;private readonly Random Effects=TigerStrikes.Effects;
        internal Rolls(){TigerStrikes.HitRoll=()=>0;TigerStrikes.ElementRoll=()=>0;TigerStrikes.PhysicalRoll=TigerStrikes.PoisonRoll=_=>0;TigerStrikes.Effects=new NoEffects();SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=_=>1;}
        public void Dispose(){TigerStrikes.HitRoll=Hit;TigerStrikes.ElementRoll=Element;TigerStrikes.PhysicalRoll=Physical;TigerStrikes.PoisonRoll=Poison;TigerStrikes.Effects=Effects;SourceBlocking.BlockRoll=Block;SourceBlocking.ChoiceRoll=Choice;}
    }
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"assassin-weapon-contact-reload-check":"assassin-weapon-contact-check";ConsoleInputHandler.RegisterCommand(name,"Shared martial item events,poison,charges and saved originals",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));}
        foreach(var entry in Cases)foreach(string mode in new[]{"right","quick"})
        {var c=entry;string label="assassin-weapon-"+c.Label+"-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated learned martial builder with equipped proc supplier",_=>Core.LoopContext.Post(()=>{try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label,c.Ability,false));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
        foreach(string side in new[]{"left","right"})
        {string label="assassin-weapon-basic-"+side;ConsoleInputHandler.RegisterCommand(label+"-check","Authenticated charged common Attack with full Hellslayer",_=>Core.LoopContext.Post(()=>{try{FixtureNetwork.Connected(label+"-result",n=>Input(n,label,CombatAbility.BasicAttack,true));Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    private static PlayerProcSave Profile(PlayerMobile p,Item item)
    {var s=PlayerItemProcs.Capture();return s with{Items=s.Items.Where(x=>x.Item==item.Serial.Value).ToArray(),Owners=s.Owners.Where(x=>x.Actor==p.Serial.Value).ToArray()};}
    private static MartialCharge[] Charges(PlayerMobile p)=>AssassinCharges.Snapshot().Where(x=>x.Owner==p.Serial.Value).ToArray();
    private static PlayerMobile Owner(CombatAbility ability,int row,out Item item,out Item gloves)
    {
        var p=HellslayerChecks.Owner(DiabloClass.Assassin,row);foreach(var c in Cases){ClassSkillChecks.Train(p,SkillMechanics.For(c.Ability).TalentId);}
        item=ability is CombatAbility.FistsOfFire or CombatAbility.ClawsOfThunder or CombatAbility.BladesOfIce?PlayerItemProcWorldChecks.Carrier(p,new Tekagi()):HellslayerChecks.Original(p);
        if(item.Parent!=p){p.AddToBackpack(item);Require(p.EquipItem(item),"Equip complete martial proc original");}ClassGear.Refresh(p);
        gloves=DruidSetupItemChecks.Named(p,"Venom Grip");if(ability!=CombatAbility.BasicAttack){Require(SkillBindings.Instance.Assign(p,false,ability)&&SkillBindings.Instance.AssignQuick(p,7,ability),"Martial learned bindings");}else{Require(SkillBindings.Instance.Assign(p,true,CombatAbility.BasicAttack)&&SkillBindings.Instance.Assign(p,false,CombatAbility.BasicAttack),"Charged common Attack bindings");}
        SkillHealth.SetBalance256(p,p.HitsMax*128);return p;
    }
    private static void Ready(PlayerMobile p,bool mana=true)
    {ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;if(mana){NecromancerEffects.SetMana256(p,p.ManaMax*256);}}
    private static PoisonDaggerContactChecks.Recipient Target(PlayerMobile p,int resistance=0)
    {var t=new PoisonDaggerContactChecks.Recipient();t.SetHits(60000);t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,100);}t.SetResistance(ResistanceType.Poison,resistance);return t;}
    private static void Finish(PlayerMobile p,BaseCreature t)
    {ClassCombat.Stop(p);SorceressSpellMissiles.Cancel(p);t?.Delete();}
    private static void Cast(PlayerMobile p,BaseCreature t,CombatAbility ability,bool miss=false,bool block=false,bool cancel=false,bool low=false)
    {
        Ready(p);if(low){NecromancerEffects.SetMana256(p,0);}TigerStrikes.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;
        int before=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);string charges=Json(Charges(p));var world=NpcWorldAllocation.Capture();
        Require(ClassCombat.Perform(p,ability,t,t.Location,Core.TickCount),"Martial start");ClassCombat.ReleaseCast(p);var s=ClassCombat.For(p);long at=s.EffectAt;
        int cost=ability==CombatAbility.BasicAttack||low?0:SkillMechanics.Values(p,ability).Mana256;
        Require(SkillHealth.Balance256(t)==before&&Json(Charges(p))==charges&&NpcWorldAllocation.Capture()==world&&NecromancerEffects.ManaBalance256(p)==mana-cost&&!ClassCombat.ResolveAction(p,at-1),"Martial windup changed contacts,charges,streams or payment");
        if(cancel){ClassCombat.Stop(p);Require(!ClassCombat.ResolveAction(p,at)&&SkillHealth.Balance256(t)==before&&Json(Charges(p))==charges&&NpcWorldAllocation.Capture()==world,"Cancelled martial contact committed");return;}
        TigerStrikes.HitRoll=()=>miss?1:0;if(block){SourceBlocking.BlockRoll=_=>0;}
        Require(ClassCombat.ResolveAction(p,at),"Martial action");ClassCombat.Stop(p);
        int life=SkillHealth.Balance256(t);string state=Json(Charges(p));var allocation=NpcWorldAllocation.Capture();Require(!ClassCombat.ResolveAction(p,at)&&SkillHealth.Balance256(t)==life&&Json(Charges(p))==state&&NpcWorldAllocation.Capture()==allocation,"Martial action replayed");
    }
    private static void Scenarios(PlayerMobile p,Item item,CombatAbility ability)
    {
        var observed=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();using var candidate=PlayerItemProcWorld.Candidate(p);
        try
        {
            PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
            foreach(string scenario in new[]{"hit","miss","block","immune","cancel","low"})
            {
                AssassinCharges.Clear(p);TigerStrikes.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;var t=Target(p,scenario=="immune"?100:0);int before=SkillHealth.Balance256(t),life=SkillHealth.Balance256(p),packets=NativeMonsterReturn.Session(t).Packets;visits.Clear();
                try
                {
                    Cast(p,t,ability,scenario=="miss",scenario=="block",scenario=="cancel",scenario=="low");
                    bool hit=scenario is not("miss" or "block" or "cancel");int rate=hit&&scenario!="immune"?153:0;
                    Require(before-SkillHealth.Balance256(t)==rate&&SkillHealth.Balance256(p)==life,"Martial initial poison rate missing,duplicated or leeched");
                    Require(AssassinCharges.State(p,ability)?.Count==(hit&&scenario!="low"?1:null),"Builder gain/fallback/miss/block/cancel changed");
                    Require(NativeMonsterReturn.Session(t).Packets==packets+(scenario=="cancel"?0:1)&&visits.Count==(scenario=="cancel"?0:hit?2:1),$"Martial packet/event allocation count {ability}/{scenario}: packets={NativeMonsterReturn.Session(t).Packets-packets} visits={visits.Count} phases={string.Join(",",visits.Select(v=>v.Phase))}");
                    if(hit){Require(visits[0].Phase=="domeleedamage"&&visits[0].Packet.Poison256==rate&&visits[1].Phase=="domeleeattack"&&visits[1].Packet.HitFlags==32,"Martial proc phases lost actual poison/attack packet");}
                    else{Require(visits.All(v=>v.Phase=="domeleeattack"&&v.Outcomes.All(o=>o.CastAttempts==0)),"Failed builder emitted proc");}
                }
                finally{Finish(p,t);}
            }
            foreach(var family in Cases)
            {
                AssassinCharges.Clear(p);for(int i=0;i<3;i++){AssassinCharges.Gain(p,SkillMechanics.Values(p,family.Ability));}var t=Target(p);visits.Clear();
                try
                {
                    Cast(p,t,CombatAbility.BasicAttack);Require(AssassinCharges.State(p,family.Ability)?.Count==2&&visits.Count==2&&visits[0].Packet.Poison256==153,"Charged common Attack lost consume-one or source poison");
                    var held=AssassinCharges.State(p,family.Ability);visits.Clear();Cast(p,t,CombatAbility.BasicAttack,miss:true);Require(AssassinCharges.State(p,family.Ability)==held&&visits.Count==1&&visits[0].Outcomes.All(o=>o.CastAttempts==0),"Missed common Attack consumed/released/procced charges");
                }
                finally{Finish(p,t);}
            }
        }
        finally{PlayerItemProcWorld.Observed=observed;AssassinCharges.Clear(p);}
    }
    private static void Dual(PlayerMobile p,CombatAbility ability)
    {
        var second=DruidSetupItemChecks.Named(p,"Cutthroat1");var t=Target(p);var observed=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();
        try
        {
            using var candidate=PlayerItemProcWorld.Candidate(p);PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};AssassinCharges.Clear(p);Ready(p);TigerStrikes.HitRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;
            int before=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;Require(ClassCombat.Perform(p,ability,t,t.Location,Core.TickCount),"Dual builder start");ClassCombat.ReleaseCast(p);var series=ClassCombat.For(p).ClawAttack;
            Require(series is{Events.Length:2}&&ClassCombat.ResolveAction(p,series.Until),"Dual full sequence");
            Require(visits.Count==4&&visits[0].Outcomes.Sum(x=>x.CastAttempts)==1&&visits[1].Outcomes.Sum(x=>x.CastAttempts)==1&&visits[2].Outcomes.Sum(x=>x.CastAttempts)==0&&visits[3].Outcomes.Sum(x=>x.CastAttempts)==0
                &&before-SkillHealth.Balance256(t)==306&&AssassinCharges.State(p,ability)?.Count==2&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Dual selected-hand proc suppression,poison,gain or once-only payment differs");
        }
        finally{PlayerItemProcWorld.Observed=observed;Finish(p,t);second.Delete();ClassGear.Refresh(p);AssassinCharges.Clear(p);}
    }
    private static void Check(bool reload)
    {
        using var rolls=new Rolls();var saved=reload?JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)):new Saved[Cases.Length];
        for(int i=0;i<Cases.Length;i++)
        {
            var ability=Cases[i].Ability;PlayerMobile p;Item item,gloves;int generation=0;
            if(!reload){p=Owner(ability,1500+i*36,out item,out gloves);}
            else
            {
                var s=saved[i];generation=s.Generation+1;p=(PlayerMobile)World.FindMobile((Serial)s.Owner);item=World.FindItem((Serial)s.Item);gloves=World.FindItem((Serial)s.Gloves);
                Require(generation<=2&&item.Parent==p&&gloves.Parent==p&&Json(new[]{ClassGear.State(item),ClassGear.State(gloves)})==s.Gear&&Json(Profile(p,item))==s.Profile&&Json(Charges(p))==s.Charges&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Saved original martial gear,profiles,charges or wounds changed");p.MoveToWorld(s.Point,Map.Trammel);
            }
            Scenarios(p,item,ability);if(ability is CombatAbility.FistsOfFire or CombatAbility.ClawsOfThunder or CombatAbility.BladesOfIce){Dual(p,ability);}
            for(int n=0;n<3;n++){AssassinCharges.Gain(p,SkillMechanics.Values(p,ability));}AssassinCharges.Restore(AssassinCharges.Snapshot().Select(x=>x.Owner==p.Serial.Value?x with{UntilUtc=TimedEffects.Now.AddHours(1)}:x).ToArray());
            SkillHealth.SetBalance256(p,SkillHealth.Balance256(p)-17);var point=p.Location;p.Internalize();saved[i]=new(p.Serial.Value,item.Serial.Value,gloves.Serial.Value,point,Json(new[]{ClassGear.State(item),ClassGear.State(gloves)}),Json(Profile(p,item)),Json(Charges(p)),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),generation);
        }
        File.WriteAllText(Manifest,Json(saved));
    }
    private static void Input(NetState n,string label,CombatAbility ability,bool basic)
    {
        var rolls=new Rolls();var p=Owner(ability,2400,out var item,out var gloves);if(basic){for(int i=0;i<3;i++){AssassinCharges.Gain(p,SkillMechanics.Values(p,CombatAbility.TigerStrike));}}
        var t=Target(p);Ready(p);var account=FixtureNetwork.Authenticate(n,p,"assassin-weapon-");var observed=PlayerItemProcWorld.Observed;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
        int before=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p),cost=basic?0:SkillMechanics.Values(p,ability).Mana256;bool began=false;long deadline=Core.TickCount+7000;
        void Cleanup(){Finish(p,t);AssassinCharges.Clear(p);PlayerItemProcWorld.Observed=observed;rolls.Dispose();p.Delete();account.Delete();}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Martial input timeout");var s=ClassCombat.For(p);
                if(!began&&s.PendingAbility==ability){Require(visits.Count==0&&SkillHealth.Balance256(t)==before&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Wire martial start changed life/events/mana");began=true;Log.Information("{Label}: windup",label);}
                if(visits.Count<2){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(began&&visits.Count==2&&visits[0].Phase=="domeleedamage"&&visits[0].Packet.Poison256==153&&visits[1].Phase=="domeleeattack"&&SkillHealth.Balance256(t)<=before-153&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Wire martial source poison,events,payment or HP differ");
                Require(AssassinCharges.State(p,basic?CombatAbility.TigerStrike:ability)?.Count==(basic?2:1),"Wire martial gain/consume differs");Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)ability,(int)DiabloClass.Assassin,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
}
