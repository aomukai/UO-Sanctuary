// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class DeathMarkDebuffChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(DeathMarkDebuffChecks));
    private sealed record Saved(uint Owner,uint Target,DeathMarkDebuff State,NecromancerCurse Curse,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"death-mark-debuff-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        Register("death-mark-debuff-check",Check);
        Register("death-mark-debuff-reload-check",Reload);
    }
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Proposed Death Mark signed stats and natural saved deadline",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Numeric()
    {
        for(int rank=1;rank<=255;rank++)
        {
            var values=new SkillValues(SkillMechanics.For(CombatAbility.DeathMark),_=>rank,_=>rank,99);
            Require(values.Eval("aurastatcalc1")==-50-35*(rank-1)&&values.Eval("aurastatcalc2")==-5-2*(rank-1)
                &&values.Eval("aurastatcalc3")==-5-2*(rank-1)&&values.Eval("auralencalc")==DeathMarkDebuffs.DurationFrames(rank),"Pinned signed stat/duration formula");
        }
        Require(GemCombat.Reduction256(5,128)==160&&GemCombat.Reduction256(5,0)==1280
            &&GemCombat.Reduction256(-5,128)==-1280&&GemCombat.Reduction256(-5,4096)==-1280
            &&GemCombat.Reduction256(int.MaxValue,int.MaxValue)==int.MaxValue&&GemCombat.Reduction256(int.MinValue,128)==int.MinValue,
            "Positive reduction scaling or proposed negative-total fixed point changed");
        Require(NamedElements.Reduce256(int.MaxValue,0,int.MinValue,0,0).Damage256==int.MaxValue,"Signed reduction overflowed final clamp");
    }
    private static void Damage(PlayerMobile owner,BaseCreature target,int penalty)
    {
        foreach(string element in new[]{"physical","fire","cold","ltng","mag"})
        {
            Require(NamedElements.Resolve256(owner,target,25600,element,0,false,128).Damage256==(100+penalty)*256,"Matching component omitted signed flat penalty: "+element);
            Require(NamedElements.Resolve256(owner,target,25600,element,50,false,128).Damage256==(100+penalty)*128,"Penalty applied after resistance: "+element);
            Require(NamedElements.Resolve256(owner,target,0,element,0,false)==default(ElementReduction),"Absent component generated damage: "+element);
            Require(NamedElements.Resolve256(owner,target,25600,element,100,false)==default(ElementReduction),"Flat penalty bypassed immunity: "+element);
        }
        Require(NamedElements.Resolve256(owner,target,25600,"pois",0,false).Damage256==25600,"Flat magic reduction changed poison");
        Require(GemCombat.Component(target,0,0,false)==0&&GemCombat.Component(target,0,0,true)==0,"Legacy route generated an absent component");
        target.SetResistance(ResistanceType.Fire,0);target.SetResistance(ResistanceType.Physical,0);
        Require(GemCombat.ResistedDamage(target,100,0,100,0,0,0)==100+penalty,"Native damage hook generated absent physical/cold/lightning components");
        Require(GemCombat.ResistedDamage(target,100,50,50,0,0,0)==100+2*penalty,"Native mixed hit failed per-component signed reduction");
    }
    private static void Check()
    {
        Numeric();var owner=ClassSkillChecks.Player(DiabloClass.Warlock);FixtureSpace.Corridor(owner,6400,40,3300);
        var other=ClassSkillChecks.Player(DiabloClass.Warlock);other.MoveToWorld(new(owner.X,owner.Y+1,owner.Z),owner.Map);
        var target=PoisonDaggerChecks.Target(owner);var at=TimedEffects.Now;
        int defense=ClassStats.Defense(target);
        Require(!DeathMarkDebuffs.ApplyPrepared(owner,owner,1,at)&&DeathMarkDebuffs.ApplyPrepared(owner,target,1,at),"Prepared recipient admission");
        var first=DeathMarkDebuffs.State(target);
        Require(first.UntilUtc==at.AddSeconds(5)&&DeathMarkDebuffs.State(target,first.UntilUtc)==null
            &&ClassStats.Defense(target)==Math.Max(0,defense-50),"Natural deadline or actual armor hook");
        Damage(owner,target,5);
        Require(DeathMarkDebuffs.ApplyPrepared(other,target,1,at.AddMilliseconds(40))&&DeathMarkDebuffs.State(target).Creator==owner.Serial.Value,"Equal rank changed original creator");
        Require(DeathMarkDebuffs.ApplyPrepared(other,target,20,at)&&DeathMarkDebuffs.State(target).Creator==other.Serial.Value,"Stronger mark failed replacement");
        var stronger=DeathMarkDebuffs.State(target);
        Require(!DeathMarkDebuffs.ApplyPrepared(owner,target,19,at)&&DeathMarkDebuffs.State(target)==stronger,"Weaker mark changed active state");
        Damage(owner,target,43);
        var sight=new SkillValues(SkillMechanics.For(CombatAbility.InnerSight),_=>1,_=>1,99);
        Require(AmazonInnerSight.Apply(owner,target,sight,at)&&AmazonInnerSight.Defense(target,1000,100)==490,"Armor/Inner Sight/common percentage ordering");
        AmazonInnerSight.Clear(target);
        var curse=new NecromancerCurse(CombatAbility.Weaken,owner.Serial.Value,1,at.AddMinutes(3),target.X,target.Y);
        NecromancerEffects.Profiles.Store(target,new(Curse:curse));
        Require(DeathMarkDebuffs.ApplyPrepared(owner,target,255,at)&&NecromancerEffects.Curse(target)==curse&&!PaladinCleansing.IsCurse("death_mark"),"Independent noncurse state replaced ordinary curse");
        var state=DeathMarkDebuffs.State(target);var image=DeathMarkDebuffs.Snapshot();bool refused=false;
        try{DeathMarkDebuffs.Restore(image.Concat([state]).ToArray());}catch(InvalidDataException){refused=true;}
        Require(refused&&DeathMarkDebuffs.State(target)==state,"Malformed restore partially replaced active states");
        var expired=PoisonDaggerChecks.Target(owner);
        DeathMarkDebuffs.Restore(image.Append(state with{Target=expired.Serial.Value,UntilUtc=at.AddSeconds(-1)}).ToArray());DeathMarkDebuffs.Prune();
        Require(DeathMarkDebuffs.State(expired)==null&&!DeathMarkDebuffs.Snapshot().Any(x=>x.Target==expired.Serial.Value),"Expired state survived prune");expired.Delete();
        Require(!WarlockCombat.Implemented(CombatAbility.DeathMark),"Prepared proposal admitted learned cast");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,target.Serial.Value,state,curse,0)));
        Log.Information("Death Mark proposal:255 pinned rank formulas;signed flat penalties before resistance,positive scaling controls,no absent components/poison changes;actual armor and damage hooks,independent curse,rank priority,atomic restore. Natural rank255 deadline saved without retention hold.");
    }
    private static void Reload()
    {
        Numeric();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        var owner=(PlayerMobile)World.FindMobile((Serial)saved.Owner);var target=(BaseCreature)World.FindMobile((Serial)saved.Target);
        Require(saved.Generation<2&&DeathMarkDebuffs.State(target)==saved.State&&NecromancerEffects.Curse(target)==saved.Curse,"Actual reload reset state/creator/rank/deadline/ordinary curse");
        Damage(owner,target,513);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
}
