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
public static class NpcItemKnockbackChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcItemKnockbackChecks));
    private sealed record Held(uint Owner,uint Source,uint Item,uint Target,string Gear,int SourceLife,int TargetLife,ulong Seed,SourceKnockbackState Motion,bool Revive);
    private sealed record Saved(Held[] Actors,int Generation);
    private static int Scene;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-item-knockback-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static Item Original()=>NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Griswolds Edge").Id,99,new Random(173),true);
    public sealed class Recipient:GraveyardSkeleton
    {
        internal int Calls;internal bool EarlyMotion;
        [Constructible]public Recipient(){NoKillAwards=true;}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int n,Mobile from,bool lethal){Calls++;EarlyMotion=SourceKnockbacks.Remaining(this)>0;base.OnDamage(n,from,lethal);}
        public override void Serialize(IGenericWriter w){base.Serialize(w);w.Write(0);}
        public override void Deserialize(IGenericReader r){base.Deserialize(r);r.ReadInt();}
    }
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-item-knockback-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var a=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();int draws=0;
            var value=NpcItemKnockback.Evaluate(a[0]!=0,a[1]!=0,a[2]!=0,a[3],a[4],a[5]!=0,(a[6]&2)!=0,(a[6]&1)!=0,a[7],()=>{draws++;return(uint)a[8];});
            Require((value.Success?1:0)==a[9]&&value.Flags==a[10]&&draws==a[11],"Complete knockback callback differs at "+count);count++;
        }
        Require(count==24576,"Incomplete knockback source comparison");
        Require(NpcCombatEvents.RestoreSubscriptions([81]).Subscriptions().SequenceEqual(new[]{81}),"Knockback subscription restore");
    }
    internal static PlayerMobile Owner()
    {
        var p=new PlayerMobile{Name="NPC knockback owner [fixture]",Body=0x190,Player=true};p.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(p,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(p,99);
        MagePetBuffChecks.Train(p,"necromancer.irongolem",1);MagePetBuffChecks.Train(p,"necromancer.revive",1);
        FixtureSpace.Corridor(p,6280,24,2100+32*Scene++);
        while(p.RawInt<200){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Energy),"Knockback owner Energy");}
        NecromancerEffects.SetMana256(p,p.ManaMax*256);return p;
    }
    internal static IronGolemBody Cast(PlayerMobile p,Item item,string bonus="knock",int expectedBonus=1)
    {
        string issue=null;bool found=false;
        for(int attempt=0;attempt<8&&!found;attempt++)
        {
            FixtureSpace.Corridor(p,6280,24,2100+32*Scene++);
            foreach(var point in new[]{p.Location,new Point3D(p.X+1,p.Y,p.Z),new Point3D(p.X,p.Y+1,p.Z),new Point3D(p.X-1,p.Y,p.Z),new Point3D(p.X,p.Y-1,p.Z)})
            {
                item.MoveToWorld(point,p.Map);
                if(IronGolemCasting.Plan(p,item,out _,out issue)){found=true;break;}
                Require(issue=="That original item is not accessible within casting range.","Original knockback nongeometry refusal: "+issue);
            }
        }
        Require(found,"Original knockback preflight: "+issue+" owner="+p.Location+" item="+item.Location+" visible="+item.Visible+" accessible="+item.IsAccessibleTo(p)+" see="+p.CanSee(item)+" los="+p.InLOS(item));
        ClassCombat.Stop(p);ClassCombat.For(p).NextAction=0;int mana=NecromancerEffects.ManaBalance256(p);
        Require(SkillBindings.Instance.AssignQuick(p,7,CombatAbility.IronGolem)&&SkillBindingNetwork.Use(p,DiabloClass.Necromancer,7,CombatAbility.IronGolem,item.Serial,new(0,0,0)),"Original knockback learned start");
        Require(ClassCombat.ResolveAction(p,ClassCombat.For(p).EffectAt)&&NecromancerEffects.ManaBalance256(p)==mana-35*256,"Original knockback learned release/payment");
        var body=NecromancerGolems.For(p).OfType<IronGolemBody>().Single();Require(item.Parent==body&&NpcEquipmentCombat.EventBonus(body,bonus)==expectedBonus,"Original knockback material identity");return body;
    }
    private static Recipient Contact(PlayerMobile p,BaseCreature source)
    {
        var t=new Recipient();t.SetHits(20000);foreach(var kind in Enum.GetValues<ResistanceType>()){t.SetResistance(kind,0);}t.MoveToWorld(new(source.X+1,source.Y,source.Z),p.Map);t.AIObject?.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);
        var block=SourceBlocking.BlockRoll;var choice=SourceBlocking.ChoiceRoll;
        try
        {
            SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=n=>Math.Min(1,n-1);
            int hp=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);var packet=new PreparedMonsterDamagePacket(256,256,0,0,1);
            foreach(uint raw in new uint[]{0,31,32,63,64,127,128,255})
            {
                var seed=NpcEquipmentRandom.State(source);int draws=0;var result=NpcItemKnockback.Apply(source,t,packet,()=>{draws++;return raw;});var rule=SourceKnockbacks.Rule(t);int chance=rule.Large==true?32:rule.Small==true?128:64;
                Require(draws==1&&result.Success==((raw&127)<chance)&&result.Packet.ResultFlags==(result.Success?9:1)&&t.Location==new Point3D(source.X+1,source.Y,source.Z)&&SourceKnockbacks.Remaining(t)==0&&SkillHealth.Balance256(t)==hp&&NecromancerEffects.ManaBalance256(p)==mana&&NpcEquipmentRandom.State(source)==seed,"Direct callback moved/damaged/billed/rolled outside its supplied packet");
            }
            var rules=IronGolemContact.Require(source,p,t);IronGolemContact.Prepared hit=null;ulong expected=0;bool found=false;
            for(ulong seed=1;seed<10000&&!found;seed++)
            {
                NpcEquipmentRandom.Set(source,seed);hit=IronGolemContact.Prepare(source,t,rules,()=>5);expected=NpcEquipmentRandom.State(source).Seed;uint raw=SourceRandom.Roll(ref expected);var rule=SourceKnockbacks.Rule(t);int chance=rule.Large==true?32:rule.Small==true?128:64;
                found=(hit.Impact.Packet.ResultFlags&1)!=0&&(raw&127)<chance;if(!found){hit.Cancel();}
            }
            Require(found,"No positive ordered knockback contact");
            var resultHit=IronGolemContact.Release(source,p,t,hit,_=>true,TimedEffects.Now);
            Require(t.Calls==1&&!t.EarlyMotion&&SkillHealth.Balance256(t)==hp-hit.Impact.Packet.Total256&&NpcEquipmentRandom.State(source).Seed==expected&&NativeMonsterReturn.Session(t).Packets==1&&NativeMonsterReturn.Session(t).LastMode==13&&SourceKnockbacks.Remaining(t)>0&&NecromancerEffects.ManaBalance256(p)==mana,"Knockback packet failed HP/RNG/return ordering");
            Require((resultHit.Packet.ResultFlags&8)!=0,"Shared return lost callback flag");return t;
        }
        catch{t.Delete();throw;}
        finally{SourceBlocking.BlockRoll=block;SourceBlocking.ChoiceRoll=choice;}
    }
    private static Held Hold(PlayerMobile p,BaseCreature source,Item item,Recipient t,bool revive)
    {
        var all=SourceKnockbacks.Snapshot();var motion=all.Single(x=>x.Target==t.Serial.Value) with{NextUtc=TimedEffects.Now.Add(FixtureSpace.Retention)};
        SourceKnockbacks.Restore(all.Select(x=>x.Target==t.Serial.Value?motion:x).ToArray());t.Frozen=true;t.Combatant=null;
        SkillHealth.SetBalance256(source,SkillHealth.Maximum256(source)-1237);TimedEffects.PreventHealing(source,TimedEffects.Now);source.AIObject?.AITimer.Stop();PersistentMinions.Suspend(p);p.Internalize();
        return new(p.Serial.Value,source.Serial.Value,item.Serial.Value,t.Serial.Value,Json(ClassGear.State(item)),SkillHealth.Balance256(source),SkillHealth.Balance256(t),NpcEquipmentRandom.State(source).Seed,motion,revive);
    }
    private static void Check(NetState net,bool reload)
    {
        Differential();
        if(!reload)
        {
            var rows=new List<Held>();
            foreach(bool graph in new[]{false,true})
            {
                var p=Owner();var item=Original();var source=Cast(p,item);source.SuspendActions();
                if(graph){var proc=NpcItemProcs.Create(source);NpcItemProcs.Refresh(source,NpcCombatEvents.LegacyForSource(source));Require(proc.Capture().Events.Entries.Count(e=>e.Function==7)==2,"Graph missing knockback registrations");}
                rows.Add(Hold(p,source,item,Contact(p,source),false));
            }
            var q=Owner();FixtureNetwork.Authenticate(net,q,"npc-knock-");var revived=NpcFreezeHitChecks.Revive(q,out var gear,"Griswolds Edge","knock",1);rows.Add(Hold(q,revived,gear,Contact(q,revived),true));
            File.WriteAllText(Manifest,Json(new Saved(rows.ToArray(),0)));
        }
        else
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));FixtureNetwork.Authenticate(net,(PlayerMobile)World.FindMobile((Serial)saved.Actors[0].Owner),"npc-knock-reload-");
            foreach(var row in saved.Actors)
            {
                var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var source=(BaseCreature)World.FindMobile((Serial)row.Source);var t=(Recipient)World.FindMobile((Serial)row.Target);var item=World.FindItem((Serial)row.Item);
                Require(source is{Alive:true,Deleted:false}&&source.ControlMaster==p&&SkillHealth.Balance256(source)==row.SourceLife&&SkillHealth.Balance256(t)==row.TargetLife&&NpcEquipmentRandom.State(source).Seed==row.Seed&&Json(ClassGear.State(item))==row.Gear&&SourceKnockbacks.Snapshot().Single(x=>x.Target==row.Target)==row.Motion,"Original knockback source/loot/wounds/seed/motion changed");
                Require(NpcItemKnockback.Bonus(source)==1&&NpcCombatEvents.ForSource(source).Subscriptions().Contains(81),"Loaded original item lost its knockback consumer");
            }
            var first=saved.Actors[0];var victim=(Recipient)World.FindMobile((Serial)first.Target);var held=SourceKnockbacks.Snapshot();var origin=victim.Location;victim.Frozen=false;SourceKnockbacks.Restore([first.Motion]);
            for(int i=0;i<8;i++){SourceKnockbacks.Tick(first.Motion.NextUtc.AddMilliseconds(i*40));}
            Require(victim.Location!=origin&&SkillHealth.Balance256(victim)==first.TargetLife,"Saved knockback failed to move independently or replayed HP");
            victim.MoveToWorld(origin,Map.Maps[first.Motion.Map]);victim.Frozen=true;SourceKnockbacks.Restore(held);
            File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
        }
        Log.Information("NPC item knockback:24576source cases;packet-only low7/size precedence;original learned Iron/graph/frozen Revive ordered HP/return and wounded motion saves pass");
    }
    public static void Configure()
    {
        foreach(string name in new[]{"npc-item-knockback-check","npc-item-knockback-reload-check"})ConsoleInputHandler.RegisterCommand(name,"Original NPC knockback packets and saved motion",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected(name,net=>
        {try{Check(net,name.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}})));
    }
}
