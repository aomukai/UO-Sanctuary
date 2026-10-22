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

public static class SmiteChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(SmiteChecks));
    private sealed record Saved(uint Owner,uint Enemy,uint Shield,uint Control,Point3D Origin,int Map,int OwnerLife,int EnemyLife,int Mana,CombatStunState State,HolyShieldState Holy,uint NormalItem=0,int NormalDamage=0);
    private sealed class ZeroRandom:Random{public override int Next(int maxValue)=>0;}
    private sealed class NoProcRandom:Random{public override int Next(int maxValue)=>maxValue-1;}
    private static string Manifest=>Path.Join(Core.BaseDirectory,"smite-checks.json");
    public static void Configure()
    {
        EventSink.WorldLoad+=()=>
        {
            if(!File.Exists(Manifest)){return;}var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
            foreach(uint id in new[]{saved.Enemy,saved.Control}){if(World.FindMobile((Serial)id) is BaseCreature actor){Stop(actor);}}
        };
        Register("smite-check",Check);Register("smite-reload-check",Reload);
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Smite shield damage, S1 timing, physical-only effects and stun persistence",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static int Life(Mobile actor)=>actor.Hits*256-SkillHealth.Remainder256(actor);
    private static void Stop(BaseCreature actor){actor.Frozen=actor.CantWalk=true;actor.Combatant=null;actor.AIObject?.AITimer.Stop();}
    private static void Freeze(Mobile actor)
    {
        TimedEffects.Poison(actor,actor,.000001,60000,TimedEffects.Now);
        TimedEffects.Load(TimedEffects.Snapshot().Select(x=>x.Owner==actor.Serial.Value?x with{UpdatedUtc=TimedEffects.Now.AddMinutes(5)}:x).ToArray());
    }
    private static void Ready(PlayerMobile owner){ClassCombat.Stop(owner);var state=ClassCombat.For(owner);state.NextAction=state.RecoveryUntil=Core.TickCount;}
    private static void Mana(PlayerMobile owner){owner.Mana=owner.ManaMax;NecromancerEffects.Profiles.Store(owner,new());}
    private static void Resist(BaseCreature target,int physical)
    {target.SetResistance(ResistanceType.Physical,physical);target.SetResistance(ResistanceType.Fire,0);target.SetResistance(ResistanceType.Cold,0);target.SetResistance(ResistanceType.Energy,0);}
    private static void Cast(PlayerMobile owner,BaseCreature target,int input=0)
    {
        target.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Ready(owner);
        Require(input==0?ClassCombat.ContinueAttack(owner,CombatAbility.Smite,target,target.Location,Core.TickCount)
            :input==1?ClassCombat.ContinueCast(owner,CombatAbility.Smite,target,target.Location,Core.TickCount)
            :ClassCombat.Perform(owner,CombatAbility.Smite,target,target.Location,Core.TickCount),"Smite input rejected");
        ClassCombat.ReleaseCast(owner);Require(ClassCombat.ResolveAction(owner,ClassCombat.For(owner).EffectAt),"Smite action event rejected");
    }
    private static void Controlled(Action action)
    {
        var roll=PaladinSmite.Roll;var effects=PaladinSmite.Effects;
        try{PaladinSmite.Roll=_=>0;PaladinSmite.Effects=new ZeroRandom();action();}
        finally{PaladinSmite.Roll=roll;PaladinSmite.Effects=effects;}
    }
    private static void Probe()
    {
        string root=Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT");
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(root,"tools/reference/smite-engine-evidence.json")));
        foreach(var row in json.RootElement.GetProperty("probe").GetProperty("damageCases").EnumerateArray())
        {
            int holy=row[0].GetInt32();var range=PaladinSmite.Endpoints256(256+holy*768,768+holy*1536,row[1].GetInt32()+row[2].GetInt32(),row[3].GetInt32());
            Require(range.Min+Math.Min(row[4].GetInt32(),range.Max-range.Min-1)==row[7].GetInt32(),"Smite shield endpoints differ from original Do150/ApplyDamageBonuses");
        }
        foreach(var row in json.RootElement.GetProperty("probe").GetProperty("stunCases").EnumerateArray())
        {Require(CombatStuns.Frames(row[6].GetInt32(),row[0].GetInt32()==1,row[1].GetInt32()==1,row[2].GetInt32()==1,row[3].GetInt32(),row[4].GetInt32()==1,row[5].GetInt32())==row[7].GetInt32(),"Stun eligibility/caps differ from original events block");}
        foreach(var row in json.RootElement.GetProperty("probe").GetProperty("timingCases").EnumerateArray())
        {
            var timing=CombatTiming.Calculate(DiabloClass.Paladin,CombatAbility.Smite,new(AttackFamily.Swing,row[1].GetInt32()),row[0].GetInt32(),0,row[3].GetInt32(),row[2].GetInt32());
            Require(timing.Frames==row[5].GetInt32() && timing.EffectFrame==(7*256+row[4].GetInt32()-1)/row[4].GetInt32(),"Smite S1 timing differs from original speed/end-cycle routines");
        }
        foreach(var (rank,damage,stun) in new[]{(1,15,15),(20,300,110),(50,750,250)})
        {var v=new SkillValues(SkillMechanics.For(CombatAbility.Smite),_=>rank,_=>0,99);Require(v.Eval("calc1")==damage && v.Eval("calc2")==stun && v.Mana256==512,"Pinned Smite rank fields");}
    }
    private static Item Named(PlayerMobile owner,string key,string appearance=null,int seed=173)
    {
        var item=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey==key).Id,99,new Random(seed),true,appearance);
        Require(owner.EquipItem(item),"Actual Smite gear "+key);return item;
    }
    private static void Damage(PlayerMobile owner,Ogre enemy,int input=0)
    {
        Resist(enemy,50);int hp=Life(enemy),mana=NecromancerEffects.ManaBalance256(owner);
        int raw=PaladinSmite.DamageRange256(owner,SkillMechanics.Values(owner,CombatAbility.Smite)).Min;
        var effects=PaladinSmite.Effects;try{PaladinSmite.Effects=new NoProcRandom();Cast(owner,enemy,input);}finally{PaladinSmite.Effects=effects;}
        Require(hp-Life(enemy)==raw*50/100 && mana-NecromancerEffects.ManaBalance256(owner)==512,"Smite damage/resistance or event mana changed");
    }
    private static void Gear(PlayerMobile owner,Ogre enemy)
    {
        foreach(string skill in new[]{"paladin.smite","paladin.holyshield"})
        {while(ClassCreation.Profiles.GetRank(owner.Serial,skill)<20){Require(ClassCreation.Profiles.TryTrain(owner.Serial,skill,out _),"Smite/Holy Shield rank20 training");}}
        HolyShields.Cast(owner,SkillMechanics.Values(owner,CombatAbility.HolyShield));
        var plain=PaladinSmite.DamageRange256(owner,SkillMechanics.Values(owner,CombatAbility.Smite));
        var axe=Named(owner,"Hellplague");Require(PaladinSmite.DamageRange256(owner,SkillMechanics.Values(owner,CombatAbility.Smite))==plain,"Weapon local min/max/ED changed shield damage");
        var amulet=Named(owner,"Highlord's Wrath");var armor=Named(owner,"Greyform");
        Require(ClassGear.Bonus(owner,"deadly")>0 && ClassGear.Bonus(owner,"life-steal%")>0 && GearEffects.Damage(owner,0).Lightning>0,
            $"Smite fixture: deadly={ClassGear.Bonus(owner,"deadly")}, leech={ClassGear.Bonus(owner,"life-steal%")}, lightning={GearEffects.Damage(owner,0).Lightning}");
        Require(PaladinSmite.DamageRange256(owner,SkillMechanics.Values(owner,CombatAbility.Smite)).Min/512*ClassGear.Bonus(owner,"life-steal%")/100>0,
            "Leech exclusion fixture would round ordinary healing to zero");
        owner.Hits=owner.HitsMax-20;SkillHealth.Clear(owner);int life=Life(owner);Damage(owner,enemy,1);
        Require(Life(owner)==life,"Smite applied ordinary life steal");
        axe.Delete();amulet.Delete();armor.Delete();
        NormalDamage(owner,enemy);
    }
    private static void NormalDamage(PlayerMobile owner,Ogre enemy)
    {
        // Real source-defined items exercise the property ledger rather than a synthetic affix.
        while(owner.Str<200){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength),"Smite normal-damage weapon requirements");}
        foreach(var (seed,retain) in new[]{(173,false),(0,false),(37,true)})
        {
            var weapon=(BaseWeapon)Named(owner,"Stone Crusher","HammerPick",seed);var state=ClassGear.State(weapon);
            var definition=NamedGear.Catalog.Item(state.NamedItemId);
            var template=definition.Properties.Single(x=>x.Code=="dmg");
            int normal=state.NamedProperties.Single(x=>x.Key=="p/"+template.Slot).Value;
            Require(normal>=template.SourceMin && normal<=template.SourceMax && ClassGear.Bonus(owner,"normal-damage")==normal,"Source item_normaldamage roll was not exposed distinctly");
            var shield=PaladinSmite.Shield(owner);var holy=HolyShields.SmiteBonus(owner);var values=SkillMechanics.Values(owner,CombatAbility.Smite);
            int bonus=owner.Str*shield.StrengthBonus/100+owner.Dex*shield.DexterityBonus/100+(int)values.Eval("calc1");
            var expected=PaladinSmite.Endpoints256(shield.MinDamage*256+holy.Min256,shield.MaxDamage*256+holy.Max256,bonus,normal);
            Require(PaladinSmite.DamageRange256(owner,values)==expected,"Smite did not add normal damage before shield Strength/skill percentages, or included local weapon ED/min/max");
            // Preserve ordinary weapon endpoints: the new distinct stat must not double-count them.
            var basis=ClassGear.Base(weapon);int ed=ClassGear.Bonus(weapon,"dmg%");
            Require(ClassGear.Damage(weapon)==(basis.MinDamage*(100+ed)/100+ClassGear.Bonus(weapon,"dmg-min"),basis.MaxDamage*(100+ed)/100+ClassGear.Bonus(weapon,"dmg-max")),"Normal-damage adapter changed ordinary weapon endpoints");
            var effects=PaladinSmite.Effects;try{PaladinSmite.Effects=new NoProcRandom();Mana(owner);Damage(owner,enemy,2);}finally{PaladinSmite.Effects=effects;}
            int durability=weapon.HitPoints;weapon.HitPoints=0;
            Require(ClassGear.Bonus(owner,"normal-damage")==0,"Broken weapon retained Smite normal damage");weapon.HitPoints=durability;
            owner.AddToBackpack(weapon);Require(ClassGear.Bonus(owner,"normal-damage")==0,"Backpack weapon retained Smite normal damage");
            Require(owner.EquipItem(weapon) && ClassGear.Bonus(owner,"normal-damage")==normal,"Re-equipped weapon lost normal damage");
            _log.Information("Smite normal-damage source roll: seed={Seed}, value={Value}, min256={Minimum}, max256={Maximum}",seed,normal,expected.Min,expected.Max);
            if(!retain){weapon.Delete();}
        }
    }
    private static void Gates(PlayerMobile owner,Ogre enemy,Item shield)
    {
        Require(SkillBindings.Instance.Assign(owner,true,CombatAbility.Smite) && SkillBindings.Instance.Assign(owner,false,CombatAbility.Smite)
            && SkillBindings.Instance.AssignQuick(owner,0,CombatAbility.Smite),"Smite bindings");
        owner.Mana=0;NecromancerEffects.Profiles.Store(owner,new());Ready(owner);
        Require(!ClassCombat.Perform(owner,CombatAbility.Smite,enemy,enemy.Location,Core.TickCount),"Smite started without mana");Mana(owner);
        owner.AddToBackpack(shield);Ready(owner);int mana=NecromancerEffects.ManaBalance256(owner);
        Require(!ClassCombat.Perform(owner,CombatAbility.Smite,enemy,enemy.Location,Core.TickCount) && NecromancerEffects.ManaBalance256(owner)==mana,"Shieldless Smite started or charged mana");
        Require(owner.EquipItem(shield),"Re-equip Smite shield");Ready(owner);
        var location=enemy.Location;enemy.MoveToWorld(new(owner.X+8,owner.Y,owner.Z),owner.Map);
        Require(!ClassCombat.Perform(owner,CombatAbility.Smite,enemy,enemy.Location,Core.TickCount),"Out-of-range Smite started");enemy.MoveToWorld(location,owner.Map);Ready(owner);
        Require(ClassCombat.ContinueAttack(owner,CombatAbility.Smite,enemy,enemy.Location,Core.TickCount),"Smite windup");ClassCombat.ReleaseCast(owner);var state=ClassCombat.For(owner);
        Require(!ClassCombat.ResolveAction(owner,state.EffectAt-1) && NecromancerEffects.ManaBalance256(owner)==mana,"Smite committed before S1 event");
        owner.AddToBackpack(shield);Require(!ClassCombat.ResolveAction(owner,state.EffectAt) && NecromancerEffects.ManaBalance256(owner)==mana,"Missing shield still committed Smite");Require(owner.EquipItem(shield),"Restore Smite shield");
    }
    private static void Save(PlayerMobile owner,Ogre enemy,Item shield,BaseCreature control)
    {
        // This fixture saves the stun. Moving knockback persistence has a separate fixture.
        SourceKnockbacks.Clear(control);
        var held=CombatStuns.Snapshot().Single(x=>x.Target==control.Serial.Value) with{UntilUtc=TimedEffects.Now.AddMinutes(5)};
        CombatStuns.Restore(CombatStuns.Snapshot().Where(x=>x.Target!=held.Target).Append(held).ToArray());
        HolyShields.Cast(owner,SkillMechanics.Values(owner,CombatAbility.HolyShield));
        var holy=HolyShields.State(owner) with{UntilUtc=TimedEffects.Now.AddMinutes(5)};
        HolyShields.Restore(HolyShields.Snapshot().Where(x=>x.Owner!=owner.Serial.Value).Append(holy).ToArray());
        owner.Hits=owner.HitsMax-20;SkillHealth.Clear(owner);SkillHealth.Damage(owner,owner,17);Mana(owner);Stop(enemy);Stop(control);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,enemy.Serial.Value,shield.Serial.Value,control.Serial.Value,owner.Location,owner.Map.MapID,Life(owner),Life(enemy),NecromancerEffects.ManaBalance256(owner),held,HolyShields.State(owner),CombatTiming.Equipped(owner)?.Serial.Value??0,ClassGear.Bonus(owner,"normal-damage"))));
    }
    private static void Check()=>Controlled(()=>
    {
        Probe();var owner=ClassSkillChecks.Player(DiabloClass.Paladin);owner.AddItem(new Backpack());
        for(int i=0;i<50;i++){Require(ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Strength) && ClassCreation.Profiles.TrySpendStat(owner,CharacterStat.Dexterity),"Smite stat allocation");}
        owner.MoveToWorld(ClassSkillChecks.Corridor(owner,5680,1800),owner.Map);Freeze(owner);
        var shield=new Buckler();ClassGear.Profiles.Convert(shield,1,GearRarity.Normal,new Random(173));Require(owner.EquipItem(shield),"Real Buckler equip");
        Require(PaladinSmite.Shield(owner) is{MinDamage:1,MaxDamage:3},"Shield source fell back to projected zero damage");
        var enemy=new Ogre{NoKillAwards=true};enemy.SetHits(30000);enemy.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Stop(enemy);Freeze(enemy);
        Gates(owner,enemy,shield);Damage(owner,enemy);Gear(owner,enemy);HolyShields.Cast(owner,SkillMechanics.Values(owner,CombatAbility.HolyShield));
        var baseRange=PaladinSmite.DamageRange256(owner,SkillMechanics.Values(owner,CombatAbility.Smite));Require(HolyShields.SmiteBonus(owner).Min256>0 && baseRange.Min>256,"Holy Shield physical bonus missing");Damage(owner,enemy,2);
        enemy.MoveToWorld(new(owner.X+5,owner.Y,owner.Z),owner.Map);
        var control=new Skeleton{NoKillAwards=true};control.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);control.AIObject?.AITimer.Stop();Freeze(control);Resist(control,100);control.SetResistance(ResistanceType.Cold,100);
        Require(CombatStuns.Rules(control,out bool boss,out int velocity) && !boss && velocity>0,"Mapped stun fixture lacks source eligibility");
        control.Frozen=control.CantWalk=false;var origin=control.Location;Cast(owner,control);
        int duration=(int)SkillMechanics.Values(owner,CombatAbility.Smite).Eval("calc2")*40;
        Require(CombatStuns.Remaining(control)>0 && CombatStuns.Remaining(control)<=duration && control.Location==origin
            && SourceKnockbacks.Remaining(control)>0,"Immune target did not enter separate stun/timed knockback without instant displacement");
        Require(!control.Move(Direction.East),"Stunned native creature moved");int hp=Life(owner);((BaseWeapon)control.Weapon).OnSwing(control,owner);Require(Life(owner)==hp,"Stunned native creature attacked");
        Timer.StartTimer(TimeSpan.FromMilliseconds(duration+200),()=>Controlled(()=>
        {
            try
            {
                Require(CombatStuns.Remaining(control)==0 && SourceKnockbacks.Remaining(control)==0 && control.Location!=origin,
                    "Smite source knockback did not move/recover or natural stun did not expire");control.Combatant=null;control.AIObject?.AITimer.Stop();
                Require(CombatDisplacement.TryStep(control,Direction.East),"Movement did not resume after stun");
                Cast(owner,control);Save(owner,enemy,shield,control);_log.Information("smite-result: passed");
            }
            catch(Exception e){_log.Error(e,"smite-result: failed");}
        }));
    });
    private static void Reload()=>Controlled(()=>
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);owner.MoveToWorld(saved.Origin,Map.Maps[saved.Map]);Freeze(owner);
        var enemy=(Ogre)World.FindMobile((Serial)saved.Enemy);Stop(enemy);Freeze(enemy);Resist(enemy,50);
        var control=(BaseCreature)World.FindMobile((Serial)saved.Control);Stop(control);Freeze(control);
        Require(Life(owner)==saved.OwnerLife && Life(enemy)==saved.EnemyLife && NecromancerEffects.ManaBalance256(owner)==saved.Mana
            && HolyShields.Shield(owner)?.Serial.Value==saved.Shield && HolyShields.State(owner)==saved.Holy,"Smite resources/shield/Holy Shield persistence");
        Require(CombatStuns.Snapshot().Single(x=>x.Target==saved.Control)==saved.State && CombatStuns.Remaining(control)>0,"Stun deadline did not persist");
        Require(CombatTiming.Equipped(owner)?.Serial.Value==saved.NormalItem && ClassGear.Bonus(owner,"normal-damage")==saved.NormalDamage && saved.NormalDamage>0,"Smite item_normaldamage gear/roll changed over reload");
        control.Frozen=control.CantWalk=false;Require(!control.Move(Direction.East),"Restored stun did not block movement");
        control.Combatant=null;control.AIObject?.AITimer.Stop();Damage(owner,enemy);Save(owner,enemy,World.FindItem((Serial)saved.Shield),control);
    });
}
