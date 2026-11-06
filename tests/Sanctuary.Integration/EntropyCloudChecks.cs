// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class EntropyCloudChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(EntropyCloudChecks));
    private sealed record Saved(uint Owner,uint Helm,bool Female,int Hard,int Life,int Mana,EntropyCloudShape Bolt,EntropyCloudShape Chain);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"entropy-cloud-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,string id,int rank=1)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Warlock,id,out var row),"Warlock talent");
        foreach(string pre in row.Prerequisites){if(ClassCreation.Profiles.GetRank(p.Serial,pre)==0){Train(p,pre);}}
        while(ClassCreation.Profiles.GetRank(p.Serial,id)<rank){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}
    }
    private static EntropyCloudShape Shape(PlayerMobile p,bool chain)=>WarlockEntropyClouds.Capture(SkillMechanics.Values(p,chain?CombatAbility.MiasmaChain:CombatAbility.MiasmaBolt));
    private static void Model()
    {
        // Threshold goldens distinguish source hard-point and effective-rank
        // channels; they are not a guessed do41 geometry implementation.
        foreach(var row in new[]{new[]{0,0,50,2,2},new[]{4,4,70,2,2},new[]{5,4,75,3,2},new[]{10,9,100,4,2},new[]{20,19,150,4,3},new[]{21,20,150,4,4},new[]{255,20,150,4,4}})
        foreach(bool chain in new[]{false,true})
        {
            var skill=SkillMechanics.For(chain?CombatAbility.MiasmaChain:CombatAbility.MiasmaBolt);
            var values=new SkillValues(skill,key=>key==skill.SourceKey?1:key=="Enhanced Entropy"?row[0]:0,key=>key=="Enhanced Entropy"?row[1]:0,99);
            var shape=WarlockEntropyClouds.Capture(values);
            Require(shape==new EntropyCloudShape(row[0],row[1],row[2],row[3],chain?row[4]:null),"Pinned entropy threshold channels merged");
            if(!chain){var packet=MiasmaPackets.Capture(values);Require(packet.CloudFrames==shape.LifetimeFrames&&packet.CloudRadius==shape.Radius&&packet.CloudMinimum256==768&&packet.CloudMaximum256==1280,"Entropy changed damage or detached the existing Bolt consumer");}
        }
        foreach(int rank in new[]{0,256})
        {
            bool refused=false;try{WarlockEntropyClouds.Capture(new(SkillMechanics.For(CombatAbility.MiasmaBolt),_=>rank,_=>0,99));}catch(NotSupportedException){refused=true;}
            Require(refused,"Unmapped spell rank accepted");
        }
        Require(!WarlockCombat.Implemented(CombatAbility.EnhancedEntropy)&&!WarlockCombat.Implemented(CombatAbility.MiasmaChain),"Prepared cloud outputs enabled incomplete do41/Abyss routes");
    }
    private static void Check()
    {
        Model();var saved=new Saved[3];int index=0;
        foreach(int hard in new[]{4,9,19})
        {
            bool female=hard==9;var p=new PlayerMobile{Name="PoisonDaggerFixtureEntropyCloud",Player=true,Female=female,Body=female?0x191:0x190};p.AddItem(new Backpack());
            ClassCreation.Profiles.Assign(p,DiabloClass.Warlock);ClassCreation.Profiles.AdvanceLevel(p,99);FixtureSpace.Place(p,new(6100,1660+index*4,0),Map.Trammel);
            while(p.RawStr<50){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Fixture Strength");}
            Train(p,"warlock.enhancedentropy",hard);var boltValues=SkillMechanics.Values(p,CombatAbility.MiasmaBolt);var chainValues=SkillMechanics.Values(p,CombatAbility.MiasmaChain);
            var beforeBolt=WarlockEntropyClouds.Capture(boltValues);var beforeChain=WarlockEntropyClouds.Capture(chainValues);
            var helm=NamedGear.Create(NamedGear.Catalog.Items.Single(x=>x.SourceKey=="Tarnhelm").Id,99,new Random(173),true);p.AddToBackpack(helm);Require(p.EquipItem(helm),"Original Tarnhelm equip");ClassGear.Refresh(p);
            var bolt=Shape(p,false);var chain=Shape(p,true);
            Require(bolt.EffectiveRank==hard+1&&bolt.HardRank==hard&&chain.HardRank==hard&&chain.ServerCalculation==beforeChain.ServerCalculation,"Item bonus changed hard-only server calculation");
            Require((hard==4?bolt.Radius==3:hard==9?bolt.Radius==4:bolt.LifetimeFrames==150)&&WarlockEntropyClouds.Capture(boltValues)==beforeBolt&&WarlockEntropyClouds.Capture(chainValues)==beforeChain,"Cloud capture rewrote older packets or missed gear breakpoint");
            p.AddToBackpack(helm);ClassGear.Refresh(p);Require(Shape(p,false)==beforeBolt&&Shape(p,true)==beforeChain,"Unequipped bonus persisted");Require(p.EquipItem(helm),"Helm restore");ClassGear.Refresh(p);
            if(hard==19){Train(p,"warlock.enhancedentropy",20);bolt=Shape(p,false);chain=Shape(p,true);Require(chain.EffectiveRank==21&&chain.ServerCalculation==4&&beforeChain.ServerCalculation==3,"Hard20server threshold or prior capture changed");}
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-1237);TimedEffects.PreventHealing(p,TimedEffects.Now);NecromancerEffects.SetMana256(p,NecromancerEffects.ManaMaximum256(p)-777);
            Require(!SkillBindings.Instance.Assign(p,false,CombatAbility.EnhancedEntropy)&&!SkillBindings.Instance.AssignQuick(p,0,CombatAbility.EnhancedEntropy),"Passive accepted an active binding");
            saved[index++]=new(p.Serial.Value,helm.Serial.Value,female,hard==19?20:hard,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),bolt,chain);p.Internalize();
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));Report();
    }
    private static void Reload()
    {
        Model();foreach(var s in JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest)))
        {
            var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var helm=World.FindItem((Serial)s.Helm);ClassGear.Refresh(p);
            Require(p.Female==s.Female&&helm.Parent==p&&ClassCreation.Profiles.GetRank(p.Serial,"warlock.enhancedentropy")==s.Hard&&Shape(p,false)==s.Bolt&&Shape(p,true)==s.Chain,"Saved original Warlock/Witch/cloud rank channels changed");
            Require(SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Passive capture changed saved fractional wounds/resources");
        }
        Report();
    }
    private static void Report()=>Log.Information("Entropy clouds:distinct effective Range/Radius and hard-only Chain SrvCalc1;14threshold controls,real original gear/captured packet retention and saved Warlock/Witch resources;do41 geometry and Abyss sksrc remain unresolved");
    public static void Configure()
    {
        foreach(string name in new[]{"entropy-cloud-check","entropy-cloud-reload-check"})ConsoleInputHandler.RegisterCommand(name,"Captured Entropy cloud channels",_=>Core.LoopContext.Post(()=>
        {try{if(name.EndsWith("-reload-check",StringComparison.Ordinal)){Reload();}else{Check();}Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    }
}
