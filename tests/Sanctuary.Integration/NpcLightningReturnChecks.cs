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

public static class NpcLightningReturnChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcLightningReturnChecks));
    private sealed record Held(uint Owner,uint Source,uint Item,string Gear,int Life,ulong Seed,bool Revive);
    private sealed record Saved(Held[] Actors,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-lightning-return-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public sealed class Recipient:Skeleton
    {
        internal int Notifications;internal BaseCreature Retaliator;internal int RetaliatorLife;internal bool ForceRetarget;
        [Constructible]public Recipient(){Frozen=CantWalk=NoKillAwards=true;}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override bool CanRegenMana=>false;
        public override bool CanRegenStam=>false;
        public override void OnDamage(int amount,Mobile from,bool lethal)
        {Notifications++;Require(Retaliator==null||SkillHealth.Balance256(Retaliator)==RetaliatorLife,"Return committed after the outer incoming HP write");if(ForceRetarget){Retaliator.Combatant=this;}base.OnDamage(amount,from,lethal);}
        public override void Serialize(IGenericWriter w){base.Serialize(w);w.Write(0);}
        public override void Deserialize(IGenericReader r){base.Deserialize(r);r.ReadInt();}
    }
    private static Recipient Target(PlayerMobile p,BaseCreature source,int resistance=0,int hp=20000)
    {
        var t=new Recipient();t.SetHits(hp);foreach(var kind in Enum.GetValues<ResistanceType>()){t.SetResistance(kind,0);}t.SetResistance(ResistanceType.Energy,resistance);
        t.MoveToWorld(source.Location,p.Map);t.AIObject?.AITimer.Stop();t.Retaliator=source;t.RetaliatorLife=SkillHealth.Balance256(source);return t;
    }
    private static void Delete(BaseCreature target){NecromancerEffects.Profiles.Clear(target);TimedEffects.Clear(target);target.Corpse?.Delete();target.Delete();}
    private sealed class Incoming(BaseCreature attacker,BaseCreature defender,DateTime at):PreparedMonsterDamageServices
    {
        internal override void StoreLastAttacker(){}
        internal override PreparedMonsterDamagePacket DealtDamage(PreparedMonsterDamagePacket p)=>p;
        internal override PreparedMonsterDamagePacket TookDamage(PreparedMonsterDamagePacket p)
            =>NpcCombatEvents.ForSource(defender).Dispatch("damagedinmelee",p,defender,attacker,at);
        internal override PreparedMonsterDamagePacket MitigateMissile(PreparedMonsterDamagePacket p)=>p;
        internal override void BeforeHealth(PreparedMonsterDamagePacket p){}
        internal override void ApplyCold(int frames){Require(frames==0,"Unexpected outer cold");}
        internal override void SurvivedDamage(){}
        internal override PreparedMonsterDamagePacket Killed(PreparedMonsterDamagePacket p)=>throw new InvalidOperationException("Unexpected outer death");
        internal override PreparedMonsterDamagePacket Kill(PreparedMonsterDamagePacket p)=>throw new InvalidOperationException("Unexpected outer kill");
        internal override void PresentOverlay(int value)=>throw new InvalidOperationException("Commit has no presentation");
        internal override int BaseHitClass()=>throw new InvalidOperationException("Commit has no hit class");
        internal override PreparedMonsterDamagePacket MeleeAttack(PreparedMonsterDamagePacket p)=>throw new InvalidOperationException("Commit has no attempt");
        internal override PreparedMonsterDamagePacket AttackedInMelee(PreparedMonsterDamagePacket p)=>throw new InvalidOperationException("Commit has no attacked event");
        internal override bool AttackerInActiveMode()=>false;
        internal override void BeforeThorns(PreparedMonsterDamagePacket p)=>throw new InvalidOperationException("Commit has no Thorns");
        internal override void Return(ref PreparedMonsterDamagePacket p,Action death)=>throw new InvalidOperationException("Commit has no return");
        internal override void FreePacket()=>throw new InvalidOperationException("Commit owns no allocated packet");
    }
    private static void Prepared(PlayerMobile owner,BaseCreature source,bool missile=false,bool suppress=false,bool lethal=false)
    {
        var target=Target(owner,source,hp:lethal?1:20000);try
        {
            int sourceLife=SkillHealth.Balance256(source),life=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(owner);var seed=NpcEquipmentRandom.State(source);var at=TimedEffects.Now;
            Require(source.CanBeDamaged(),"Prepared incoming fixture is still protected/parked: "+source.GetType().Name+" blessed="+source.Blessed+" map="+source.Map);
            var packet=new PreparedMonsterDamagePacket(256,256,0,0,suppress?33:1);
            PreparedMonsterDamage.CommitQualified(target,source,packet,new(false,()=>false,()=>0,()=>false),
                _=>throw new InvalidOperationException("Outer zero stun drew RNG"),new Incoming(target,source,at),at,missile:missile);
            bool fires=!missile&&!suppress;int loss=fires?7*256:0;
            Require(SkillHealth.Balance256(target)==Math.Max(0,life-loss)&&target.Notifications==(fires?1:0),"Prepared return did not run exactly once or wrong lethal HP");
            var state=NativeMonsterReturn.Session(target);
            Require(state.Packets==(fires?1:0)&&state.LastHitClass==(fires?0x4D:0)&&state.RecoveryNotifications==(fires&&!lethal?1:0),"Prepared nested return/free/hit class/recovery differed");
            Require(SkillHealth.Balance256(source)==sourceLife-256&&NpcEquipmentRandom.State(source)==seed&&NecromancerEffects.ManaBalance256(owner)==mana&&NecromancerEffects.Curse(target)==null,"Nested lightning became an outgoing weapon/proc,mana or repeated outer hit");
            Require(target.Alive!= (fires&&lethal),"Nested death did not reach actual native return");
        }
        finally{Delete(target);}
    }
    private static void Native(PlayerMobile owner,BaseCreature source,bool ranged=false,bool spell=false)
    {
        var target=Target(owner,source);try
        {
            int hp=SkillHealth.Balance256(target),sourceLife=SkillHealth.Balance256(source);var seed=NpcEquipmentRandom.State(source);int mana=NecromancerEffects.ManaBalance256(owner);
            AOS.Damage(source,target,1,false,100,0,0,0,0,archer:ranged,weaponDamage:!spell,spellDamage:spell);
            bool fires=!ranged&&!spell;
            int physical=fires?PaladinThorns.FlatDamage(source):0;
            Require(SkillHealth.Balance256(target)==hp-(fires?7*256:0)-physical*256&&target.Notifications==(fires?1+(physical>0?1:0):0)&&NativeMonsterReturn.Session(target).Packets==(fires?1:0),"Native weapon return differs: loss="+(hp-SkillHealth.Balance256(target))+" physical="+physical+" notifications="+target.Notifications+" packets="+NativeMonsterReturn.Session(target).Packets);
            Require(SkillHealth.Balance256(source)<=sourceLife&&NpcEquipmentRandom.State(source)==seed&&NecromancerEffects.ManaBalance256(owner)==mana,"Native lightning consumed offensive RNG or owner mana");
        }
        finally{Delete(target);}
    }
    private static void Direct(PlayerMobile owner,BaseCreature source)
    {
        var current=Target(owner,source);var forced=Target(owner,source);try
        {
            source.Combatant=current;Require(source.Combatant==current,"Initial retaliation target fixture refused");forced.ForceRetarget=true;
            Require(NpcLightningReturn.Apply(source,forced,TimedEffects.Now)&&source.Combatant==current&&!NpcItemSpellCasts.KeepsCombatTarget(source),"Retaliation stole ordinary combat target or leaked target scope");
            source.Combatant=forced;Require(source.Combatant==forced,"Retaliation scope suppressed later ordinary targeting");source.Combatant=null;
        }
        finally{Delete(current);Delete(forced);}
        foreach(int resistance in new[]{0,50,100})
        {
            var target=Target(owner,source,resistance);try
            {
                int hp=SkillHealth.Balance256(target);var seed=NpcEquipmentRandom.State(source);int mana=NecromancerEffects.ManaBalance256(owner);
                Require(NpcLightningReturn.Apply(source,target,TimedEffects.Now),"Positive lightning callback refused");
                int expected=7*256*(100-resistance)/100;var state=NativeMonsterReturn.Session(target);
                Require(SkillHealth.Balance256(target)==hp-expected&&target.Notifications==(expected>0?1:0)&&state.Packets==1&&state.RecoveryNotifications==1&&state.LastMode==0&&state.LastHitClass==0x4D,"Typed lightning mitigation/SOFTHIT/zero-loss return differs at "+resistance);
                Require(NpcEquipmentRandom.State(source)==seed&&NecromancerEffects.ManaBalance256(owner)==mana,"Direct lightning drew RNG or owner mana");
            }
            finally{Delete(target);}
        }
        // Successful contact still retaliates with no physical/life loss.
        var zero=Target(owner,source);try
        {
            int hp=SkillHealth.Balance256(zero),original=SkillHealth.Balance256(source);
            PreparedWeaponEvents.BeforeReturn(zero,source,0,false);
            int physical=PaladinThorns.FlatDamage(source);
            Require(SkillHealth.Balance256(zero)==hp-(7+physical)*256&&SkillHealth.Balance256(source)==original&&NativeMonsterReturn.Session(zero).Packets==1,"Zero-physical contact return differs: loss="+(hp-SkillHealth.Balance256(zero))+" physical="+physical+" packets="+NativeMonsterReturn.Session(zero).Packets);
        }
        finally{Delete(zero);}
        var doomed=Target(owner,source,hp:1);try
        {
            var at=TimedEffects.Now;NecromancerEffects.Profiles.Store(doomed,new(Curse:new(CombatAbility.IronMaiden,owner.Serial.Value,1,at.AddMinutes(1),0,0)));
            PreparedWeaponEvents.BeforeReturn(doomed,source,256,false);
            Require(!doomed.Alive&&doomed.Notifications==1&&NativeMonsterReturn.Session(doomed).Packets==0,"Native lightning ran before lethal attacker Iron Maiden event");
        }
        finally{Delete(doomed);}
        int life=SkillHealth.Balance256(source);var random=NpcEquipmentRandom.State(source);int manaBefore=NecromancerEffects.ManaBalance256(owner);bool refused=false;
        try{NpcLightningReturn.NativeIncoming(owner,source,false,TimedEffects.Now);}catch(NotSupportedException){refused=true;}
        Require(refused&&SkillHealth.Balance256(source)==life&&NpcEquipmentRandom.State(source)==random&&NecromancerEffects.ManaBalance256(owner)==manaBefore,"Unsupported player return mutated before refusal");
    }
    private static Held Hold(PlayerMobile p,BaseCreature source,Item item,bool revive)
    {
        Require(NpcEquipmentRandom.State(source)!=null,"Persistence fixture needs an initialized real offensive binding: "+source.GetType().Name);
        SkillHealth.SetBalance256(source,SkillHealth.Maximum256(source)-1237);PersistentMinions.Suspend(p);p.Internalize();
        return new(p.Serial.Value,source.Serial.Value,item.Serial.Value,Json(ClassGear.State(item)),SkillHealth.Balance256(source),NpcEquipmentRandom.State(source).Seed,revive);
    }
    private static void Exercise(PlayerMobile p,BaseCreature source,bool graph)
    {
        Direct(p,source);Prepared(p,source);Prepared(p,source,missile:true);Prepared(p,source,suppress:true);Prepared(p,source,lethal:true);
        Native(p,source);Native(p,source,ranged:true);Native(p,source,spell:true);
        if(graph)
        {
            var g=(SkeletonGuardian)source;NpcItemProcs.Create(g);NpcItemProcs.Refresh(g,NpcCombatEvents.LegacyForSource(g));
            Require(NpcItemProcs.Get(g).Capture().Events.Entries.Count(e=>e.Function==10)==1,"Graph incoming lightning registration missing");
            Prepared(p,source);Prepared(p,source,lethal:true);Native(p,source);
        }
    }
    private static void Check(NetState net,bool reload)
    {
        if(!reload)
        {
            var rows=new List<Held>();var p=NpcFreezeHitChecks.Owner();var item=NamedGear.Create(NamedGear.Catalog.Items.Single(d=>d.SourceKey=="Coif of Glory").Id,99,new Random(173),true);
            var source=NpcFreezeHitChecks.Cast(p,item,"light-thorns",7);Exercise(p,source,true);rows.Add(Hold(p,source,item,false));
            var q=NpcFreezeHitChecks.Owner();FixtureNetwork.Authenticate(net,q,"npc-lightning-");var revive=NpcFreezeHitChecks.Revive(q,out var gear,"Coif of Glory","light-thorns",7);
            // Retaliation correctly makes no offensive RNG call. Initialize a
            // real original-body binding once so reload can test its retention.
            NpcEquipmentRandom.Set(revive,1237);Exercise(q,revive,false);rows.Add(Hold(q,revive,gear,true));
            File.WriteAllText(Manifest,Json(new Saved(rows.ToArray(),0)));
        }
        else
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
            foreach(var row in saved.Actors)
            {
                var p=World.FindMobile((Serial)row.Owner)as PlayerMobile;var source=World.FindMobile((Serial)row.Source)as BaseCreature;var item=World.FindItem((Serial)row.Item);
                Require(source is{Alive:true,Deleted:false}&&source.ControlMaster==p&&SkillHealth.Balance256(source)==row.Life&&NpcEquipmentRandom.State(source).Seed==row.Seed&&Json(ClassGear.State(item))==row.Gear,"Original lightning actor/gear/wounds/RNG changed on reload");
                Require(row.Revive?item.Parent is Corpse:item.Parent==source,"Original equipment loot/body separation changed");
                Require(NpcLightningReturn.Bonus(source)==7&&NpcCombatEvents.ForSource(source).Subscriptions().Contains(128),"Saved lightning subscription missing");
                if(!row.Revive){Require(NpcItemProcs.Get((SkeletonGuardian)source).Capture().Events.Entries.Count(e=>e.Function==10)==1,"Saved graph lightning event duplicated/lost");}
                FixtureNetwork.Authenticate(net,p,"npc-lightning-reload-");FixtureSpace.Corridor(p,6100,24,2800);PersistentMinions.Resume(p);
                Require(source.Map==p.Map&&source.CanBeDamaged()&&SkillHealth.Balance256(source)==row.Life,"Actual minion resume differs: type="+source.GetType().Name+" sourceMap="+source.Map+" ownerMap="+p.Map+" blessed="+source.Blessed+" life="+SkillHealth.Balance256(source)+" expected="+row.Life+" parked="+Json(PersistentMinions.Profiles.Find(source))+" location="+source.Location);
                Direct(p,source);Prepared(p,source);Native(p,source);
                SkillHealth.SetBalance256(source,row.Life);PersistentMinions.Suspend(p);p.Internalize();
            }
            File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
        }
        Log.Information("NPC lightning return:learned original Coif Iron/frozen Revive,incoming legacy/graph/native AOS,zero physical,resistance/immunity,lethal SOFTHIT,nonrecursive RNG-free typed HP,missile/spell/suppression and original wounded saves");
    }
    public static void Configure()
    {
        foreach(string name in new[]{"npc-lightning-return-check","npc-lightning-return-reload-check"})ConsoleInputHandler.RegisterCommand(name,"NPC incoming lightning item return",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected(name,net=>
        {try{Check(net,name.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}})));
    }
}
