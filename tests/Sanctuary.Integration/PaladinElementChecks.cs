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

public static class PaladinElementChecks
{
    private static readonly ILogger _log=LogFactory.GetLogger(typeof(PaladinElementChecks));
    private sealed record Saved(uint Owner,uint Ally,uint Pet,uint[] Enemies,Point3D Origin,PaladinAuraSave State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"paladin-element-checks.json");
    public static void Configure(){Register("paladin-element-check",Check);Register("paladin-element-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Holy Fire/Shock native pulse, weapon, lifecycle and persistence checks",
        _=>Core.LoopContext.Post(()=>{try{action();_log.Information("{Name}: passed",name);}catch(Exception e){_log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Later(int ms,string name,Action action)=>Timer.StartTimer(TimeSpan.FromMilliseconds(ms),()=>
    {try{action();}catch(Exception e){_log.Error(e,"{Name}: failed",name);}});
    private static SkillValues Golden(CombatAbility ability,int rank,int hard=0)=>new(SkillMechanics.For(ability),
        key=>key==SkillMechanics.For(ability).SourceKey?rank:40,key=>key==SkillMechanics.For(ability).SourceKey?rank:hard,99);
    private static GraveyardSkeleton Enemy(PlayerMobile owner,int dx,int dy=0,int dz=0)
    {
        var target=new GraveyardSkeleton{Frozen=true,CantWalk=true,NoKillAwards=true};target.MoveToWorld(new(owner.X+dx,owner.Y+dy,owner.Z+dz),owner.Map);
        target.AIObject.AITimer.Stop();target.SetHits(20000);Reset(target);Require(ClassCombat.IsHostile(owner,target),"Element fixture hostility");return target;
    }
    private static void Reset(BaseCreature target)
    {
        target.Hits=target.HitsMax;SkillHealth.Clear(target);target.SetResistance(ResistanceType.Physical,0);
        target.SetResistance(ResistanceType.Fire,0);target.SetResistance(ResistanceType.Energy,0);
    }
    private static void Select(PlayerMobile owner,CombatAbility ability)=>Require(SkillBindings.Instance.Assign(owner,false,ability),"Element aura selection");
    private static void GoldenValues(PlayerMobile owner)
    {
        var fire=Golden(CombatAbility.HolyFire,1);var shock=Golden(CombatAbility.HolyShock,1);
        Require(fire.Damage256(false)==256 && fire.Damage256(true)==768 && PaladinElements.WeaponRange(owner,CombatAbility.HolyFire,fire)==(6,18)
            && shock.Damage256(false)==256 && shock.Damage256(true)==2560 && PaladinElements.WeaponRange(owner,CombatAbility.HolyShock,shock)==(1,60),"Rank1 pinned elemental endpoints");
        fire=Golden(CombatAbility.HolyFire,20,20);shock=Golden(CombatAbility.HolyShock,20,20);
        Require(fire.Eval("EDmgSymPerCalc")==620 && fire.Damage256(false)==59904 && fire.Damage256(true)==81100
            && PaladinElements.WeaponRange(owner,CombatAbility.HolyFire,fire)==(1404,1900)
            && shock.Eval("EDmgSymPerCalc")==320 && shock.Damage256(false)==1075 && shock.Damage256(true)==167731
            && PaladinElements.WeaponRange(owner,CombatAbility.HolyShock,shock)==(1,3931),"Rank20 hard synergies or whole weapon-stat truncation");
        Require(PaladinElements.Proximity(1,5)==2 && PaladinElements.Proximity(3,5)==1.5 && PaladinElements.Proximity(5,5)==1,"Declared tile projection endpoints");
        var actor=ClassSkillChecks.Player(DiabloClass.Paladin);actor.AddItem(new Backpack());actor.MoveToWorld(new(owner.X,owner.Y+12,owner.Z),owner.Map);
        Select(actor,CombatAbility.HolyFire);PaladinElements.Pulse(actor,CombatAbility.HolyFire,Golden(CombatAbility.HolyFire,1),TimedEffects.Now);
        actor.Kill();Require(!PaladinElements.WeaponActive(actor) && PaladinAuras.Selected(actor)==CombatAbility.None,"Death retained elemental aura");
        actor.Resurrect();Select(actor,CombatAbility.HolyShock);PaladinElements.Pulse(actor,CombatAbility.HolyShock,Golden(CombatAbility.HolyShock,1),TimedEffects.Now);
        ClassCreation.Profiles.TryGet(actor.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(actor,profile);
        Require(!PaladinElements.WeaponActive(actor) && PaladinAuras.Selected(actor)==CombatAbility.None,"Respec retained elemental aura");actor.Corpse?.Delete();actor.Delete();
    }
    private static void Check()
    {
        var owner=ClassSkillChecks.Player(DiabloClass.Paladin);owner.AddItem(new Backpack());owner.RawStr=owner.RawDex=1000;owner.MoveToWorld(ClassSkillChecks.Corridor(owner,5900,2380),owner.Map);GoldenValues(owner);
        var ally=ClassSkillChecks.Player(DiabloClass.Sorceress);ally.MoveToWorld(new(owner.X,owner.Y+1,owner.Z),owner.Map);
        var pet=Enemy(owner,0,2);Require(pet.SetControlMaster(ally),"Element pet owner");
        BaseCreature[] enemies=[Enemy(owner,1),Enemy(owner,3),Enemy(owner,4),Enemy(owner,-1),Enemy(owner,0,-1,16)];
        enemies[3].SetResistance(ResistanceType.Fire,100);int[] hp=enemies.Select(x=>x.Hits).ToArray();int allyHp=ally.Hits,petHp=pet.Hits;
        var roll=PaladinElements.Roll;PaladinElements.Roll=range=>range/2;Select(owner,CombatAbility.HolyFire);
        Require(!PaladinElements.WeaponActive(owner) && enemies.Select(x=>x.Hits).SequenceEqual(hp),"Non-immediate aura fired on selection");
        Later(2300,"paladin-element-result",()=>
        {
            try
            {
                Require(enemies[0].Hits==hp[0]-5 && SkillHealth.Remainder256(enemies[0])==60 && enemies[1].Hits==hp[1]-2
                    && SkillHealth.Remainder256(enemies[1])==158,"Shared fire roll/proximity/fractional life");
                Require(enemies[2].Hits==hp[2] && enemies[3].Hits==hp[3] && enemies[4].Hits==hp[4] && ally.Hits==allyHp && pet.Hits==petHp
                    && !PaladinElements.WeaponActive(ally) && !PaladinElements.WeaponActive(pet),"Elemental range/height/immunity/friendly or supplier-only filter");
                Require(CreatureHitRecovery.Remaining(enemies[0])==0,"Aura soft hit requested recovery");
                Require(PaladinAuras.Effect(owner,CombatAbility.HolyFire) is{WeaponMin:7,WeaponMax:23},"Fire passive stat endpoints");
                Reset(enemies[0]);enemies[0].SetResistance(ResistanceType.Physical,100);
                Require(GearEffects.Strike(owner,enemies[0],.5,new Random(6),false)==15,"Actual class weapon fire delivery");
                foreach(var enemy in enemies){Reset(enemy);}enemies[3].SetResistance(ResistanceType.Energy,100);
                Select(owner,CombatAbility.HolyShock);Require(PaladinAuras.Effect(owner,CombatAbility.HolyFire)==null && !PaladinElements.WeaponActive(owner),"Aura replacement retained prior weapon damage");
                Later(2300,"paladin-element-result",()=>
                {
                    try
                    {
                        Require(enemies[0].Hits==enemies[0].HitsMax-12 && enemies[1].Hits==enemies[1].HitsMax-6 && enemies[3].Hits==enemies[3].HitsMax
                            && PaladinAuras.Effect(owner,CombatAbility.HolyShock) is{WeaponMin:1,WeaponMax:69},"Natural shock cadence/endpoints/immunity");
                        Delivery(owner,enemies[0]);Seed(owner,ally,pet,enemies);_log.Information("paladin-element-result: passed");
                    }
                    finally{PaladinElements.Roll=roll;}
                });
            }
            catch{PaladinElements.Roll=roll;throw;}
        });
    }
    private static void Delivery(PlayerMobile owner,BaseCreature enemy)
    {
        Reset(enemy);enemy.SetResistance(ResistanceType.Physical,100);owner.Mana=0;
        PaladinElements.Pulse(owner,CombatAbility.HolyShock,Golden(CombatAbility.HolyShock,1),TimedEffects.Now);
        Require(owner.Mana==0 && PaladinAuras.Effect(owner,CombatAbility.HolyShock) is{WeaponMin:1,WeaponMax:60},"Zero-mana aura disabled or charged");
        Reset(enemy);enemy.SetResistance(ResistanceType.Physical,100);
        Require(GearEffects.Strike(owner,enemy,.5,new Random(6),false)==30,"Class weapon lightning delivery");
        Reset(enemy);enemy.SetResistance(ResistanceType.Physical,100);int hp=enemy.Hits;
        AOS.Damage(enemy,owner,100,false,100,0,0,0,0,weaponDamage:true);
        Require(enemy.Hits<hp && enemy.Hits>=hp-60,"Native typed weapon aura delivery or double application");
        Reset(enemy);hp=enemy.Hits;SkillCombat.AuraElement(owner,enemy,5000*256,"fire");
        Require(enemy.Hits==hp-5000 && CreatureHitRecovery.Remaining(enemy)==0,"Large aura soft hit requested ordinary recovery");
        Mastery(owner,enemy);
        PaladinElements.Pulse(owner,CombatAbility.HolyShock,Golden(CombatAbility.HolyShock,1),TimedEffects.Now);
    }
    private static void Mastery(PlayerMobile owner,BaseCreature enemy)
    {
        var definition=NamedGear.Catalog.Items.First(x=>NamedGear.Available(x.Id) && NamedGear.Catalog.Base(x.BaseCode).ItemType=="circ"
            && x.Properties.Any(x=>x.Code=="extra-ltng") && x.Properties.Any(x=>x.Code=="pierce-ltng"));
        var helm=NamedGear.Create(definition.Id,99,new Random(1));Require(owner.EquipItem(helm),"Elemental enhancement fixture helm");
        var bow=new Bow();ClassGear.Profiles.Convert(bow,99,GearRarity.Normal,new Random(1));
        try
        {
            int enhancement=ClassGear.Bonus(owner,"extra-ltng"),pierce=ClassGear.Bonus(owner,"pierce-ltng");
            Require(enhancement>0 && pierce>0,"Fixture lightning enhancement/pierce");
            PaladinElements.Pulse(owner,CombatAbility.HolyShock,Golden(CombatAbility.HolyShock,1),TimedEffects.Now);
            int maximum=(2560*(100+enhancement)/100)*6/256;
            Require(PaladinAuras.Effect(owner,CombatAbility.HolyShock) is{WeaponMin:1} lease && lease.WeaponMax==maximum,"Mastery before passive truncation or minimum-one special case");
            double average=(1+maximum)/2.0;Reset(enemy);enemy.SetResistance(ResistanceType.Physical,100);
            int melee=(int)Math.Floor(average*(100+enhancement)/100*(100+pierce)/100);
            Require(GearEffects.Strike(owner,enemy,.5,new Random(6),false)==melee,"Melee aura second enhancement or pierce order");
            Require(owner.EquipItem(bow),"Elemental bow fixture");Reset(enemy);enemy.SetResistance(ResistanceType.Physical,100);
            int ranged=(int)Math.Floor(average*(100+pierce)/100);
            Require(GearEffects.Strike(owner,enemy,.5,new Random(6),false)==ranged,"Ranged aura incorrectly received melee second enhancement");
        }
        finally{helm.Delete();bow.Delete();}
    }
    private static void Seed(PlayerMobile owner,PlayerMobile ally,BaseCreature pet,BaseCreature[] enemies)
    {
        var state=PaladinAuras.Snapshot();var at=TimedEffects.Now.AddMinutes(5);
        state=state with{Emitters=state.Emitters.Select(x=>x.Owner==owner.Serial.Value?x with{NextUtc=at}:x).ToArray(),
            Leases=state.Leases.Select(x=>x.Source==owner.Serial.Value?x with{UntilUtc=at.AddMilliseconds(40)}:x).ToArray()};
        PaladinAuras.Restore(state);File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,ally.Serial.Value,pet.Serial.Value,enemies.Select(x=>x.Serial.Value).ToArray(),owner.Location,state)));
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var state=PaladinAuras.Snapshot();
        PaladinAuraSave Scope(PaladinAuraSave x)=>new(x.Emitters.Where(x=>x.Owner==saved.Owner).ToArray(),x.Leases.Where(x=>x.Source==saved.Owner).ToArray());
        Require(JsonSerializer.Serialize(Scope(state))==JsonSerializer.Serialize(Scope(saved.State)),"Saved elemental rank/endpoints/source/deadline changed");
        var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);owner.MoveToWorld(saved.Origin,Map.Trammel);
        var ally=(PlayerMobile)World.FindMobile((Serial)saved.Ally);ally.MoveToWorld(new(owner.X,owner.Y+1,owner.Z),owner.Map);ally.ClaimAutoStabledPets();
        var pet=(BaseCreature)World.FindMobile((Serial)saved.Pet);Require(pet.Controlled && pet.ControlMaster==ally,"Element pet claim");
        var enemies=saved.Enemies.Select(x=>(BaseCreature)World.FindMobile((Serial)x)).ToArray();
        enemies[0].MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);Reset(enemies[0]);
        PaladinAuras.Restore(state with{Emitters=state.Emitters.Select(x=>x.Owner==saved.Owner?x with{NextUtc=TimedEffects.Now}:x).ToArray()});
        var roll=PaladinElements.Roll;PaladinElements.Roll=_=>0;
        Later(120,"paladin-element-reload-result",()=>
        {
            try
            {
                Require(PaladinAuras.Effect(owner,CombatAbility.HolyShock) is{WeaponMin:1,WeaponMax:69} lease && lease.UntilUtc<TimedEffects.Now.AddSeconds(3)
                    && enemies[0].Hits==enemies[0].HitsMax-2,"Restored aura failed to recalculate current rank and resume pulse");
                Delivery(owner,enemies[0]);PaladinAuras.Restore(state);_log.Information("paladin-element-reload-result: passed");
            }
            finally{PaladinElements.Roll=roll;}
        });
    }
}
