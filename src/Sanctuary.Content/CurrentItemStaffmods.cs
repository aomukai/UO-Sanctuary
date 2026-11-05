// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
namespace Sanctuary;

// Reuses all current raw skill rows/type hierarchy and the item-owned graph.
// Current required-level grouping and approved stable ID ordering replace
// only the older fixed-five candidate mapping,including Barbarian/Warlock.
internal sealed class CurrentItemStaffmods:ItemStaffmodServices
{
    private static readonly string[] Classes=["ama","sor","nec","pal","bar","dru","ass","war"];
    private static readonly int[] Levels=[1,6,12,18,24,30];
    private static readonly Lazy<Dictionary<string,int[][]>> Groups=new(()=>
    {
        var result=new Dictionary<string,int[][]>();
        foreach(string c in Classes)
        {
            var rows=ItemSkillExecution.All.Where(r=>r.Text("charclass")==c).ToArray();
            var ids=Levels.Select(level=>rows.Where(r=>r.Number("reqlevel")==level).Select(r=>r.Id).Order().ToArray()).ToArray();
            if(rows.Length!=30||ids.Any(t=>t.Length==0)||ids.Sum(t=>t.Length)!=30)
            {throw new InvalidDataException("Current staffmod class needs its complete required-level tiers");}
            result.Add(c,ids);
        }
        return result;
    });
    private static readonly Lazy<Dictionary<int,StaffmodSkill>> Records=new(()=>
    {
        var types=CurrentItemFinishing.Catalogue.Types.ToDictionary(t=>t.Code,t=>t.Ordinal);
        return ItemSkillExecution.All.ToDictionary(r=>r.Id,r=>new StaffmodSkill(r.Text("itypea1")is"0"or""?0:types[r.Text("itypea1")]));
    });
    internal static int RequireClass(string label)
    {
        int c=Array.IndexOf(Classes,label);
        if(c<0){throw new NotSupportedException("Current staffmod class mapping remains unbound: "+label);}
        _=Groups.Value;_=Records.Value;return c;
    }
    private readonly ItemPropertyCreationContext Graph;
    private readonly ItemFinishingParameters Parameters;
    private readonly int Class;
    internal CurrentItemStaffmods(ItemPropertyCreationContext graph,ItemFinishingParameters parameters,string label)
    {
        ArgumentNullException.ThrowIfNull(graph);ArgumentNullException.ThrowIfNull(parameters);
        Class=RequireClass(label);Graph=graph;Parameters=parameters;
        var type=NpcItemAffixes.Pool.Basis(parameters.Code).Type;
        if(CurrentItemFinishing.Catalogue.Types.Single(t=>t.Code==type).StaffMods!=label)
        {throw new NotSupportedException("Staffmod class must come from the current item's primary type");}
        if(graph.Seed==0||parameters.Level is<1 or>787||parameters.Format!=100)
        {throw new NotSupportedException("Current Reign staffmods need initialized item RNG,format100 and source item level");}
    }
    internal override int ClassId=>Class;
    internal override int SkillCount(int c)=>Groups.Value[Classes[c]].Sum(t=>t.Length);
    internal override int FirstSkill(int c)=>Groups.Value[Classes[c]].SelectMany(t=>t).Min();
    internal void Apply()=>SourceItemStaffmods.ModernByTier(this,ItemLevel,(DropFlags&0x20)!=0?ItemLevel:0,(tier,draw)=>
    {
        var ids=Groups.Value[Classes[Class]][tier-1];return ids[draw%(uint)ids.Length];
    });
    internal override int Format=>Parameters.Format;
    internal override int Quality=>Parameters.Quality;
    internal override int ItemLevel=>Parameters.Level;
    internal override uint DropFlags=>Parameters.Flags2;
    internal override uint Random()=>Graph.Random();
    internal override StaffmodSkill Skill(int id)=>Records.Value.GetValueOrDefault(id);
    internal override bool IsType(int type)=>CurrentItemFinishing.Catalogue.Types.FirstOrDefault(t=>t.Ordinal==type)is{} t&&NpcItemAffixes.Pool.IsType(Parameters.Code,t.Code);
    internal override bool List()=>Graph.GetOrCreateList(0,0x40);
    internal override void Write(int skill,int value)=>Graph.SetList(107,value,skill);
}
