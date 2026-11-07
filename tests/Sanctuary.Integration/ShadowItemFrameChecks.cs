// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ShadowItemFrameChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ShadowItemFrameChecks));
    private sealed record Saved(uint Owner,uint Body,string BodyText,string Resources,int[] Numbers);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-item-frame-fixtures.json");
    private static readonly int[] Stats=[21,22,23,24,25,48,49,57,58,81,108,112,113,134,135,136,150,195,196,198];
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool yes,string why){if(!yes){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("shadow-item-frame-check",false);Register("shadow-item-frame-reload-check",true);}
    private static void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Original NPC weapon inclusion state across graph reconstruction",_=>Core.LoopContext.Post(()=>{try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static int[] Numbers(AssassinShadowBody b)=>Stats.Select(s=>b.EquipmentNumbers.Read(s)).ToArray();
    private static void LegacyAndInvalid(AssassinShadowBody b)
    {
        var states=GeneratedNpcLiveResources.Snapshot();var mine=states.Single(s=>s.Actor==b.Serial.Value);
        // Before any hand entry this is the historical ordinary item replay.
        GeneratedNpcLiveResources.Restore(states.Select(s=>s.Actor==mine.Actor?s with{DamageItems=null}:s).ToArray());
        var before=Numbers(b);bool rejected=false;
        try{GeneratedNpcLiveResources.Restore(states.Select(s=>s.Actor==mine.Actor?s with{DamageItems=[mine.Owner]}:s).ToArray());}
        catch(InvalidDataException){rejected=true;}
        Require(rejected&&Numbers(b).SequenceEqual(before),"Malformed hand mask changed the authoritative graph");
    }
    private static void EventCase(PlayerMobile owner)
    {
        var game=NpcWorldAllocation.Capture();int found=-1;string failure=null;
        try
        {
            // Independent declared allocation contexts;every temporary actor
            // is deleted and the actual world stream is restored afterward.
            for(int low=1;low<=64&&found<0;low++)
            {
                NpcWorldAllocation.Restore(new(0,NpcWorldAllocation.Profile,(uint)low,SourceRandom.InitLow(low)));
                var b=AssassinShadowBody.CreateInitialized(owner,CombatAbility.ShadowMaster,TimedEffects.Now);
                try
                {
                    GeneratedNpcLiveResources.ActivateItemEvents(b);b.ChangeSkills(book=>book.Assign(278,1));var graph=b.EquipmentNumbers;var binding=ShadowLoadout.Binding(b);
                    var candidates=ShadowLoadout.Originals(b).Where(i=>i.Snapshot.Equipment.Body is 4 or 5&&graph.Item(i.Serial.Value).Unit.Stats.Values.Any(v=>v.Value!=0
                        &&(EquipmentStatValues.Definition(v.Stat).Flags&4)!=0&&ItemEventDefinitions.Bindings(v.Stat)is{Length:>0} row&&row[0].Event>0)).ToArray();
                    if(candidates.Length==0){continue;}
                    var at=TimedEffects.Now.AddMinutes(5);GeneratedNpcLiveResources.ApplyShadowBuff(b,278,ShadowSkillCasting.Values(b,b.OwnedSkillRank(278),278),at);
                    graph=b.EquipmentNumbers;graph.Weapons(binding.Primary,binding.Secondary,true,true,candidates[0].Snapshot.Equipment.Body==4?0:1,0,0).Enter();
                    var expected=Numbers(b);var until=graph.Passives.Capture().Lists.Single(l=>l.Skill==278).UntilUtc.Value;
                    GeneratedNpcLiveResources.ExpireShadowBuffs(b,until);var expired=Numbers(b);
                    Require(Stats.Select((stat,i)=>stat is 57 or 58||expected[i]==expired[i]).All(equal=>equal),"Venom expiry lost selected-hand non-poison fields");
                    expected=expired;var states=GeneratedNpcLiveResources.Snapshot();bool rejected=false;
                    try{GeneratedNpcLiveResources.Restore(states.Select(s=>s.Actor==b.Serial.Value?s with{DamageItems=null}:s).ToArray());}
                    catch(InvalidDataException e)when(e.Message.Contains("Saved NPC event history lost",StringComparison.Ordinal)){rejected=true;failure=e.Message;}
                    if(!rejected){continue;}
                    // This is the exact former event mismatch,not only a
                    // numeric approximation. The real inclusion mask must
                    // let the same original graph reconstruct intact.
                    b.ChangeSkills(book=>book.Assign(253,1));Require(Numbers(b).SequenceEqual(expected),"Exact event case lost numeric hand fields after reconstruction");
                    // A normal numeric read during an actual item-event
                    // callback must also allow lazy non-event buff expiry.
                    GeneratedNpcLiveResources.ApplyShadowBuff(b,278,ShadowSkillCasting.Values(b,b.OwnedSkillRank(278),278),at);
                    graph=b.EquipmentNumbers;var venom=graph.Passives.Capture().Lists.Single(l=>l.Skill==278);
                    graph.Passives.ApplyBuff(venom with{UntilUtc=TimedEffects.Now.AddSeconds(-1)});
                    byte phase=graph.ItemEvents.Capture().Events.Entries[0].Event;int visits=0;
                    graph.ItemEvents.Trigger(phase,(function,eventPhase,guid1,guid2)=>{visits++;_=b.EquipmentNumbers.Read(21);return 0;});
                    Require(visits>0&&!graph.Passives.Capture().Lists.Any(l=>l.Skill==278)&&Numbers(b).SequenceEqual(expected),"Callback read/expiry lost the live selected-hand frame");found=low;
                }
                finally{b.Delete();}
            }
        }
        finally{NpcWorldAllocation.Restore(game);}
        Require(found>=0,"No bounded original damage-event loadout fixture");
        File.WriteAllText(Path.Join(Core.BaseDirectory,"shadow-item-frame-event-case.json"),Json(new{Seed=found,Reproduced=failure}));
        Log.Information("Original event-frame regression reproduced at declared seed {Seed}: {Diagnostic}",found,failure);
    }
    private static void Check(bool reload)
    {
        PlayerMobile owner;AssassinShadowBody b;
        if(reload)
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);b=(AssassinShadowBody)World.FindMobile((Serial)saved.Body);
            Require(b.SavedText==saved.BodyText&&Json(GeneratedNpcLiveResources.State(b))==saved.Resources&&Numbers(b).SequenceEqual(saved.Numbers),"Reload lost original selected-hand numeric/event frame");
        }
        else
        {
            owner=BladeBodyChecks.Fresh(6260,24,3480);ClassSkillChecks.Train(owner,"assassin.shadowmaster",20);b=AssassinShadowBody.CreateInitialized(owner,CombatAbility.ShadowMaster,TimedEffects.Now);
            GeneratedNpcLiveResources.ActivateItemEvents(b);LegacyAndInvalid(b);EventCase(owner);
            foreach(int selection in new[]{0,1})
            {
                var binding=ShadowLoadout.Binding(b);b.EquipmentNumbers.Weapons(binding.Primary,binding.Secondary,true,true,selection,0,0).Enter();var before=Numbers(b);
                Require(before[0]>0||before[2]>0,"Actual original claw damage fixture is empty");
                b.ChangeSkills(book=>book.Assign(278,10));
                Require(Numbers(b).SequenceEqual(before),"Rebuilding the skill book lost selected-hand damage-related stats: before="+Json(before)+",after="+Json(Numbers(b)));
            }
        }
        GeneratedNpcLiveResources.Snapshot();PersistentMinions.Suspend(owner);owner.Internalize();NpcWorldAllocationChecks.Checked(NpcWorldAllocation.Capture());File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,b.Serial.Value,b.SavedText,Json(GeneratedNpcLiveResources.State(b)),Numbers(b))));
    }
}
