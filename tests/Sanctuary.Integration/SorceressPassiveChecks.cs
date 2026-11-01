// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class SorceressPassiveChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(SorceressPassiveChecks));
    private static readonly CombatAbility[] Abilities=[CombatAbility.Warmth,CombatAbility.FireMastery,CombatAbility.LightningMastery,CombatAbility.ColdMastery];
    private sealed record Saved(uint Owner,uint Target,uint Necro,uint Orb,uint Helm,int Map,Point3D Point,int Mana,int Life,int NativeColdBase,int ColdResistance,int[] Ranks,int[] Values,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"sorceress-passives-fixtures.json");
    public static void Configure()
    {
        var old=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name!="SorcPassiveSaved"&&old?.Invoke(p)!=false;
        Register("sorceress-passives-check",Check);Register("sorceress-passives-reload-check",()=>FixtureNetwork.Connected("sorceress-passives-reload-result",Reload));
    }
    private static void Register(string key,Action action)=>ConsoleInputHandler.RegisterCommand(key,"Four Sorceress passives,broken cold immunity,T09stacking and saved profiles",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Key}: passed",key);}catch(Exception e){Log.Error(e,"{Key}: failed",key);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,CombatAbility a,int rank)
    {while(ClassCreation.Profiles.GetRank(p.Serial,SkillMechanics.For(a).TalentId)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,SkillMechanics.For(a).TalentId,out _),"Train "+a);}}
    private static PlayerMobile Owner(int rank=1)
    {var p=FireBoltChecks.Owner(CombatAbility.FireBolt,6510);p.Name="SorcPassiveFixture";foreach(var a in Abilities){Train(p,a,rank);}return p;}
    private static GraveyardSkeleton Target(PlayerMobile p,int basis=0)
    {var t=FireBoltChecks.Target(p,3);t.SetResistance(ResistanceType.Cold,basis);return t;}
    private static int[] Ranks(PlayerMobile p)=>Abilities.Select(a=>SkillMechanics.Rank(p,a)).ToArray();
    private static int[] Values(PlayerMobile p)=>Abilities.Select(a=>SorceressCombat.Passive(p,a)).ToArray();
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"sorceress-passive-rules.json")));int count=0;
        foreach(var r in doc.RootElement.GetProperty("passives").EnumerateArray())
        {
            int id=r.GetProperty("sourceId").GetInt32();var row=SkillMechanics.All.Single(x=>x.SourceId==id);int rank=0;
            foreach(var expected in r.GetProperty("rankValues").EnumerateArray())
            {rank++;var values=new SkillValues(row,k=>k==row.SourceKey?rank:200,_=>20,99);Require(values.Eval("passivecalc1")==expected.GetInt32(),"Pinned independent passive value "+id+"/"+rank);count++;}
            Require(rank==255,"Incomplete passive rank domain");
        }
        Require(count==1020,"Missing passive goldens");
    }
    private static void Check()
    {
        Source();RanksAndGear();MasteryCapture();ColdImmunities();Recovery();Lifecycle();Save();
        Log.Information("Sorceress passives:1020independent rank values,real learned/absent/gear/mastery capture,180curse/Conviction/cold-immunity contexts,item-pierce separation,T09Warmth/gear/Meditation/material stacking,lifecycle and controlled saves pass");
    }
    private static void RanksAndGear()
    {
        var p=FireBoltChecks.Owner(CombatAbility.FireBolt,6510);try
        {
            var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");var helm=PoisonDaggerChecks.Named(p,"Nightwing's Veil");
            Require(Abilities.All(a=>SkillMechanics.Rank(p,a)==0&&SorceressCombat.Passive(p,a)==0),"General gear granted unlearned passives");
            foreach(var a in Abilities)
            {Train(p,a,1);int rank=SkillMechanics.Rank(p,a);var row=SkillMechanics.For(a);Require(rank>1&&SorceressCombat.Passive(p,a)==row.Parameter(1)+(rank-1)*row.Parameter(2)
                &&!ClassCombat.Available(p,a)&&!SkillBindings.Instance.Assign(p,false,a)&&!SkillBindings.Instance.AssignQuick(p,7,a),"Passive rank/active binding policy");}
            p.Backpack.DropItem(orb);p.Backpack.DropItem(helm);ClassGear.Refresh(p);Require(Values(p).SequenceEqual(new[]{30,30,50,20}),"Actual gear removal/passive base values");
            foreach(var a in Abilities){Train(p,a,20);}Require(Values(p).SequenceEqual(new[]{258,163,278,115}),"Rank20 learned passive values");
        }finally{FireBoltChecks.Clean(p);}
    }
    private static void MasteryCapture()
    {
        var p=Owner();var t=Target(p);try
        {
            var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");var values=SkillMechanics.Values(p,CombatAbility.FireBolt);var mods=DruidFirestorm.Modifiers(p);
            var fire=SorceressSpellMissiles.Prepare(p,t.Location,values,TimedEffects.Now);long id=SorceressSpellMissiles.Commit(fire);
            Require(fire.Minimum256==values.Damage256(false)*(100L+mods.FireMastery)/100&&fire.Maximum256==values.Damage256(true)*(100L+mods.FireMastery)/100,"Fire mastery/item addition at capture");
            p.Backpack.DropItem(orb);ClassGear.Refresh(p);Train(p,CombatAbility.FireMastery,20);Require(FireBoltChecks.State(id)==fire,"Positive mastery reread in flight");
            int life=SkillHealth.Balance256(t);IceBoltChecks.Finish(id);int loss=life-SkillHealth.Balance256(t);Require(loss>=fire.Minimum256&&loss<fire.Maximum256,"Positive mastery double enhancement at impact");
            var lightning=new SkillValues(SkillMechanics.For(CombatAbility.Nova),k=>k=="Nova"?1:0,_=>0,99);
            var ends=DruidFirestorm.Endpoints(lightning,NamedElements.MasteryPercent(p,"ltng"),SlowMissilePayload.Normal);
            Require(ends.Min==384&&ends.Max==7680,"Lightning mastery endpoint supplier");
            Require(ClassCreation.Profiles.TryTrain(p.Serial,"sorceress.icebolt",out _),"Learn cold capture control");t.SetResistance(ResistanceType.Cold,80);
            var cold=SorceressSpellMissiles.Prepare(p,t.Location,SkillMechanics.Values(p,CombatAbility.IceBolt),TimedEffects.Now) with{Minimum256=25600,Maximum256=25600};id=SorceressSpellMissiles.Commit(cold);
            Train(p,CombatAbility.ColdMastery,20);life=SkillHealth.Balance256(t);IceBoltChecks.Finish(id);
            Require(life-SkillHealth.Balance256(t)==34560,"Cold Mastery did not remain a live receiver reduction or became positive enhancement");
        }finally{FireBoltChecks.Clean(p,t);}
    }
    private static void ColdImmunities()
    {
        var p=Owner();var necro=ClassSkillChecks.Player(DiabloClass.Necromancer);var paladin=ClassSkillChecks.Player(DiabloClass.Paladin);var t=Target(p);var original=PaladinConviction.Snapshot();int contexts=0;
        try
        {
            necro.MoveToWorld(new(p.X,p.Y+2,p.Z),p.Map);paladin.MoveToWorld(new(p.X,p.Y+3,p.Z),p.Map);Train(necro,CombatAbility.LowerResist,20);Train(paladin,CombatAbility.Conviction,20);
            foreach(int coldRank in new[]{1,20})
            {
                Train(p,CombatAbility.ColdMastery,coldRank);int mastery=coldRank==1?20:115;
                foreach(int basis in new[]{0,50,99,100,105,110,115,125,150,200})foreach(int curse in new[]{0,1,20})foreach(int conviction in new[]{0,1,20})
                {
                    NecromancerEffects.ClearOrdinaryCurse(t);PaladinConviction.Restore(original);t.SetResistance(ResistanceType.Cold,basis);
                    if(curse>0){Require(NecromancerEffects.Apply(necro,t,CombatAbility.LowerResist,curse),"Actual learned Lower Resist");}
                    if(conviction>0){Require(PaladinConviction.Apply(paladin,t,SkillMechanics.Values(paladin,CombatAbility.Conviction,conviction),TimedEffects.Now.AddHours(1)),"Actual Conviction supplier");}
                    int lower=curse==0?0:curse==1?31:62,conv=conviction==0?0:conviction==1?30:125;
                    int reduced=basis-(basis>=100?lower/5:lower)-(basis>=100?conv/5:conv);
                    int expected=Math.Clamp(reduced-(reduced>=100?0:basis>=100?mastery/5:mastery),-100,200);
                    int actual=NamedElements.Resistance(p,t,"cold",t.ColdResistance);Require(actual==expected,"Cold immunity/current mastery: "+coldRank+"/"+basis+"/"+curse+"/"+conviction+" actual="+actual+" expected="+expected);
                    int withPierce=NamedElements.Resistance(p,t,"cold",t.ColdResistance,10);
                    Require(withPierce==(actual>=100?actual:Math.Max(-100,actual-10)),"Item pierce was scaled with Cold Mastery or broke immunity");contexts++;
                }
            }
            Require(contexts==180,"Missing current cold-immunity matrix");
            PaladinConviction.Restore(original);NecromancerEffects.ClearOrdinaryCurse(t);t.SetResistance(ResistanceType.Cold,110);
            Require(NecromancerEffects.Apply(necro,t,CombatAbility.LowerResist,20)&&NamedElements.Resistance(p,t,"cold",110)==75,"Broken110/LowerResist20/ColdMastery20 example");
            int life=SkillHealth.Balance256(t);SkillCombat.Hit(p,t,100,"cold",recover:false);Require(life-SkillHealth.Balance256(t)==6400,"Actual shared cold damage skipped one-fifth mastery");
            t.SetResistance(ResistanceType.Cold,100);NecromancerEffects.ClearOrdinaryCurse(t);life=SkillHealth.Balance256(t);SkillCombat.Hit(p,t,100,"cold",recover:false);Require(life==SkillHealth.Balance256(t),"Cold Mastery itself broke immunity");
        }finally{PaladinConviction.Restore(original);necro.Delete();paladin.Delete();FireBoltChecks.Clean(p,t);}
    }
    private static void Recovery()
    {
        var p=Owner();try
        {
            var baseline=TimeSpan.FromSeconds(2);Require(Math.Abs(GearEffects.RegenerationRate(p,baseline,"mana-per-five").TotalSeconds-2/1.3)<.000001,"Approved T09Warmth30 recovery");
            var weapon=new Dagger{Resource=CraftResource.Verite};var state=ClassGear.Profiles.Convert(weapon,99,GearRarity.Normal,new Random(1));ClassGear.Profiles.Store(weapon,state with{CraftedEquipment=true,MaterialRolls=[new("mana-per-five",1)]});Require(p.EquipItem(weapon),"Real material recovery item");
            var armor=PoisonDaggerChecks.Named(p,"Magefist"); // Actual fire-skill and mana-recovery modifiers.
            int gear=ClassGear.Bonus(p,"mana-regen%"),warm=SorceressCombat.Passive(p,CombatAbility.Warmth),material=ClassGear.Material(p,"mana-per-five");
            var old=PaladinAuras.Snapshot();try
            {
                var lease=new PaladinAuraLease(p.Serial.Value,p.Serial.Value,CombatAbility.Meditation,1,300,TimedEffects.Now.AddHours(1));
                PaladinAuras.Restore(old with{Leases=old.Leases.Where(x=>x.Target!=p.Serial.Value||x.Ability!=CombatAbility.Meditation).Append(lease).ToArray()});
                double expected=1/(.5*(100+gear+warm+300)/100.0+material/5.0);
                Require(material==1&&gear>0&&Math.Abs(GearEffects.RegenerationRate(p,baseline,"mana-per-five").TotalSeconds-expected)<.000001,"Warmth/gear/Meditation addition or independent material");
            }finally{PaladinAuras.Restore(old);}
            p.Backpack.DropItem(armor);p.Backpack.DropItem(weapon);ClassGear.Refresh(p);Train(p,CombatAbility.Warmth,20);
            Require(Math.Abs(GearEffects.RegenerationRate(p,baseline,"mana-per-five").TotalSeconds-2/3.58)<.000001,"Warmth20 retained multiplicative/gear residue");
        }finally{FireBoltChecks.Clean(p);}
    }
    private static void Lifecycle()
    {
        var p=Owner();try
        {
            ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(Values(p).All(x=>x==0),"Respec retained learned passive bonuses");
            foreach(var a in Abilities){Train(p,a,1);}Require(WardrobeTransaction.ChangeClass(p,DiabloClass.Paladin,out _,out _)&&Values(p).All(x=>x==0),"Class switch retained Sorceress passive bonuses");
        }finally{FireBoltChecks.Clean(p);}
    }
    private static void Save()
    {
        var p=Owner(20);var orb=PoisonDaggerChecks.Named(p,"Eschuta's temper");var helm=PoisonDaggerChecks.Named(p,"Nightwing's Veil");p.Name="SorcPassiveSaved";
        var necro=ClassSkillChecks.Player(DiabloClass.Necromancer);necro.MoveToWorld(new(p.X,p.Y+2,p.Z),p.Map);Train(necro,CombatAbility.LowerResist,20);
        var target=Target(p,110);Require(NecromancerEffects.Apply(necro,target,CombatAbility.LowerResist,20),"Saved broken immunity");
        int resistance=NamedElements.Resistance(p,target,"cold",target.ColdResistance);SkillCombat.Hit(p,target,100,"cold",recover:false);
        var saved=new Saved(p.Serial.Value,target.Serial.Value,necro.Serial.Value,orb.Serial.Value,helm.Serial.Value,p.Map.MapID,p.Location,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(target),target.ColdResistance,resistance,Ranks(p),Values(p),0);
        // Hold the source curse for transport; ordinary expiry has separate controls.
        var curse=NecromancerEffects.Profiles.Find(target);NecromancerEffects.Profiles.Store(target,curse with{Curse=curse.Curse with{UntilUtc=TimedEffects.Now.Add(FixtureSpace.Retention)}});
        p.Internalize();necro.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var target=World.FindMobile((Serial)s.Target);
        Log.Information("Passive reload:ranks={Ranks} expected={Expected} values={Values} expectedValues={ExpectedValues} life={Life}/{ExpectedLife} mana={Mana}/{ExpectedMana} orb={Orb} helm={Helm} baseCold={BaseCold} cold={Cold}/{ExpectedCold} curse={Curse}",string.Join(",",Ranks(p)),string.Join(",",s.Ranks),string.Join(",",Values(p)),string.Join(",",s.Values),SkillHealth.Balance256(target),s.Life,NecromancerEffects.ManaBalance256(p),s.Mana,World.FindItem((Serial)s.Orb).Parent==p,World.FindItem((Serial)s.Helm).Parent==p,target.ColdResistance,NamedElements.Resistance(p,target,"cold",target.ColdResistance),s.ColdResistance,NecromancerEffects.Curse(target)?.Ability);
        Require(s.Generation<2&&Ranks(p).SequenceEqual(s.Ranks)&&Values(p).SequenceEqual(s.Values)&&SkillHealth.Balance256(target)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana
            &&World.FindItem((Serial)s.Orb).Parent==p&&World.FindItem((Serial)s.Helm).Parent==p&&NecromancerEffects.Curse(target)?.ElementDeltas?.Cold==-12,"Saved passives/gear/wounds/mana/captured curse");
        var account=FixtureNetwork.Authenticate(network,p,"sorc-passive-reload-");p.MoveToWorld(s.Point,Map.Maps[s.Map]);
        void Resume()
        {
            try
            {
                // GraveyardUndead reapplies its profile on load. Restore only
                // this synthetic receiver input; do not reset wounds/resources.
                ((BaseCreature)target).SetResistance(ResistanceType.Cold,s.NativeColdBase);
                Require(NamedElements.Resistance(p,target,"cold",target.ColdResistance)==s.ColdResistance,"Postload controlled base/live mastery");
                int old=SkillHealth.Balance256(target);var reduced=NamedElements.Resolve256(p,target,25600,"cold");SkillCombat.Hit(p,target,100,"cold",recover:false);
                Require(old-SkillHealth.Balance256(target)==reduced.Damage256&&reduced.Damage256>0,"Postload live Cold Mastery reduction");
                int life=SkillHealth.Balance256(target);p.Internalize();network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();
                File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1,Life=life}));Log.Information("sorceress-passives-reload-result: passed");
            }catch(Exception e){Log.Error(e,"sorceress-passives-reload-result: failed");}
        }
        FixtureClock.NextLoop("sorceress-passives-reload-result",Resume);
    }
}
