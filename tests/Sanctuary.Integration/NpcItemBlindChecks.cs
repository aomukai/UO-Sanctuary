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

public static class NpcItemBlindChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcItemBlindChecks));
    private sealed record Held(uint Owner,uint Source,uint Item,string Gear,uint Target,int Life,int SourceLife,ulong Seed,NecromancerState State,bool Revive);
    private sealed record Saved(Held[] Actors,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-item-blind-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static int[] N(JsonElement x)=>x.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-item-blind-cases.json")));int callback=0,composition=0;
        foreach(var row in doc.RootElement.GetProperty("callback").EnumerateArray())
        {
            var a=N(row.GetProperty("input"));var b=N(row.GetProperty("output"));bool draws=a[2]!=0&&a[5]==1;
            int rank=draws?NamedControl.BlindLevel(a[0],a[1],a[2],a[3]!=0,a[4]):0;
            int[] expected=[rank>0?1:0,draws?1:0,rank,rank>0?7:0,rank>0?100+175+50*(rank-1):0,rank>0?1:0,rank>0?1:0,rank>0?7:-1,0,rank>0?1:0];
            Require(b.SequenceEqual(expected),"Full blind callback differs at "+callback);callback++;
        }
        foreach(var row in doc.RootElement.GetProperty("composition").EnumerateArray())
        {
            var a=N(row.GetProperty("input"));var b=N(row.GetProperty("output"));var at=DateTime.UnixEpoch.AddMilliseconds(100*40);
            int frames=(175+50*(a[0]-1))/Math.Max(1,a[1]);bool duration=SourceCurseDuration.Reduce(frames,true,a[5],out frames);
            NecromancerCurse old=a[6]==0?null:new(a[6]==4?CombatAbility.Weaken:CombatAbility.DimVision,999,a[6]==1?a[0]:a[6]==2?a[0]+1:a[6]==3?a[0]-1:9,DateTime.UnixEpoch.AddMilliseconds(777*40),0,0);
            var next=a[2]!=0&&a[3]!=1&&a[4]==0&&duration?NecromancerCurseCasts.Replacement(old,CombatAbility.DimVision,7,a[0],null,at.AddMilliseconds(frames*40L),at,0,0):null;
            var value=next??old;int[] expected=[1,1,value?.Rank??0,(int)(value?.Source??0),value==null?0:(int)(value.UntilUtc-DateTime.UnixEpoch).TotalMilliseconds/40,a[3]!=1?1:0,next!=null?1:0,7,next!=null&&a[6]is 3 or 4?1:0,next!=null?1:0];
            Require(b.SequenceEqual(expected),"Full direct blind/factory differs at "+composition);composition++;
        }
        Require(callback==2560&&composition==4800,"Incomplete blind source cases");
        var guards=doc.RootElement.GetProperty("guards");Require(guards.GetArrayLength()==12&&N(guards[0].GetProperty("output")).SequenceEqual(new[]{1,1,11,7,775,1,1,7,0,1}),"Independent positive guard baseline missing");
        foreach(var row in guards.EnumerateArray().Skip(1)){var b=N(row.GetProperty("output"));Require(b[0]==1&&b[1]==1&&b[2]==0&&b[6]==0,"Source failed downstream guard changed attempted callback return");}
    }
    public sealed class Recipient:GraveyardSkeleton
    {
        internal int Notifications;internal bool BlindAtNotification;
        [Constructible]public Recipient(){Frozen=CantWalk=NoKillAwards=true;}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int amount,Mobile from,bool lethal){Notifications++;BlindAtNotification=NecromancerEffects.Curse(this)?.Ability==CombatAbility.DimVision;base.OnDamage(amount,from,lethal);}
        public override void Serialize(IGenericWriter w){base.Serialize(w);w.Write(0);}
        public override void Deserialize(IGenericReader r){base.Deserialize(r);r.ReadInt();}
    }
    private static Recipient Target(PlayerMobile p,BaseCreature source,int offset=0)
    {var t=new Recipient();t.SetHits(20000);foreach(var k in Enum.GetValues<ResistanceType>()){t.SetResistance(k,0);}t.MoveToWorld(new(source.X+offset,source.Y,source.Z),p.Map);t.AIObject?.AITimer.Stop();return t;}
    private static void Delete(BaseCreature t){NecromancerEffects.Profiles.Clear(t);TimedEffects.Clear(t);t.Corpse?.Delete();t.Delete();}
    private static Item Original(string name)=>NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==name).Id,99,new Random(173),true);
    private static void Controls(PlayerMobile p,BaseCreature source)
    {
        var target=Target(p,source);var neighbor=Target(p,source,2);var retained=Target(p,source,6);
        try
        {
            source.Combatant=retained;var at=TimedEffects.Now;int hp=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(p);var seed=NpcEquipmentRandom.State(source);int draws=0;
            try
            {
                using var outer=NpcItemSpellCasts.PreserveCombatTarget(source);
                using(var inner=NpcItemSpellCasts.PreserveCombatTarget(source))
                {source.Combatant=neighbor;Require(source.Combatant==retained,"Nested source target guard did not veto native assignment");}
                source.Combatant=neighbor;
                Require(source.Combatant==retained&&NpcItemSpellCasts.KeepsCombatTarget(source)&&source.CanBeHarmful(neighbor,false),"Veto escaped the Combatant validation phase or inner disposal lost outer scope");
                throw new ArithmeticException("Deliberate NPC target-scope exception");
            }
            catch(ArithmeticException){}
            Require(!NpcItemSpellCasts.KeepsCombatTarget(source),"Exception leaked NPC target scope");
            source.Combatant=neighbor;Require(source.Combatant==neighbor,"Ordinary native target assignment stayed vetoed");source.Combatant=retained;
            Require(NpcItemBlind.Apply(source,target,false,at,()=>{draws++;return 0;}),"Positive direct callback failed");
            var curse=NecromancerEffects.Curse(target);int rank=NamedControl.BlindLevel(ClassStats.Level(source),ClassStats.Level(target),1,false,0);
            Require(draws==1&&curse is{Ability:CombatAbility.DimVision}&&curse.Rank==rank&&curse.Source==source.Serial.Value&&NecromancerEffects.Profiles.Find(target)?.BlindAi!=null,"Direct target did not attach actual curse/AI");
            Require(SkillHealth.Balance256(target)==hp&&target.Notifications==0&&NecromancerEffects.Curse(neighbor)==null&&NpcEquipmentRandom.State(source)==seed&&NecromancerEffects.ManaBalance256(p)==mana,"Blind replayed damage,RNG,owner mana or an area cast");
            Require(source.Combatant==retained&&!NpcItemSpellCasts.KeepsCombatTarget(source),"Direct NPC curse lost ordinary targeting or leaked scope");
            // Equal rank retains its original creator but replaces even a
            // longer expiry. Stronger existing blind refuses the effect while
            // function09 still returns success after its positive chance.
            NecromancerEffects.Profiles.Store(target,NecromancerEffects.Profiles.Find(target)with{Curse=curse with{Source=p.Serial.Value,UntilUtc=at.AddHours(1)}});
            Require(NpcItemBlind.Apply(source,target,false,at,()=>0)&&NecromancerEffects.Curse(target).Source==p.Serial.Value&&NecromancerEffects.Curse(target).UntilUtc<at.AddHours(1),"Equal-rank creator/deadline semantics lost");
            var stronger=NecromancerEffects.Curse(target)with{Rank=255};NecromancerEffects.Profiles.Store(target,NecromancerEffects.Profiles.Find(target)with{Curse=stronger});
            Require(NpcItemBlind.Apply(source,target,false,at,()=>0)&&NecromancerEffects.Curse(target)==stronger,"Positive callback overwrote stronger blind or returned failure");
            NecromancerEffects.Profiles.Clear(target);NecromancerEffects.Profiles.Store(target,new(Curse:new(CombatAbility.Attract,p.Serial.Value,1,at.AddHours(1),p.X,p.Y)));
            Require(NpcItemBlind.Apply(source,target,true,at,()=>0)&&NecromancerEffects.Curse(target)?.Ability==CombatAbility.Attract,"Direct item blind overwrote protected Attract");
            source.Combatant=target;Require(source.Combatant==target,"Scope veto escaped ordinary combat changes");source.Combatant=null;
        }
        finally{Delete(target);Delete(neighbor);Delete(retained);}
    }
    private static Recipient Contact(PlayerMobile p,BaseCreature source,bool controller)
    {
        Recipient t=null;var oldBlock=SourceBlocking.BlockRoll;var oldChoice=SourceBlocking.ChoiceRoll;
        try
        {
            // The attacker stream does not own the defender's shield/block
            // rolls. Keep both inputs explicit when replaying a cached hit.
            SourceBlocking.ChoiceRoll=n=>Math.Min(1,n-1);
            t=Target(p,source);
            Require(SourceBlocking.TryRate(t,out int blockRate,out bool blockMode)&&blockRate>0&&blockMode,"Blind fixture needs its real shield/block supplier");
            SourceBlocking.BlockRoll=n=>n-1;
            var at=TimedEffects.Now;IronGolemWorldState world=null;
            if(controller){var b=(IronGolemBody)source;world=b.Prepared.Controller with{Mode=4,Target=t.Serial.Value,NextThink=0,ActionAt=b.Prepared.Controller.Frame+1,EndAt=b.Prepared.Controller.Frame+20,AttackSpeed=256};b.RestoreController(world);at=world.NextUtc;}
            var rules=IronGolemContact.Require(source,p,t);IronGolemContact.Prepared prepared=null;ulong initial=1,expected=0;int rank=0;
            for(int n=0;n<1000&&rank==0;n++,initial++)
            {
                NpcEquipmentRandom.Set(source,initial);prepared=IronGolemContact.Prepare(source,t,rules,()=>controller?IronGolemWorld.Result((IronGolemBody)source,p,world,t):5);
                expected=NpcEquipmentRandom.State(source).Seed;uint raw=SourceRandom.Roll(ref expected);
                rank=(prepared.Impact.Packet.ResultFlags&1)!=0?NamedControl.BlindLevel(ClassStats.Level(source),ClassStats.Level(t),1,false,(int)(raw%100)):0;
                if(rank==0){prepared.Cancel();}
            }
            initial--;Require(rank>0,"No deterministic positive blind contact");int hp=SkillHealth.Balance256(t);
            if(controller)
            {
                // Same attacker seed,opposite independent defender draw: a
                // block consumes only the hit draw,not damage or blind RNG.
                SourceBlocking.BlockRoll=_=>0;NpcEquipmentRandom.Set(source,initial);
                int blocked=IronGolemWorld.Result((IronGolemBody)source,p,world,t);ulong hitOnly=initial;SourceRandom.Roll(ref hitOnly);
                Require(blocked==SourceMeleeResult.Block&&NpcEquipmentRandom.State(source).Seed==hitOnly&&SkillHealth.Balance256(t)==hp&&t.Notifications==0,"Independent block counterexample did not isolate the missing fixture input");
                SourceBlocking.BlockRoll=n=>n-1;NpcEquipmentRandom.Set(source,initial);IronGolemWorld.Advance((IronGolemBody)source,at,true);
            }
            else{IronGolemContact.Release(source,p,t,prepared,_=>true,at);}
            Require(t.Notifications==1&&t.BlindAtNotification&&SkillHealth.Balance256(t)==hp-prepared.Impact.Packet.Total256&&NpcEquipmentRandom.State(source).Seed==expected&&NativeMonsterReturn.Session(t).Packets==1,"Blind contact differs: source="+source.GetType().Name+" controller="+controller+" notifications="+t.Notifications+" blindAtNotify="+t.BlindAtNotification+" life="+SkillHealth.Balance256(t)+" expectedLife="+(hp-prepared.Impact.Packet.Total256)+" seed="+NpcEquipmentRandom.State(source).Seed+" expectedSeed="+expected+" packets="+NativeMonsterReturn.Session(t).Packets+" blockRate="+blockRate+" returnMode="+NativeMonsterReturn.Session(t).LastMode+" world="+Json((source as IronGolemBody)?.Prepared.Controller));
            Require(NecromancerEffects.Curse(t)is{Ability:CombatAbility.DimVision} c&&c.Rank==rank&&c.Source==source.Serial.Value&&NecromancerEffects.Profiles.Find(t)?.BlindAi!=null,"Contact lost NPC curse source/rank/AI");return t;
        }
        finally{SourceBlocking.BlockRoll=oldBlock;SourceBlocking.ChoiceRoll=oldChoice;}
    }
    private static Held Hold(PlayerMobile p,BaseCreature source,Item item,Recipient t,bool revive)
    {
        var state=NecromancerEffects.Profiles.Find(t);var hold=TimedEffects.Now.Add(FixtureSpace.Retention);state=state with{Curse=state.Curse with{UntilUtc=hold},BlindAi=state.BlindAi with{NextUtc=hold}};NecromancerEffects.Profiles.Store(t,state);
        SkillHealth.SetBalance256(source,SkillHealth.Maximum256(source)-1237);PersistentMinions.Suspend(p);p.Internalize();t.Internalize();
        return new(p.Serial.Value,source.Serial.Value,item.Serial.Value,Json(ClassGear.State(item)),t.Serial.Value,SkillHealth.Balance256(t),SkillHealth.Balance256(source),NpcEquipmentRandom.State(source).Seed,state,revive);
    }
    private static void Check(NetState net,bool reload)
    {
        Differential();
        if(!reload)
        {
            var rows=new List<Held>();var p=NpcFreezeHitChecks.Owner();var blocked=Original("Tyrael's Might");blocked.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);
            var before=Json(ClassGear.State(blocked));int mana=NecromancerEffects.ManaBalance256(p);
            Require(!IronGolemCasting.Plan(p,blocked,out _,out var issue)&&issue.Contains("consumer",StringComparison.Ordinal)
                &&!IronGolemCasting.Release(p,blocked,TimedEffects.Now,out _)&&blocked.Parent==null&&blocked.Map==p.Map&&Json(ClassGear.State(blocked))==before
                &&NecromancerEffects.ManaBalance256(p)==mana&&!NecromancerGolems.For(p).Any(),"Unsupported Tyrael's Might callback was discarded or failed cast consumed item/mana");blocked.Delete();
            var original=Original("The Patriarch");var body=NpcFreezeHitChecks.Cast(p,original,"stupidity");Controls(p,body);
            var t=Contact(p,body,false);Delete(t);NpcItemProcs.Create(body);NpcItemProcs.Refresh(body,NpcCombatEvents.LegacyForSource(body));
            Require(NpcItemProcs.Get(body).Capture().Events.Entries.Count(e=>e.Function==9)==2,"Live graph blind subscriptions missing");
            t=Contact(p,body,true);rows.Add(Hold(p,body,original,t,false));
            var q=NpcFreezeHitChecks.Owner();FixtureNetwork.Authenticate(net,q,"npc-blind-");var revived=NpcFreezeHitChecks.Revive(q,out var gear,"The Patriarch","stupidity");Controls(q,revived);t=Contact(q,revived,false);rows.Add(Hold(q,revived,gear,t,true));
            File.WriteAllText(Manifest,Json(new Saved(rows.ToArray(),0)));
        }
        else
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
            FixtureNetwork.Authenticate(net,(PlayerMobile)World.FindMobile((Serial)saved.Actors[0].Owner),"npc-blind-reload-");
            foreach(var row in saved.Actors)
            {
                var p=World.FindMobile((Serial)row.Owner)as PlayerMobile;var source=World.FindMobile((Serial)row.Source)as BaseCreature;var t=World.FindMobile((Serial)row.Target)as BaseCreature;var item=World.FindItem((Serial)row.Item);
                Require(source is{Alive:true,Deleted:false}&&source.ControlMaster==p&&SkillHealth.Balance256(source)==row.SourceLife&&SkillHealth.Balance256(t)==row.Life&&NpcEquipmentRandom.State(source).Seed==row.Seed&&Json(NecromancerEffects.Profiles.Find(t))==Json(row.State)&&Json(ClassGear.State(item))==row.Gear,"Original blind source/gear/wounds/RNG/curse/AI changed on reload");
                Require(NpcItemBlind.Bonus(source)==1&&NpcCombatEvents.ForSource(source).Subscriptions().Contains(113),"Saved blind admission lost");
                Require(row.Revive?item.Parent is Corpse:item.Parent==source,"Original blind item/loot ownership changed");
            }
            File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
        }
        Log.Information("NPC item blind:2560callback/4800factory/12guard source contexts;direct one-target real curse/AI,no cast/mana/extra HP;original learned Patriarch Iron/frozen Revive,graph,ordered contact,RNG and original wounded curse/AI persist through two reloads");
    }
    public static void Configure()
    {
        foreach(string name in new[]{"npc-item-blind-check","npc-item-blind-reload-check"})ConsoleInputHandler.RegisterCommand(name,"NPC blind-on-hit direct curse and original equipment",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected(name,net=>
        {try{Check(net,name.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}})));
    }
}
