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
public static class PsychicWardChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PsychicWardChecks));
    private sealed record Saved(uint Owner,uint Helm,bool Female,PsychicWardState State);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"psychic-ward-fixtures.json");
    private static void Require(bool v,string why){if(!v){throw new InvalidOperationException(why);}}
    private static void Register(string n,Action run)=>ConsoleInputHandler.RegisterCommand(n,"Prepared Ward pool, group, native damage and persistence",_=>Core.LoopContext.Post(()=>
    {try{run();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));
    public static void Configure(){Register("psychic-ward-check",Check);Register("psychic-ward-reload-check",Reload);}
    private static SkillValues Values(int rank,int levitate=0,int cleave=0)=>new(SkillMechanics.For(CombatAbility.PsychicWard),k=>k=="Psychic Ward"?rank:255,k=>k=="Levitate"?levitate:k=="Cleave"?cleave:0,99);
    private static void Model()
    {
        int count=0;for(int rank=1;rank<=255;rank++)foreach(int levitate in new[]{0,1,20})foreach(int cleave in new[]{0,1,20})
        {var v=PsychicWard.Values(Values(rank,levitate,cleave));Require(v.Capacity256==(15+10*(rank-1)+15*(levitate+cleave))*256&&v.Mana256==(20+2*(rank-1))*256,"Current Ward capacity/hard synergies/mana");count++;}
        Require(count==2295,"Incomplete Ward formula controls");
        foreach(var (rank,frames) in new[]{(1,44),(20,77),(21,77),(255,85)}){Require(PsychicWard.Values(Values(rank)).StunFrames==frames,"Ward stun frames/current shared diminishing curve");}
        foreach(int pool in new[]{0,1,255,256,257,3840})foreach(int damage in new[]{0,1,255,256,257,3840,9999})
        {var s=SourceAbsorptionPool.Take(pool,damage);Require(s.Remaining256+s.Absorbed256==pool&&s.Damage256+s.Absorbed256==damage&&s.Remaining256>=0&&s.Damage256>=0,"Physical pool conservation");}
        Require(!WarlockCombat.Implemented(CombatAbility.PsychicWard),"Prepared Ward enabled incomplete learned casting");
        Log.Information("Psychic Ward:2295current formula controls,44frame rank1stun and fractional pool conservation pass; event37delivery remains supplied");
    }
    private static PlayerMobile Fresh(bool female=false)
    {
        var p=new PlayerMobile{Body=female?0x191:0x190,Female=female,Player=true,Name="PsychicWardFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);
        FixtureSpace.Place(p,new(6100,1580,0),Map.Trammel);while(p.RawStr<50){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Fixture strength");}Train(p,"warlock.psychicward");Mana(p);return p;
    }
    private static void Train(PlayerMobile p,string id,int rank=1)
    {Require(TalentCatalog.TryGet(DiabloClass.Warlock,id,out var t),"Ward talent");foreach(var pre in t.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,pre)==0){Train(p,pre);}}while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Ward train "+id);}}
    private static void Mana(PlayerMobile p)=>NecromancerEffects.SetMana256(p,p.ManaMax*256);
    private static void Pool(PlayerMobile p,int amount)
    {var state=PsychicWard.State(p);Require(state!=null,"Missing Ward pool");PsychicWard.Restore(PsychicWard.Snapshot().Select(x=>x.Owner==p.Serial.Value?x with{Remaining256=amount}:x).ToArray());}
    private static void GroupCases()
    {
        var p=Fresh();var allocate=PsychicWard.Allocate;
        try
        {
            foreach(var cold in new[]{CombatAbility.FrozenArmor,CombatAbility.ShiverArmor,CombatAbility.ChillingArmor})foreach(bool fail in new[]{false,true})
            {
                NecromancerEffects.BoneArmor(p,new(SkillMechanics.For(CombatAbility.BoneArmor),_=>1,_=>0,99));
                Require(NecromancerEffects.Armor(p)==20,"Bone fixture");Mana(p);Require(PsychicWard.CastPrepared(p)&&NecromancerEffects.Armor(p)==0,"Ward did not replace Bone Armor");
                SorceressEffects.CastArmor(p,cold,new(SkillMechanics.For(cold),_=>1,_=>0,99));Require(PsychicWard.State(p)==null&&SorceressEffects.Armor(p)?.Ability==cold,"Cold armor did not replace Ward");
                // Source group clearing also handles preexisting mixed states.
                NecromancerEffects.Profiles.Store(p,(NecromancerEffects.Profiles.Find(p)??new()) with{BoneArmor=20});
                PsychicWard.Allocate=()=>!fail;Mana(p);int mana=NecromancerEffects.ManaBalance256(p);bool cast=PsychicWard.CastPrepared(p);
                Require(cast==!fail&&NecromancerEffects.Armor(p)==0&&SorceressEffects.Armor(p)==null&&(PsychicWard.State(p)!=null)==!fail,"Group clear/factory-failure order");
                Require(NecromancerEffects.ManaBalance256(p)==mana-(fail?0:PsychicWard.Values(SkillMechanics.Values(p,CombatAbility.PsychicWard)).Mana256),"Prepared Ward payment/factory-failure debit");
                PsychicWard.Allocate=allocate;
            }
            Mana(p);Require(PsychicWard.CastPrepared(p),"Group fixture Ward");var before=PsychicWard.State(p);NecromancerEffects.SetMana256(p,20*256-1);
            Require(!PsychicWard.CastPrepared(p)&&PsychicWard.State(p)==before,"Insufficient mana removed old armor");
            SorceressEffects.CastShield(p,new(SkillMechanics.For(CombatAbility.EnergyShield),_=>1,_=>0,99));
            try{PsychicWard.CastPrepared(p);throw new InvalidOperationException("Unverified Energy Shield ordering accepted");}catch(NotSupportedException){}
            SorceressEffects.Profiles.Clear(p);
        }
        finally{PsychicWard.Allocate=allocate;p.Delete();PsychicWard.Prune();}
    }
    private static void IncomingAndReaction()
    {
        var p=Fresh();var enemy=new ShockWaveTarget{Frozen=true,CantWalk=true,NoKillAwards=true};enemy.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);enemy.AIObject.AITimer.Stop();
        try
        {
            Mana(p);Require(PsychicWard.CastPrepared(p),"Native Ward");int initial=PsychicWard.State(p).Remaining256;SkillHealth.SetBalance256(p,p.HitsMax*256);int hp=SkillHealth.Balance256(p);
            AOS.Damage(p,enemy,10,false,100,0,0,0,0);Require(SkillHealth.Balance256(p)==hp&&PsychicWard.State(p).Remaining256==initial-2560,"Actual native physical hit bypassed Ward");
            int pool=PsychicWard.State(p).Remaining256;AOS.Damage(p,enemy,7,false,0,100,0,0,0,spellDamage:true);Require(SkillHealth.Balance256(p)==hp-1792&&PsychicWard.State(p).Remaining256==pool,"Elemental hit consumed physical-only pool");
            Pool(p,641);Require(PsychicWard.AbsorbPhysical(p,1/256.0)==0&&PsychicWard.State(p).Remaining256==640,"Sub-HP pool debit rounded up");
            Require(PsychicWard.AbsorbPhysical(p,3)==0.5&&PsychicWard.State(p)==null,"Pool depletion/overflow or clearing");
            while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Resistance gear Strength");}
            var armor=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Shaftstop").Id,99,new Random(173),true);p.AddToBackpack(armor);Require(p.EquipItem(armor),"Real resistance armor");ClassGear.Refresh(p);
            Mana(p);PsychicWard.CastPrepared(p);Pool(p,640);
            try{Require(NamedElements.Resistance(enemy,p,"physical",p.PhysicalResistance)==30,"Real physical-resistance fixture");var hit=SkillCombat.MitigatePrepared(enemy,p,new(10,0,0,0,0));Require(hit.Physical==5.25&&PsychicWard.State(p)==null,"Ward did not precede physical resistance");}finally{p.AddToBackpack(armor);armor.Delete();ClassGear.Refresh(p);}
            Mana(p);PsychicWard.CastPrepared(p);var registered=PsychicWard.State(p);CombatStuns.Clear(enemy);
            Require(!PsychicWard.ReactPrepared(p,registered,enemy,false,TimedEffects.Now)&&CombatStuns.Remaining(enemy)==0,"Ineligible event applied stun");
            Require(PsychicWard.ReactPrepared(p,registered,enemy,true,TimedEffects.Now)&&CombatStuns.Remaining(enemy)>1500,"Explicit eligible event did not reach shared native stun");CombatStuns.Clear(enemy);
            Require(!SkillBindings.Instance.Assign(p,false,CombatAbility.PsychicWard)&&!SkillBindings.Instance.AssignQuick(p,0,CombatAbility.PsychicWard),"Unfinished Ward input accepted");
        }
        finally{enemy.Delete();p.Delete();PsychicWard.Prune();}
    }
    private static void Lifecycle()
    {
        var p=Fresh();PsychicWard.CastPrepared(p);p.Kill();p.Resurrect();Require(PsychicWard.State(p)==null,"Death retained Ward");p.Corpse?.Delete();p.Delete();
        p=Fresh();PsychicWard.CastPrepared(p);ClassCreation.Profiles.TryGet(p.Serial,out var profile);ClassCreation.Profiles.ResetAllocatedPoints(p,profile);Require(PsychicWard.State(p)==null,"Respec retained Ward");p.Delete();
        p=Fresh();PsychicWard.CastPrepared(p);Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Necromancer),"Ward history exit");Require(!PsychicWard.Snapshot().Any(x=>x.Owner==p.Serial.Value),"History retained hidden Ward");p.Delete();
    }
    private static void Check()
    {
        Model();GroupCases();IncomingAndReaction();Lifecycle();var saved=new Saved[2];int i=0;
        foreach(bool female in new[]{false,true})
        {
            var p=Fresh(female);foreach(string id in new[]{"warlock.levitationmastery","warlock.cleave","warlock.psychicward"}){Train(p,id,20);}
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Ward soft-rank helm");ClassGear.Refresh(p);Mana(p);
            Require(PsychicWard.CastPrepared(p)&&PsychicWard.State(p) is{Rank:21,Maximum256:208640,StunFrames:77},"Effective rank/hard-only Ward synergies");Pool(p,123457);var captured=PsychicWard.State(p);
            p.AddToBackpack(helm);ClassGear.Refresh(p);Require(PsychicWard.State(p)==captured,"Soft rank removal retroactively changed cast pool");Require(p.EquipItem(helm),"Ward helm restore");ClassGear.Refresh(p);
            var held=PsychicWard.Snapshot();
            foreach(var bad in new[]{captured with{Remaining256=0},captured with{Remaining256=captured.Maximum256+1},captured with{StunFrames=44},captured with{Maximum256=captured.Maximum256+1}})
            {try{PsychicWard.Restore([bad]);throw new InvalidOperationException("Malformed pool accepted");}catch(InvalidDataException){}Require(PsychicWard.Snapshot().SequenceEqual(held),"Malformed restore mutated live pools");}
            saved[i++]=new(p.Serial.Value,helm.Serial.Value,female,captured);
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
    }
    private static void Reload()
    {
        Model();foreach(var s in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var helm=World.FindItem((Serial)s.Helm);
            Require(p.Female==s.Female&&helm.Parent==p&&PsychicWard.State(p)==s.State,"Saved Witch/Warlock pool/rank/gear changed");
            Require(PsychicWard.AbsorbPhysical(p,1/256.0)==0&&PsychicWard.State(p).Remaining256==s.State.Remaining256-1,"Saved fractional pool could not resume");
            Pool(p,s.State.Remaining256);Require(PsychicWard.State(p)==s.State,"Reload pool restore");
        }
    }
}
