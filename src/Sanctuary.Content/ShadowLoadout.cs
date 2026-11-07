// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record ShadowEquipmentAttempt(SummonEquipmentRequest Request,bool Success,uint Item);
internal sealed record ShadowLoadoutState(uint Actor,uint Owner,ShadowOwnerSnapshot Birth,MonsterDifficulty Difficulty,
    ulong PetBefore,ulong PetAfter,ulong GameBefore,ulong GameAfter,ShadowEquipmentAttempt[] Attempts);
internal sealed record ShadowWeaponBinding(uint Primary,uint Secondary,string Identity);

// Original private NPC items,source allocation witnesses and hand identity.
// The owner bases are captured before allocation; no owner item is borrowed.
internal static class ShadowLoadout
{
    internal static ShadowLoadoutState Copy(ShadowLoadoutState s)=>s==null?null:s with{Attempts=(ShadowEquipmentAttempt[])s.Attempts.Clone()};
    internal static void Validate(ShadowLoadoutState s)
    {
        if(s==null||!((Serial)s.Actor).IsMobile||!((Serial)s.Owner).IsMobile||s.Actor==s.Owner||s.Birth==null
            ||!Enum.IsDefined(s.Difficulty)||s.Attempts==null||s.Attempts.Length>19
            ||s.Attempts.Any(a=>a?.Request==null||a.Success!=((Serial)a.Item).IsItem||!a.Success&&a.Item!=0)
            ||s.Attempts.Where(a=>a.Success).Select(a=>a.Item).Distinct().Count()!=s.Attempts.Count(a=>a.Success))
        {throw new InvalidDataException("Invalid original Shadow loadout");}
        s.Birth.Validate();int index=0;ulong pet=s.PetBefore,game=s.GameBefore;
        ShadowEquipment.Select(s.Birth.Ability,s.Birth.Rank,ref pet,r=>
        {
            if(index>=s.Attempts.Length||s.Attempts[index].Request!=r){throw new InvalidDataException("Shadow request order changed");}
            _=NpcItemAllocations.Take(ref game);return s.Attempts[index++].Success;
        },s.Birth.OwnerBase);
        if(index!=s.Attempts.Length||pet!=s.PetAfter||game!=s.GameAfter){throw new InvalidDataException("Shadow loadout stream continuation changed");}
    }
    internal static EquipMonster Kind(CombatAbility ability)=>ability switch
    {CombatAbility.ShadowWarrior=>EquipMonster.ShadowWarrior,CombatAbility.ShadowMaster=>EquipMonster.ShadowMaster,_=>throw new ArgumentOutOfRangeException(nameof(ability))};
    internal static GeneratedNpcItem[] Originals(AssassinShadowBody body)
    {
        var s=body.Prepared?.Equipment??throw new NotSupportedException("Shadow original equipment is not complete");Validate(s);
        if(body is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile{Deleted:false} owner}||s.Actor!=body.Serial.Value||s.Owner!=owner.Serial.Value)
        {throw new NotSupportedException("Shadow loadout lost its original body/owner");}
        var items=GeneratedNpcEquipment.Items(body);var ids=s.Attempts.Where(a=>a.Success).Select(a=>a.Item).ToArray();
        if(items.Length!=ids.Length||items.Any(i=>!ids.Contains(i.Serial.Value))){throw new NotSupportedException("Shadow original inventory changed");}
        ulong game=s.GameBefore;var result=new List<GeneratedNpcItem>();
        foreach(var a in s.Attempts)
        {
            var allocation=NpcItemAllocations.Take(ref game);if(!a.Success){continue;}
            var item=items.Single(i=>i.Serial.Value==a.Item);var state=item.Snapshot;var r=a.Request;var e=state.Equipment;
            if(state.Allocation!=allocation||state.Request!=NpcItemAllocations.Request(r.Code,r.ItemLevel,r.Quality,(int)s.Difficulty,allocation)
                ||e==null||e.Kind!=Kind(s.Birth.Ability)||e.Actor!=s.Actor||e.Owner!=s.Owner||e.Body!=r.Slot)
            {throw new NotSupportedException("Shadow original allocation/request/item binding changed");}
            result.Add(item);
        }
        return result.ToArray();
    }
    internal static ShadowLoadoutState Materialize(AssassinShadowBody body,SourceSeedStream pet,SourceSeedStream game)
    {
        ArgumentNullException.ThrowIfNull(pet);ArgumentNullException.ThrowIfNull(game);
        if(ReferenceEquals(pet,game)||body is not{Alive:true,Deleted:false,ControlMaster:PlayerMobile{Alive:true,Deleted:false} owner}
            ||body.Prepared is not{Equipment:null} birth||birth.Owner!=owner.Serial.Value||GeneratedNpcEquipment.Items(body).Length!=0
            ||pet.Value!=birth.Initialization.AfterInitialization||game.Value!=birth.Initialization.WorldAfter)
        {throw new NotSupportedException("Shadow original loadout needs its allocated empty body and independent continuations");}
        ulong petBefore=pet.Value,gameBefore=game.Value;var attempts=new List<ShadowEquipmentAttempt>();var created=new List<GeneratedNpcItem>();
        try
        {
            ShadowEquipment.Select(birth.Capture.Ability,birth.Capture.Rank,ref pet.Value,r=>
            {
                if(!r.NoSockets||!r.NoEthereal||!r.NoSell||!r.RestoreDurability){throw new NotSupportedException("Unsupported Shadow creation flags");}
                bool success=GeneratedNpcEquipment.TryCreateAllocated(body,owner,Kind(birth.Capture.Ability),r.Code,r.ItemLevel,r.Quality,(int)birth.Difficulty,r.Slot,game,out var item);
                attempts.Add(new(r,success,item?.Serial.Value??0));if(success){created.Add(item);}return success;
            },birth.Capture.OwnerBase);
            var state=new ShadowLoadoutState(body.Serial.Value,owner.Serial.Value,birth.Capture,birth.Difficulty,petBefore,pet.Value,gameBefore,game.Value,attempts.ToArray());Validate(state);
            body.RestorePrepared(birth with{Equipment=state});_=Originals(body);return Copy(state);
        }
        catch
        {
            foreach(var item in created){item.Delete();}
            if(body.Prepared?.Equipment!=null){body.RestorePrepared(birth);}
            throw;
        }
    }
    private static ShadowWeaponBinding OriginalBinding(AssassinShadowBody body)
    {
        var originals=Originals(body);var(primary,secondary)=GeneratedNpcHands.Read(originals);
        if(primary==null){throw new NotSupportedException("Shadow has no qualified original primary weapon");}
        uint other=secondary?.Guid??0;
        string frame=JsonSerializer.Serialize(new{body.Prepared.Capture,body.Prepared.Initialization,body.Prepared.Equipment,
            Payloads=originals.Select(i=>i.SavedText).ToArray(),primary.Guid,Secondary=other});
        return new(primary.Guid,other,Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(frame))));
    }
    internal static ShadowWeaponBinding Binding(AssassinShadowBody body)
    {
        if(body?.Prepared?.Weapons is not{} saved||saved!=OriginalBinding(body))
        {throw new NotSupportedException("Shadow original hand/inventory identity changed");}
        return saved;
    }
    internal static void BindStream(AssassinShadowBody body,ulong seed)
    {
        if(body?.Prepared is not{Weapons:null,Equipment:{} equipment} old||NpcEquipmentRandom.State(body)!=null||seed!=equipment.PetAfter)
        {throw new NotSupportedException("Shadow stream binding requires its unbound original inventory continuation");}
        var binding=OriginalBinding(body);
        try{body.RestorePrepared(old with{Weapons=binding});NpcEquipmentRandom.Set(body,seed);}
        catch{NpcEquipmentRandom.Forget(body);body.RestorePrepared(old);throw;}
    }
}
