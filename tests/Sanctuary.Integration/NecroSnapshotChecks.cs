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

public static class NecroSnapshotChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NecroSnapshotChecks));
    private static readonly CombatAbility[] Skills=[CombatAbility.RaiseSkeletalMage,CombatAbility.ClayGolem,CombatAbility.BloodGolem,CombatAbility.IronGolem,CombatAbility.FireGolem];
    private sealed record Actor(uint Owner,uint Body,CombatAbility Ability,int Hard,int OwnerHard,int Life,int Maximum,int Rank,long Order,ulong Seed,string Numeric,SummonUpgradeSnapshot Upgrade,uint Item);
    private sealed record Saved(Actor[] Actors,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"necro-snapshot-fixtures.json");
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    public static void Configure(){Register("necro-snapshot-check",Check);Register("necro-snapshot-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Learned Mage/golem born upgrades and original saves",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    // Deliberately construct the optional-table-absent historical profile for
    // retained live-upgrade/packet controls. This is not an older-writer run.
    internal static void MissingMetadata(SkeletonGuardian actor)=>SummonUpgradeSnapshots.Forget(actor);
    private static PlayerMobile Owner(CombatAbility ability,int hard)
    {
        var p=PoisonDaggerChecks.Owner();FixtureSpace.Corridor(p,6400,30,1500);MagePetBuffChecks.Train(p,SkillMechanics.For(ability).TalentId,hard);PoisonDaggerChecks.Named(p,"Wormskull");return p;
    }
    private static void Hold(SkeletonGuardian b)
    {
        var at=TimedEffects.Now.AddDays(3);
        if(b is MageWorldBody mage){mage.Ground.Store(IronGolemWorld.Park(mage.WorldState.Controller)with{NextUtc=at});}
        else if(b is IUnarmedGroundPet ground){ground.Ground.Store(IronGolemWorld.Park(ground.Ground.State.Controller)with{NextUtc=at});}
        else if(b is IronGolemBody iron){iron.RestoreController(IronGolemWorld.Park(iron.Prepared.Controller)with{NextUtc=at});}
        else{throw new InvalidOperationException("Missing held summon controller");}
    }
    private static SkeletonGuardian Cast(PlayerMobile p,CombatAbility ability,int element=1)
    {
        NecromancerEffects.SetMana256(p,p.ManaMax*256);int before=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,ability).Mana256;
        SkeletonGuardian body;
        if(ability==CombatAbility.IronGolem)
        {
            var item=new Broadsword();ClassGear.Profiles.Ensure(item);item.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);
            Require(IronGolemCasting.Release(p,item,TimedEffects.Now,out var iron),"Learned original-item Iron release");body=iron;
        }
        else if(ability==CombatAbility.RaiseSkeletalMage)
        {
            var dead=new GraveyardZombie{NoKillAwards=true};dead.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);dead.Damage(100000,p);if(dead.Alive){dead.Kill();}var corpse=dead.Corpse as Corpse;var roll=NecromancerSummons.ElementRoll;
            try{NecromancerSummons.ElementRoll=()=>element;Require(NecromancerCombat.Release(p,ability,corpse.Serial,corpse.Location),"Learned Mage release");}
            finally{NecromancerSummons.ElementRoll=roll;corpse?.Delete();dead.Delete();}
            body=NecromancerSkeletons.For(p,ability).OfType<MageWorldBody>().Last();
        }
        else
        {
            Require(NecromancerCombat.Release(p,ability,Serial.Zero,new(p.X+2,p.Y,p.Z)),"Learned golem release");body=NecromancerGolems.For(p).Single();
        }
        Require(NecromancerEffects.ManaBalance256(p)==before-cost&&SummonUpgradeSnapshots.State(body)is{} state&&state.Owner==p.Serial.Value&&state.Ability==ability,"Actual once-only payment or birth supplement");Hold(body);return body;
    }
    private static string Numeric(SkeletonGuardian b)
    {
        var weapon=b is NecromancerMinion{IsMage:true}?default:b.SourceWeaponBase;var damage=b.SkillWeaponDamage;
        return Json(new
    {
        b.Rank,b.Mastery,Weapon=new[]{weapon.Min,weapon.Max,weapon.Percent},Damage=new[]{damage.Min,damage.Max},b.CombatProfile,
        b.PhysicalResistance,b.FireResistance,b.ColdResistance,b.EnergyResistance,b.PoisonResistance,
        Magic=NamedElements.MagicResistance(b),Hard=SummonUpgradeSnapshots.State(b).HardRank,
        Copies=b is NecromancerMinion{IsMage:true} m?MageBasicSkills.Copies(m):0,
        Armor=b is NecromancerMinion{IsMage:true} mage?MagePetBuffs.Armor(mage)?.Defense:null,
        Freeze=ItemDurationDefenses.CannotFreeze(b),Wounds=NpcOpenWounds.Inherent(b),Blood=NpcHitSkills.BloodChance(b),
        Strong=IronGolemUpgrades.DamagePercent(b)
    });
    }
    private static void Upgrades(SkeletonGuardian b,CombatAbility ability,int hard)
    {
        Require(SummonUpgradeSnapshots.HardRank(b,ability)==hard,"Born upgrade rank changed");
        if(ability==CombatAbility.RaiseSkeletalMage)
        {
            var mage=(MageWorldBody)b;Require(MageBasicSkills.Copies(mage)==(hard>=5?3:1)&&(MagePetBuffs.Armor(mage)!=null)==(hard>=20&&mage.MageElement==1),"Mage born triple/armor gate");
        }
        else if(ability==CombatAbility.FireGolem){Require(ItemDurationDefenses.CannotFreeze(b)==(hard>=10)&&(b.ColdResistance>=100)==(hard>=20),"Fire born CBF/cold immunity gate");}
        else if(ability==CombatAbility.BloodGolem){Require(NpcOpenWounds.Inherent(b)==(hard>=10?100:0)&&NpcHitSkills.BloodChance(b)==(hard>=20?5:0),"Blood born wound/proc gate");}
        else if(ability==CombatAbility.IronGolem){Require((IronGolemUpgrades.DamagePercent(b)>0)==(hard>=10)&&(NpcSpectralHit.Capture(b,_=>0)!=null)==(hard>=20),"Iron born Extra Strong/Spectral gate");}
        else{Require(ClayGolemUpgrades.HardRank(b)==hard,"Clay born affix gate");}
    }
    private static void ActualEffects(PlayerMobile p,SkeletonGuardian b,CombatAbility ability,int hard)
    {
        if(ability==CombatAbility.RaiseSkeletalMage&&hard==10)
        {
            var mage=(MageWorldBody)b;var target=PoisonDaggerChecks.Target(p);target.SetHits(20000);target.SetResistance(ResistanceType.Cold,0);target.MoveToWorld(new(b.X+4,b.Y,b.Z),b.Map);
            try
            {
                int hp=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(p);NecromancerMageMissiles.SetActorSeed(mage,MageStaticFieldChecks.Seed(0,4,false));
                Require(mage.Launch(p,target,TimedEffects.Now),"Born hard10 Mage launch");var flight=NecromancerMageMissiles.Snapshot().Bolts.Single(f=>f.Actor==b.Serial.Value);
                Require(flight.Packet.Spell==CombatAbility.GlacialSpike&&flight.Packet.Rank==10,"Born Mage fixed extra spell/copy rule");
                for(int i=0;i<40&&SkillHealth.Balance256(target)==hp;i++)
                {var next=NecromancerMageMissiles.Snapshot().Bolts.FirstOrDefault(f=>f.Id==flight.Id);if(next==null){break;}NecromancerMageMissiles.Tick(next.NextUtc,true,flight.Id);}
                Require(SkillHealth.Balance256(target)<hp&&FreezeStates.Shattered(target)&&NecromancerEffects.ManaBalance256(p)==mana,"Born Mage spell failed actual damage/freeze or charged owner mana");
            }
            finally
            {target.Delete();var pool=NecromancerMageMissiles.Snapshot();NecromancerMageMissiles.Restore(pool with{Bolts=pool.Bolts.Where(f=>f.Actor!=b.Serial.Value).ToArray()});}
        }
        if(ability==CombatAbility.BloodGolem&&hard==20)
        {
            var blood=(BloodGolemBody)b;var target=PoisonDaggerChecks.Target(p);target.MoveToWorld(b.Location,b.Map);target.SetResistance(ResistanceType.Physical,0);var block=SourceBlocking.BlockRoll;SourceBlocking.BlockRoll=_=>99;
            try
            {
                var packet=BloodLifeTapChecks.Packet(p,blood,target);BloodLifeTapChecks.Release(p,blood,target,packet);
                Require(NecromancerEffects.Curse(target)is{Ability:CombatAbility.LifeTap,Rank:5} curse&&curse.Source==b.Serial.Value
                    &&TimedEffects.TryActor(target,out var state)&&state.OpenWounds!=null,"Born Blood abilities did not deliver actual curse/wounds after owner respec");
            }
            finally{SourceBlocking.BlockRoll=block;target.Delete();}
        }
        if(ability==CombatAbility.FireGolem&&hard>=10)
        {
            var at=TimedEffects.TickAt(TimedEffects.Now);Require(!GearEffects.ApplyChill(b,at,1600,p)&&!FreezeStates.Apply(p,b,40,at),"Born Fire CBF failed actual cold-state consumers");
            if(hard==20){Require(NamedElements.Damage(p,b,100,"cold",enhance:false)==0,"Born Fire cold immunity failed actual damage consumer");}
        }
    }
    private static void Rollback()
    {
        foreach(var ability in new[]{CombatAbility.ClayGolem,CombatAbility.RaiseSkeletalMage})
        {
            var p=Owner(ability,10);var old=Cast(p,ability);old.MoveToWorld(new(p.X+5,p.Y,p.Z),p.Map);SkillHealth.SetBalance256(old,1111);var state=SummonUpgradeSnapshots.State(old);
            var seed=NpcEquipmentRandom.SeedLow;var element=NecromancerSummons.ElementRoll;GraveyardZombie dead=null;Corpse corpse=null;
            try
            {
                NecromancerEffects.SetMana256(p,p.ManaMax*256);
                if(ability==CombatAbility.RaiseSkeletalMage)
                {
                    dead=new GraveyardZombie{NoKillAwards=true};dead.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);dead.Damage(100000,p);if(dead.Alive){dead.Kill();}corpse=(Corpse)dead.Corpse;
                    NecromancerSummons.ElementRoll=()=>{NecromancerEffects.SetMana256(p,0);return 1;};
                }
                else{NpcEquipmentRandom.SeedLow=()=>{NecromancerEffects.SetMana256(p,0);return 73;};}
                Require(!NecromancerCombat.Release(p,ability,corpse?.Serial??Serial.Zero,corpse?.Location??new(p.X+2,p.Y,p.Z)),"Reentrant mana loss committed summon");
                Require(!old.Deleted&&SkillHealth.Balance256(old)==1111&&SummonUpgradeSnapshots.State(old)==state
                    &&(p.AllFollowers??[]).OfType<SkeletonGuardian>().Count(b=>!b.Deleted)==1&&NecromancerEffects.ManaBalance256(p)==0,"Failed payment replaced/healed old summon or leaked candidate");
                if(corpse!=null){Require(!corpse.Deleted&&!corpse.Animated&&!corpse.SanctuaryConsumed,"Failed Mage payment consumed corpse");}
            }
            finally
            {NpcEquipmentRandom.SeedLow=seed;NecromancerSummons.ElementRoll=element;corpse?.Delete();dead?.Delete();PersistentMinions.Dismiss(p);p.Delete();}
        }
    }
    private static void Check()
    {
        Rollback();
        var saved=new System.Collections.Generic.List<Actor>();
        foreach(var ability in Skills)foreach(int hard in ability==CombatAbility.RaiseSkeletalMage?new[]{4,5,9,10,19,20}:new[]{9,10,20})
        {
            var p=Owner(ability,hard);var b=Cast(p,ability);Upgrades(b,ability,hard);string numeric=Numeric(b);int maximum=SkillHealth.Maximum256(b);SkillHealth.SetBalance256(b,maximum/2+17);TimedEffects.PreventHealing(b,TimedEffects.Now);int life=SkillHealth.Balance256(b);long order=BattleCommand.PetOrder(b);
            MagePetBuffChecks.Train(p,SkillMechanics.For(ability).TalentId,20);MagePetBuffChecks.Train(p,"necromancer.golemmastery",20);
            var helm=p.FindItemOnLayer(Layer.Helm);Require(helm!=null,"Missing real soft-rank helm");p.Backpack.DropItem(helm);ClassGear.Refresh(p);
            Require(Numeric(b)==numeric&&SkillHealth.Balance256(b)==life&&SkillHealth.Maximum256(b)==maximum&&BattleCommand.PetOrder(b)==order,"Owner training/gear recaptured or healed original summon");
            int ownerHard=20;
            if(hard==20)
            {
                ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);MagePetBuffChecks.Train(p,SkillMechanics.For(ability).TalentId,1);ownerHard=1;
                Require(Numeric(b)==numeric&&SkillHealth.Balance256(b)==life,"Respec changed born upgrades/resources");
            }
            ActualEffects(p,b,ability,hard);SkillHealth.SetBalance256(b,life);Upgrades(b,ability,hard);Hold(b);
            uint item=b is IronGolemBody iron?iron.Prepared.Recipe.Item:0;
            var row=new Actor(p.Serial.Value,b.Serial.Value,ability,hard,ownerHard,life,maximum,b.Rank,order,NpcEquipmentRandom.State(b)?.Seed??0,numeric,SummonUpgradeSnapshots.State(b),item);
            PersistentMinions.Suspend(p);p.Internalize();saved.Add(row);
        }
        // Fire Mage's rank20 ally buff also remains available at owner hard1.
        var owner=Owner(CombatAbility.RaiseSkeletalMage,20);var fire=(MageWorldBody)Cast(owner,CombatAbility.RaiseSkeletalMage,2);
        ClassCreation.Profiles.TryGet(owner.Serial,out var spec);ClassCreation.Profiles.ResetAllocatedPoints(owner,spec);MagePetBuffChecks.Train(owner,"necromancer.skeletalmage",1);NecromancerEffects.SetMana256(owner,0);MagePetBuffs.CombatStart(fire,owner);
        Require(SorceressEffects.Enchantment(owner)is{Rank:20,Caster:var caster}&&caster==fire.Serial.Value&&NecromancerEffects.ManaBalance256(owner)==0,"Born rank20 Fire Mage failed free ally Enchant at owner hard1");PersistentMinions.Dismiss(owner);owner.Delete();
        File.WriteAllText(Manifest,Json(new Saved(saved.ToArray())));
        Log.Information("Necromancer snapshots:18actual Mage/golem births;born4/5/9/10/19/20gates,owner training/gear/mastery/respec isolation,actual Blood curse/wounds,Fire cold consumers and Fire Mage Enchant;original actor/resources/item/order snapshots staged");
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2&&saved.Actors.Length==18,"Wrong snapshot cohort/generation");
        foreach(var row in saved.Actors)
        {
            var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var b=(SkeletonGuardian)World.FindMobile((Serial)row.Body);
            Require(b.ControlMaster==p&&SummonUpgradeSnapshots.State(b)==row.Upgrade&&ClassCreation.Profiles.GetRank(p.Serial,SkillMechanics.For(row.Ability).TalentId)==row.OwnerHard
                &&Numeric(b)==row.Numeric&&b.Rank==row.Rank&&SkillHealth.Balance256(b)==row.Life&&SkillHealth.Maximum256(b)==row.Maximum&&BattleCommand.PetOrder(b)==row.Order
                &&(NpcEquipmentRandom.State(b)?.Seed??0)==row.Seed,"Original saved Mage/golem ranks,upgrades,wounds,owner,item stats,order or RNG changed");
            if(row.Item!=0){Require(World.FindItem((Serial)row.Item)?.Parent==b,"Original Iron item lost");}
            Upgrades(b,row.Ability,row.Hard);
        }
        File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
    }
}
