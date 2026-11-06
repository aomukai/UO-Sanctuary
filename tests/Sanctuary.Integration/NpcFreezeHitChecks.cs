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

public static class NpcFreezeHitChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcFreezeHitChecks));
    private sealed record Held(uint Owner,uint Source,uint Item,string Gear,uint Target,int SourceLife,int TargetLife,ulong Seed,NamedControlState Control,bool Revive);
    private sealed record Saved(Held[] Actors,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-freeze-hit-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(n=>n.GetInt32()).ToArray();
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-freeze-hit-cases.json")));int callbacks=0,status=0,removal=0;
        foreach(var row in doc.RootElement.GetProperty("callback").EnumerateArray())
        {
            var a=N(row);int frames=a[2]>0?NamedControl.FreezeFrames(a[0],a[1],a[2],a[3]!=0,a[4]):0;
            Require(a[5]==(frames>0?1:0)&&a[6]==(a[2]>0?1:0)&&a[7]==(frames>0?1:0)&&a[8]==(frames>0?1000+frames:0)&&a[9]==(frames>0?17:0)&&a[10]==25600&&a[11]==(frames>0?1:0),"Complete nested callback/status differs at "+callbacks);callbacks++;
        }
        foreach(var row in doc.RootElement.GetProperty("status").EnumerateArray())
        {
            var a=N(row);bool dead=a[3]==16;int frames=dead?a[0]:ColdStates.LengthFrames(a[0],a[3]==8?Math.Min(a[2],75):a[2],a[5]!=0,a[4]!=0);
            int mode=!dead&&frames>0?FreezeStates.Mode(1,a[3]==1,a[3]==2,a[3]==4,a[3]==8,a[6]):0;
            int raw=mode==2?FreezeStates.RawFrames(frames,true,new[]{1,2,4}[a[1]]):0;
            int until=mode==2?Math.Max(a[7]!=0?1200:0,1000+raw):a[7]!=0?1200:0;
            Require(a[8]==frames&&a[9]==(mode==2||a[7]!=0?1:0)&&a[10]==until&&a[11]==(a[7]!=0?41:mode==2?17:0)&&a[12]==(mode==2?until:0)&&a[13]==(mode==2?1000+raw+1:0)&&a[14]==(dead?0:25600),"Complete zero-result status differs at "+status);status++;
        }
        foreach(var row in doc.RootElement.GetProperty("remove").EnumerateArray())
        {
            var a=N(row);bool stays=a[0]!=0&&a[1]!=0;int delay=FreezeStates.RemovalDelay(a[6],a[6]+a[5]*10,a[4]!=0);
            Require(a[7]==(stays?1:0)&&a[8]==(stays?0:1)&&a[9]==(!stays&&a[2]!=0&&a[3]!=0?1000+delay:0),"Complete freeze removal differs at "+removal);removal++;
        }
        Require(callbacks==384&&status==5760&&removal==432,"Incomplete freeze source cases");
    }
    private static void Train(PlayerMobile p,string id)
    {TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(string prior in row.Prerequisites){Train(p,prior);}if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}}
    internal static PlayerMobile Owner()
    {
        var p=new PlayerMobile{Name="NPC freeze owner [fixture]",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(p,99);Train(p,"necromancer.irongolem");Train(p,"necromancer.revive");
        FixtureSpace.Corridor(p,6100,24,2800);while(p.RawInt<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Energy),"Energy preparation");}NecromancerEffects.SetMana256(p,p.ManaMax*256);return p;
    }
    private static Item Iceblink()=>NamedGear.Create(NamedGear.Catalog.Items.Single(d=>d.SourceKey=="Iceblink").Id,99,new Random(173),true);
    internal static IronGolemBody Cast(PlayerMobile p,Item original,string code="freeze",int expectedBonus=1)
    {
        original.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);ClassCombat.Stop(p);ClassCombat.For(p).NextAction=0;
        Require(SkillBindings.Instance.AssignQuick(p,7,CombatAbility.IronGolem),"Iron quick binding");int mana=NecromancerEffects.ManaBalance256(p);
        Require(SkillBindingNetwork.Use(p,DiabloClass.Necromancer,7,CombatAbility.IronGolem,original.Serial,new(0,0,0)),"Iceblink learned start refused");
        long at=ClassCombat.For(p).EffectAt;Require(ClassCombat.ResolveAction(p,at)&&NecromancerEffects.ManaBalance256(p)==mana-35*256,"Learned release/payment failed");
        var body=NecromancerGolems.For(p).OfType<IronGolemBody>().Single();Require(original.Parent==body&&NpcEquipmentCombat.EventBonus(body,code)==expectedBonus,"Original item event did not bind");return body;
    }
    public sealed class Recipient:Skeleton
    {
        internal int Calls;internal bool FrozenAtNotification;
        [Constructible]public Recipient(){Frozen=true;CantWalk=true;NoKillAwards=true;}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int amount,Mobile from,bool lethal){Calls++;FrozenAtNotification=FreezeStates.Shattered(this);base.OnDamage(amount,from,lethal);}
        public override void Serialize(IGenericWriter w){base.Serialize(w);w.Write(0);}
        public override void Deserialize(IGenericReader r){base.Deserialize(r);r.ReadInt();}
    }
    private static Recipient Target(PlayerMobile p,BaseCreature source)
    {
        var t=new Recipient();t.SetHits(20000);foreach(var kind in Enum.GetValues<ResistanceType>()){t.SetResistance(kind,0);}t.MoveToWorld(source.Location,p.Map);t.AIObject?.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);return t;
    }
    private static void Remove(BaseCreature t){TimedEffects.Clear(t);t.Corpse?.Delete();t.Delete();}
    private static void Direct(PlayerMobile p,IronGolemBody body)
    {
        foreach(int resistance in new[]{0,50,100})
        {
            var t=Target(p,body);try
            {
                t.SetResistance(ResistanceType.Cold,resistance);int hp=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);var seed=NpcEquipmentRandom.State(body);int draws=0;var at=TimedEffects.Now;
                Require(NpcItemFreeze.Apply(body,t,false,at,()=>{draws++;return 0;}),"Positive function14 chance did not return success");
                Require(draws==1&&t.Calls==0&&SkillHealth.Balance256(t)==hp&&NecromancerEffects.ManaBalance256(p)==mana&&NpcEquipmentRandom.State(body)==seed,"Nested status replayed hit/HP/owner mana or wrong RNG ownership");
                Require(FreezeStates.Shattered(t)==(resistance<100),"Freeze immunity branch incorrect");
                if(resistance<100)
                {
                    int raw=NamedControl.FreezeFrames(ClassStats.Level(body),ClassStats.Level(t),1,false,0);int frames=ColdStates.LengthFrames(raw,resistance,false,false)/NecromancerEffects.DifficultyDivisor(t);
                    var state=NamedControl.State(t);Require(state.Freeze.Source==body.Serial.Value&&state.FreezeUntil==TimedEffects.TimeAt(TimedEffects.TickAt(at)).AddMilliseconds(Math.Max(1,frames)*40),"Source freeze owner/frame deadline differs");
                    var prior=state;Require(NpcItemFreeze.Apply(body,t,false,at.AddMilliseconds(40),()=>99)&&NamedControl.State(t).FreezeUntil==prior.FreezeUntil&&NamedControl.State(t).Freeze.Source==prior.Freeze.Source,"Weaker refresh shortened/reassigned active freeze");
                }
            }
            finally{Remove(t);}
        }
    }
    private static ulong Seed(BaseCreature source,BaseCreature target,bool iron)
    {
        double chance=ClassStats.HitChance(ClassStats.AttackRating(source),ClassStats.Defense(target),ClassStats.Level(source),ClassStats.Level(target));
        for(ulong s=1;s<10000;s++){ulong next=s;if(SourceRandom.Roll(ref next)%100/100.0<chance){return s;}}
        throw new InvalidOperationException("No deterministic hit seed");
    }
    private static void Contact(PlayerMobile p,BaseCreature source,Recipient target,bool controller)
    {
        var oldBlock=SourceBlocking.BlockRoll;
        try
        {
            // This replay owns the actor seed and the independent defender
            // block input; a blocked preview is not a positive freeze case.
            SourceBlocking.BlockRoll=n=>n-1;
            var at=TimedEffects.Now;ulong initial=Seed(source,target,source is IronGolemBody);NpcEquipmentRandom.Set(source,initial);
            IronGolemWorldState world=null;
            if(controller)
            {
                var body=(IronGolemBody)source;world=body.Prepared.Controller with{Mode=4,Target=target.Serial.Value,NextThink=0,ActionAt=body.Prepared.Controller.Frame+1,EndAt=body.Prepared.Controller.Frame+20,AttackSpeed=256};body.RestoreController(world);at=world.NextUtc;
            }
            var rules=IronGolemContact.Require(source,p,target);IronGolemContact.Prepared prepared=null;ulong expected=0;int frames=0;
            // Pick a positive callback control, rather than assuming that the
            // first actor seed must also pass the separate freeze-chance roll.
            for(int tries=0;tries<1000&&frames<=0;tries++,initial++)
            {
                NpcEquipmentRandom.Set(source,initial);
                prepared=IronGolemContact.Prepare(source,target,rules,()=>controller?IronGolemWorld.Result((IronGolemBody)source,p,world,target):5);
                expected=NpcEquipmentRandom.State(source).Seed;uint draw=SourceRandom.Roll(ref expected);
                frames=(prepared.Impact.Packet.ResultFlags&1)!=0?NamedControl.FreezeFrames(ClassStats.Level(source),ClassStats.Level(target),1,false,(int)(draw%100)):0;
                if(frames<=0){prepared.Cancel();}
            }
            initial--;Require(frames>0,"No deterministic positive freeze control");
            int hp=SkillHealth.Balance256(target);
            if(controller){NpcEquipmentRandom.Set(source,initial);IronGolemWorld.Advance((IronGolemBody)source,at,true);}
            else{var result=IronGolemContact.Release(source,p,target,prepared,_=>true,at);Require(!result.DeathModeAttempted,"Nonlethal freeze contact killed recipient");}
            Require(target.Calls==1&&target.FrozenAtNotification&&FreezeStates.Shattered(target)&&SkillHealth.Balance256(target)==hp-prepared.Impact.Packet.Total256,"Freeze/main HP notification mismatch: controller="+controller+" source="+source.GetType().Name+" calls="+target.Calls+" frozenAtNotify="+target.FrozenAtNotification+" frozen="+FreezeStates.Shattered(target)+" hp="+SkillHealth.Balance256(target)+" expected="+(hp-prepared.Impact.Packet.Total256)+" frames="+frames+" result="+prepared.Impact.Packet.ResultFlags);
            Require(NpcEquipmentRandom.State(source).Seed==expected&&NativeMonsterReturn.Session(target).Packets==1&&frames>0,"Nested packet duplicated a stream draw/return/free");

        }
        finally{SourceBlocking.BlockRoll=oldBlock;}
    }
    private static void Filled(PlayerMobile p,IronGolemBody source)
    {
        var target=Target(p,source);
        try
        {
            target.SetResistance(ResistanceType.Cold,50);var at=TimedEffects.Now;int hp=SkillHealth.Balance256(target);
            // Suppress damage callbacks here to distinguish the allocated
            // packet's freeze lane from Iceblink's separate nested callback.
            var fill=new NpcFillPacket(Physical:512,Poison:32,ColdFrames:50,PoisonFrames:100,FreezeFrames:100,ResultFlags:33);
            var impact=NpcEquipmentImpact.FromFill(source,target,fill,NpcEquipmentPoison.Capture(source));
            Require(impact.Packet.FreezeFrames==50&&impact.Packet.ColdFrames==25,"Allocated durations were not mitigated exactly once");
            var pending=new IronGolemContact.Prepared(IronGolemContact.Require(source,p,target),impact);
            IronGolemContact.Release(source,p,target,pending,_=>true,at);
            Require(target.Calls==1&&!target.FrozenAtNotification&&FreezeStates.Shattered(target)&&ColdStates.OtherRate(target)<0&&PoisonStates.Active(target,at)
                &&SkillHealth.Balance256(target)==hp-impact.Packet.Total256,"Allocated HP/cold/freeze/poison phases changed order or lost a status");
            Require(NamedControl.State(target).FreezeUntil==TimedEffects.TimeAt(TimedEffects.TickAt(at)).AddMilliseconds(50*40),"Allocated freeze resistance was applied twice");
        }
        finally{Remove(target);}
    }
    internal static BaseCreature Revive(PlayerMobile p,out Item gear,string name="Iceblink",string code="freeze",int expectedBonus=1)
    {
        var b=new Skeleton{Name="NPC freeze original [fixture]",NoKillAwards=true};foreach(var i in b.Items.Where(i=>i is not Backpack&&i.Layer!=Layer.Bank).ToArray()){i.Delete();}gear=NamedGear.Create(NamedGear.Catalog.Items.Single(d=>d.SourceKey==name).Id,99,new Random(173),true);b.AddItem(gear);Require(gear.Parent==b,"Frozen original gear equip");
        b.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);b.AIObject?.AITimer.Stop();Corpse corpse=null;var old=ClassLootHooks.CreatureDied;
        ClassLootHooks.CreatureDied=(who,item)=>{old?.Invoke(who,item);if(who==b){corpse=item as Corpse;corpse?.DropItem(new Gold(1));}};
        try{p.DoHarmful(b,true);b.Damage(b.Hits+1,p);CombatDamage.Finish(b,p);}finally{ClassLootHooks.CreatureDied=old;}
        Require(corpse is{Deleted:false}&&ReviveOriginals.Find(corpse)==b,"Original freeze corpse not retained");corpse.BeginDecay(FixtureSpace.Retention);NecromancerEffects.SetMana256(p,p.ManaMax*256);
        Require(ReviveCasts.Plan(p,corpse,out _,out _,out var issue)&&ReviveCasts.Release(p,corpse),"Learned frozen Revive failed: "+issue);
        Require(gear.Parent==corpse&&NpcEquipmentCombat.EventBonus(b,code)==expectedBonus&&ReviveSwings.Handles(b),"Frozen Revive lost loot/combat event/swing profile");return b;
    }
    private static Held Hold(PlayerMobile p,BaseCreature source,Item item,Recipient target,bool revive)
    {
        var now=TimedEffects.Now;TimedEffects.Actor(target,now).Control=NamedControl.State(target) with{FreezeUntil=now.Add(FixtureSpace.Retention),Freeze=NamedControl.State(target).Freeze with{AiUntilUtc=now.Add(FixtureSpace.Retention).AddMilliseconds(40)}};
        SkillHealth.SetBalance256(source,SkillHealth.Maximum256(source)-1237);source.AIObject?.AITimer.Stop();target.AIObject?.AITimer.Stop();PersistentMinions.Suspend(p);p.Internalize();target.Internalize();
        return new(p.Serial.Value,source.Serial.Value,item.Serial.Value,Json(ClassGear.State(item)),target.Serial.Value,SkillHealth.Balance256(source),SkillHealth.Balance256(target),NpcEquipmentRandom.State(source).Seed,NamedControl.State(target),revive);
    }
    private static void Check(bool reload,NetState net)
    {
        SourceCases();
        if(!reload)
        {
            var rows=new List<Held>();var p=Owner();var item=Iceblink();var body=Cast(p,item);Direct(p,body);Filled(p,body);
            var t=Target(p,body);Contact(p,body,t,false);Remove(t);var graph=NpcItemProcs.Create(body);NpcItemProcs.Refresh(body,NpcCombatEvents.LegacyForSource(body));
            Require(graph.Capture().Innate.Any(s=>s.Stat==134&&s.Value==1)&&graph.Capture().Events.Entries.Count(e=>e.Function==14)==2,"Live graph freeze subscription missing");
            var ranged=Target(p,body);ulong rangeSeed=173,after=rangeSeed;SourceRandom.Roll(ref after);NpcEquipmentRandom.Set(body,rangeSeed);
            NpcCombatEvents.ForSource(body).Dispatch("domissiledamage",null,body,ranged,TimedEffects.Now);
            Require(ranged.Calls==0&&FreezeStates.Shattered(ranged)&&NpcEquipmentRandom.State(body).Seed==after,"Graph ranged function14 failed or replayed a damage event");Remove(ranged);
            t=Target(p,body);Contact(p,body,t,true);rows.Add(Hold(p,body,item,t,false));
            var q=Owner();FixtureNetwork.Authenticate(net,q,"npc-freeze-");var revived=Revive(q,out var original);var victim=Target(q,revived);Contact(q,revived,victim,false);rows.Add(Hold(q,revived,original,victim,true));
            File.WriteAllText(Manifest,Json(new Saved(rows.ToArray(),0)));
        }
        else
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
            FixtureNetwork.Authenticate(net,(PlayerMobile)World.FindMobile((Serial)saved.Actors[0].Owner),"npc-freeze-reload-");
            foreach(var row in saved.Actors)
            {
                var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var source=World.FindMobile((Serial)row.Source)as BaseCreature;var item=World.FindItem((Serial)row.Item);var target=World.FindMobile((Serial)row.Target)as BaseCreature;
                Require(source is{Alive:true,Deleted:false}&&source.ControlMaster==p&&target is{Alive:true,Deleted:false}&&SkillHealth.Balance256(source)==row.SourceLife&&SkillHealth.Balance256(target)==row.TargetLife&&NpcEquipmentRandom.State(source).Seed==row.Seed&&NamedControl.State(target)==row.Control&&Json(ClassGear.State(item))==row.Gear,"Original freeze source/item/stream/wounds/status changed on reload");
                Require(NpcItemFreeze.Bonus(source)==1&&NpcCombatEvents.ForSource(source).Subscriptions().Contains(134),"Saved freeze registration lost");
                if(!row.Revive){Require(item.Parent==source&&NpcItemProcs.Get((SkeletonGuardian)source).Capture().Events.Entries.Count(e=>e.Function==14)==2,"Original graph lost freeze callbacks");}
                else{Require(item.Parent is Corpse&&!source.Items.Contains(item),"Frozen Revive consumed or equipped its loot");}
            }
            File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
        }
        Log.Information("NPC freeze:384callback/5760status/432removal source comparisons;learned Iceblink Iron and original frozen Revive;no duplicate damage/return events,RNG,graph registration and original wounded status/gear persist through two reloads");
    }
    public static void Configure()
    {
        foreach(string command in new[]{"npc-freeze-hit-check","npc-freeze-hit-reload-check"})
        {
            ConsoleInputHandler.RegisterCommand(command,"Learned NPC freeze-target items and ordered status",_=>Core.LoopContext.Post(()=>
            {
                FixtureNetwork.Connected(command,net=>
                {
                    try{Check(command.EndsWith("-reload-check",StringComparison.Ordinal),net);Log.Information("{Name}: passed",command);}
                    catch(Exception e){Log.Error(e,"{Name}: failed",command);}
                });
            }));
        }
    }
}
