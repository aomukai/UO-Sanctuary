// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class SigilLethargyChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(SigilLethargyChecks));
    private sealed record Saved(uint Owner,uint Target,SigilActor Actor,LethargyStatus Status,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"sigil-lethargy-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static SkillValues Values(int rank)=>new(SkillMechanics.For(CombatAbility.SigilLethargy),k=>k=="Sigil Lethargy"?rank:0,_=>0,99);
    public static void Configure()
    {
        foreach(bool reload in new[]{false,true})
        {
            string name=reload?"sigil-lethargy-reload-check":"sigil-lethargy-check";bool again=reload;
            ConsoleInputHandler.RegisterCommand(name,"Lethargy independent recipient stats/source/UTC/saves",_=>Core.LoopContext.Post(()=>
            {
                Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",network=>
                {try{if(again){Reload(network);Log.Information("{Name}: passed",name+"-result");}else{Check(network,name+"-result");}}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});
            }));
        }
    }
    private static void Source()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"sigil-lethargy-cases.json")));int n=0;
        foreach(var row in json.RootElement.GetProperty("cases").EnumerateArray())
        {
            int[] input=row.GetProperty("input").EnumerateArray().Select(x=>x.GetInt32()).ToArray();int[] output=row.GetProperty("output").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            var old=input[1]==0?null:new LethargyList(input[1],999,777,-17,-900,-23);
            var r=SigilLethargyOperation.Apply(input[2],9,100,old);var s=r.List;
            Require(new[]{r.Accepted?1:0,s.Rank,(int)s.Creator,s.Expiry,s.Attack,s.Defense,s.Velocity,(int)s.Flags,r.Removed,r.Events,r.Updates,1}.SequenceEqual(output)
                &&r.Writes.SequenceEqual(row.GetProperty("writes").EnumerateArray().Select(x=>x.GetInt32())),"Lethargy source non-curse/rank/creator/primary/secondary/list flags differ");n++;
        }
        Require(n==128,"Missing focused Lethargy source cases");
    }
    private static void Check(NetState network,string result)
    {
        Source();var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.AddItem(new Backpack());ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"warlock.sigillethargy",1);
        FixtureSpace.Corridor(p,6840,24,3520);FixtureNetwork.Authenticate(network,p,"Lethargy");
        var target=PoisonDaggerChecks.Target(p);target.MoveToWorld(new(p.X+3,p.Y,p.Z),p.Map);
        var at=TimedEffects.Now;var actor=SigilActors.CreatePrepared(p,Values(1),target.Location,at);var marker=World.FindItem((Serial)actor.Marker);
        int defense=ClassStats.Defense(target),hp=target.Hits,mana=NecromancerEffects.ManaBalance256(p);Require(defense>0,"Defense fixture has no base pool");
        var weaken=new NecromancerCurse(CombatAbility.Weaken,p.Serial.Value,1,at.AddMinutes(1),p.X,p.Y);
        NecromancerEffects.Profiles.Store(target,new(Curse:weaken));
        SigilActors.Tick(at);
        Require(SigilLethargy.State(target)is{Rank:1}status&&status.Creator==actor.Marker&&status.UntilUtc==at.AddMilliseconds(480)
            &&NecromancerEffects.Curse(target)==weaken&&SigilLethargy.Attack(target)==-50&&SigilLethargy.Velocity(target)==-50&&SigilLethargy.Defense(target)==-33,"Pulse missed independent Lethargy stats or overwrote ordinary curse");
        Require(ClassStats.Defense(target)==ClassStats.ScaleDefense(defense,-33)&&GearEffects.AttackSlowTotal(target)==50&&GearEffects.MovementSlowTotal(target)==50
            &&SourceAttackRate.WolfRate(target)==50&&Math.Abs(ClassGearHooks.MovementDelay(target,1)-2)<0.0001
            &&ClassGearHooks.AttackDelay(target,TimeSpan.FromSeconds(1))==TimeSpan.FromSeconds(2),"Actual defense/movement/attack-rate consumers missed Lethargy");
        var attract=weaken with{Ability=CombatAbility.Attract};NecromancerEffects.Profiles.Store(target,new(Curse:attract));
        var other=new Static(0x1B7A){Movable=false};other.MoveToWorld(new(p.X+4,p.Y,p.Z),p.Map);
        Require(SigilLethargy.Apply(p,other,target,20,at)&&SigilLethargy.State(target)is{Rank:20}strong&&strong.Creator==other.Serial.Value&&NecromancerEffects.Curse(target)==attract,"Attract blocked non-curse state or higher-rank replacement failed");
        var before=SigilLethargy.State(target);Require(!SigilLethargy.Apply(p,marker,target,1,at.AddMilliseconds(40))&&SigilLethargy.State(target)==before,"Weaker rank changed stronger state");
        Require(SigilLethargy.Apply(p,marker,target,20,at.AddMilliseconds(40))&&SigilLethargy.State(target).Creator==other.Serial.Value
            &&SigilLethargy.State(target).UntilUtc==at.AddMilliseconds(520),"Equal-rank refresh changed original creator or duration");
        NecromancerEffects.Profiles.Store(target,new());
        var ward=new GrimWardCursePacket(1,60,6,-20,-20,-20,-20);
        Require(GrimWardCurseTransport.ApplyPacket(p,marker,target,ward,at)&&GearEffects.AttackSlowTotal(target)==70&&GearEffects.MovementSlowTotal(target)==70
            &&Math.Abs(ClassGearHooks.MovementDelay(target,1)-100.0/30)<0.0001,"Ward and Lethargy velocity did not add before the common source floor");
        NecromancerEffects.ClearOrdinaryCurse(target);Require(SigilLethargy.State(target)!=null,"Ordinary curse clearing removed non-curse Lethargy");
        target.Controlled=true;target.ControlMaster=p;Require(!SigilLethargy.Apply(p,marker,target,255,at),"Friendly pet admitted as hostile recipient");target.ControlMaster=null;target.Controlled=false;
        var snap=SigilLethargy.Snapshot();SigilLethargy.Restore(snap);Require(SigilLethargy.State(target)==snap.Single(s=>s.Target==target.Serial.Value),"Immediate original status roundtrip changed state");
        bool invalid=false;try{SigilLethargy.Restore([snap.Single(s=>s.Target==target.Serial.Value)with{Creator=p.Serial.Value}]);}catch(InvalidDataException){invalid=true;}
        Require(invalid&&SigilLethargy.State(target)!=null,"Malformed creator replaced live statuses");
        SigilActors.Clear(p);other.Delete();target.MoveToWorld(new(p.X+16,p.Y,p.Z),p.Map);
        Require(target.Hits==hp&&NecromancerEffects.ManaBalance256(p)==mana,"Recipient effects dealt damage or paid player mana");
        Timer.StartTimer(TimeSpan.FromMilliseconds(650),()=>Core.LoopContext.Post(()=>
        {
            try
            {
                Require(SigilLethargy.State(target)==null&&SigilLethargy.Attack(target)==0&&SigilLethargy.Defense(target)==0&&GearEffects.AttackSlowTotal(target)==0
                    &&ClassStats.Defense(target)==defense,"Natural12frame state expiry retained modifiers");SigilLethargy.Prune();
                var point=new Point3D(p.X+3,p.Y,p.Z);target.MoveToWorld(point,p.Map);var saved=SigilActors.CreatePrepared(p,Values(200),point,TimedEffects.Now);SigilActors.Tick(saved.BornUtc);
                Require(SigilLethargy.State(target)is{Rank:200},"New original high-rank pulse did not deliver recipient effect");
                target.MoveToWorld(new(p.X+16,p.Y,p.Z),p.Map);
                // Normal480ms expiry was tested above. Hold only this inspected
                // payload so its original identity can cross slow native boots.
                var held=SigilLethargy.State(target)with{UntilUtc=TimedEffects.Now+FixtureSpace.Retention};
                SigilLethargy.Restore(SigilLethargy.Snapshot().Where(s=>s.Target!=target.Serial.Value).Append(held).ToArray());
                File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(p.Serial.Value,target.Serial.Value,saved,held,0)));
                Require(WarlockCombat.Implemented(CombatAbility.SigilLethargy),"Verified recipient lost learned admission");
                Log.Information("Lethargy:128complete receiver/factory cases;real pulse and independent ordinary-curse/Attract coexistence,rank/creator retention,defense/movement/attack hooks,Ward additive slow,friendly refusal,normal12frame expiry and atomic status restore pass. Inspection-only held payload and original marker saved for reloads;learned input has separate acceptance checks.");
                Log.Information("{Name}: passed",result);
            }
            catch(Exception e){Log.Error(e,"{Name}: failed",result);}
        }));
    }
    private static void Reload(NetState network)
    {
        Source();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;var target=World.FindMobile((Serial)saved.Target)as BaseCreature;
        Require(saved.Generation<2&&p!=null&&target!=null&&SigilLethargy.State(target)==saved.Status&&SigilLethargy.Attack(target)==-50
            &&SigilLethargy.Defense(target)==-232&&World.FindItem((Serial)saved.Actor.Marker)is{Deleted:false},"Original held recipient/creator/rank/expiry or marker lost on reload");
        FixtureNetwork.Authenticate(network,p,"LethargyReload");p.MoveToWorld(new(saved.Actor.Origin.X-3,saved.Actor.Origin.Y,saved.Actor.Origin.Z),Map.Maps[saved.Actor.Map]);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
    }
}
