// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
namespace Sanctuary;

internal sealed record DefilerLinkShape(int Rank,int SelfRank,int RawSharedPercent,int RawMaximumLinks,int PerCast,int DurationFrames,int RadiusCells);

// Exact table suppliers,not a do162/event38 implementation. In particular,
// high-rank callback caps must not be inferred from a familiar dm curve.
internal static class DefilerLinkNumbers
{
    internal static readonly string PercentFormula="((110 *  (skill('Summon Defiler'.lvl))) * (par2 - par1)) / (100 * ((skill('Summon Defiler'.lvl))+ 6)) + par1";
    internal static readonly string LimitFormula="((110 *skill('Summon Defiler'.lvl) * (par4 - par3)) / (100 * (skill('Summon Defiler'.lvl) + 6)) + par3";
    private static readonly Lazy<SkillDefinition> Row=new(()=>
    {
        var r=BalanceCatalog.Read<SkillDefinition>("defiler-health-link").Single();
        if(r.SourceId!=405||r.SourceKey!="Health Link"||r.Number("srvstfunc")!=70||r.Number("srvdofunc")!=162
            ||r.Text("auraevent1")!="absorbdamage"||r.Number("auraeventfunc1")!=38||r.Text("monanim")!="S1"
            ||r.Text("aurastate")!="healthlinkcaster"||r.Text("auratargetstate")!="healthlink"
            ||r.Text("calc1")!=PercentFormula||r.Text("calc2")!=LimitFormula||r.Text("calc3")!="\"min(5,(lvl+1))\""
            ||r.Number("auralencalc")!=500||r.Number("aurarangecalc")!=25||r.Number("aurafilter")!=33027
            ||r.Parameter(1)!=20||r.Parameter(2)!=60||r.Parameter(3)!=2||r.Parameter(4)!=6)
        {throw new NotSupportedException("Unknown pinned Defiler Health Link suppliers");}
        return r;
    });
    internal static DefilerLinkShape Capture(int assignedRank,int assignedSelfRank,int command=0)
    {
        int rank=InnateNpcSkills.Level(assignedRank,command),self=InnateNpcSkills.Level(assignedSelfRank,command);var row=Row.Value;
        int Curve(int first,int second)=>checked(110*self*(row.Parameter(second)-row.Parameter(first)))/(100*(self+6))+row.Parameter(first);
        return new(rank,self,Curve(1,2),Curve(3,4),Math.Min(5,rank+1),row.Number("auralencalc"),row.Number("aurarangecalc"));
    }
    internal static DefilerLinkShape Capture(WarlockDefilerBody body)
    {
        var s=body?.Prepared?.Snapshot.Birth.Suppliers??throw new NotSupportedException("Health Link needs its captured Defiler");
        return Capture(s.HealthLinkRank,s.SelfSkillRank,BattleCommand.Bonus(body));
    }
}
