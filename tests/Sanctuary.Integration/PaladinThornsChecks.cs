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

public static class PaladinThornsChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(PaladinThornsChecks));
    private sealed record Saved(uint Owner,uint Ally,uint Pet,uint Enemy,Point3D Origin,PaladinAuraSave State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"paladin-thorns-checks.json");
    public static void Configure(){Register("paladin-thorns-check",Check);Register("paladin-thorns-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Thorns attack attempts, prepared physical reflection and native reload checks",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(int ms,string name,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>
    {try{action();}catch(Exception e){_log.Error(e,"{Name}: failed",name);}});
    private static long Life(Mobile actor)=>(long)actor.Hits*256-SkillHealth.Remainder256(actor);
    private static void FreezeRecovery(Mobile actor)=>TimedEffects.Poison(actor,actor,.000001,60000,TimedEffects.Now);
    private static T Place<T>(PlayerMobile owner,T actor,int dx) where T:BaseCreature
    {
        actor.NoKillAwards=actor.CantWalk=true;actor.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);actor.AIObject.AITimer.Stop();
        actor.SetHits(20000);actor.Hits=actor.HitsMax;Zero(actor);FreezeRecovery(actor);return actor;
    }
    private static void Zero(BaseCreature actor)
    {actor.SetResistance(ResistanceType.Physical,0);actor.SetResistance(ResistanceType.Fire,0);actor.SetResistance(ResistanceType.Energy,0);}
    private static void Copies(PaladinAuraSave state,uint owner)
    {
        var current=PaladinAuras.Snapshot();
        PaladinAuras.Restore(new(current.Emitters.Where(x=>x.Owner!=owner).Concat(state.Emitters).ToArray(),
            current.Leases.Where(x=>x.Source!=owner || x.Ability!=CombatAbility.Thorns).Concat(state.Leases).ToArray()));
    }
    private static void Cases(PlayerMobile owner,BaseCreature pet,BaseCreature enemy)
    {
        long before=Life(enemy);int hp=pet.Hits;
        AOS.Damage(pet,enemy,10,false,100,0,0,0,0,weaponDamage:true);
        Require(Life(enemy)==before-29*256 && pet.Hits==hp-10,"Native physical return or duplicate flat damage");
        before=Life(enemy);hp=pet.Hits;
        AOS.Damage(pet,enemy,7,false,50,50,0,0,0,weaponDamage:true);
        Require(Life(enemy)==before-3264 && pet.Hits==hp-7,$"Mixed/fractional prepared physical: return{before-Life(enemy)} incoming{hp-pet.Hits}");
        before=Life(enemy);AOS.Damage(pet,enemy,10,false,0,100,0,0,0,weaponDamage:true);
        Require(Life(enemy)==before-4*256,"Zero-physical weapon hit lost flat damage or reflected elemental damage");
        before=Life(enemy);AOS.Damage(pet,enemy,10,false,100,0,0,0,0,archer:true,weaponDamage:true);
        AOS.Damage(pet,enemy,10,false,100,0,0,0,0,spellDamage:true);
        Require(Life(enemy)==before,"Thorns retaliated against ranged/spell damage");
        before=Life(enemy);
        using(PaladinThorns.Attempt(enemy,pet,false))
        {AOS.Damage(pet,enemy,1,false,0,0,0,0,0,weaponDamage:true);}
        Require(Life(enemy)==before-4*256,"Fully absorbed/zero-damage attack lost attempt damage");
        before=Life(enemy);long petBefore=Life(pet);
        AOS.Damage(pet,enemy,1,false,50,0,0,0,0,weaponDamage:true);
        Require(Life(enemy)==before-1344 && Life(pet)==petBefore-128,
            "Sub-integer prepared physical disappeared before reflection or native incoming fractional damage was discarded");
        before=Life(enemy);using(PaladinThorns.Attempt(enemy,pet,false)){}
        Require(Life(enemy)==before-4*256,"Blocked attempt without a damage packet lost flat damage");
        var oldHit=ClassCombatHooks.ClassHitCheck;var oldPrepared=ClassCombatHooks.PreparedWeaponDamage;
        try
        {
            ClassCombatHooks.ClassHitCheck=(_,_)=>false;enemy.Frozen=false;before=Life(enemy);hp=pet.Hits;
            ((BaseWeapon)enemy.Weapon).OnSwing(enemy,pet);
            Require(Life(enemy)==before-4*256 && pet.Hits==hp,"Actual native missed swing lost attempt damage");
            ClassCombatHooks.ClassHitCheck=(_,_)=>true;int prepared=0,calls=0;
            ClassCombatHooks.PreparedWeaponDamage=(a,d,p,r)=>{prepared=p;calls++;oldPrepared?.Invoke(a,d,p,r);};
            before=Life(enemy);((BaseWeapon)enemy.Weapon).OnSwing(enemy,pet);
            Require(calls==1 && prepared>0 && Life(enemy)==before-1024-(long)prepared*250/100,
                $"Actual native hit/attempt duplicated or returned life loss: calls{calls}, prepared{prepared}, return{before-Life(enemy)}");
        }
        finally{ClassCombatHooks.ClassHitCheck=oldHit;ClassCombatHooks.PreparedWeaponDamage=oldPrepared;enemy.Frozen=true;}
        // A standalone typed weapon hit uses the same two packets without a native OnSwing scope.
        before=Life(enemy);hp=pet.Hits;SkillCombat.HitParts(enemy,pet,new(10,0,0,0,0),true,weapon:true);
        Require(Life(enemy)==before-29*256 && pet.Hits==hp-10,"Class/summon typed weapon return");
        enemy.SetResistance(ResistanceType.Physical,50);before=Life(enemy);
        AOS.Damage(pet,enemy,10,false,100,0,0,0,0,weaponDamage:true);
        Require(Life(enemy)==before-3712,"Each separate return packet lost attacker physical mitigation/fractional damage");
        enemy.SetResistance(ResistanceType.Physical,100);before=Life(enemy);
        AOS.Damage(pet,enemy,10,false,100,0,0,0,0,weaponDamage:true);
        Require(Life(enemy)==before,"Thorns bypassed physical immunity");Zero(enemy);
        // Return damage itself is not another attack attempt, even when both actors have Thorns.
        var all=PaladinAuras.Snapshot();var lease=PaladinAuras.Effect(pet,CombatAbility.Thorns);
        PaladinAuras.ElementalWeapon(lease with{Target=enemy.Serial.Value});
        before=Life(enemy);hp=pet.Hits;AOS.Damage(pet,enemy,10,false,100,0,0,0,0,weaponDamage:true);
        Require(Life(enemy)==before-29*256 && pet.Hits==hp-10,"Recursive Thorns or wrong retaliation ownership");PaladinAuras.Restore(all);
        PaladinAuras.ElementalWeapon(lease with{Ability=CombatAbility.Might,Percent=500});
        before=Life(enemy);AOS.Damage(pet,enemy,10,false,100,0,0,0,0,weaponDamage:true);
        Require(Life(enemy)==before-29*256,"Recipient Might enhanced reflected damage");PaladinAuras.Restore(all);
        PaladinAuras.ElementalWeapon(lease with{Rank=20,Percent=144,DamagePercent=1010});
        before=Life(enemy);AOS.Damage(pet,enemy,10,false,100,0,0,0,0,weaponDamage:true);
        Require(Life(enemy)==before-245*256,"Rank20 flat/percentage consumer");PaladinAuras.Restore(all);
        Require(PaladinThorns.ReturnedPercent(owner,250)==31 && PaladinThorns.ReturnedPercent(owner,1010)==126
            && PaladinThorns.ReturnedPercent(enemy,1010)==1010,"Player/monster percentage reduction");
    }
    private static void Lethal(PlayerMobile owner,BaseCreature pet)
    {
        var poor=Place(owner,new Ogre(),5);var enemy=Place(owner,new Ogre(),6);poor.SetHits(10);poor.Hits=10;enemy.SetHits(100);enemy.Hits=100;
        // Isolated consumer fixture: copy a valid aura lease; do not change recipient eligibility in production.
        PaladinAuras.ElementalWeapon(PaladinAuras.Effect(pet,CombatAbility.Thorns) with{Target=poor.Serial.Value});
        AOS.Damage(poor,enemy,100,false,100,0,0,0,0,weaponDamage:true);
        Require(!poor.Alive && enemy.Alive && enemy.Hits==71,"Copied physical must cap to remaining life10 before return, not return uncapped100 or disappear after death");
        poor.Corpse?.Delete();poor.Delete();enemy.Delete();
        poor=Place(owner,new Ogre(),5);enemy=Place(owner,new Ogre(),6);poor.SetHits(10);poor.Hits=10;enemy.SetHits(20);enemy.Hits=20;
        PaladinAuras.ElementalWeapon(PaladinAuras.Effect(pet,CombatAbility.Thorns) with{Target=poor.Serial.Value});
        AOS.Damage(poor,enemy,100,false,100,0,0,0,0,weaponDamage:true);
        Require(!poor.Alive && !enemy.Alive,"Original lethal attack did not land after capped return killed attacker");
        poor.Corpse?.Delete();enemy.Corpse?.Delete();poor.Delete();enemy.Delete();
    }
    private static void Check()
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Paladin);owner.AddItem(new Backpack());owner.MoveToWorld(ClassSkillChecks.Corridor(owner,5720,3000),owner.Map);FreezeRecovery(owner);
        var ally=ClassSkillChecks.Player(DiabloClass.Sorceress);ally.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);FreezeRecovery(ally);
        var pet=Place(owner,new GraveyardSkeleton(),2);Require(pet.SetControlMaster(owner),"Thorns pet owner");pet.Frozen=true;
        var enemy=Place(owner,new Ogre(),3);
        var one=SkillMechanics.Values(owner,CombatAbility.Thorns,1);var twenty=SkillMechanics.Values(owner,CombatAbility.Thorns,20);
        Require(one.Eval("aurastatcalc1")==4 && one.Eval("aurastatcalc2")==250 && one.Eval("aurarangecalc")==16 && one.Mana256==0
            && twenty.Eval("aurastatcalc1")==144 && twenty.Eval("aurastatcalc2")==1010 && twenty.Eval("aurarangecalc")==54,"Pinned Thorns values");
        owner.Mana=0;Require(!SkillBindings.Instance.Assign(owner,true,CombatAbility.Thorns) && SkillBindings.Instance.Assign(owner,false,CombatAbility.Thorns)
            && ClassAbilities.SelfTarget(CombatAbility.Thorns) && PaladinAuras.Effect(owner,CombatAbility.Thorns)==null,"Thorns slot, free selection or first pulse delay");
        Later(2200,"paladin-thorns-result",()=>
        {
            Require(PaladinAuras.Effect(owner,CombatAbility.Thorns) is{Rank:1,Percent:4,DamagePercent:250}
                && PaladinAuras.Effect(ally,CombatAbility.Thorns)!=null && PaladinAuras.Effect(pet,CombatAbility.Thorns)!=null
                && PaladinAuras.Effect(enemy,CombatAbility.Thorns)==null && !PaladinAuras.ManaSuppressed(owner),"Free delayed self/ally/pet aura or enemy filter");
            Cases(owner,pet,enemy);Lethal(owner,pet);Seed(owner,ally,pet,enemy);_log.Information("paladin-thorns-result: passed");
        });
    }
    private static void Seed(PlayerMobile owner,PlayerMobile ally,BaseCreature pet,BaseCreature enemy)
    {
        var all=PaladinAuras.Snapshot();var at=TimedEffects.Now.AddMinutes(5);
        var saved=new PaladinAuraSave(all.Emitters.Where(x=>x.Owner==owner.Serial.Value).Select(x=>x with{NextUtc=at}).ToArray(),
            all.Leases.Where(x=>x.Source==owner.Serial.Value && x.Ability==CombatAbility.Thorns).Select(x=>x with{UntilUtc=at.AddSeconds(3)}).ToArray());
        Copies(saved,owner.Serial.Value);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,ally.Serial.Value,pet.Serial.Value,enemy.Serial.Value,owner.Location,saved)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var all=PaladinAuras.Snapshot();
        var current=new PaladinAuraSave(all.Emitters.Where(x=>x.Owner==saved.Owner).ToArray(),all.Leases.Where(x=>x.Source==saved.Owner && x.Ability==CombatAbility.Thorns).ToArray());
        Require(JsonSerializer.Serialize(current)==JsonSerializer.Serialize(saved.State),"Saved Thorns source/rank/flat/percentage/leases/deadlines changed");
        var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);owner.MoveToWorld(saved.Origin,Map.Felucca);FreezeRecovery(owner);
        var ally=(PlayerMobile)World.FindMobile((Serial)saved.Ally);ally.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);FreezeRecovery(ally);
        owner.ClaimAutoStabledPets();var pet=(BaseCreature)World.FindMobile((Serial)saved.Pet);Require(pet.ControlMaster==owner,"Thorns pet claim");
        pet.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);Zero(pet);FreezeRecovery(pet);pet.Frozen=pet.CantWalk=true;pet.AIObject.AITimer.Stop();
        var enemy=(BaseCreature)World.FindMobile((Serial)saved.Enemy);enemy.MoveToWorld(new(owner.X+3,owner.Y,owner.Z),owner.Map);Zero(enemy);FreezeRecovery(enemy);enemy.AIObject.AITimer.Stop();
        Cases(owner,pet,enemy);
        Copies(saved.State with{Emitters=saved.State.Emitters.Select(x=>x with{NextUtc=TimedEffects.Now}).ToArray()},saved.Owner);
        Later(120,"paladin-thorns-reload-result",()=>
        {
            Require(PaladinAuras.Effect(pet,CombatAbility.Thorns) is{Percent:4,DamagePercent:250} && PaladinAuras.Snapshot().Emitters.Single(x=>x.Owner==saved.Owner).NextUtc<TimedEffects.Now.AddSeconds(3),"Reloaded Thorns pulse did not resume");
            Copies(saved.State,saved.Owner);_log.Information("paladin-thorns-reload-result: passed");
        });
    }
}
