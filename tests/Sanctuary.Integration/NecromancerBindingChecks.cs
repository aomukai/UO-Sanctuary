// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NecromancerBindingChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NecromancerBindingChecks));
    private sealed record Saved(uint Owner,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"necromancer-binding-fixtures.json");
    public static void Configure()
    {Register("necromancer-binding-check",Check);Register("necromancer-binding-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Pinned Necromancer mouse policy and save migration",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static byte[] Encode()
    {var writer=new BufferWriter(new byte[4*1024*1024],true);SkillBindings.Instance.Serialize(writer);return writer.Buffer[..(int)writer.Position];}
    private static void Train(PlayerMobile p,string id)
    {TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row);foreach(var pre in row.Prerequisites){Train(p,pre);}if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}}
    private static void Reference(PlayerMobile p)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"necromancer-binding-reference.json")));int count=0;
        foreach(var row in doc.RootElement.GetProperty("skills").EnumerateArray())
        {
            var skill=SkillMechanics.All.Single(x=>x.SourceId==row.GetProperty("sourceId").GetInt32());Train(p,skill.TalentId);
            bool left=row.GetProperty("left").GetBoolean(),right=row.GetProperty("right").GetBoolean();
            Require(skill.CharacterClass==DiabloClass.Necromancer&&skill.Left==left&&skill.Right==right,"Pinned skill flag differs");
            Require(SkillBindings.MouseAllowed(DiabloClass.Necromancer,true,skill.Ability)==left
                &&SkillBindings.MouseAllowed(DiabloClass.Necromancer,false,skill.Ability)==right,"Mouse policy differs: "+skill.Name);
            bool available=ClassCombat.Available(p,skill.Ability);Require(available==!skill.Passive,"Necromancer handler availability differs: "+skill.Name);
            foreach(bool isLeft in new[]{false,true})
            {
                var old=SkillBindings.Instance.Get(p);var session=ClassCombat.For(p);long next=session.NextAction;int mana=NecromancerEffects.ManaBalance256(p);
                bool expected=available&&(isLeft?left:right);
                Require(SkillBindings.Instance.Assign(p,isLeft,skill.Ability)==expected,"Direct assignment: "+skill.Name);
                if(!expected){Require(SkillBindings.Instance.Get(p)==old&&session.NextAction==next&&NecromancerEffects.ManaBalance256(p)==mana,"Rejected assignment changed state");}
            }
            Require(SkillBindings.Instance.AssignQuick(p,7,skill.Ability)==available,"Quick selection incorrectly follows mouse flags");count++;
        }
        Require(count==30&&!SkillBindings.MouseAllowed(DiabloClass.Necromancer,true,(CombatAbility)255),"Missing pinned Necromancer rows or invalid ID accepted");
    }
    private static void Migration(PlayerMobile p)
    {
        byte[] original=Encode();
        try
        {
            foreach(int version in new[]{0,1})foreach(var oldLeft in new[]{CombatAbility.RaiseSkeleton,CombatAbility.RaiseSkeletalMage,CombatAbility.AmplifyDamage,CombatAbility.Teeth})
            {
                var writer=new BufferWriter(new byte[1024],true);writer.Write(version);writer.Write(1);writer.Write(p.Serial.Value);
                if(version==1){writer.Write((byte)DiabloClass.Necromancer);}
                writer.Write((byte)oldLeft);writer.Write((byte)CombatAbility.BoneSpear);
                if(version==1){for(int slot=0;slot<SkillBindingProtocol.Slots;slot++){writer.Write((byte)(slot==7?CombatAbility.RaiseSkeleton:CombatAbility.None));}}
                SkillBindings.Instance.Deserialize(new BufferReader(writer.Buffer[..(int)writer.Position]));SkillBindings.Instance.PostDeserialize();
                var expected=oldLeft==CombatAbility.Teeth?oldLeft:CombatAbility.BasicAttack;
                Require(SkillBindings.Instance.Get(p)==(expected,CombatAbility.BoneSpear),"Old binding migration lost valid right or retained forbidden left");
                Require(SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Necromancer,7)==(version==1?CombatAbility.RaiseSkeleton:CombatAbility.None),"Old quick slots were rewritten");
                byte[] first=Encode();SkillBindings.Instance.Deserialize(new BufferReader(first));SkillBindings.Instance.PostDeserialize();
                Require(first.SequenceEqual(Encode()),"Binding migration was not idempotent");
            }
        }
        finally{SkillBindings.Instance.Deserialize(new BufferReader(original));SkillBindings.Instance.PostDeserialize();}
    }
    private static void Bind(PlayerMobile p)
    {
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Teeth)&&SkillBindings.Instance.Assign(p,false,CombatAbility.RaiseSkeleton)
            &&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.RaiseSkeleton),"Valid left/right/quick bindings");
        var session=ClassCombat.For(p);Require(session.Left==CombatAbility.Teeth&&session.Right==CombatAbility.RaiseSkeleton,"Session differs from canonical binding");
    }
    private static void Check()
    {
        var p=PoisonDaggerChecks.Owner();p.Name="PoisonDaggerFixtureNecromancerBinding";Reference(p);Migration(p);Bind(p);
        p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,0)));
        Log.Information("Necromancer bindings:30 pinned rows,direct/passive/quick gates,8 legacy migrations and idempotent encoding pass;valid offensive left and summon right retained");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        Require(SkillBindings.Instance.Get(p)==(CombatAbility.Teeth,CombatAbility.RaiseSkeleton)
            &&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Necromancer,7)==CombatAbility.RaiseSkeleton,"Saved valid bindings changed");
        Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.RaiseSkeleton),"Reload allowed forbidden left summon");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
}
