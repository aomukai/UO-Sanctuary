// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class BarbarianBindingChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BarbarianBindingChecks));
    private sealed record Saved(uint Owner,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"barbarian-binding-fixtures.json");
    public static void Configure(){Register("barbarian-binding-check",Check);Register("barbarian-binding-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Barbarian pinned mouse slots, deferred skills and saved normalization",_=>Core.LoopContext.Post(()=>{try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile p,string id)
    {TalentCatalog.TryGet(DiabloClass.Barbarian,id,out var row);foreach(var pre in row.Prerequisites){Train(p,pre);}if(ClassCreation.Profiles.GetRank(p.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(p.Serial,id,out _),"Train "+id);}}
    private static void Reference(PlayerMobile p)
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"barbarian-binding-reference.json")));int count=0;
        foreach(var entry in doc.RootElement.GetProperty("skills").EnumerateArray())
        {
            var row=SkillMechanics.All.Single(x=>x.SourceId==entry.GetProperty("sourceId").GetInt32());Train(p,row.TalentId);
            bool sourceLeft=entry.GetProperty("sourceLeft").GetBoolean(),right=entry.GetProperty("sourceRight").GetBoolean(),left=entry.GetProperty("left").GetBoolean();
            Require(row.CharacterClass==DiabloClass.Barbarian&&row.Left==sourceLeft&&row.Right==right,"Pinned Barbarian flags differ: "+row.Name);
            Require(SkillBindings.MouseAllowed(DiabloClass.Barbarian,true,row.Ability)==left&&SkillBindings.MouseAllowed(DiabloClass.Barbarian,false,row.Ability)==right,"Barbarian mouse policy differs: "+row.Name);
            bool available=!row.Passive&&!Deferred(row.Ability);Require(ClassCombat.Available(p,row.Ability)==available,"Barbarian learned availability differs: "+row.Name);
            foreach(bool isLeft in new[]{false,true})
            {
                var before=SkillBindings.Instance.Get(p);var session=ClassCombat.For(p);long next=session.NextAction;int mana=NecromancerEffects.ManaBalance256(p);bool expected=available&&(isLeft?left:right);
                Require(SkillBindings.Instance.Assign(p,isLeft,row.Ability)==expected,"Barbarian assignment differs: "+row.Name);
                if(!expected){Require(SkillBindings.Instance.Get(p)==before&&session.NextAction==next&&NecromancerEffects.ManaBalance256(p)==mana,"Rejected Barbarian assignment changed bindings/resources/action");}
            }
            Require(SkillBindings.Instance.AssignQuick(p,7,row.Ability)==available,"Quick cast incorrectly follows mouse flags");count++;
        }
        Require(count==30&&!SkillBindings.MouseAllowed(DiabloClass.Barbarian,true,(CombatAbility)255),"Incomplete Barbarian policy scope/invalid ID admitted");
        foreach(bool left in new[]{false,true})
        {
            Require(SkillBindings.MouseAllowed(DiabloClass.Barbarian,left,CombatAbility.BasicAttack)&&SkillBindings.MouseAllowed(DiabloClass.Barbarian,left,CombatAbility.None),"Basic/empty mouse choices changed");
            Require(!SkillBindings.MouseAllowed(DiabloClass.Barbarian,left,CombatAbility.FireBolt)&&!SkillBindings.MouseAllowed(DiabloClass.Barbarian,left,(CombatAbility)255),"Foreign/invalid mouse choice admitted");
        }
    }
    private static bool Deferred(CombatAbility ability)=>ability is CombatAbility.LeapAttack or CombatAbility.GrimWard;
    private static void Migration(PlayerMobile p)
    {
        byte[] original=NecromancerBindingChecks.Encode();int count=0;
        // Exercise both slots independently, including passive and foreign-class
        // legacy values. Quick slots are intentionally not mouse-normalized.
        var abilities=SkillMechanics.All.Where(x=>x.CharacterClass==DiabloClass.Barbarian).Select(x=>x.Ability)
            .Concat(new[]{CombatAbility.BasicAttack,CombatAbility.None,CombatAbility.FireBolt}).ToArray();
        try
        {
            foreach(int version in new[]{0,1})
            foreach(bool isLeft in new[]{false,true})
            foreach(var old in abilities)
            {
                var writer=new BufferWriter(new byte[1024],true);writer.Write(version);writer.Write(1);writer.Write(p.Serial.Value);if(version==1){writer.Write((byte)DiabloClass.Barbarian);}
                writer.Write((byte)(isLeft?old:CombatAbility.Bash));writer.Write((byte)(isLeft?CombatAbility.Shout:old));
                if(version==1){for(int slot=0;slot<SkillBindingProtocol.Slots;slot++){writer.Write((byte)(slot==7?CombatAbility.FindPotion:CombatAbility.None));}}
                SkillBindings.Instance.Deserialize(new BufferReader(writer.Buffer[..(int)writer.Position]));
                bool allowed=old is CombatAbility.None or CombatAbility.BasicAttack;
                if(!allowed&&old!=CombatAbility.FireBolt)
                {var row=SkillMechanics.For(old);allowed=isLeft?row.Left:row.Right;}
                var expected=(isLeft?(allowed?old:CombatAbility.BasicAttack):CombatAbility.Bash,
                    isLeft?CombatAbility.Shout:(allowed?old:CombatAbility.None));
                if(version==1)
                {
                    byte[] before=NecromancerBindingChecks.Encode();
                    Require(SkillBindings.Instance.Get(p)==expected&&SkillBindings.Instance.Get(p)==expected,"Get failed to normalize raw version1 Barbarian binding");
                    Require(before.SequenceEqual(NecromancerBindingChecks.Encode()),"Get mutated persisted binding state");
                }
                SkillBindings.Instance.PostDeserialize();
                Require(SkillBindings.Instance.Get(p)==expected,"Legacy Barbarian normalization lost valid slot/retained forbidden slot");
                Require(SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Barbarian,7)==(version==1?CombatAbility.FindPotion:CombatAbility.None),"Legacy Barbarian normalization rewrote quick slots");
                byte[] first=NecromancerBindingChecks.Encode();SkillBindings.Instance.Deserialize(new BufferReader(first));SkillBindings.Instance.PostDeserialize();Require(first.SequenceEqual(NecromancerBindingChecks.Encode()),"Barbarian migration not idempotent");count++;
            }
        }
        finally{SkillBindings.Instance.Deserialize(new BufferReader(original));SkillBindings.Instance.PostDeserialize();}
        Require(count==132,"Incomplete Barbarian migration scope");
    }
    private static void Bind(PlayerMobile p)
    {Require(SkillBindings.Instance.Assign(p,true,CombatAbility.Bash)&&SkillBindings.Instance.Assign(p,false,CombatAbility.Shout)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.Shout),"Valid Bash left and Shout right/quick selection");}
    private static void History(PlayerMobile p)
    {
        Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Paladin),"Barbarian history exit");
        Require(SkillBindings.Instance.Get(p)==(CombatAbility.BasicAttack,CombatAbility.None)&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Paladin,7)==CombatAbility.None,"Barbarian bindings leaked into another history");
        Require(SkillBindings.Instance.Assign(p,true,CombatAbility.BasicAttack)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.BasicAttack),"Independent history binding");
        Require(ClassCreation.Profiles.TryActivateHistory(p,DiabloClass.Barbarian),"Barbarian history return");
        Require(SkillBindings.Instance.Get(p)==(CombatAbility.Bash,CombatAbility.Shout)&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Barbarian,7)==CombatAbility.Shout,"Barbarian history lost bindings");
        Require(SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Paladin,7)==CombatAbility.BasicAttack,"Barbarian return rewrote inactive history");
    }
    private static void Check()
    {
        var p=ClassSkillChecks.Player(DiabloClass.Barbarian);p.AddItem(new Server.Items.Backpack());p.Name="BarbarianBindingFixture";Migration(p);Reference(p);Bind(p);History(p);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,0)));
        Log.Information("Barbarian bindings:30pinned rows,60mouse checks,direct/passive/quick and two deferred gates,132legacy migrations/idempotence and history;Bash left/Shout right/quick retained");
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        Require(s.Generation<2&&SkillBindings.Instance.Get(p)==(CombatAbility.Bash,CombatAbility.Shout)&&SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Barbarian,7)==CombatAbility.Shout,"Original saved Barbarian bindings changed");
        Require(!SkillBindings.Instance.Assign(p,true,CombatAbility.Shout),"Reload admitted forbidden left war cry");
        Require(SkillBindings.Instance.GetQuick(p.Serial,DiabloClass.Paladin,7)==CombatAbility.BasicAttack,"Reload lost independent inactive history");
        foreach(var ability in new[]{CombatAbility.LeapAttack,CombatAbility.GrimWard})
        {Require(!ClassCombat.Available(p,ability)&&!SkillBindings.Instance.AssignQuick(p,0,ability),"Deferred skill became available after reload");}
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
    }
}
