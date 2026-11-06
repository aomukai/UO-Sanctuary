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

public static class NpcItemSlowChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcItemSlowChecks));
    private sealed record Held(uint Owner,uint Source,uint Item,uint Target,string Gear,string Numeric,int Life,int SourceLife,int OwnerLife,int OwnerMana,ulong Seed,TimedEffectState Slow,bool Clay);
    private sealed record Saved(Held[] Actors,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-item-slow-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static Item Original(string key)=>NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==key).Id,99,new Random(173),true);
    public sealed class Recipient:Skeleton
    {
        internal bool SlowedAtNotification;
        [Constructible]public Recipient(){Frozen=CantWalk=true;NoKillAwards=true;}
        public Recipient(Serial serial):base(serial){}
        public override bool CanRegenHits=>false;
        public override bool CanRegenMana=>false;
        public override bool CanRegenStam=>false;
        public override void OnDamage(int amount,Mobile from,bool lethal)
        {SlowedAtNotification=TimedEffects.Slow(this,TimedEffects.Now)>0;base.OnDamage(amount,from,lethal);}
        public override void Serialize(IGenericWriter writer){base.Serialize(writer);writer.Write(0);}
        public override void Deserialize(IGenericReader reader){base.Deserialize(reader);reader.ReadInt();}
    }
    private static Recipient Target(PlayerMobile p,BaseCreature source,int physical)
    {
        var t=new Recipient();t.SetHits(20000);t.Hits=t.HitsMax;
        foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,type==ResistanceType.Physical?physical:0);}
        t.MoveToWorld(new(source.X+1,source.Y,source.Z),p.Map);t.AIObject?.AITimer.Stop();return t;
    }
    private static PlayerMobile Owner()
    {
        var p=NpcItemKnockbackChecks.Owner(new NpcKillRecoveryChecks.RecoveryOwner());p.Name="NPC approved slow owner [fixture]";
        MagePetBuffChecks.Train(p,"necromancer.claygolem",1);return p;
    }
    private static void Policy(PlayerMobile p,BaseCreature source)
    {
        var targets=new List<(Mobile Target,int Cap)>{(new Recipient(),90),(new HireFighter(),50),(p,50)};
        foreach(var grade in new[]{EncounterGrade.Ordinary,EncounterGrade.Champion,EncounterGrade.Unique,EncounterGrade.SuperUnique})
        {
            var t=new EncounterCreature();var profile=EncounterProfiles.All.First(r=>r.Archetype==MonsterArchetype.Tank);
            t.Assign(profile,Math.Clamp(60,profile.MinimumLevel,profile.MaximumLevel),MonsterDifficulty.Normal,grade,ChampionKind.Champion,[],EliteAura.None,"slow policy",new Random(173));
            t.AIObject?.AITimer.Stop();targets.Add((t,grade==EncounterGrade.Ordinary?90:50));
        }
        int cases=0;var seed=NpcEquipmentRandom.State(source);int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(p);
        try
        {
            foreach(var (target,cap) in targets)
            {
                if(target!=p){target.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);}
                Require(NpcItemSlow.Cap(target)==cap,"Approved category cap differs");
                foreach(int amount in new[]{0,1,25,49,50,51,75,90,91,127})
                {
                    TimedEffects.Clear(target);var at=TimedEffects.Now;
                    Require(NpcItemSlow.ApplyAmount(source,target,amount,false,at)==(amount>0),"Approved slow zero/positive result differs");
                    if(amount>0)
                    {
                        var state=TimedEffects.Snapshot().Single(s=>s.Owner==target.Serial.Value);
                        Require(state.SlowPercent==Math.Min(amount,cap)&&state.SlowAttackPercent==Math.Min(amount,cap)&&state.SlowUntilUtc==at.AddSeconds(30)
                            &&TimedEffects.Slow(target,at.AddSeconds(30))==0,"Approved amount/duration/expiry differs");
                        Require(GearEffects.MovementSlowTotal(target)==Math.Min(amount,cap)
                            &&Math.Abs(ClassGearHooks.MovementDelay(target,1)-100.0/Math.Max(25,100-Math.Min(amount,cap)))<1e-10,"No-haste native movement delay differs from source25percent floor");
                        Require(NecromancerEffects.Curse(target)==null,"Slow became a curse");
                    }
                    cases++;
                }
            }
            var ordinary=targets[0].Target;TimedEffects.Clear(ordinary);var start=TimedEffects.Now;
            NpcItemSlow.ApplyAmount(source,ordinary,25,false,start);NpcItemSlow.ApplyAmount(source,ordinary,80,false,start.AddMilliseconds(400));
            var refreshed=TimedEffects.Snapshot().Single(s=>s.Owner==ordinary.Serial.Value);
            Require(refreshed.SlowPercent==25&&refreshed.SlowAttackPercent==80&&refreshed.SlowUntilUtc==start.AddMilliseconds(30400),"Reapplication replaced primary velocity or lost attack/expiry refresh");
            Require(TimedEffects.SlowAttack(ordinary,start.AddMilliseconds(30400))==0,"Refreshed attack slow did not expire");
            string before=Json(refreshed with{UpdatedUtc=default});
            foreach(bool ranged in new[]{true,false})
            {bool refused=false;try{NpcItemSlow.ApplyAmount(source,ordinary,ranged?25:-1,ranged,TimedEffects.Now);}catch(NotSupportedException){refused=true;}Require(refused&&Json(TimedEffects.Snapshot().Single(s=>s.Owner==ordinary.Serial.Value) with{UpdatedUtc=default})==before,"Unapproved ranged/negative path changed state");}
            Require(cases==70&&NpcEquipmentRandom.State(source)==seed&&NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(p)==life,"Policy matrix changed resources or RNG");
        }
        finally{foreach(var (target,_) in targets){TimedEffects.Clear(target);if(target!=p){target.Delete();}}}
    }
    private static Held Contact(PlayerMobile p,BaseCreature source,Item item,bool clay)
    {
        var target=Target(p,source,100);int hp=SkillHealth.Balance256(target);int amount=clay?((ClayGolemBody)source).ReactiveSlowPercent:NpcItemSlow.Bonus(source);
        Require(amount>0,"Original/Clay slow amount absent");
        var priorBlock=SourceBlocking.BlockRoll;var priorChoice=SourceBlocking.ChoiceRoll;
        try
        {
            SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=n=>Math.Min(1,n-1);
            var rules=IronGolemContact.Require(source,p,target);
            foreach(int flags in new[]{0,SourceMeleeResult.Block,SourceMeleeResult.Dodge,SourceMeleeResult.Avoid,SourceMeleeResult.WeaponBlock})
            {
                var miss=IronGolemContact.Prepare(source,target,rules,()=>flags);IronGolemContact.Release(source,p,target,miss,_=>true,TimedEffects.Now);
                Require(TimedEffects.Slow(target,TimedEffects.Now)==0&&SkillHealth.Balance256(target)==hp,"Miss/block/avoid/dodge applied slow/damage");
            }
            var hit=IronGolemContact.Prepare(source,target,rules,()=>5);var seed=NpcEquipmentRandom.State(source);var at=TimedEffects.Now;
            var result=IronGolemContact.Release(source,p,target,hit,_=>true,at);
            Require(result.Commit!=null&&result.Packet.Total256==0&&SkillHealth.Balance256(target)==hp&&TimedEffects.Slow(target,at)==amount
                &&TimedEffects.SlowAttack(target,at)==amount&&NpcEquipmentRandom.State(source)==seed,"Fully absorbed contact lost slow or changed HP/RNG");
            TimedEffects.Clear(target);target.SetResistance(ResistanceType.Physical,0);var damaging=IronGolemContact.Prepare(source,target,IronGolemContact.Require(source,p,target),()=>5);
            IronGolemContact.Release(source,p,target,damaging,_=>true,TimedEffects.Now);
            Require(SkillHealth.Balance256(target)<hp&&target.SlowedAtNotification,"Successful slow did not precede HP notification");
            // Serialization hold only, after the real30s duration/expiry controls.
            TimedEffects.Load(TimedEffects.Snapshot().Select(s=>s.Owner==target.Serial.Value?s with{SlowUntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}:s).ToArray());
            SkillHealth.SetBalance256(source,SkillHealth.Maximum256(source)-1237);TimedEffects.PreventHealing(source,TimedEffects.Now);
            PersistentMinions.Suspend(p);p.Internalize();target.Internalize();
            var state=TimedEffects.Snapshot().Single(s=>s.Owner==target.Serial.Value);
            return new(p.Serial.Value,source.Serial.Value,item?.Serial.Value??0,target.Serial.Value,item==null?null:Json(ClassGear.State(item)),item==null?null:Json(NpcItemStatCapture.Capture(item)),SkillHealth.Balance256(target),SkillHealth.Balance256(source),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),NpcEquipmentRandom.State(source).Seed,state,clay);
        }
        finally{SourceBlocking.BlockRoll=priorBlock;SourceBlocking.ChoiceRoll=priorChoice;}
    }
    private static void Check(NetState net,bool reload)
    {
        using(var velocities=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-slow-velocity-cases.json"))))
        {
            int count=0;
            foreach(var row in velocities.RootElement.EnumerateArray())
            {
                var a=row.EnumerateArray().Select(v=>v.GetInt32()).ToArray();int rate=Math.Max(25,a[1]+a[2]);
                Require(a[5]==(a[0]!=0?rate*a[3]/100:0)&&a[6]==(a[0]!=0?rate*a[4]/100:0)&&a[7]==a[0]&&a[8]==a[0]&&a[9]==(a[0]==0?1:0),"Complete movement body differs");count++;
            }
            Require(count==640,"Incomplete movement-floor source matrix");
        }
        Require(NpcCombatEvents.RestoreSubscriptions([150]).Subscriptions().SequenceEqual(new[]{150}),"Slow subscription restore missing");
        if(!reload)
        {
            var rows=new List<Held>();
            foreach(bool graph in new[]{false,true})
            {
                var p=Owner();var item=Original("Razortine");var source=NpcItemKnockbackChecks.Cast(p,item,"slow",25);source.SuspendActions();
                if(graph){NpcItemProcs.Create(source);NpcItemProcs.Refresh(source,NpcCombatEvents.LegacyForSource(source));Require(NpcItemProcs.Get(source).Capture().Events.Entries.Count(e=>e.Function==19)==2,"Slow graph registrations missing");}
                Policy(p,source);rows.Add(Contact(p,source,item,false));
            }
            var q=Owner();FixtureNetwork.Authenticate(net,q,"npc-slow-");var revived=NpcFreezeHitChecks.Revive(q,out var gear,"Kelpie Snare","slow",75);rows.Add(Contact(q,revived,gear,false));
            var owner=Owner();Require(NecromancerSummons.Create(owner,CombatAbility.ClayGolem,Serial.Zero,new(owner.X+1,owner.Y,owner.Z)),"Learned Clay cast");
            var clay=NecromancerGolems.For(owner).OfType<ClayGolemWorldBody>().Single();clay.SuspendActions();
            var paragon=Target(owner,clay,0);paragon.IsParagon=true;
            try
            {
                var seed=NpcEquipmentRandom.State(clay);bool refused=false;
                try{_=NpcItemSlow.Cap(paragon);}catch(NotSupportedException){refused=true;}
                int life=SkillHealth.Balance256(paragon);
                _=SkillCombat.ResolveHitParts(clay,paragon,new(10,0,0,0,0),false,true,enhanceBase:false,addWeaponElements:false);
                Require(refused&&TimedEffects.Slow(paragon,TimedEffects.Now)==0&&SkillHealth.Balance256(paragon)<life&&NpcEquipmentRandom.State(clay)==seed,"Unmapped fallback slow blocked ordinary damage or invented a classification");
            }
            finally{TimedEffects.Clear(paragon);paragon.Delete();}
            rows.Add(Contact(owner,clay,null,true));
            var tyrael=Original("Tyrael's Might");try{Require(NpcItemStatCapture.Capture(tyrael).Properties.Any(s=>s.Stat==152&&s.Value==1),"Saved indestructible property omitted");}finally{tyrael.Delete();}
            File.WriteAllText(Manifest,Json(new Saved(rows.ToArray(),0)));
        }
        else
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));FixtureNetwork.Authenticate(net,(PlayerMobile)World.FindMobile((Serial)saved.Actors[0].Owner),"npc-slow-reload-");
            foreach(var row in saved.Actors)
            {
                var owner=(PlayerMobile)World.FindMobile((Serial)row.Owner);var source=(BaseCreature)World.FindMobile((Serial)row.Source);var target=(BaseCreature)World.FindMobile((Serial)row.Target);var item=World.FindItem((Serial)row.Item);
                var slow=TimedEffects.Snapshot().Single(s=>s.Owner==row.Target);
                Require(source is{Alive:true,Deleted:false}&&source.ControlMaster==owner&&SkillHealth.Balance256(source)==row.SourceLife&&SkillHealth.Balance256(target)==row.Life
                    &&SkillHealth.Balance256(owner)==row.OwnerLife&&NecromancerEffects.ManaBalance256(owner)==row.OwnerMana&&NpcEquipmentRandom.State(source).Seed==row.Seed
                    &&slow.SlowPercent==row.Slow.SlowPercent&&slow.SlowAttackPercent==row.Slow.SlowAttackPercent&&slow.SlowUntilUtc==row.Slow.SlowUntilUtc&&slow.UpdatedUtc>=row.Slow.UpdatedUtc
                    &&(row.Clay||Json(ClassGear.State(item))==row.Gear&&Json(NpcItemStatCapture.Capture(item))==row.Numeric),"Original slow/actor/item/wounds/resources/RNG changed on reload: source="+row.Source+" clay="+row.Clay+" live="+(source is{Alive:true,Deleted:false})+" owner="+(source.ControlMaster==owner)+" sourceLife="+SkillHealth.Balance256(source)+"/"+row.SourceLife+" targetLife="+SkillHealth.Balance256(target)+"/"+row.Life+" ownerLife="+SkillHealth.Balance256(owner)+"/"+row.OwnerLife+" mana="+NecromancerEffects.ManaBalance256(owner)+"/"+row.OwnerMana+" seed="+NpcEquipmentRandom.State(source).Seed+"/"+row.Seed+" slow="+slow.SlowPercent+"/"+row.Slow.SlowPercent+" attack="+slow.SlowAttackPercent+"/"+row.Slow.SlowAttackPercent+" until="+slow.SlowUntilUtc+"/"+row.Slow.SlowUntilUtc);
            }
            File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
        }
        Log.Information("Approved pet slow:70category/amount controls per Iron variant;30s/caps/primary-retention/attack-refresh/expiry;legacy/graph/frozen-Revive/Clay actual absorbed and damaging contacts;original wounds/gear/RNG/slow retained;current D2R callback semantics unverified");
    }
    public static void Configure()
    {
        foreach(string name in new[]{"npc-item-slow-check","npc-item-slow-reload-check"})ConsoleInputHandler.RegisterCommand(name,"Approved shared melee pet slow",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected(name,net=>
        {try{Check(net,name.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}})));
    }
}
