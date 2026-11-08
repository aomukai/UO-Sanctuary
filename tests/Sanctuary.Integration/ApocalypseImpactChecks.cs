// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class ApocalypseImpactChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ApocalypseImpactChecks));
    private sealed record Saved(uint Owner,uint Target,NecromancerCurse Curse,Point3D OwnerPoint,int Map,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"apocalypse-impact-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("apocalypse-impact-check",Check);Register("apocalypse-impact-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Current resistance leaf and per-hit Apocalypse fire receiver",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Source()
    {
        using var data=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"skill-resistance-cases.json")));int n=0;
        foreach(var row in data.RootElement.EnumerateArray())
        {
            var i=row.GetProperty("input");int I(string k)=>i.GetProperty(k).GetInt32();bool B(string k)=>i.GetProperty(k).GetBoolean();
            int actual=SourceSkillResistance.Scale(I("stat"),I("value"),B("present"),I("kind"),B("record")&&I("flag")!=0,B("definition"),B("root"),I("resistance"));
            Require(actual==row.GetProperty("output").GetProperty("value").GetInt32(),"Current453660numeric mismatch at "+n);n++;
        }
        Require(n==142,"Incomplete current resistance fixture");
        for(int rank=1;rank<=255;rank++){Require(ApocalypseImpact.Reduction(rank)==-Math.Min(5+rank-1,40),"Pinned Apocalypse penalty/cap at "+rank);}
    }
    private static void Impacts(PlayerMobile owner,BaseCreature target)
    {
        (int Resist,int Rank,int Pierce,int Damage)[] cases=[(0,1,0,105),(99,1,0,6),(99,1,20,26),(100,1,0,1),(100,1,20,21),
            (105,1,100,0),(105,40,0,3),(105,40,20,23),(0,255,0,140),(-95,255,0,200)];
        foreach(var c in cases)
        {
            target.SetResistance(ResistanceType.Fire,c.Resist);Require(target.FireResistance==c.Resist,"Native fire resistance fixture was clamped");
            int before=SkillHealth.Balance256(target);var curse=NecromancerEffects.Curse(target);
            var result=ApocalypseImpact.ReleasePrepared(owner,target,100*256,c.Rank,pierceOverride:c.Pierce);
            Require(result.Applied&&result.Damage256==c.Damage*256&&before-SkillHealth.Balance256(target)==c.Damage*256,
                $"Apocalypse r{c.Rank}/res{c.Resist}/pierce{c.Pierce}:applied={result.Applied},damage={result.Damage256},HP={before-SkillHealth.Balance256(target)},expected={c.Damage*256},casterMap={owner.Map},targetMap={target.Map}");
            Require(target.FireResistance==c.Resist&&NecromancerEffects.Curse(target)==curse
                &&NamedElements.Resolve256(owner,target,100*256,"fire",enhance:false,pierceOverride:0).Damage256==Math.Max(0,100-c.Resist)*256,
                "Per-hit resistance reduction leaked into native stats,curse or later spell");
        }
        target.SetResistance(ResistanceType.Fire,0);int life=SkillHealth.Balance256(target);
        Require(!ApocalypseImpact.ReleasePrepared(owner,target,25600,1,false).Applied&&SkillHealth.Balance256(target)==life,"Temporary allocation refusal still damaged");
        Require(ApocalypseImpact.ReleasePrepared(owner,target,0,1).Damage256==0&&SkillHealth.Balance256(target)==life,"Absent fire component generated damage");
        // Existing skill reductions have already joined the aggregate pool
        // when Apocalypse chooses full versus one-fifth effectiveness.
        var previous=NecromancerEffects.Curse(target);
        var lower=new NecromancerCurse(CombatAbility.LowerResist,owner.Serial.Value,1,TimedEffects.Now.AddMinutes(3),target.X,target.Y);
        NecromancerEffects.Profiles.Store(target,new(Curse:lower));target.SetResistance(ResistanceType.Fire,100);
        int reduced=NamedElements.Resistance(owner,target,"fire",100,0);Require(reduced<100&&reduced>5,"Lower Resist pre-existing pool control");
        int combined=NamedElements.Resistance(owner,target,"fire",100,0,ApocalypseImpact.Reduction(1));
        Require(combined==reduced-5&&NecromancerEffects.Curse(target)==lower&&target.FireResistance==100,"Apocalypse rescaled existing curse or used original immunity after it broke");
        NecromancerEffects.Profiles.Store(target,new(Curse:previous));target.SetResistance(ResistanceType.Fire,0);
        Require(!WarlockCombat.Implemented(CombatAbility.Apocalypse),"Prepared fire receiver admitted unfinished Apocalypse launch/area packet");
    }
    private static void Check()
    {
        Source();var owner=ClassSkillChecks.Player(DiabloClass.Warlock);FixtureSpace.Corridor(owner,6400,30,2700);var target=FireBoltChecks.Target(owner,6);
        var curse=new NecromancerCurse(CombatAbility.Weaken,owner.Serial.Value,1,TimedEffects.Now.AddMinutes(3),target.X,target.Y);
        NecromancerEffects.Profiles.Store(target,new(Curse:curse));Impacts(owner,target);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,target.Serial.Value,curse,owner.Location,owner.Map.MapID,0)));
        Log.Information("Apocalypse receiver:142 current resistance leaf comparisons/255 pinned penalties;actual HP,temporary-only fire reduction,full/one-fifth immunity,break-before-item-pierce,zero/allocation controls and existing curse coexistence. Enhanced packet/area/launch remain caller-owned;no learned admission.");
    }
    private static void Reload()
    {
        Source();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=(BaseCreature)World.FindMobile((Serial)saved.Target);
        // Native offline players load internalized;restore this fixture's
        // original world pose before asking the hostile-target predicate.
        owner.MoveToWorld(saved.OwnerPoint,Map.Maps[saved.Map]);
        Require(saved.Generation<2&&target.FireResistance==0&&NecromancerEffects.Curse(target)==saved.Curse
            &&NamedElements.Resolve256(owner,target,25600,"fire",enhance:false,pierceOverride:0).Damage256==25600,"Reload retained an Apocalypse reduction or replaced ordinary curse");
        Impacts(owner,target);File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
}
