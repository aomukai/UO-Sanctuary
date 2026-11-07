// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

// Runs inside the real Shadow body/save cohorts, using original source output.
internal static class ShadowSkillBookChecks
{
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Reject(Action run)
    {try{run();}catch(Exception e)when(e is ArgumentException or InvalidDataException or NotSupportedException or InvalidOperationException){return;}throw new Exception("Unsupported owned Shadow skill accepted");}
    private static ShadowOwnerSnapshot Capture(CombatAbility ability,int rank,ShadowCapturedSkill[] skills)
    {
        int own=ability==CombatAbility.ShadowMaster?279:268;
        return new(ability,99,rank,0,0,0,0,0,null,null)
        {Skills=JsonSerializer.Serialize(skills.Select(s=>s.Id==own?s with{Hard=1,Rank=rank}:s).ToArray())};
    }
    private static ShadowInitialization Init(CombatAbility ability,MonsterDifficulty difficulty)
    {
        ulong before=SourceRandom.InitLow(12345),world=before;ulong seed=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref world)));
        var roll=ShadowInitializer.Roll(ability,seed,difficulty);
        return new(ability,before,world,seed,roll.After,difficulty,96256,ability==CombatAbility.ShadowWarrior?70:47);
    }
    internal static void SourceControls()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-skill-assignment-cases.json")));int master=0,warrior=0;
        foreach(var row in doc.RootElement.GetProperty("Master").EnumerateArray())
        {
            int rank=row[0].GetInt32();var skills=Enumerable.Range(251,30).Select(id=>new ShadowCapturedSkill(id,(id+rank)%21,0)).ToArray();
            foreach(var difficulty in Enum.GetValues<MonsterDifficulty>())
            {
                var capture=Capture(CombatAbility.ShadowMaster,rank,skills);var book=ShadowSkillBook.Initialize(capture,Init(capture.Ability,difficulty));
                var entries=book.Snapshot().Entries;var requests=row[1].EnumerateArray().Select(r=>new[]{r[0].GetInt32(),r[1].GetInt32()}).ToArray();
                Require(entries.Length==29&&book.Left==0&&book.Right==0&&!book.Used.HasValue&&book.Find(268)==null&&book.Find(279)==null,"Master intrinsic membership/selection/pet-type exclusion");
                Require(requests.All(r=>book.Find(r[0]).Base==r[1]),"Whole older Master initializer rank differs");
                int[] initial=[259,266,274,260,275,269,0];
                Require(entries.Select(e=>e.Id).SequenceEqual(initial.Concat(requests.Select(r=>r[0]).Where(id=>!initial.Contains(id)))),"Assignment replaced source linked-list order");
                Require(entries.Take(6).All(e=>e.Mode==14)&&book.Find(0).Mode==5&&book.Find(0).Base==new[]{1,4,8}[(int)difficulty],"Master assignment reset initial NPC custom modes/Attack difficulty rank");
                Require(book.Find(252).Mode==7&&ShadowSkillBook.Metadata(252).PassiveType==68&&ShadowSkillBook.Metadata(263).PassiveType==68,"Claw/Weapon Block passive metadata layers");master++;
            }
        }
        foreach(var row in doc.RootElement.GetProperty("Warrior").EnumerateArray())
        {
            int rank=row[0].GetInt32(),left=row[1].GetInt32(),right=row[2].GetInt32();
            var skills=Enumerable.Range(251,30).Select(id=>new ShadowCapturedSkill(id,0,id==251?left:id==266?right:0)).ToArray();
            var capture=Capture(CombatAbility.ShadowWarrior,rank,skills);var book=ShadowSkillBook.Initialize(capture,Init(capture.Ability,MonsterDifficulty.Hell));
            book.Mirror(capture,251,266);
            foreach(var expected in row[3].EnumerateArray()){Require(book.Find(expected[0].GetInt32()).Base==expected[1].GetInt32(),"Older Warrior assignment-slice rank differs");}
            Require(book.Find(251).Mode==9&&book.Find(266).Mode==14,"Mirroring used player mode or reset custom Blade Fury SQ mode");warrior++;
        }
        Require(master==765&&warrior==1530,"Missing source assignment controls");
        var cap=Capture(CombatAbility.ShadowWarrior,20,Enumerable.Range(251,30).Select(id=>new ShadowCapturedSkill(id,0,0)).ToArray());
        var b=ShadowSkillBook.Initialize(cap,Init(cap.Ability,MonsterDifficulty.Normal));
        b.Assign(251,6);b.SetBonus(251,2);int Stat(int id,int layer)=>id switch{127=>3,97 when layer==251=>7,107 when layer==251=>4,126 when layer==1=>5,_=>throw new Exception("Unexpected NPC rank stat/class/tree read")};
        Require(b.Rank(251,Stat,1,shrine:true)==26,"Own NPC skill bonus composition/element/oskill cap");
        Require(b.Rank(251,(_,_)=>throw new Exception("Hard rank queried item bonuses"),bonuses:false)==6,"Hard rank reads item graph");
        b.Assign(251,0);Require(b.Rank(251,Stat,1,shrine:true)==24,"Base-zero oskill used learned-skill cap");
        b.SetBonus(251,-100,true);Require(b.Find(251).Bonus==0,"Additive level bonus did not clamp at zero");
        b.Assign(259,40);b.Add(259);b.SetMode(259,99);Require(b.Find(259).Base==41&&b.Find(259).Mode==14,"NPC rank capped at20 or assignment reset mode");
        b.SetBonus(252,-2);Require(b.Find(252)==null,"Nonpositive missing skill bonus allocated entry");b.SetBonus(252,5);Require(b.Find(252)is{Base:0,Bonus:5,Mode:7},"Bonus skill entry allocation");
        b.Select(259,false);b.Select(259,true);b.Use(259);b.Select(253,false);Require(b.Left==259,"Absent selection changed active pointer");b.Assign(259,0,true);
        Require(b.Left==0&&b.Right==0&&b.Used==null,"Intrinsic removal lost Attack fallback/used pointer clearing");
        var saved=b.Save();var exposed=b.Snapshot();exposed.Entries[0]=new(0,999,0,0);Require(b.Save()==saved,"Skill snapshot exposed live entry storage");
        Require(ShadowSkillBook.Restore(saved).Save()==saved,"Owned skill roundtrip changed bytes");
        Reject(()=>b.Assign(429,1));Reject(()=>b.Assign(251,-1));Reject(()=>b.Use(253));Reject(()=>b.Assign(0,0,true));Reject(()=>ShadowSkillBook.Restore(null));
        Reject(()=>ShadowSkillBook.Restore(JsonSerializer.Serialize(b.Snapshot()with{Left=253})));Reject(()=>ShadowSkillBook.Restore(JsonSerializer.Serialize(b.Snapshot()with{Entries=[b.Find(0),b.Find(0)]})));
        Reject(()=>b.Mirror(cap,251,33));Require(b.Save()==saved,"Failed mirror changed partial skill state");
    }
    internal static void Body(AssassinShadowBody body,PlayerMobile owner)
    {
        var initial=body.OwnedSkills;var capture=body.Prepared.Capture;var world=NpcWorldAllocation.Capture();var random=NpcEquipmentRandom.Require(body);var graph=body.EquipmentNumbers;
        Require(capture.SkillRanks().Single(s=>s.Id==251)is{Hard:3,Rank:>=3},"Actual owner hard/effective skills were not captured");
        if(capture.Ability==CombatAbility.ShadowWarrior){body.ChangeSkills(b=>b.Mirror(capture,251,266));}
        var book=body.OwnedSkills;int before=body.OwnedSkillRank(251);
        Require(before==book.Rank(251,graph.Read,BattleCommand.Bonus(body)),"Actual own inventory not used for NPC effective rank");
        int original=ClassCreation.Profiles.GetRank(owner.Serial,SkillMechanics.Source("Fire Trauma").TalentId);
        ClassSkillChecks.Train(owner,SkillMechanics.Source("Fire Trauma").TalentId,original+1);
        Require(body.Prepared.Capture==capture&&body.OwnedSkillRank(251)==before,"Existing Shadow recaptured changed owner skill");
        body.ChangeSkills(b=>{b.SetBonus(251,2);b.Select(251,false);b.Select(266,true);b.Use(251);});
        Require(body.OwnedSkills is{Left:251,Right:266,Used:251}&&body.OwnedSkills.Find(251).Bonus==2,"Real body did not persist intrinsic skill pointers/bonus");
        Require(NpcWorldAllocation.Capture()==world&&NpcEquipmentRandom.Require(body)==random,"Skill assignment consumed item/actor/world RNG");
        string saved=body.SavedText;Reject(()=>body.ChangeSkills(b=>{b.Assign(251,99);throw new InvalidOperationException("abort");}));Require(body.SavedText==saved,"Failed owned skill change partially committed");
        var old=body.Prepared;var legacy=new ShadowBodyState(old.Owner,capture with{Skills=null},old.Initialization,old.Difficulty,old.NextRecoveryUtc);
        AssassinShadowBody.Validate(legacy);Require(!JsonSerializer.Serialize(legacy).Contains("\"Skills\""),"Legacy null fields changed original save shape");
        Require(initial.Snapshot().Entries.Length==(capture.Ability==CombatAbility.ShadowMaster?29:7),"Fresh actor lacks its actual initialized intrinsic entries");
    }
}
