// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;
internal sealed record ValkyrieEquipmentAttempt(SummonEquipmentRequest Request,bool Success,uint Item);
internal sealed record ValkyrieEquipmentState(uint Actor,uint Owner,int Rank,int ItemLevel,MonsterDifficulty Difficulty,
    ulong PetBefore,ulong PetAfter,ulong GameBefore,ulong GameAfter,ValkyrieEquipmentAttempt[] Attempts);

// Actual private item delivery with supplied independent stream contexts.
// Full live stat/skill/event and combat admission remain separate.
internal static class ValkyrieEquipment
{
    internal static ValkyrieEquipmentState Copy(ValkyrieEquipmentState s)=>s==null?null:s with{Attempts=(ValkyrieEquipmentAttempt[])s.Attempts.Clone()};
    internal static void Validate(ValkyrieEquipmentState s)
    {
        if(s==null||!((Serial)s.Actor).IsMobile||!((Serial)s.Owner).IsMobile||s.Actor==s.Owner||s.Rank is<1 or>255||s.ItemLevel!=25+3*(s.Rank-1)
            ||!Enum.IsDefined(s.Difficulty)||s.Attempts==null||s.Attempts.Length>19||s.Attempts.Any(a=>a?.Request==null||a.Success!=((Serial)a.Item).IsItem||!a.Success&&a.Item!=0)
            ||s.Attempts.Where(a=>a.Success).Select(a=>a.Item).Distinct().Count()!=s.Attempts.Count(a=>a.Success))
        {throw new InvalidDataException("Invalid Valkyrie equipment identity/context");}
        int index=0;ulong pet=s.PetBefore,game=s.GameBefore;
        ValkyrieAffixes.Equip(s.Rank,s.ItemLevel,ref pet,request=>
        {
            if(index>=s.Attempts.Length||s.Attempts[index].Request!=request){throw new InvalidDataException("Valkyrie equipment request order changed");}
            _=NpcItemAllocations.Take(ref game);return s.Attempts[index++].Success;
        });
        if(index!=s.Attempts.Length||pet!=s.PetAfter||game!=s.GameAfter){throw new InvalidDataException("Valkyrie equipment stream continuation changed");}
    }
    internal static GeneratedNpcItem[] Originals(AmazonValkyrieBody body)
    {
        var s=body.Prepared?.Equipment??throw new NotSupportedException("Valkyrie has no completed supplied loadout");Validate(s);
        if(body is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile owner}||s.Actor!=body.Serial.Value||s.Owner!=owner.Serial.Value)
        {throw new NotSupportedException("Valkyrie equipment lost its original body/owner");}
        var items=GeneratedNpcEquipment.Items(body);var ids=s.Attempts.Where(a=>a.Success).Select(a=>a.Item).ToArray();
        if(items.Length!=ids.Length||items.Any(i=>!ids.Contains(i.Serial.Value))){throw new NotSupportedException("Valkyrie original inventory changed");}
        ulong game=s.GameBefore;var result=new List<GeneratedNpcItem>();
        foreach(var a in s.Attempts)
        {
            var allocation=NpcItemAllocations.Take(ref game);if(!a.Success){continue;}
            var item=items.Single(i=>i.Serial.Value==a.Item);var state=item.Snapshot;var r=a.Request;
            if(state.Allocation!=allocation||state.Request!=NpcItemAllocations.Request(r.Code,r.ItemLevel,r.Quality,(int)s.Difficulty,allocation)
                ||state.Equipment is not{Kind:EquipMonster.Other}e||e.Actor!=s.Actor||e.Owner!=s.Owner||e.Body!=r.Slot)
            {throw new NotSupportedException("Valkyrie original item allocation/request/binding changed");}
            result.Add(item);
        }
        return result.ToArray();
    }
    internal static ValkyrieEquipmentState Materialize(AmazonValkyrieBody body,SourceSeedStream pet,SourceSeedStream game)
    {
        ArgumentNullException.ThrowIfNull(pet);ArgumentNullException.ThrowIfNull(game);
        if(ReferenceEquals(pet,game)){throw new NotSupportedException("Valkyrie pet and game streams must be independent");}
        if(body is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile{Alive:true,Deleted:false} owner}
            ||body.Prepared is not{Equipment:null}birth||birth.Owner!=owner.Serial.Value||GeneratedNpcEquipment.Items(body).Length!=0)
        {throw new NotSupportedException("Valkyrie loadout requires its original living empty body");}
        ulong petBefore=pet.Value,gameBefore=game.Value;var attempts=new List<ValkyrieEquipmentAttempt>();var created=new List<GeneratedNpcItem>();
        try
        {
            int level=body.BirthStats.EquipmentItemLevel;
            ValkyrieAffixes.Equip(birth.Birth.Rank,level,ref pet.Value,request=>
            {
                if(!request.NoSockets||!request.NoEthereal||!request.NoSell||!request.RestoreDurability){throw new NotSupportedException("Unsupported Valkyrie item creation flags");}
                bool success=GeneratedNpcEquipment.TryCreateAllocated(body,owner,EquipMonster.Other,request.Code,level,request.Quality,(int)birth.Difficulty,request.Slot,game,out var item);
                attempts.Add(new(request,success,item?.Serial.Value??0));if(success){created.Add(item);}return success;
            });
            var state=new ValkyrieEquipmentState(body.Serial.Value,owner.Serial.Value,birth.Birth.Rank,level,birth.Difficulty,petBefore,pet.Value,gameBefore,game.Value,attempts.ToArray());Validate(state);
            body.RestorePrepared(birth with{Equipment=state});_ = Originals(body);return Copy(state);
        }
        catch
        {
            // Unsupported source rolls are never filtered or downgraded. Drop
            // this prepared transaction's items;retain its consumed seed words.
            foreach(var item in created){item.Delete();}
            if(body.Prepared?.Equipment!=null){body.RestorePrepared(birth);}
            throw;
        }
    }
}
