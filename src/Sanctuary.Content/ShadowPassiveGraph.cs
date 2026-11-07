// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
namespace Sanctuary;

internal sealed record ShadowPassiveList(int Skill,int State,int Level,EquipmentStatValue[] Values)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public DateTime? UntilUtc{get;init;}
}
internal sealed record ShadowPassiveAttachment(uint Item,int State)
{[JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)]public bool Birth{get;init;}}
internal sealed record ShadowPassiveFrame(string Book,ShadowPassiveList[] Lists,ShadowPassiveAttachment[] Order)
{
    public string Profile{get;init;}="shadow-passive-lists-v1";
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingDefault)]public bool ItemSkillCallbacks{get;init;}
}

// Actual posted source stat lists. Only the two current Assassin passives are
// admitted. Their fields have no value operations/dependents/resource effects;
// restoring saved links after numeric replay does not replay item callbacks.
internal sealed class ShadowPassiveGraph
{
    private readonly GeneratedNpcStatGraph Graph;
    private readonly Func<int> Command;
    private readonly Dictionary<int,EquipmentStatNode> Lists=new();
    private readonly Dictionary<int,DateTime> Timed=new();
    private ShadowSkillBook Book;
    private bool Refreshing;
    private readonly bool ItemSkillCallbacks;
    internal int Writes{get;private set;}
    internal int GraphicsNotifications{get;private set;}
    private ShadowPassiveGraph(GeneratedNpcStatGraph graph,ShadowSkillBook book,Func<int> command,bool itemSkillCallbacks=false)
    {Graph=graph;Book=book;Command=command;ItemSkillCallbacks=itemSkillCallbacks;}
    private static (SkillDefinition Definition,ShadowSkillMetadata Metadata,int[] Stats) Row(int id)
    {
        int[] stats=id switch{252=>[342,343,344],263=>[348],_=>throw new NotSupportedException("Unknown Shadow passive skill")};
        var d=SkillMechanics.All.Single(s=>s.SourceId==id);var row=ShadowSkillBook.Metadata(id);
        if(!d.Passive||row.PassiveType!=68||row.PassiveState!=(id==252?152:155)||d.Text("aurastate")!="0")
        {throw new NotSupportedException("Unsupported current Shadow passive state/type/aura");}
        for(int i=1;i<=14;i++)
        {
            string name=d.Text("passivestat"+i);
            if(i<=stats.Length?name!=EquipmentStatValues.Definition(stats[i-1]).Name:name!="0")
            {throw new NotSupportedException("Current Shadow passive slots changed");}
        }
        foreach(int stat in stats.Concat(new[]{350,351}))
        {
            var cost=EquipmentStatValues.Definition(stat);
            if(cost.Flags!=0||cost.Operation!=0||cost.HasOperations||cost.BaseOfOther||cost.AppliesToItem||cost.Dependents.Length!=0)
            {throw new NotSupportedException("Passive replay requires independent plain numeric fields");}
        }
        return(d,row,stats);
    }
    private static int Value(SkillDefinition d,int rank,int slot)
        =>checked((int)new SkillValues(d,key=>key==d.SourceKey?rank:throw new NotSupportedException("Unqualified passive skill dependency"),
            _=>throw new NotSupportedException("Unqualified passive hard-point dependency"),1).Eval("passivecalc"+slot));
    private static IEnumerable<int> PassiveIds(ShadowSkillBook book)=>book.Snapshot().Entries
        .Where(s=>ShadowSkillBook.Metadata(s.Id).PassiveState>0).Select(s=>s.Id);
    internal bool State(int state)=>Timed.TryGetValue(state,out var until)?until>TimedEffects.Now:PassiveIds(Book).Any(id=>Row(id).Metadata.PassiveState==state);
    internal static ShadowPassiveFrame Copy(ShadowPassiveFrame frame)=>frame==null?null:frame with
    {Lists=frame.Lists.Select(s=>s with{Values=(EquipmentStatValue[])s.Values.Clone()}).ToArray(),Order=(ShadowPassiveAttachment[])frame.Order.Clone()};
    internal static void Validate(ShadowPassiveFrame frame,uint[] active)
    {
        if(frame?.Profile!="shadow-passive-lists-v1"||frame.Lists==null||frame.Order==null||frame.Lists.Length>3
            ||frame.Lists.Any(l=>l==null)||frame.Order.Any(a=>a==null)||frame.Lists.Select(l=>l.State).Distinct().Count()!=frame.Lists.Length)
        {throw new InvalidDataException("Invalid Shadow passive frame");}
        var book=ShadowSkillBook.Restore(frame.Book);var states=new HashSet<int>();
        foreach(int id in PassiveIds(book)){_=Row(id);}
        foreach(var list in frame.Lists)
        {
            if(list.UntilUtc.HasValue)
            {
                ShadowDefensiveBuffs.Validate(list);
                if(frame.Lists.Count(l=>l.UntilUtc.HasValue)>1){throw new InvalidDataException("Shadow group2 buffs must replace each other");}
                states.Add(list.State);continue;
            }
            var row=Row(list.Skill);
            if(list.State!=row.Metadata.PassiveState||book.Find(list.Skill)==null||list.Level is<1 or>99||list.Values==null
                ||list.Values.Select(v=>v.Packed).Distinct().Count()!=list.Values.Length)
            {throw new InvalidDataException("Invalid source Shadow passive list");}
            var expected=row.Stats.Select((stat,i)=>new EquipmentStatValue(EquipmentStatKey.Pack(stat,68),Value(row.Definition,list.Level,i+1)))
                .Concat(new[]{new EquipmentStatValue(EquipmentStatKey.Pack(350),list.Skill),new EquipmentStatValue(EquipmentStatKey.Pack(351),list.Level)})
                .Where(v=>v.Value!=0).OrderBy(v=>v.Packed).ToArray();
            if(!list.Values.SequenceEqual(expected)){throw new InvalidDataException("Shadow passive cache lost its captured level/values");}
            states.Add(list.State);
        }
        var items=frame.Order.Where(o=>o.Item!=0).Select(o=>o.Item).ToArray();
        if(!items.SequenceEqual(active)||frame.Order.Count(o=>o.Birth)>1
            ||frame.Order.Any(o=>o.Birth?o.Item!=0||o.State!=0:(o.Item==0)==(o.State==0))
            ||!frame.Order.Where(o=>o.State!=0).Select(o=>o.State).OrderBy(s=>s).SequenceEqual(states.OrderBy(s=>s)))
        {throw new InvalidDataException("Shadow passive attachment order lost an original item/list");}
    }
    internal static ShadowPassiveGraph Initialize(GeneratedNpcStatGraph graph,string json,Func<int> command,bool itemSkillCallbacks=false)
    {
        var result=new ShadowPassiveGraph(graph,ShadowSkillBook.Restore(json),command,itemSkillCallbacks);result.Refresh(json);return result;
    }
    internal static ShadowPassiveGraph Restore(GeneratedNpcStatGraph graph,ShadowPassiveFrame frame,Func<int> command)
    {
        Validate(frame,graph.ActiveItems);var result=new ShadowPassiveGraph(graph,ShadowSkillBook.Restore(frame.Book),command,frame.ItemSkillCallbacks);
        foreach(var saved in frame.Lists)
        {
            var node=result.Create(saved.State,saved.UntilUtc.HasValue);
            foreach(var field in saved.Values){graph.Values.SetBase(node,field.Packed,field.Value);}
            if(saved.UntilUtc.HasValue){result.Timed.Add(saved.State,saved.UntilUtc.Value);}
        }
        var order=frame.Order.Select(o=>o.Birth?graph.SourceListOrder().Single(graph.IsBirthList):o.Item!=0?graph.Item(o.Item).Unit.Stats:result.Lists[o.State]).ToArray();
        graph.RestoreSourceListOrder(order,frame.Order.Any(o=>o.Birth));return result;
    }
    private EquipmentStatNode Create(int state,bool timed=false)
    {
        var node=new EquipmentStatNode{OwnerKind=1,State=state,Flags=timed?EquipmentStatNode.NewLength:0};EquipmentStatGraph.Post(Graph.Actor,node,true,Graph.Values);
        Graph.RegisterPassiveList(node);Lists.Add(state,node);return node;
    }
    private void Remove(int state)
    {
        if(!Lists.Remove(state,out var node)){return;}
        EquipmentStatGraph.Detach(node,Graph.Values);Graph.UnregisterPassiveList(node);Timed.Remove(state);
    }
    internal bool Expire(DateTime at)
    {
        if(at.Kind!=DateTimeKind.Utc){throw new ArgumentException("Shadow expiry needs UTC");}
        var expired=Timed.Where(s=>s.Value<=at).Select(s=>s.Key).ToArray();
        foreach(int state in expired){Remove(state);GraphicsNotifications++;}return expired.Length!=0;
    }
    internal void ApplyBuff(ShadowPassiveList saved)
    {
        ShadowDefensiveBuffs.Validate(saved);
        // Current source group2 contains only quickness157/fade159. Do18
        // removes the same state as well, so lower-rank recasts are allowed.
        foreach(int state in Timed.Keys.ToArray()){Remove(state);}
        var node=Create(saved.State,true);
        foreach(var field in saved.Values){Graph.Values.SetBase(node,field.Packed,field.Value);Writes++;}
        Timed.Add(saved.State,saved.UntilUtc.Value);GraphicsNotifications++;
    }
    internal void Refresh(string json)
    {
        if(Refreshing){throw new NotSupportedException("Reentrant Shadow passive refresh");}
        var next=ShadowSkillBook.Restore(json);var ids=PassiveIds(next).ToArray();
        foreach(int id in ids){_=Row(id);}
        Refreshing=true;
        try
        {
            Book=next;var states=ids.Select(id=>Row(id).Metadata.PassiveState).ToHashSet();
            foreach(int state in Lists.Keys.Where(s=>!states.Contains(s)&&!Timed.ContainsKey(s)).ToArray()){Remove(state);}
            foreach(int id in ids){RefreshSkill(id);}
        }
        finally{Refreshing=false;}
    }
    private void RefreshSkill(int id)
    {
        if(ShadowSkillBook.Metadata(id).PassiveState<=0){return;}
        var row=Row(id);int state=row.Metadata.PassiveState;
        int rank=Book.Rank(id,Graph.Read,Command());
        if(rank==0){Remove(state);return;}
        if(!Lists.TryGetValue(state,out var node)){node=Create(state);}
        if(EquipmentStatValues.Total(node,EquipmentStatKey.Pack(351))==rank){return;}
        for(int i=0;i<row.Stats.Length;i++)
        {Graph.Values.SetBase(node,EquipmentStatKey.Pack(row.Stats[i],68),Value(row.Definition,rank,i+1));Writes++;}
        Graph.Values.SetBase(node,EquipmentStatKey.Pack(350),id);Graph.Values.SetBase(node,EquipmentStatKey.Pack(351),rank);
        Writes+=2;GraphicsNotifications++;
    }
    internal void InitializeMaster(ShadowOwnerSnapshot capture)=>Book.MasterInitialize(capture,RefreshSkill);
    internal void Changed(EquipmentStatNode node,uint key)
    {
        // Complete old server callbacks for elemental/all-skills refresh all
        // existing passives. NPC single-skill107 has no such callback.
        if(node!=Graph.Actor.Stats){return;}
        int stat=EquipmentStatKey.Stat(key);
        if(stat is 126 or 127){Refresh(Book.Save());}
        else if(ItemSkillCallbacks&&stat==97)
        {
            int id=(ushort)key;if(id==0){return;}
            if(ShadowSkillBook.Metadata(id).PassiveState>0){_=Row(id);}
            Book.ItemGrant(stat,id,Graph.Read(stat,id),Graph.Read,RefreshSkill,true,Command());
        }
    }
    internal ShadowPassiveFrame Capture()
    {
        var lists=new List<ShadowPassiveList>();var order=new List<ShadowPassiveAttachment>();
        foreach(var node in Graph.SourceListOrder())
        {
            if(Graph.IsBirthList(node))
            {if(ItemSkillCallbacks){order.Add(new(0,0){Birth=true});}continue;}
            if(Lists.TryGetValue(node.State,out var known)&&known==node)
            {
                int skill=EquipmentStatValues.Total(node,EquipmentStatKey.Pack(350)),rank=EquipmentStatValues.Total(node,EquipmentStatKey.Pack(351));
                lists.Add(new(skill,node.State,rank,(EquipmentStatValue[])node.BaseValues.Clone()){UntilUtc=Timed.TryGetValue(node.State,out var until)?until:null});order.Add(new(0,node.State));
            }
            else if(node.Owner?.Entity is GeneratedNpcItem item){_=Graph.Item(item.Serial.Value);order.Add(new(item.Serial.Value,0));}
            else{throw new NotSupportedException("Unknown Shadow source attachment");}
        }
        var result=new ShadowPassiveFrame(Book.Save(),lists.ToArray(),order.ToArray()){ItemSkillCallbacks=ItemSkillCallbacks};Validate(result,Graph.ActiveItems);return result;
    }
}
