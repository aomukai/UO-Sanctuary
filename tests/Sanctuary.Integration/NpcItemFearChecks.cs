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

public static class NpcItemFearChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcItemFearChecks));
    private sealed record Held(uint Owner,uint Source,uint Item,uint Target,string Gear,int SourceLife,int TargetLife,ulong Seed,NecromancerState State,bool Revive,Point3D Origin,int Map);
    private sealed record Saved(Held[] Actors,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-item-fear-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int[] N(JsonElement x)=>x.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static Item Original()=>NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="The Face of Horror").Id,99,new Random(173),true);
    public sealed class Recipient:GraveyardSkeleton
    {
        internal int Notifications;internal bool FearAtNotification;
        [Constructible]public Recipient(){Frozen=CantWalk=NoKillAwards=true;}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int amount,Mobile from,bool lethal){Notifications++;FearAtNotification=SourceTerror.ItemActive(this);base.OnDamage(amount,from,lethal);}
        public override void Serialize(IGenericWriter w){base.Serialize(w);w.Write(0);}
        public override void Deserialize(IGenericReader r){base.Deserialize(r);r.ReadInt();}
    }
    private static Recipient Target(PlayerMobile p,BaseCreature source,int offset=0)
    {var t=new Recipient();t.SetHits(20000);foreach(var k in Enum.GetValues<ResistanceType>()){t.SetResistance(k,0);}t.MoveToWorld(new(source.X+offset,source.Y,source.Z),p.Map);t.AIObject?.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static void Delete(BaseCreature t){NecromancerEffects.Profiles.Clear(t);TimedEffects.Clear(t);t.Corpse?.Delete();t.Delete();}
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-item-fear-cases.json")));int callbacks=0,compositions=0;
        foreach(var row in doc.RootElement.GetProperty("callback").EnumerateArray())
        {
            var a=N(row.GetProperty("input"));var b=N(row.GetProperty("output"));bool draw=a[2]>=0&&a[3]==1&&(a[4]&12)==0&&a[0]>0;
            bool success=draw&&(a[1]&127)<a[0];
            Require(b[0]==(success?1:0)&&b[1]==(draw?1:0)&&b[2]==(success?77:0)&&b[3]==(success?1:0)&&b[5]==(success?120:0),"Full item howl callback differs at "+callbacks);callbacks++;
        }
        foreach(var row in doc.RootElement.GetProperty("composition").EnumerateArray())
        {
            var a=N(row.GetProperty("input"));var b=N(row.GetProperty("output"));bool helper=a[1]!=0&&a[2]==0;
            bool factory=helper&&a[5]!=0&&a[6]==0&&a[7]!=0&&a[3]<100&&a[0]!=4;
            int frames=SourceTerror.Duration(20,a[3]);var at=DateTime.UnixEpoch.AddMilliseconds(4000);
            NecromancerCurse old=a[0]==0?null:new(a[0]==3?CombatAbility.AmplifyDamage:a[0]==4?CombatAbility.Attract:CombatAbility.Terror,999,a[0]==2?2:1,DateTime.UnixEpoch.AddMilliseconds(777*40),0,0);
            var next=factory?NecromancerCurseCasts.Replacement(old,CombatAbility.Terror,7,1,null,at.AddMilliseconds(frames*40L),at,0,0):null;
            var value=next??old;
            int skill=value==null?0:value.Ability==CombatAbility.AmplifyDamage?66:value.Ability==CombatAbility.Attract?86:77;
            int[] expected=[1,1,skill,value?.Rank??0,(int)(value?.Source??0),value==null?0:(int)(value.UntilUtc-DateTime.UnixEpoch).TotalMilliseconds/40,helper?7:0,helper&&a[4]!=0?1:0,helper&&a[4]!=0?1:0,next!=null&&a[0]==3?1:0,next!=null?1:0];
            Require(b.SequenceEqual(expected),"Full item Terror helper/factory differs at "+compositions);compositions++;
        }
        Require(callbacks==5376&&compositions==1280,"Incomplete item fear source cases");
        var oldJson=JsonSerializer.SerializeToNode(new NecromancerState());oldJson.AsObject().Remove("ItemTerror");Require(oldJson.Deserialize<NecromancerState>().ItemTerror==null,"Legacy state invented an item initializer");
    }
    private static void Controls(PlayerMobile p,BaseCreature source)
    {
        var t=Target(p,source,4);var other=Target(p,source,7);var facets=SummonDifficulties.Facets();
        try
        {
            source.Combatant=other;int hp=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);var seed=NpcEquipmentRandom.State(source);
            foreach(uint roll in new uint[]{0,63,64,127,128,255})
            {
                NecromancerEffects.Profiles.Clear(t);int draws=0;var at=TimedEffects.Now;
                bool result=NpcItemFear.Apply(source,t,at,()=>{draws++;return roll;});bool success=(roll&127)<64;
                Require(result==success&&draws==1&&SourceTerror.ItemActive(t)==success,"Fear chance did not use one raw low7 draw");
                if(success){var s=NecromancerEffects.Profiles.Find(t);Require(s.Curse is{Ability:CombatAbility.Terror,Rank:1}&&s.Curse.Source==source.Serial.Value&&s.Curse.UntilUtc==at.AddMilliseconds(800)&&s.ItemTerror is{Started:false,LaunchRank:1}&&s.ItemTerror.Override==source.Serial.Value&&s.ItemTerror.NextUtc==at.AddMilliseconds(40),"Fixed direct Terror/initializer contract changed");}
            }
            foreach(var difficulty in Enum.GetValues<MonsterDifficulty>())
            {
                SummonDifficulties.SetFacets([new(p.Map.MapID,difficulty)]);NecromancerEffects.Profiles.Clear(t);var at=TimedEffects.Now;
                Require(NpcItemFear.Apply(source,t,at,()=>0)&&NecromancerEffects.Curse(t).UntilUtc==at.AddMilliseconds(800),"Item fear acquired a facet/difficulty multiplier");
            }
            SummonDifficulties.SetFacets(facets);NecromancerEffects.Profiles.Clear(t);Require(NpcItemFear.Apply(source,t,TimedEffects.Now,()=>0),"Before neighbor lease");
            var escaping=NecromancerEffects.Profiles.Find(t);NecromancerEffects.Profiles.Store(t,escaping with{ItemTerror=escaping.ItemTerror with{NextUtc=TimedEffects.Now.AddMilliseconds(-40)}});
            Require(SourceTerror.ThinkItem(t)&&SourceTerror.State(t)is{Started:true,Override:0},"Item fear did not reach first escape");
            escaping=NecromancerEffects.Profiles.Find(t);var lease=SourceMind.Attract(t,escaping,other,100,TimedEffects.Now);
            NecromancerEffects.Profiles.Store(t,escaping with{Mind=lease});
            Require(SourceTerror.ItemActive(t)&&NecromancerEffects.Profiles.Find(t).Mind==lease,"Attract neighbor lease replaced item AI or was lost: item="+SourceTerror.ItemActive(t)+",mind="+Json(NecromancerEffects.Profiles.Find(t).Mind)+",expected="+Json(lease));
            var redirected=NecromancerEffects.Profiles.Find(t);NecromancerEffects.Profiles.Store(t,redirected with{ItemTerror=redirected.ItemTerror with{NextUtc=TimedEffects.Now.AddMilliseconds(-40)}});
            Require(SourceTerror.ThinkItem(t)&&SourceTerror.State(t).Focus==other.Serial.Value&&NecromancerEffects.Profiles.Find(t).Mind==lease,"Escaping item AI ignored or rolled the later explicit neighbor target");
            NecromancerEffects.Profiles.Clear(t);Require(NpcItemFear.Apply(source,t,TimedEffects.Now,()=>0),"Before equal-rank refresh");
            var now=TimedEffects.Now;var first=NecromancerEffects.Profiles.Find(t);
            var equal=first.Curse with{Source=p.Serial.Value,UntilUtc=now.AddHours(1)};NecromancerEffects.Profiles.Store(t,first with{Curse=equal,ItemTerror=null});
            Require(NpcItemFear.Apply(source,t,now,()=>0)&&NecromancerEffects.Curse(t).Source==p.Serial.Value&&NecromancerEffects.Curse(t).UntilUtc==now.AddMilliseconds(800),"Equal rank did not retain creator and shorten to20frames");
            var state=NecromancerEffects.Profiles.Find(t);var stronger=state.Curse with{Rank=2,UntilUtc=now.AddHours(1)};NecromancerEffects.Profiles.Store(t,state with{Curse=stronger,ItemTerror=null});
            Require(NpcItemFear.Apply(source,t,now,()=>0)&&NecromancerEffects.Curse(t)==stronger&&SourceTerror.State(t).Override==source.Serial.Value,"Stronger curse was replaced or suppressed separate AI initialization");
            var attract=new NecromancerCurse(CombatAbility.Attract,p.Serial.Value,1,now.AddHours(1),p.X,p.Y);
            NecromancerEffects.Profiles.Store(t,new(Curse:attract,Mind:new(17,p.Map.MapID,now.AddHours(1),2,p.Serial.Value)));
            Require(NpcItemFear.Apply(source,t,now,()=>0)&&NecromancerEffects.Curse(t)==attract&&SourceTerror.ItemActive(t)&&NecromancerEffects.Profiles.Find(t).Mind.Mode==0,"Protected Attract did not retain curse and supersede target override");
            state=NecromancerEffects.Profiles.Find(t);NecromancerEffects.Profiles.Store(t,state with{ItemTerror=state.ItemTerror with{NextUtc=TimedEffects.Now.AddMilliseconds(-40)}});
            Require(SourceTerror.ThinkItem(t)&&!SourceTerror.ItemActive(t)&&!SourceTerror.ThinkItem(t)&&NecromancerEffects.Curse(t)==attract,"Rejected curse initializer did not return to ordinary AI without deleting Attract");
            NecromancerEffects.Profiles.Clear(t);Require(NpcItemFear.Apply(source,t,TimedEffects.Now,()=>0),"Before learned AI replacement");
            Require(NecromancerCurseCasts.Apply(p,t,CombatAbility.Terror,20)&&!SourceTerror.ItemActive(t)&&SourceTerror.State(t)is{Skill:CombatAbility.Terror} learned&&learned.Override==source.Serial.Value,"Learned Terror lost the item's existing dynamic target while replacing its AI");
            Require(NecromancerCurseCasts.Apply(p,t,CombatAbility.DimVision,1)&&!SourceTerror.ItemActive(t),"A later learned AI initializer retained the item's obsolete AI");
            Require(SkillHealth.Balance256(t)==hp&&t.Notifications==0&&NpcEquipmentRandom.State(source)==seed&&NecromancerEffects.ManaBalance256(p)==mana&&source.Combatant==other&&NecromancerEffects.Curse(other)==null,"Direct fear replayed damage,RNG,mana,target change or area cast");
            var invalid=new TerrorAi(CombatAbility.Howl,2,p.Map.MapID,false,0,0,now);bool refused=false;try{SourceTerror.ValidateItem(invalid);}catch(InvalidDataException){refused=true;}Require(refused,"Malformed saved item rank accepted");
            Require(NpcCombatEvents.RestoreSubscriptions([112]).Subscriptions().SequenceEqual(new[]{112}),"Fear subscription restore refused");
        }
        finally{SummonDifficulties.SetFacets(facets);source.Combatant=null;Delete(t);Delete(other);}
    }
    private static Recipient Contact(PlayerMobile p,BaseCreature source)
    {
        var t=Target(p,source);var oldBlock=SourceBlocking.BlockRoll;var oldChoice=SourceBlocking.ChoiceRoll;
        try
        {
            SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=n=>Math.Min(1,n-1);
            var rules=IronGolemContact.Require(source,p,t);IronGolemContact.Prepared hit=null;ulong expected=0;bool found=false;
            for(ulong seed=1;seed<10000&&!found;seed++)
            {
                NpcEquipmentRandom.Set(source,seed);hit=IronGolemContact.Prepare(source,t,rules,()=>5);expected=NpcEquipmentRandom.State(source).Seed;uint raw=SourceRandom.Roll(ref expected);
                found=(hit.Impact.Packet.ResultFlags&1)!=0&&(raw&127)<64;if(!found){hit.Cancel();}
            }
            Require(found,"No positive ordered fear contact");int hp=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);var at=TimedEffects.Now;
            IronGolemContact.Release(source,p,t,hit,_=>true,at);
            Require(t.Notifications==1&&t.FearAtNotification&&SkillHealth.Balance256(t)==hp-hit.Impact.Packet.Total256&&NpcEquipmentRandom.State(source).Seed==expected&&NativeMonsterReturn.Session(t).Packets==1&&NecromancerEffects.ManaBalance256(p)==mana,"Fear contact duplicated/misordered HP,RNG,notification or mana");
            Require(NecromancerEffects.Curse(t)is{Ability:CombatAbility.Terror,Rank:1} curse&&curse.Source==source.Serial.Value&&curse.UntilUtc==at.AddMilliseconds(800),"Contact lost fixed real-caster Terror");return t;
        }
        catch{Delete(t);throw;}
        finally{SourceBlocking.BlockRoll=oldBlock;SourceBlocking.ChoiceRoll=oldChoice;}
    }
    private static Held Hold(PlayerMobile p,BaseCreature source,Item item,Recipient target,bool revive)
    {
        var state=NecromancerEffects.Profiles.Find(target);var hold=TimedEffects.Now.Add(FixtureSpace.Retention);
        state=state with{Curse=state.Curse with{UntilUtc=hold},ItemTerror=state.ItemTerror with{NextUtc=hold}};NecromancerEffects.Profiles.Store(target,state);
        SkillHealth.SetBalance256(source,SkillHealth.Maximum256(source)-1237);TimedEffects.PreventHealing(source,TimedEffects.Now);var origin=p.Location;int map=p.Map.MapID;
        source.AIObject?.AITimer.Stop();target.AIObject?.AITimer.Stop();PersistentMinions.Suspend(p);p.Internalize();target.Internalize();
        return new(p.Serial.Value,source.Serial.Value,item.Serial.Value,target.Serial.Value,Json(ClassGear.State(item)),SkillHealth.Balance256(source),SkillHealth.Balance256(target),NpcEquipmentRandom.State(source).Seed,state,revive,origin,map);
    }
    private static void Check(NetState net,bool reload)
    {
        Differential();
        if(!reload)
        {
            var rows=new List<Held>();
            foreach(bool graph in new[]{false,true})
            {
                var p=NpcFreezeHitChecks.Owner();var item=Original();var source=NpcFreezeHitChecks.Cast(p,item,"howl",64);source.SuspendActions();Controls(p,source);
                if(graph){var proc=NpcItemProcs.Create(source);NpcItemProcs.Refresh(source,NpcCombatEvents.LegacyForSource(source));Require(proc.Capture().Events.Entries.Count(e=>e.Function==8)==2,"Fear graph did not bind both damage events");}
                rows.Add(Hold(p,source,item,Contact(p,source),false));
            }
            var q=NpcFreezeHitChecks.Owner();FixtureNetwork.Authenticate(net,q,"npc-fear-");var revived=NpcFreezeHitChecks.Revive(q,out var gear,"The Face of Horror","howl",64);Controls(q,revived);rows.Add(Hold(q,revived,gear,Contact(q,revived),true));
            File.WriteAllText(Manifest,Json(new Saved(rows.ToArray(),0)));
        }
        else
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));FixtureNetwork.Authenticate(net,(PlayerMobile)World.FindMobile((Serial)saved.Actors[0].Owner),"npc-fear-reload-");
            foreach(var row in saved.Actors)
            {
                var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var source=(BaseCreature)World.FindMobile((Serial)row.Source);var target=(Recipient)World.FindMobile((Serial)row.Target);var item=World.FindItem((Serial)row.Item);
                Require(source is{Alive:true,Deleted:false}&&source.ControlMaster==p&&SkillHealth.Balance256(source)==row.SourceLife&&SkillHealth.Balance256(target)==row.TargetLife&&NpcEquipmentRandom.State(source).Seed==row.Seed&&Json(ClassGear.State(item))==row.Gear&&Json(NecromancerEffects.Profiles.Find(target))==Json(row.State),"Saved original fear source/loot/wounds/seed/curse/independent AI changed");
                Require(NpcItemFear.Bonus(source)==64&&NpcCombatEvents.ForSource(source).Subscriptions().Contains(112)&&(row.Revive?item.Parent is Corpse:item.Parent==source),"Saved fear admission/original gear ownership lost");
            }
            // Resume the original first owner/minion through the real parking
            // service, advance its saved victim's phase, then retain the held
            // effect/deadline for the second independent reload comparison.
            var first=saved.Actors[0];var owner=(PlayerMobile)World.FindMobile((Serial)first.Owner);var actor=(IronGolemBody)World.FindMobile((Serial)first.Source);var victim=(Recipient)World.FindMobile((Serial)first.Target);
            owner.MoveToWorld(first.Origin,Map.Maps[first.Map]);PersistentMinions.Resume(owner);Require(actor.Map==owner.Map,"Official fear source resume failed");actor.SuspendActions();
            victim.MoveToWorld(new(actor.X+4,actor.Y,actor.Z),actor.Map);var raw=NecromancerEffects.Profiles.Find(victim);NecromancerEffects.Profiles.Store(victim,raw with{ItemTerror=raw.ItemTerror with{NextUtc=TimedEffects.Now.AddMilliseconds(-40)}});
            Require(SourceTerror.ThinkItem(victim)&&SourceTerror.State(victim)is{Started:true,Override:0,Goal:not null} advanced&&advanced.Focus==actor.Serial.Value,"Original saved fear did not resume escape phase");
            Require(SkillHealth.Balance256(actor)==first.SourceLife&&SkillHealth.Balance256(victim)==first.TargetLife&&NpcEquipmentRandom.State(actor).Seed==first.Seed,"Fear phase resume healed/damaged or rolled its original source");
            NecromancerEffects.Profiles.Store(victim,first.State);victim.Internalize();PersistentMinions.Suspend(owner);owner.Internalize();
            File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
        }
        Log.Information("NPC item fear:5376callback/1280factory cases;raw low7/fixed20frames/equal-stronger-Attract AI separation;learned original Iron/graph/frozen Revive ordered contact and original wounded saves pass");
    }
    private static void Live(NetState net)
    {
        var p=NpcFreezeHitChecks.Owner();FixtureNetwork.Authenticate(net,p,"npc-fear-live-");var item=Original();var actor=NpcFreezeHitChecks.Cast(p,item,"howl",64);actor.SuspendActions();var target=Target(p,actor,4);target.Frozen=target.CantWalk=false;var origin=target.Location;var at=TimedEffects.Now;int mana=NecromancerEffects.ManaBalance256(p),hp=SkillHealth.Balance256(target);bool moved=false;long deadline=Core.TickCount+5000;
        Require(NpcItemFear.Apply(actor,target,at,()=>0),"Live fear failed");target.AIObject.NextMove=Core.TickCount;target.AIObject.AITimer.Start();
        void End(){Delete(target);actor.Delete();item.Delete();p.Delete();}
        void Wait()
        {
            try
            {
                Require(Core.TickCount<deadline,"Live fear movement/expiry timeout");
                if(target.Location!=origin&&SourceTerror.State(target)is{Started:true}){moved=true;}
                if(SourceTerror.ItemActive(target)||NecromancerEffects.Curse(target)!=null){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Wait);return;}
                Require(moved&&SkillHealth.Balance256(target)==hp&&NecromancerEffects.ManaBalance256(p)==mana,"Natural fear did not move/expire freely");End();Log.Information("npc-item-fear-live-result: passed");
            }
            catch(Exception e){End();Log.Error(e,"npc-item-fear-live-result: failed");}
        }
        Log.Information("npc-item-fear-live: ready");Timer.StartTimer(TimeSpan.FromMilliseconds(10),Wait);
    }
    public static void Configure()
    {
        foreach(string name in new[]{"npc-item-fear-check","npc-item-fear-reload-check"})ConsoleInputHandler.RegisterCommand(name,"Original NPC item fear and independent AI",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected(name,net=>
        {try{Check(net,name.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}})));
        ConsoleInputHandler.RegisterCommand("npc-item-fear-live","Natural item Terror movement/expiry",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected("npc-item-fear-live",Live)));
    }
}
