// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class PaladinBindingChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(PaladinBindingChecks));
    private sealed record Saved(uint Owner,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"paladin-binding-fixtures.json");
    public static void Configure(){Register("paladin-binding-check",Check);Register("paladin-binding-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Paladin pinned mouse policy,valid Smite and saved normalization",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,string id)
    {TalentCatalog.TryGet(DiabloClass.Paladin,id,out var row);foreach(var pre in row.Prerequisites){Train(p,pre);}if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}}
    private static void Reference(PlayerMobile p)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"paladin-binding-reference.json")));int count=0;
        foreach(var entry in doc.RootElement.GetProperty("skills").EnumerateArray())
        {
            var row=SkillMechanics.All.Single(x=>x.SourceId==entry.GetProperty("sourceId").GetInt32());Train(p,row.TalentId);
            bool sourceLeft=entry.GetProperty("sourceLeft").GetBoolean(),right=entry.GetProperty("sourceRight").GetBoolean(),left=entry.GetProperty("left").GetBoolean();
            Require(row.CharacterClass==DiabloClass.Paladin&&row.Left==sourceLeft&&row.Right==right,"Pinned Paladin flags differ: "+row.Name);
            Require(SkillBindings.MouseAllowed(DiabloClass.Paladin,true,row.Ability)==left&&SkillBindings.MouseAllowed(DiabloClass.Paladin,false,row.Ability)==right,"Paladin mouse policy differs: "+row.Name);
            bool available=!row.Passive;Require(ClassCombat.Available(p,row.Ability)==available,"Paladin learned availability differs: "+row.Name);
            foreach(bool isLeft in new[]{false,true})
            {
                var before=SkillBindings.Instance.Get(p);var session=ClassCombat.For(p);long next=session.NextAction;int mana=NecromancerEffects.ManaBalance256(p);bool expected=available&&(isLeft?left:right);
                Require(SkillBindings.Instance.Assign(p,isLeft,row.Ability)==expected,"Paladin assignment differs: "+row.Name);
                if(!expected){Require(SkillBindings.Instance.Get(p)==before&&session.NextAction==next&&NecromancerEffects.ManaBalance256(p)==mana,"Rejected Paladin assignment changed bindings/resources/action");}
            }
            Require(SkillBindings.Instance.AssignQuick(p,7,row.Ability)==available,"Quick cast incorrectly follows mouse flags");count++;
        }
        Require(count==30&&!SkillBindings.MouseAllowed(DiabloClass.Paladin,true,(CombatAbility)255),"Incomplete Paladin policy scope/invalid ID admitted");
    }
    private static void Migration(PlayerMobile p)
    {
        byte[] original=NecromancerBindingChecks.Encode();int count=0;
        try
        {
            foreach(int version in new[]{0,1})
            foreach(var oldLeft in new[]{CombatAbility.Prayer,CombatAbility.Might,CombatAbility.Fanaticism,CombatAbility.Conviction,CombatAbility.HolyShield,CombatAbility.Thorns,CombatAbility.BlessedHammer,CombatAbility.Smite,CombatAbility.Sacrifice})
            {
                var writer=new BufferWriter(new byte[1024],true);writer.Write(version);writer.Write(1);writer.Write(p.Serial.Value);if(version==1){writer.Write((byte)DiabloClass.Paladin);}
                writer.Write((byte)oldLeft);writer.Write((byte)CombatAbility.HolyBolt);
                if(version==1){for(int slot=0;slot<SkillBindingProtocol.Slots;slot++){writer.Write((byte)(slot==7?CombatAbility.Fanaticism:CombatAbility.None));}}
                SkillBindings.Instance.Deserialize(new BufferReader(writer.Buffer[..(int)writer.Position]));SkillBindings.Instance.PostDeserialize();
                var left=oldLeft is CombatAbility.BlessedHammer or CombatAbility.Smite or CombatAbility.Sacrifice?oldLeft:CombatAbility.BasicAttack;
                Require(SkillBindings.Instance.Get(p)==(left,CombatAbility.HolyBolt),"Legacy Paladin normalization lost valid right/retained forbidden left");
                Require(SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Paladin,7)==(version==1?CombatAbility.Fanaticism:CombatAbility.None),"Legacy Paladin normalization rewrote quick slots");
                byte[] first=NecromancerBindingChecks.Encode();SkillBindings.Instance.Deserialize(new BufferReader(first));SkillBindings.Instance.PostDeserialize();Require(first.SequenceEqual(NecromancerBindingChecks.Encode()),"Paladin migration not idempotent");count++;
            }
        }
        finally{SkillBindings.Instance.Deserialize(new BufferReader(original));SkillBindings.Instance.PostDeserialize();}
        Require(count==18,"Incomplete migration scope");
    }
    private static void Bind(PlayerMobile p)
    {Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Smite)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Fanaticism)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Fanaticism),"Valid Smite/valid right and quick selection");}
    private static void Check()
    {
        var p=ClassSkillChecks.Player(DiabloClass.Paladin);p.Name="PaladinBindingFixture";Reference(p);Migration(p);Bind(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,0)));
        Log.Information("Paladin bindings:30pinned rows,60mouse checks,direct/quick availability,18legacy migrations/idempotence;valid Smite left and Fanaticism right/quick retained");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        Require(s.Generation<2&&SkillBindings.Instance.Get(p)==(CombatAbility.Smite,CombatAbility.Fanaticism)&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Paladin,7)==CombatAbility.Fanaticism,"Original saved Paladin bindings changed");
        Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.Fanaticism),"Reload admitted forbidden left aura");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
}
