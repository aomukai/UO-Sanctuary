// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
namespace Sanctuary;

internal sealed record ShadowSkillMetadata(int Id,string Name,int Class,int Mode,int Element,int PassiveState,int PassiveType,int Summon,int PetType);
internal sealed record ShadowOwnedSkill(int Id,int Base,int Bonus,int Mode);
internal sealed record ShadowSkillBookState(ShadowOwnedSkill[] Entries,int? Left=null,int? Right=null,int? Used=null)
{public string Profile{get;init;}="shadow-intrinsic-skills-v1";}

// Intrinsic NPC entries (source owner GUID -1), not player talents or charged
// item skills. Assignment preserves an existing entry's mode and list order.
// Passive refresh, item-grant callbacks and execution are separate consumers.
internal sealed class ShadowSkillBook
{
    private static readonly Lazy<Dictionary<int,ShadowSkillMetadata>> Rows=new(()=>
    {
        var rows=BalanceCatalog.Read<ShadowSkillMetadata>("shadow-skill-metadata");
        if(rows.Length!=31||!rows.Select(r=>r.Id).SequenceEqual(new[]{0}.Concat(Enumerable.Range(251,30)))
            ||rows.Any(r=>r.Mode is<0 or>32||r.Element is<0 or>9)||rows.Where(r=>r.Id is 268 or 279).Any(r=>r.PetType!=16))
        {throw new InvalidDataException("Unsupported Shadow NPC skill metadata");}
        return rows.ToDictionary(r=>r.Id);
    });
    private readonly List<ShadowOwnedSkill> Entries;
    internal int? Left{get;private set;}
    internal int? Right{get;private set;}
    internal int? Used{get;private set;}
    private ShadowSkillBook(ShadowSkillBookState state)
    {Entries=new(state.Entries);Left=state.Left;Right=state.Right;Used=state.Used;}
    internal static ShadowSkillMetadata Metadata(int id)=>Rows.Value.TryGetValue(id,out var row)?row
        :throw new NotSupportedException("Shadow skill book does not admit this foreign skill: "+id);
    internal ShadowOwnedSkill Find(int id)=>Entries.FirstOrDefault(s=>s.Id==id);
    internal ShadowSkillBookState Snapshot()=>new(Entries.ToArray(),Left,Right,Used);
    internal string Save()=>JsonSerializer.Serialize(Snapshot());
    internal static ShadowSkillBook Restore(string json)
    {
        if(json==null){throw new NotSupportedException("Legacy parked Shadow has no captured NPC skill book");}
        var s=JsonSerializer.Deserialize<ShadowSkillBookState>(json);
        if(s?.Profile!="shadow-intrinsic-skills-v1"||s.Entries==null||s.Entries.Length>31
            ||s.Entries.Any(e=>e==null)||s.Entries.Select(e=>e.Id).Distinct().Count()!=s.Entries.Length)
        {throw new InvalidDataException("Invalid intrinsic Shadow skill list");}
        foreach(var e in s.Entries)
        {_=Metadata(e.Id);if(e.Base<0||e.Mode is<0 or>32){throw new InvalidDataException("Invalid owned Shadow skill rank/mode");}}
        foreach(var id in new[]{s.Left,s.Right,s.Used})
        {if(id.HasValue&&!s.Entries.Any(e=>e.Id==id.Value)){throw new InvalidDataException("Shadow skill selection lost its intrinsic entry");}}
        return new(s);
    }
    internal void Assign(int id,int rank,bool remove=false)
    {
        var row=Metadata(id);if(rank<0){throw new ArgumentOutOfRangeException(nameof(rank));}
        int at=Entries.FindIndex(e=>e.Id==id);
        if(rank==0&&remove)
        {
            if(id==0){throw new NotSupportedException("Removing the intrinsic Attack entry is not admitted");}
            if((Left==id||Right==id)&&Find(0)==null){throw new NotSupportedException("Skill removal needs its existing Attack fallback");}
            if(at>=0){Entries.RemoveAt(at);}
            if(Left==id){Left=0;}if(Right==id){Right=0;}if(Used==id){Used=null;}return;
        }
        if(at<0){Entries.Add(new(id,rank,0,row.Mode));}
        else{Entries[at]=Entries[at]with{Base=rank};}
    }
    internal void Add(int id)=>Assign(id,checked((Find(id)?.Base??0)+1));
    internal void SetMode(int id,int mode)
    {int at=Entries.FindIndex(e=>e.Id==id);if(at>=0&&mode is>=0 and<=32){Entries[at]=Entries[at]with{Mode=mode};}}
    internal void SetBonus(int id,int value,bool add=false)
    {
        _=Metadata(id);var e=Find(id);if(e==null&&value<=0){return;}
        if(e==null){Assign(id,0);e=Find(id);}
        int next=add?Math.Max(0,checked(e.Bonus+value)):value;
        Entries[Entries.FindIndex(s=>s.Id==id)]=e with{Bonus=next};
    }
    internal void Select(int id,bool right)
    {if(Find(id)==null){return;}if(right){Right=id;}else{Left=id;}}
    internal void Use(int? id)
    {if(id.HasValue&&Find(id.Value)==null){throw new NotSupportedException("Cannot use an unowned Shadow skill");}Used=id;}
    internal int Rank(int id,Func<int,int,int> stat,int command=0,bool bonuses=true,bool shrine=false)
    {
        ArgumentNullException.ThrowIfNull(stat);var e=Find(id);if(e==null){return 0;}var r=Metadata(id);
        if(!bonuses){return NpcInnateSkillRank.Read(e.Base,0,0,0,0,bonuses:false);}
        return NpcInnateSkillRank.Read(e.Base,e.Bonus,checked(stat(127,0)+command),stat(97,id),stat(107,id),
            r.Element>0?stat(126,r.Element):0,r.Element>0,bonuses:bonuses,shrine:shrine);
    }
    internal static ShadowSkillBook Initialize(ShadowOwnerSnapshot capture,ShadowInitialization init)
    {
        capture.Validate();ShadowInitializer.Validate(init);
        if(capture.Ability!=init.Ability){throw new InvalidDataException("Shadow skill initializer species mismatch");}
        var book=new ShadowSkillBook(new([]));
        foreach(var s in ShadowInitializer.Roll(init.Ability,init.UnitSeed,init.Difficulty).Skills)
        {book.Assign(s.Id,s.Rank);book.SetMode(s.Id,s.Mode);}
        if(capture.Ability==CombatAbility.ShadowMaster)
        {
            var owner=capture.SkillRanks();book.Select(0,false);book.Select(0,true);
            foreach(var row in Rows.Value.Values.Where(r=>r.Class==6).OrderBy(r=>r.Id))
            {
                // Both summon skills use pet type16, so the owner's actual
                // Shadow roster excludes both, not merely the same species.
                if(row.Summon==418||row.Summon!=65535&&row.PetType==16){continue;}
                var s=owner.Single(s=>s.Id==row.Id);
                book.Assign(row.Id,Math.Clamp(capture.Rank/2+s.Hard/2,1,24));
            }
        }
        return book;
    }
    // AI105's assignment step only. Controller eligibility/RNG/execution is
    // deliberately not hidden here. Selected IDs are inputs; ranks snapshot.
    internal void Mirror(ShadowOwnerSnapshot capture,int left,int right)
    {
        if(capture.Ability!=CombatAbility.ShadowWarrior){throw new NotSupportedException("Only Shadow Warrior mirrors selections");}
        var ranks=capture.SkillRanks();
        int Rank(int id)=>id==0?1:ranks.SingleOrDefault(s=>s.Id==id)?.Rank
            ??throw new NotSupportedException("Uncaptured owner skill selection");
        _=Metadata(left);_=Metadata(right);int l=Rank(left),r=Rank(right);
        Assign(right,Math.Max(r/2+capture.Rank/3,1));Assign(left,Math.Max(l/2+capture.Rank/3,1));
    }
}
