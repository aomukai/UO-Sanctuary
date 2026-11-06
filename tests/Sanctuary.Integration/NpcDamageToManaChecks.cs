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
public static class NpcDamageToManaChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcDamageToManaChecks));
    private sealed record Held(uint Owner,uint Source,uint Item,uint Target,string Gear,int SourceLife,int TargetLife,int OwnerMana,int SourceMana,ulong Seed,NecromancerState Curse,SourceKnockbackState Motion,bool Revive);
    private sealed record Saved(Held[] Actors,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-damage-to-mana-fixtures.json");
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Differential()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-damage-to-mana-cases.json")));int count=0,npc=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var a=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();bool reads=a[0]!=0&&a[1]==0&&a[2]!=0&&a[3]>0;bool pools=reads&&a[4]>0;bool writes=pools&&a[5]<10000;
            int after=writes?Math.Clamp(a[5]+SourcePercentage.Calculate(a[3],a[4],100),0,10000):a[5];
            Require(a[6]==(writes?1:0)&&a[7]==after&&a[8]==(reads?1:0)&&a[9]==(pools?1:0)&&a[10]==(pools?1:0)&&a[11]==(writes?1:0)&&a[12]==(writes?1:0),"Complete damage-to-mana callback/helper differs at "+count);
            if(a[1]!=0){Require(a[6]==0&&a[7]==a[5]&&a.Skip(8).All(v=>v==0),"NPC callback read stats/pools or wrote recovery/overlay");npc++;}count++;
        }
        Require(count==4032&&npc==2688,"Incomplete damage-to-mana source scope");
        Require(NpcCombatEvents.RestoreSubscriptions([114]).Subscriptions().SequenceEqual(new[]{114}),"Player-only event subscription restore");
    }
    public sealed class Recipient:GraveyardSkeleton
    {
        internal int Calls;internal bool FearAtDamage,EarlyMotion;
        [Constructible]public Recipient(){NoKillAwards=true;}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override void OnDamage(int n,Mobile from,bool lethal){Calls++;FearAtDamage=SourceTerror.ItemActive(this);EarlyMotion=SourceKnockbacks.Remaining(this)>0;base.OnDamage(n,from,lethal);}
        public override void Serialize(IGenericWriter w){base.Serialize(w);w.Write(0);}
        public override void Deserialize(IGenericReader r){base.Deserialize(r);r.ReadInt();}
    }
    private static void NoRecovery(PlayerMobile p,BaseCreature source,BaseCreature other)
    {
        var seed=NpcEquipmentRandom.State(source);int mana=NecromancerEffects.ManaBalance256(source),ownerMana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(source),otherLife=SkillHealth.Balance256(other);
        foreach(var phase in new[]{"damagedinmelee","damagedbymissile"})foreach(var packet in new PreparedMonsterDamagePacket[]{null,new(0,0,0,0,1),new(256,256,0,0,1)})
        {
            Require(NpcCombatEvents.ForSource(source).Dispatch(phase,packet,source,other,TimedEffects.Now)==packet,"Player-only event changed incoming packet");
            if(source is SkeletonGuardian g&&NpcItemProcs.Get(g)!=null){NpcItemProcs.Received(phase,other,g,packet??new(0,0,0,0,0),TimedEffects.Now);}
        }
        Require(NpcEquipmentRandom.State(source)==seed&&NecromancerEffects.ManaBalance256(source)==mana&&NecromancerEffects.ManaBalance256(p)==ownerMana&&SkillHealth.Balance256(source)==life&&SkillHealth.Balance256(other)==otherLife,"NPC damage-to-mana recovered owner/pet resources or consumed RNG");
    }
    private static Held Contact(PlayerMobile p,BaseCreature source,Item item,bool revive)
    {
        Require(NpcEquipmentCombat.EventBonus(source,"dmg-to-mana")==35&&NpcEquipmentCombat.EventBonus(source,"howl")==33&&NpcEquipmentCombat.EventBonus(source,"thorns")==3,"Unmodified Howltusk totals changed");
        var t=new Recipient();t.SetHits(20000);foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}t.MoveToWorld(new(source.X+1,source.Y,source.Z),p.Map);t.AIObject?.AITimer.Stop();TimedEffects.PreventHealing(t,TimedEffects.Now);
        NecromancerEffects.SetMana256(p,257);NoRecovery(p,source,t);var block=SourceBlocking.BlockRoll;var choice=SourceBlocking.ChoiceRoll;
        try
        {
            SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=n=>Math.Min(1,n-1);var rules=IronGolemContact.Require(source,p,t);IronGolemContact.Prepared hit=null;ulong expected=0;bool found=false;
            for(ulong seed=1;seed<10000&&!found;seed++)
            {
                NpcEquipmentRandom.Set(source,seed);hit=IronGolemContact.Prepare(source,t,rules,()=>5);expected=NpcEquipmentRandom.State(source).Seed;uint knock=SourceRandom.Roll(ref expected),fear=SourceRandom.Roll(ref expected);var rule=SourceKnockbacks.Rule(t);int chance=rule.Large==true?32:rule.Small==true?128:64;
                found=(hit.Impact.Packet.ResultFlags&1)!=0&&(knock&127)<chance&&(fear&127)<33;if(!found){hit.Cancel();}
            }
            Require(found,"No positive combined Howltusk contact");int hp=SkillHealth.Balance256(t);var result=IronGolemContact.Release(source,p,t,hit,_=>true,TimedEffects.Now);
            Require(t.Calls==1&&t.FearAtDamage&&!t.EarlyMotion&&SkillHealth.Balance256(t)==hp-hit.Impact.Packet.Total256&&NpcEquipmentRandom.State(source).Seed==expected&&(result.Packet.ResultFlags&8)!=0&&SourceKnockbacks.Remaining(t)>0&&NecromancerEffects.ManaBalance256(p)==257,"Howltusk fear/knockback/HP/RNG/mana order changed");
            NoRecovery(p,source,t);
        }
        finally{SourceBlocking.BlockRoll=block;SourceBlocking.ChoiceRoll=choice;}
        var hold=TimedEffects.Now.Add(FixtureSpace.Retention);var curse=NecromancerEffects.Profiles.Find(t);curse=curse with{Curse=curse.Curse with{UntilUtc=hold},ItemTerror=curse.ItemTerror with{NextUtc=hold}};NecromancerEffects.Profiles.Store(t,curse);
        var all=SourceKnockbacks.Snapshot();var motion=all.Single(x=>x.Target==t.Serial.Value)with{NextUtc=hold};SourceKnockbacks.Restore(all.Select(x=>x.Target==t.Serial.Value?motion:x).ToArray());t.Frozen=true;t.Combatant=null;
        SkillHealth.SetBalance256(source,SkillHealth.Maximum256(source)-1237);TimedEffects.PreventHealing(source,TimedEffects.Now);source.AIObject?.AITimer.Stop();PersistentMinions.Suspend(p);p.Internalize();
        return new(p.Serial.Value,source.Serial.Value,item.Serial.Value,t.Serial.Value,Json(ClassGear.State(item)),SkillHealth.Balance256(source),SkillHealth.Balance256(t),NecromancerEffects.ManaBalance256(p),NecromancerEffects.ManaBalance256(source),NpcEquipmentRandom.State(source).Seed,curse,motion,revive);
    }
    private static void Check(NetState net,bool reload)
    {
        Differential();
        if(!reload)
        {
            var rows=new List<Held>();foreach(bool graph in new[]{false,true})
            {
                var p=NpcItemKnockbackChecks.Owner();p.Name="NpcDTMOwner [fixture]";var item=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Howltusk").Id,99,new Random(173),true);var source=NpcItemKnockbackChecks.Cast(p,item);source.SuspendActions();
                if(graph){var procs=NpcItemProcs.Create(source);NpcItemProcs.Refresh(source,NpcCombatEvents.LegacyForSource(source));Require(procs.Capture().Events.Entries.Count(e=>e.Function==13)==2,"Howltusk graph lost player-only registrations");}
                rows.Add(Contact(p,source,item,false));
            }
            var q=NpcItemKnockbackChecks.Owner();q.Name="NpcDTMOwner [fixture]";FixtureNetwork.Authenticate(net,q,"npc-dtm-");var revived=NpcFreezeHitChecks.Revive(q,out var gear,"Howltusk","dmg-to-mana",35);rows.Add(Contact(q,revived,gear,true));File.WriteAllText(Manifest,Json(new Saved(rows.ToArray(),0)));
        }
        else
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));FixtureNetwork.Authenticate(net,(PlayerMobile)World.FindMobile((Serial)saved.Actors[0].Owner),"npc-dtm-reload-");
            foreach(var r in saved.Actors)
            {
                var p=(PlayerMobile)World.FindMobile((Serial)r.Owner);var source=(BaseCreature)World.FindMobile((Serial)r.Source);var t=(Recipient)World.FindMobile((Serial)r.Target);var item=World.FindItem((Serial)r.Item);
                Require(source is{Alive:true,Deleted:false}&&source.ControlMaster==p&&SkillHealth.Balance256(source)==r.SourceLife&&SkillHealth.Balance256(t)==r.TargetLife&&NecromancerEffects.ManaBalance256(source)==r.SourceMana&&NecromancerEffects.ManaBalance256(p)==r.OwnerMana&&NpcEquipmentRandom.State(source).Seed==r.Seed&&Json(ClassGear.State(item))==r.Gear&&Json(NecromancerEffects.Profiles.Find(t))==Json(r.Curse)&&SourceKnockbacks.Snapshot().Single(x=>x.Target==r.Target)==r.Motion,"Original Howltusk/owner/wounds/resources/seed/fear/movement changed after reload");
                Require(NpcCombatEvents.ForSource(source).Subscriptions().Contains(114),"Loaded Howltusk lost player-only event");NoRecovery(p,source,t);
            }
            File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
        }
        Log.Information("NPC damage-to-mana:4032source cases/2688nonplayer guards;unmodified Howltusk learned Iron/graph/frozen Revive;no owner/pet mana recovery;combined fear/knockback and wounded originals survive reload");
    }
    public static void Configure()
    {
        var prior=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name!="NpcDTMOwner [fixture]"&&prior?.Invoke(p)!=false;
        foreach(string name in new[]{"npc-damage-to-mana-check","npc-damage-to-mana-reload-check"})ConsoleInputHandler.RegisterCommand(name,"NPC player-only mana property and complete Howltusk",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected(name,net=>
        {try{Check(net,name.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}})));
    }
}
