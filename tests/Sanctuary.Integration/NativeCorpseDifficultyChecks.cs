// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NativeCorpseDifficultyChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(NativeCorpseDifficultyChecks));
    private sealed record Saved(int Generation,uint Owner,CorpseSkillRecord[] Corpses);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"native-corpse-difficulties-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string label=reload?"native-corpse-difficulties-reload-check":"native-corpse-difficulties-check";
            ConsoleInputHandler.RegisterCommand(label,"Existing native Lich difficulty corpse bases",_=>Core.LoopContext.Post(()=>
            {try{if(reload){Reload();}else{Check();}Log.Information("{Label}: passed",label);}catch(Exception e){Log.Error(e,"{Label}: failed",label);}}));
        }
    }
    private static void Check()
    {
        var p=NativeCorpseChecks.Fresh();bool kept=false;
        try
        {
            foreach(var difficulty in Enum.GetValues<MonsterDifficulty>())
            {
                foreach(var ability in new[]{CombatAbility.CorpseExplosion,CombatAbility.PoisonExplosion})
                {
                    var corpse=NativeCorpseChecks.Corpse(p,difficulty:difficulty);var history=CorpseSkillRules.Profiles.Find(corpse);
                    Require(NativeCorpseSources.TryCalculate(history.Source,difficulty,history.BaseLife.Level,out var bounds)&&bounds.Base256==history.BaseLife.Base256
                        &&!NativeCorpseSources.TryCalculate(history.Source,difficulty,history.BaseLife.Level+1,out _)
                        &&!NativeCorpseSources.TryCalculate(history.Source,(MonsterDifficulty)99,history.BaseLife.Level,out _),"Difficulty mismatch admitted guessed native base life");
                    Require(CorpseBaseSnapshots.Bounds(history.Source,history.BaseLife)==bounds,"Captured native bounds cannot be restored");
                    var target=PoisonDaggerChecks.Target(p);target.MoveToWorld(new(corpse.X+1,corpse.Y,corpse.Z),p.Map);int hp=SkillHealth.Balance256(target);
                    NativeCorpseChecks.Cast(p,ability,corpse);
                    if(ability==CombatAbility.CorpseExplosion){Require(SkillHealth.Balance256(target)<hp,"Difficulty corpse did not cause actual explosion damage");}
                    target.Delete();corpse.Delete();NecromancerSpellMissiles.Cancel(p);
                }
            }
            var rows=Enum.GetValues<MonsterDifficulty>().Select((difficulty,index)=>
                CorpseSkillRules.Profiles.Find(NativeCorpseChecks.KeepForReload(NativeCorpseChecks.Corpse(p,5+index,difficulty)))).ToArray();
            Require(rows.Length==3&&rows.All(row=>row.BaseLife.Seed.HasValue),"Incomplete native difficulty fixtures");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,rows)));kept=true;
        }
        finally{if(!kept){NativeCorpseChecks.Clean(p);}}
    }
    private static void Reload()
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        Require(p is{Alive:true,Deleted:false}&&saved.Corpses.Length==3,"Native difficulty owner or fixture list missing");
        foreach(var history in saved.Corpses)
        {
            var corpse=World.FindItem((Serial)history.Corpse)as Corpse;
            Require(corpse is{Deleted:false},"Saved native difficulty corpse missing");if(p.Map==Map.Internal){p.MoveToWorld(p.Location,corpse.Map);}
            Require(CorpseSkillRules.Profiles.Find(corpse)==history&&CorpseSkillRules.Usable(p,corpse,SkeletonPets.Range)&&CorpseExplosionCasts.Supported(corpse)
                &&corpse.Items.OfType<Gold>().Single().Amount==17&&CorpseBaseSnapshots.Bounds(history.Source,history.BaseLife).Base256==history.BaseLife.Base256,
                "Saved difficulty corpse metadata/base/seed/loot/availability changed");
            int mana=NecromancerEffects.ManaBalance256(p);
            bool planned=ReviveCasts.Plan(p,corpse,out _,out _,out string issue);
            Require(planned==NativeReviveRules.Approved&&(planned||issue==NativeCorpseSources.ForSource(history.Source).ReviveIssue)
                &&NecromancerEffects.ManaBalance256(p)==mana&&!CorpseSkillRules.NoSelect(corpse),"Difficulty support accidentally admitted native Revive");
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
}
