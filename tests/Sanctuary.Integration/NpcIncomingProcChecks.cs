// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcIncomingProcChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcIncomingProcChecks));
    private sealed record Saved(uint Owner,uint Body,uint Enemy,uint[] Items,NpcProcState Graph,ulong Seed,int Life,int Mana,NecromancerCurse Curse,string BodyState,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-incoming-proc-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("npc-incoming-proc-check",Check);Register("npc-incoming-proc-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Qualified incoming packet/stat201 source callback and original graph",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    internal static Item Add(PlayerMobile p,NpcItemProcGraph graph,int skill,int chance)
    {
        var item=new Dagger();p.AddToBackpack(item);graph.Add(item,4,SourceRandom.InitLow(511));
        graph.Apply(item,new("incoming-curse",0,false,[new(11,201,false,0)]),new(chance,5,skill),ItemSkillProperties.ForLevel(40));return item;
    }
    // The caller supplies an already qualified post-defense packet. This
    // exercises the real shared HP/return path, not a fabricated UO scalar
    // damage flag or a claim of automatic native monster AI admission.
    internal sealed class Receiver(PlayerMobile owner,BaseCreature attacker,WarlockTaintedBody target,DateTime at,bool missile,Action before):PreparedMonsterDamageServices
    {
        private readonly MonsterReturnMetadata Metadata=NativeMonsterReturn.Metadata(target);
        internal int Incoming,Outgoing;private bool Freed;
        internal override void StoreLastAttacker()=>attacker.DoHarmful(target,true);
        internal override PreparedMonsterDamagePacket MitigateMissile(PreparedMonsterDamagePacket p)=>p;
        internal override PreparedMonsterDamagePacket DealtDamage(PreparedMonsterDamagePacket p){Outgoing++;return p;}
        internal override PreparedMonsterDamagePacket TookDamage(PreparedMonsterDamagePacket p)
        {Incoming++;return NpcCombatEvents.Empty.Dispatch(missile?"damagedbymissile":"damagedinmelee",p,target,attacker,at);}
        internal override void BeforeHealth(PreparedMonsterDamagePacket p)=>before();
        internal override void BeforeHealthCommit(Mobile source,BaseCreature recipient,int after)=>PreparedMonsterNativeDamage.Notify(source,recipient,after);
        internal override void ApplyCold(int frames)=>Require(frames==0,"Fixture has no cold state");
        internal override void SurvivedDamage(){}
        internal override PreparedMonsterDamagePacket Killed(PreparedMonsterDamagePacket p)=>p;
        internal override PreparedMonsterDamagePacket Kill(PreparedMonsterDamagePacket p)=>p;
        internal override void PresentOverlay(int overlay)=>throw new InvalidOperationException("Fixture has no overlay");
        internal override int BaseHitClass()=>0;
        internal override PreparedMonsterDamagePacket MeleeAttack(PreparedMonsterDamagePacket p)=>p;
        internal override PreparedMonsterDamagePacket AttackedInMelee(PreparedMonsterDamagePacket p)=>NpcCombatEvents.Empty.Dispatch("attackedinmelee",p,target,attacker,at);
        internal override bool AttackerInActiveMode()=>attacker.Alive&&!attacker.Deleted;
        internal override void BeforeThorns(PreparedMonsterDamagePacket p){}
        internal override void Return(ref PreparedMonsterDamagePacket p,Action death)=>PreparedMonsterReturn.Dispatch(ref p,new NativeMonsterReturn(attacker,owner,target,Metadata,death),true);
        internal override void FreePacket(){Require(!Freed,"Incoming fixture freed twice");Freed=true;NativeMonsterReturn.Session(target).Packets++;}
    }
    private static void Deliver(PlayerMobile owner,WarlockTaintedBody b,BaseCreature enemy,bool missile,int result,uint flags,int total,int draws,CombatAbility? curse,ulong seed=99)
    {
        NecromancerEffects.Profiles.Clear(enemy);int life=SkillHealth.Balance256(b),mana=NecromancerEffects.ManaBalance256(owner);
        PreparedMonsterNativeDamage.RequireIncoming(owner,b,enemy);NpcEquipmentRandom.Set(b,seed);ulong expected=seed;for(int i=0;i<draws;i++){SourceRandom.Roll(ref expected);}
        var at=TimedEffects.Now;bool observed=false;
        var receiver=new Receiver(owner,enemy,b,at,missile,()=>
        {
            observed=true;var effect=NecromancerEffects.Curse(enemy);
            Require(SkillHealth.Balance256(b)==life&&(curse==null?effect==null:effect is{Rank:5}&&effect.Ability==curse&&effect.Source==b.Serial.Value),"Incoming curse phase/role/order before health");
        });
        var packet=new PreparedMonsterDamagePacket(total,total,0,0,result,HitFlags:flags);
        var recipient=new PreparedMonsterRecipient(false,()=>false,()=>8,()=>false);
        if(missile){PreparedMonsterDamage.ReleaseMissile(enemy,b,packet,recipient,_=>throw new InvalidOperationException("No stun roll"),receiver,at);}
        else{PreparedMonsterDamage.Release(enemy,b,packet,recipient,_=>throw new InvalidOperationException("No stun roll"),receiver,at);}
        Require(observed&&NpcEquipmentRandom.State(b).Seed==expected&&SkillHealth.Balance256(b)==life-total&&NecromancerEffects.ManaBalance256(owner)==mana,"Incoming damage/RNG/payment mismatch");
        bool eventPhase=(result&32)==0&&(!missile||(flags&32)!=0);
        Require(receiver.Incoming==(eventPhase?1:0)&&receiver.Outgoing==(eventPhase&&(!missile||(flags&128)==0)?1:0),"Source incoming/outgoing producer mask or duplicate phase");
    }
    private static ulong Seed(bool success)
    {for(ulong seed=1;seed<1000;seed++){ulong next=seed;if((SourceRandom.Roll(ref next)%100<50)==success){return seed;}}throw new InvalidOperationException("Need source chance seed");}
    private static void Check()
    {
        var p=TaintedFireBallChecks.Owner();var at=TimedEffects.Now.AddDays(3);var b=WarlockTaintedBody.CreatePrepared(p,new(p.X+1,p.Y,p.Z),at);var enemy=FireBoltChecks.Target(p,2);
        NpcEquipmentRandom.Set(b,99);var graph=NpcItemProcs.Create(b);var first=Add(p,graph,72,50);NecromancerEffects.SetMana256(p,0);
        foreach(bool missile in new[]{false,true})foreach(int result in new[]{1,5,37})foreach(bool success in new[]{false,true})
        {
            bool eligible=(result&32)==0&&(result&4)!=0;
            Deliver(p,b,enemy,missile,result,32,256,eligible?1:0,eligible&&success?CombatAbility.Weaken:null,Seed(success));
        }
        Deliver(p,b,enemy,true,5,0,256,0,null);Deliver(p,b,enemy,true,5,160,256,1,CombatAbility.Weaken,Seed(true));
        // Result4, not positive HP loss, gates the source callback.
        Deliver(p,b,enemy,false,5,0,0,1,CombatAbility.Weaken,Seed(true));
        var second=Add(p,graph,72,50);Require(graph.Capture().Events.Entries.Count(e=>e.Guid1==unchecked((int)EquipmentStatKey.Pack(201,(72<<6)|5)))==2,"Incoming duplicate layer created duplicate phase callbacks");
        Deliver(p,b,enemy,false,5,0,256,1,CombatAbility.Weaken);
        var lower=Add(p,graph,91,100);Deliver(p,b,enemy,true,5,32,256,2,CombatAbility.Weaken);
        graph.Remove(first.Serial.Value);graph.Remove(second.Serial.Value);Deliver(p,b,enemy,false,5,0,256,1,CombatAbility.LowerResist);
        graph.Remove(lower.Serial.Value);lower.Delete();first.Delete();second.Delete();Deliver(p,b,enemy,false,5,0,256,0,null);
        var deleted=Add(p,graph,72,100);deleted.Delete();Deliver(p,b,enemy,false,5,0,256,0,null);
        Require(graph.Capture().Items.Length==0&&graph.Capture().Events.Entries.Length==0,"Deleted incoming carrier remained active");
        first=Add(p,graph,72,50);second=Add(p,graph,72,50);
        // Existing real NPC fire delivery carries no ordinary-hit event32.
        NecromancerEffects.Profiles.Clear(enemy);NpcEquipmentRandom.Set(b,123);int before=SkillHealth.Balance256(b);
        NpcFireSpellHit.Deliver(enemy,p,b,256,TimedEffects.Now,"incoming proc exclusion");Require(SkillHealth.Balance256(b)<before&&NpcEquipmentRandom.State(b).Seed==123&&NecromancerEffects.Curse(enemy)==null,"Direct fire inferred an unqualified incoming proc");
        int hp=SkillHealth.Balance256(b);ulong original=NpcEquipmentRandom.State(b).Seed;bool refused=false;
        try{NpcItemProcs.RequireIncoming(p,b,new(256,256,0,0,5));}catch(NotSupportedException){refused=true;}
        Require(refused&&SkillHealth.Balance256(b)==hp&&NpcEquipmentRandom.State(b).Seed==original,"Player-attacker unsupported context mutated HP/RNG");
        var state=graph.Capture();var json=Json(state);graph.Dispatch("damagedinmelee",null,b,enemy,TimedEffects.Now,null);
        Require(Json(graph.Capture())==json&&NpcEquipmentRandom.State(b).Seed==original,"Missing packet invoked callback or changed subscriptions");
        Deliver(p,b,enemy,false,5,0,256,1,CombatAbility.Weaken);
        var curse=NecromancerEffects.Curse(enemy)with{UntilUtc=TimedEffects.Now.AddDays(1)};NecromancerEffects.Profiles.Store(enemy,NecromancerEffects.Profiles.Find(enemy)with{Curse=curse});
        PersistentMinions.Suspend(p);enemy.Internalize();p.Internalize();
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,b.Serial.Value,enemy.Serial.Value,[first.Serial.Value,second.Serial.Value],graph.Capture(),NpcEquipmentRandom.State(b).Seed,SkillHealth.Balance256(b),NecromancerEffects.ManaBalance256(p),curse,b.SavedText,0)));
        Log.Information("NPC incoming procs:actual stat201/function21 carriers and shared qualified melee/missile HP receiver;Result4/32 masks,zero-loss contact,50percent seeds,LIFO/duplicate layers,actor RNG,pre-health curse,no owner mana,fire exclusion and explicit player refusal;original actors/graph staged");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var b=(WarlockTaintedBody)World.FindMobile((Serial)s.Body);var enemy=(BaseCreature)World.FindMobile((Serial)s.Enemy);var graph=NpcItemProcs.Get(b);
        Require(s.Generation<2&&b.ControlMaster==p&&b.SavedText==s.BodyState&&Json(graph.Capture())==Json(s.Graph)&&NpcEquipmentRandom.State(b).Seed==s.Seed
            &&SkillHealth.Balance256(b)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana&&NecromancerEffects.Curse(enemy)==s.Curse
            &&s.Items.All(id=>World.FindItem((Serial)id)is{Deleted:false}),"Original incoming graph/body/resources/curse changed on reload");
        FixtureSpace.Corridor(p,6300,30,3600);b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);enemy.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);enemy.AIObject?.AITimer.Stop();
        Deliver(p,b,enemy,true,5,32,256,1,CombatAbility.Weaken);var curse=NecromancerEffects.Curse(enemy)with{UntilUtc=TimedEffects.Now.AddDays(1)};NecromancerEffects.Profiles.Store(enemy,NecromancerEffects.Profiles.Find(enemy)with{Curse=curse});
        PersistentMinions.Suspend(p);enemy.Internalize();p.Internalize();
        File.WriteAllText(Manifest,Json(s with{Graph=graph.Capture(),Seed=NpcEquipmentRandom.State(b).Seed,Life=SkillHealth.Balance256(b),Mana=NecromancerEffects.ManaBalance256(p),Curse=curse,Generation=s.Generation+1}));
    }
}
