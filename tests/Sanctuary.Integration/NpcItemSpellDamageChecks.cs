// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcItemSpellDamageChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NpcItemSpellDamageChecks));
    public static void Configure(){Register("npc-item-spell-damage-check");Register("npc-item-spell-damage-reload-check");}
    private static void Register(string command)=>ConsoleInputHandler.RegisterCommand(command,"Original NPC proc spell numeric capture",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("{Name}: passed",command);}catch(Exception e){Log.Error(e,"{Name}: failed",command);}}));
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Expected(NpcItemSpellPacket packet,JsonElement expected)
    {
        Require(packet.Skill==expected.GetProperty("Skill").GetInt32()&&packet.Rank==expected.GetProperty("Rank").GetInt32()
            &&packet.Minimum256==expected.GetProperty("Minimum256").GetInt32()&&packet.Maximum256==expected.GetProperty("Maximum256").GetInt32()
            &&packet.ColdFrames==expected.GetProperty("ColdFrames").GetInt32()&&packet.Emissions==expected.GetProperty("Emissions").GetInt32(),
            "Original NPC spell source packet differs: "+Json(packet)+" expected="+expected.GetRawText());
        var ability=NpcItemSpellDamage.Ability(packet.Skill);var definition=SkillMechanics.For(ability);
        Require(packet.Element==definition.Text("EType")&&packet.Missile==definition.Text(packet.Skill==36?"srvmissile":"srvmissilea"),"Original spell identity lost");
    }
    private static void Check()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-item-spell-damage-cases.json")));
        var cases=source.RootElement.GetProperty("Cases").EnumerateArray().ToArray();Require(cases.Length==2558,"Missing complete spell source controls");
        foreach(var c in cases)
        {
            int skill=c.GetProperty("Skill").GetInt32(),rank=c.GetProperty("Rank").GetInt32(),mastery=c.GetProperty("Mastery").GetInt32();
            foreach(int level in new[]{1,99}){var packet=NpcItemSpellDamage.Calculate(skill,rank,level,mastery);Expected(packet,c);Require(packet.FirePierce==0&&packet.ColdPierce==0&&packet.LightningPierce==0,"Invented numeric pierce context");}
        }
        foreach(var a in new[]{(36,0,99,0),(36,64,99,0),(36,1,0,0),(36,1,100,0),(36,1,99,-101),(36,1,99,10001),(82,5,99,0)})
        {bool refused=false;try{NpcItemSpellDamage.Calculate(a.Item1,a.Item2,a.Item3,a.Item4);}catch(NotSupportedException){refused=true;}Require(refused,"Invalid proc/source/rank accepted");}
        using var originals=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-contact-fixtures.json")));
        var held=originals.RootElement.GetProperty("Bodies").EnumerateArray().ToArray();Require(held.Length==57,"Original spell corpus lost bodies");
        var rows=source.RootElement.GetProperty("Originals").EnumerateArray().ToArray();Require(rows.Length==38,"Original proc layers missing");int count=0;
        for(int i=0;i<held.Length;i++)
        {
            var b=World.FindMobile((Serial)held[i].GetProperty("Body").GetUInt32())as AmazonValkyrieBody;Require(b is{Alive:true,Deleted:false,ControlMaster:PlayerMobile},"Original spell actor/owner lost");
            string text=b.SavedText,stats=Json(b.EquipmentNumbers.Stats),items=Json(ValkyrieEquipment.Originals(b).Select(x=>x.SavedText).ToArray()),resources=Json(GeneratedNpcLiveResources.State(b));
            var seed=NpcEquipmentRandom.State(b);int life=SkillHealth.Balance256(b);var procs=b.EquipmentNumbers.Stats.Where(v=>v.Value!=0&&v.Stat is 195 or 198 or 201).ToArray();
            var expected=rows.Where(r=>r.GetProperty("Case").GetInt32()==i).ToArray();Require(procs.Length==expected.Length,"Original proc sample was filtered or duplicated");
            foreach(var r in expected)
            {
                uint key=r.GetProperty("Key").GetUInt32();Require(procs.Single(v=>v.Packed==key).Value==r.GetProperty("Chance").GetInt32(),"Original proc chance/layer changed");
                int rank=(int)(key&63),skill=(int)((key&65535)>>6);var packet=NpcItemSpellDamage.CaptureValkyrie(b,skill,rank);Expected(packet,cases[r.GetProperty("Input").GetInt32()]);
                Require(packet.FirePierce==r.GetProperty("FirePierce").GetInt32()&&packet.ColdPierce==r.GetProperty("ColdPierce").GetInt32()&&packet.LightningPierce==r.GetProperty("LightningPierce").GetInt32(),"Original own-pierce snapshot differs");
                Require(JsonSerializer.Deserialize<NpcItemSpellPacket>(Json(packet))==packet,"Immutable numeric packet roundtrip changed");count++;
            }
            Require(text==b.SavedText&&stats==Json(b.EquipmentNumbers.Stats)&&items==Json(ValkyrieEquipment.Originals(b).Select(x=>x.SavedText).ToArray())
                &&resources==Json(GeneratedNpcLiveResources.State(b))&&seed==NpcEquipmentRandom.State(b)&&life==SkillHealth.Balance256(b),"Numeric capture changed original items/resources/wounds/RNG");
            Require(!ClassCombat.Available((PlayerMobile)b.ControlMaster,CombatAbility.Valkyrie),"Numeric supplier enabled unfinished learned casting");
        }
        Require(count==38,"Missing original spell observations");
        Log.Information("NPC item spell damage:2558 complete source controls at two actor levels;57original bodies/330items/38proc layers,own damage/pierce/rank,unchanged original resources/wounds/RNG across reloads;delivery/callbacks/AI/learned cast remain gated");
    }
}
