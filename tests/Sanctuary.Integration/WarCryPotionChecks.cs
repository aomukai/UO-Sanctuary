// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class WarCryPotionChecks
{
    private static bool _creating;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(WarCryPotionChecks));
    private sealed record Saved(uint Owner,uint Target,uint Item,string ItemState,ShoutRay Ray,int Life,int Mana,int Map,Point3D Origin,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"warcry-potion-fixtures.json");
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        EventSink.WorldLoad+=WorldLoaded;
        foreach(bool again in new[]{false,true}){bool reload=again;string command=reload?"warcry-potion-reload-check":"warcry-potion-check";
            ConsoleInputHandler.RegisterCommand(command,"T29 War Cry producer capture/live potion and saves",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}else{Check();}Log.Information("{Name}: passed",command);}catch(Exception e){Log.Error(e,"{Name}: failed",command);}}));}
    }
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    [CallPriority(int.MaxValue)] public static void Initialize()
    {var mana=Mobile.ManaRegenRateHandler;Mobile.ManaRegenRateHandler=p=>_creating||p.Name=="WarCryFixturePotion"?TimeSpan.FromHours(1):mana?.Invoke(p)??Mobile.DefaultManaRate;}
    private static void WorldLoaded()
    {
        if(!File.Exists(Manifest)){return;}
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        if(World.FindMobile((Serial)saved.Target)is BaseCreature target){target.Frozen=true;target.CantWalk=true;target.AIObject?.AITimer.Stop();}
    }
    private static PlayerMobile Fresh()
    {
        PlayerMobile p;_creating=true;try{p=ClassSkillChecks.Player(DiabloClass.Barbarian);Require(Mobile.ManaRegenRateHandler(p)==TimeSpan.FromHours(1),"Creation rate wrapper was replaced");}finally{_creating=false;}p.Name="WarCryFixturePotion";p.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"barbarian.warcry",1);
        while(p.RawStr<150){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Potion fixture Strength");}
        FixtureSpace.Corridor(p,6550,20,3760);return p;
    }
    private static GraveyardSkeleton Target(PlayerMobile p)
    {
        var t=new GraveyardSkeleton{Name="WarCryFixturePotionTarget",Frozen=true,CantWalk=true,NoKillAwards=true};t.SetHits(50000);t.Hits=t.HitsMax;t.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);t.AIObject?.AITimer.Stop();
        Zero(t);SourceBlocking.Restore(SourceBlocking.Snapshot().Where(s=>s.Actor!=t.Serial.Value).Append(new SourceShield(t.Serial.Value,CreatureProfiles.Definition(t).Reference,0)).ToArray());return t;
    }
    private static void Zero(BaseCreature t){foreach(var type in Enum.GetValues<ResistanceType>()){t.SetResistance(type,0);}CombatStuns.Clear(t);}
    private static void Smoke()
    {
        var held=BarbarianShout.Snapshot();var shields=SourceBlocking.Snapshot();var p=Fresh();var t=Target(p);
        try
        {
            Require(CombatPotions.Apply(p,3),"War Cry potion application");int mana=NecromancerEffects.ManaBalance256(p);
            Require(BarbarianWarCry.Release(p),"Real War Cry release");var ray=BarbarianShout.Snapshot().Rays.First(r=>r.Owner==p.Serial.Value)with{MinDamage256=25600,MaxDamage256=25600};int life=SkillHealth.Balance256(t);
            Require(BarbarianWarCry.Hit(p,t,ray),"War Cry potion contact");int loss=life-SkillHealth.Balance256(t);
            Require(loss==35840,$"T29 War Cry damage:{loss}/35840FP256");Require(NecromancerEffects.ManaBalance256(p)==mana-SkillMechanics.Values(p,CombatAbility.WarCry).Mana256,"Potion contact replayed release mana");
        }
        finally{BarbarianShout.Restore(held);SourceBlocking.Restore(shields);t.Delete();p.Delete();}
    }
    private static Item Helmet(PlayerMobile p)
    {var item=NamedGear.Create("unique/343",99,new Random(343),true);p.AddToBackpack(item);Require(p.EquipItem(item)&&NamedElements.MasteryPercent(p,"cold")>0,"Original Nightwing capture");return item;}
    private static ShoutRay One(PlayerMobile p,DateTime at)
    {
        var ids=BarbarianShout.Snapshot().Rays.Select(r=>r.Id).ToHashSet();BarbarianShout.Cast(p,SkillMechanics.Values(p,CombatAbility.WarCry),at,CombatAbility.WarCry);
        var rays=BarbarianShout.Snapshot().Rays.Where(r=>r.Owner==p.Serial.Value&&!ids.Contains(r.Id)).ToArray();Require(rays.Length==64&&rays.All(r=>r.Modifiers!=null&&r.Modifiers.Potion==0&&r.Modifiers==rays[0].Modifiers),"One immutable non-potion modifier capture per ring");
        return rays.Single(r=>r.Ray==0)with{MinDamage256=25600,MaxDamage256=25600};
    }
    private static void Hit(PlayerMobile p,BaseCreature t,ShoutRay ray,int expected)
    {
        int before=SkillHealth.Balance256(t),mana=NecromancerEffects.ManaBalance256(p);CombatStuns.Clear(t);Require(BarbarianWarCry.Hit(p,t,ray),$"T29 War Cry accepted contact:ownerMap={p.Map},targetMap={t.Map},LOS={p.InLOS(t)}");
        Require(before-SkillHealth.Balance256(t)==expected&&NecromancerEffects.ManaBalance256(p)==mana&&t.Poison==null&&ColdStates.OtherRate(t)==0,"T29 War Cry damage/payment/no added status");
    }
    private static void Policies()
    {
        var held=BarbarianShout.Snapshot();var shields=SourceBlocking.Snapshot();var block=SourceBlocking.BlockRoll;var p=Fresh();var t=Target(p);
        try
        {
            var helmet=Helmet(p);var shot=One(p,TimedEffects.Now);int mastery=shot.Modifiers.ColdMastery;p.AddToBackpack(helmet);
            Require(NamedElements.MasteryPercent(p,"cold")==0&&CombatPotions.Apply(p,3),"Post-launch potion/unequip");
            int extra=(int)Math.Floor(2560.0*mastery/100);Hit(p,t,shot,35840+extra);
            Zero(t);t.SetResistance(ResistanceType.Physical,100);Hit(p,t,shot,10240+extra);
            Zero(t);t.SetResistance(ResistanceType.Physical,50);t.SetResistance(ResistanceType.Fire,50);t.SetResistance(ResistanceType.Cold,75);t.SetResistance(ResistanceType.Energy,100);t.SetResistance(ResistanceType.Poison,25);
            Hit(p,t,shot,16640+(int)Math.Floor(640.0*mastery/100));
            Zero(t);t.SetResistance(ResistanceType.Poison,100);Hit(p,t,shot,33280+extra);
            Zero(t);var buff=TimedEffects.Buff(p,TimedEffectCategory.Potion);Hit(p,t,shot with{NextUtc=buff.ExpiresUtc.AddMilliseconds(40)},25600);
            Require(CombatPotions.Apply(p,1),"Different potion replacement");Hit(p,t,shot,25600);Require(CombatPotions.Apply(p,3),"Potion restore");
            int before=SkillHealth.Balance256(t);CombatStuns.Clear(t);SourceBlocking.Restore(SourceBlocking.Snapshot().Where(s=>s.Actor!=t.Serial.Value).Append(new SourceShield(t.Serial.Value,CreatureProfiles.Definition(t).Reference,1)).ToArray());SourceBlocking.BlockRoll=_=>0;
            Require(!BarbarianWarCry.Hit(p,t,shot)&&SkillHealth.Balance256(t)==before&&CombatStuns.Remaining(t)==0,"Shield block admitted potion or stun");
            SourceBlocking.Restore(shields);SourceBlocking.BlockRoll=_=>99;Zero(t);Hit(p,t,shot with{MinDamage256=0,MaxDamage256=0},0);
            var legacy=shot with{Modifiers=null};before=SkillHealth.Balance256(t);CombatStuns.Clear(t);
            Require(!BarbarianWarCry.Hit(p,t,legacy)&&SkillHealth.Balance256(t)==before&&CombatStuns.Remaining(t)==0,"Legacy contact invented captured mastery");
            TimedEffects.RemoveBuff(p,TimedEffectCategory.Potion);Hit(p,t,legacy,25600);Require(!Json(legacy).Contains("Modifiers"),"Legacy v0 JSON gained false fields");
            var snapshot=BarbarianShout.Snapshot();string prior=Json(snapshot);
            foreach(var invalid in new[]{shot with{Modifiers=shot.Modifiers with{Potion=10}},shot with{Ability=CombatAbility.Shout}})
            {
                bool refused=false;try{BarbarianShout.Restore(snapshot with{Rays=snapshot.Rays.Select(r=>r.Id==shot.Id?invalid:r).ToArray()});}catch(InvalidDataException){refused=true;}
                Require(refused&&Json(BarbarianShout.Snapshot())==prior,"Invalid optional capture partially restored");
            }
            var legacySave=snapshot with{Rays=snapshot.Rays.Select(r=>r.Id==shot.Id?legacy:r).ToArray()};BarbarianShout.Restore(JsonSerializer.Deserialize<ShoutSave>(Json(legacySave)));
            Require(BarbarianShout.Snapshot().Rays.Single(r=>r.Id==shot.Id).Modifiers==null,"Legacy restore invented modifiers");
            Require(CombatPotions.Apply(p,3),"Landing exclusion potion");Zero(t);before=SkillHealth.Balance256(t);
            Require(BarbarianWarCry.PhysicalHit(p,t,25600,25600,_=>0)&&before-SkillHealth.Balance256(t)==25600,"Shared landing receiver acquired War Cry potion");
        }
        finally{BarbarianShout.Restore(held);SourceBlocking.Restore(shields);SourceBlocking.BlockRoll=block;t.Delete();p.Delete();}
    }
    private static void Check()
    {
        Smoke();Policies();var p=Fresh();var t=Target(p);var helmet=Helmet(p);var at=TimedEffects.Now.Add(FixtureSpace.Retention).Add(FixtureSpace.Retention);var shot=One(p,at);p.AddToBackpack(helmet);
        var pool=BarbarianShout.Snapshot();BarbarianShout.Restore(pool with{Rays=pool.Rays.Where(r=>r.Owner!=p.Serial.Value).Append(shot).ToArray()});
        Require(CombatPotions.Apply(p,3,at),"Held saved potion");SkillHealth.SetBalance256(t,SkillHealth.Maximum256(t)-257);NecromancerEffects.SetMana256(p,NecromancerEffects.ManaBalance256(p)-17);
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,t.Serial.Value,helmet.Serial.Value,Json(ClassGear.State(helmet)),shot,SkillHealth.Balance256(t),NecromancerEffects.ManaBalance256(p),p.Map.MapID,p.Location)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;var t=World.FindMobile((Serial)saved.Target)as BaseCreature;var item=World.FindItem((Serial)saved.Item);
        Log.Information("Potion owner map before resume: {Map}",p.Map);if(p.Map==Map.Internal){p.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);}
        Require(p is{Alive:true,Deleted:false}&&t is{Alive:true,Deleted:false}&&item?.Parent==p.Backpack&&Json(ClassGear.State(item))==saved.ItemState,"Saved potion owner/target/original item");
        var ray=BarbarianShout.Snapshot().Rays.Single(r=>r.Id==saved.Ray.Id);Require(ray==saved.Ray&&ray.Modifiers.ColdMastery>0&&NamedElements.MasteryPercent(p,"cold")==0,"Saved mastery or unequipped state changed");
        Require(SkillHealth.Balance256(t)==saved.Life&&NecromancerEffects.ManaBalance256(p)==saved.Mana,$"Saved balances:life={SkillHealth.Balance256(t)}/{saved.Life};mana={NecromancerEffects.ManaBalance256(p)}/{saved.Mana};maxLife={SkillHealth.Maximum256(t)};maxMana={NecromancerEffects.ManaMaximum256(p)}");Zero(t);SourceBlocking.Restore(SourceBlocking.Snapshot().Where(s=>s.Actor!=t.Serial.Value).Append(new SourceShield(t.Serial.Value,CreatureProfiles.Definition(t).Reference,0)).ToArray());
        int extra=(int)Math.Floor(2560.0*ray.Modifiers.ColdMastery/100);Require(CombatPotions.Apply(p,3,ray.NextUtc),"Reload potion");Hit(p,t,ray,35840+extra);
        File.WriteAllText(Manifest,Json(saved with{Life=SkillHealth.Balance256(t),Generation=saved.Generation+1}));
    }
    internal static void Links(PlayerMobile owner,WarlockDefilerBody defiler,BaseCreature primary,BaseCreature receiver,int percent)
    {
        var pool=BarbarianShout.Snapshot();var p=Fresh();var origin=primary.Location;var received=receiver.Location;
        try
        {
            p.MoveToWorld(new(owner.X+15,owner.Y+2,owner.Z),owner.Map);primary.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);receiver.MoveToWorld(new(p.X+13,p.Y,p.Z),p.Map);Require(CombatPotions.Apply(p,3),"Linked War Cry potion");
            foreach(string mode in new[]{"normal","resisted","physical-immune"})
            {
                Zero(primary);Zero(receiver);if(mode=="physical-immune"){primary.SetResistance(ResistanceType.Physical,100);}
                if(mode=="resisted"){receiver.SetResistance(ResistanceType.Physical,25);receiver.SetResistance(ResistanceType.Fire,50);receiver.SetResistance(ResistanceType.Cold,75);receiver.SetResistance(ResistanceType.Energy,100);}
                var shot=One(p,TimedEffects.Now);var typed=new DirectDamageParts(mode=="physical-immune"?0:25600,0,2560,2560,2560);var share=typed.Fraction(percent);
                int expected=mode=="resisted"?share.Physical256*75/100+share.Fire256/2+share.Cold256/4:checked((int)share.Total);int before=SkillHealth.Balance256(primary),secondary=SkillHealth.Balance256(receiver),events=0;SkillDamageContact observed=default;
                void Observe(SkillDamageContact c){events++;observed=c;}
                SkillDamageEvents.BeforeDirectDamage+=Observe;
                try
                {
                    Require(BarbarianWarCry.Hit(p,primary,shot)&&before-SkillHealth.Balance256(primary)==(mode=="physical-immune"?10240:35840)&&secondary-SkillHealth.Balance256(receiver)==expected
                        &&events==1&&observed.Source==p&&observed.Target==primary&&observed.Parts==typed&&defiler.Prepared.Links.Fault==null,"War Cry potion typed sharing/poison exclusion/independent immunity");
                }
                finally{SkillDamageEvents.BeforeDirectDamage-=Observe;}
            }
        }
        finally
        {
            BarbarianShout.Restore(pool);primary.MoveToWorld(origin,owner.Map);receiver.MoveToWorld(received,owner.Map);Zero(primary);Zero(receiver);p.Delete();
        }
    }
}
