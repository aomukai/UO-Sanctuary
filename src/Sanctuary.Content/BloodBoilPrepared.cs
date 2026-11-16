// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record BloodBoilNumbers(int Rank,int Mana256,int Radius,int DelayFrames,int CostOne,int CostTwo,int CostThree,
    int PhysicalMinimum256,int PhysicalMaximum256,int FireMinimum256,int FireMaximum256);
internal sealed record BloodBoilBurst(uint Demon,Point3D Center,int LifePaid256,int Physical256,int Fire256,uint[] Recipients);
internal sealed record BloodBoilRelease(bool Released,int ManaPaid256,int DemonCount,ulong InitialSeed,ulong FinalSeed,BloodBoilBurst[] Bursts,string Fault=null);

// Approved Sanctuary world rule,not recovered do160. All borrowed recipient,
// health,roll,area and spell services keep their original implementation.
internal static class BloodBoilPrepared
{
    internal static bool Approved=>true;
    internal static Func<int> SeedLow=()=>SorceressSpellMissiles.SeedLow();
    private static readonly Dictionary<(uint Owner,uint Target),DateTime> Delays=new();
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BloodBoilPrepared));
    internal static BloodBoilNumbers Numbers(SkillValues values)
    {
        ArgumentNullException.ThrowIfNull(values);var row=SkillMechanics.For(CombatAbility.BloodBoil);
        if(values.Ability!=CombatAbility.BloodBoil||values.Rank is<1 or>255||row.SourceId!=380||row.Number("srvdofunc")!=160
            ||row.Number("srvstfunc")!=0||row.Text("pettype")!="demon"||row.Number("requirespettype")!=1||row.Text("EType")!="fire"
            ||row.Number("calc4")!=3||row.Number("localdelay")!=0)
        {throw new NotSupportedException("Unqualified Blood Boil supplier context");}
        int Eval(string key)=>checked((int)values.Eval(key));
        return new(values.Rank,values.Mana256,Eval("aurarangecalc"),Eval("calc4"),Eval("calc1"),Eval("calc2"),Eval("calc3"),
            values.PhysicalDamage256(false),values.PhysicalDamage256(true),values.Damage256(false),values.Damage256(true));
    }
    internal static int CostPercent(BloodBoilNumbers numbers,int demons)=>demons switch
    {1=>numbers.CostOne,2=>numbers.CostTwo,3=>numbers.CostThree,_=>throw new NotSupportedException("Blood Boil has only three qualified demon-count formulas")};
    // Same active/range rule as Engorge;original native bound NPCs keep their
    // own identity/AI rather than pretending to have a summon controller.
    internal static BaseCreature[] Roster(PlayerMobile owner)=>WarlockDemonRecipients.Roster(owner).Where(b=>WarlockDemonRecipients.InRange(owner,b,19)).ToArray();
    internal static bool HasLife(BaseCreature body)=>body is{Alive:true,Deleted:false}
        &&(long)SkillHealth.Balance256(body)*100>(long)SkillHealth.Maximum256(body)*5;
    internal static bool Ready(PlayerMobile owner,uint target,DateTime at)=>!Delays.TryGetValue((owner.Serial.Value,target),out var until)||until<=at;
    internal static int PayLife(BaseCreature body,int requested256)
    {
        if(body is not{Alive:true,Deleted:false}||requested256<0){throw new ArgumentException("Invalid Blood Boil life cost");}
        int before=SkillHealth.Balance256(body),after=Math.Max(0,before-requested256);if(after<256){after=0;}
        // Resource cost,not an attack:do not pass through guardian friendship,
        // native OnDamage,retaliation,mitigation,leech or reflected damage.
        SkillHealth.SetBalance256(body,after);CombatDamage.Finish(body,body);return before-after;
    }
    internal static BloodBoilRelease ReleasePrepared(PlayerMobile owner,DateTime at)
    {
        var empty=new BloodBoilRelease(false,0,0,0,0,[]);
        if(!BloodBoilCasting.Actor(owner)){return empty;}
        if(at.Kind!=DateTimeKind.Utc||at<=DateTime.UnixEpoch||at>DateTime.MaxValue.AddMilliseconds(-120))
        {throw new ArgumentException("Blood Boil needs a bounded UTC release clock");}
        var roster=Roster(owner);if(roster.Length==0||!roster.Any(HasLife)){return empty;}
        var numbers=Numbers(SkillMechanics.Values(owner,CombatAbility.BloodBoil));int percent=CostPercent(numbers,roster.Length);
        if(!NecromancerEffects.CanPay(owner,numbers.Mana256)){return empty;}
        foreach(var key in Delays.Where(p=>p.Value<=at).Select(p=>p.Key).ToArray()){Delays.Remove(key);}
        // Preflight ordinary area recipients before consuming resources. The
        // actual per-explosion snapshot is rechecked after earlier callbacks.
        var castMap=owner.Map;
        BaseCreature[] Nearby(Point3D center)=>SkillCombat.NearbySnapshot<BaseCreature>(castMap,center,(numbers.Radius+1)/2)
            .Where(t=>t is{Alive:true,Deleted:false,Blessed:false}&&SourceDamageAreas.Recipient(owner,t,castMap.MapID,center.X*2,center.Y*2,center.Z,numbers.Radius,true,center))
            .OrderBy(t=>t.Serial.Value).ToArray();
        try{foreach(var body in roster.Where(HasLife)){foreach(var t in Nearby(body.Location)){TaintedFireBallMissiles.RequireOrdinaryRecipient(owner,t);}}}
        catch(NotSupportedException e){Log.Warning("Blood Boil recipient preflight refused: {Reason}",e.Message);return empty with{Fault=e.Message};}
        ulong seed=SourceRandom.InitLow(SeedLow()),initial=seed;var bursts=new List<BloodBoilBurst>();
        if(!NecromancerEffects.Pay(owner,numbers.Mana256)){return empty;}
        try
        {
            foreach(var body in roster)
            {
                if(!BloodBoilCasting.Actor(owner)||owner.Map!=castMap){throw new NotSupportedException("Blood Boil owner retired or changed facet during committed explosions");}
                if(!WarlockDemonRecipients.InRange(owner,body,19)||!HasLife(body)){continue;}
                var center=body.Location;var map=body.Map;int cost=checked((int)((long)SkillHealth.Maximum256(body)*percent/100));
                int physical=SorceressSpellMissiles.Roll(numbers.PhysicalMinimum256,numbers.PhysicalMaximum256,seed,out var next);seed=next;
                int fire=SorceressSpellMissiles.Roll(numbers.FireMinimum256,numbers.FireMaximum256,seed,out next);seed=next;
                int paid=PayLife(body,cost);var recipients=new List<uint>();
                // The captured center/packet survives its own lethal cost.
                foreach(var target in Nearby(center))
                {
                    if(!BloodBoilCasting.Actor(owner)||owner.Map!=castMap){throw new NotSupportedException("Blood Boil owner retired during recipient callbacks");}
                    if(!Ready(owner,target.Serial.Value,at)){continue;}TaintedFireBallMissiles.RequireOrdinaryRecipient(owner,target);
                    Delays[(owner.Serial.Value,target.Serial.Value)]=at.AddMilliseconds(40L*numbers.DelayFrames);
                    SkillCombat.HitParts(owner,target,new(physical/256.0,fire/256.0,0,0,0),true,spell:true);
                    recipients.Add(target.Serial.Value);
                }
                bursts.Add(new(body.Serial.Value,center,paid,physical,fire,recipients.ToArray()));
                Effects.SendLocationEffect(center,map,0x36BD,10); // Identified shared explosion placeholder.
            }
            owner.PlaySound(0x1E5); // Identified shared cast placeholder.
            return new(true,numbers.Mana256,roster.Length,initial,seed,bursts.ToArray());
        }
        catch(Exception e)when(e is NotSupportedException or System.IO.InvalidDataException)
        {
            Log.Warning("Blood Boil refused unsupported committed context: {Reason}",e.Message);
            return new(true,numbers.Mana256,roster.Length,initial,seed,bursts.ToArray(),e.Message);
        }
    }
}
