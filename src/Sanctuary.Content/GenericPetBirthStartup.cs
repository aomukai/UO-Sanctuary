// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using Server;
namespace Sanctuary;

// Caller supplies an already-qualified native path, not guessed D2 birth
// geometry. Startup approval covers initial AI words, not that path producer.
internal sealed record GenericPetBirthPath(GenericPetMonsterPlacementUnit Unit,GenericPetCommitPath Path,byte Selector,int Mode,DateTime NextAiUtc);
internal sealed record GenericPetBirthBinding(int Game,Func<SkeletonGuardian,GenericPetBirthPath> Path)
{
    internal void RegisterNew(SkeletonGuardian body)
    {
        GenericPetStartupProposal.RequireApproval();ArgumentNullException.ThrowIfNull(Path);
        int owner=checked((int)WarlockDemonPlacement.Owner(body));var identity=new GenericPetWorldIdentity(Game);
        if(identity.Resolve(Game,0,owner)!=owner||body.GetMaster()?.Serial.Value!=(uint)owner)
        {throw new NotSupportedException("New demon startup requires its original native player owner");}
        var draft=GenericPetStartupProposal.Draft(WarlockDemonPlacement.ClassId(body),Game,owner);
        var path=Path(body)??throw new NotSupportedException("New demon startup requires an explicit native path");
        DeathMarkWorld.RegisterNew(body,path.Unit,path.Path,path.Selector,path.Mode,path.NextAiUtc,draft);
    }
}

internal sealed record GenericPetHeaderImage(int Game,int SourceClass,int OwnerId,int State,int Function,int Mode10,int Field14,
    ushort Flags38,int TargetId3c,int TargetKind40,int Ai90);

// Reuses the actual managed AI control/target objects. The existing registered
// command and three decision words remain in DeathMarkWorld, not a second copy.
internal sealed class GenericPetLiveHeader
{
    internal readonly GenericPetAiControl Control;
    internal readonly GenericPetAiSavedTarget Memory;
    private readonly int Game,SourceClass,OwnerId;
    internal int Mode10,Ai90;
    internal ushort Flags38;
    internal GenericPetLiveHeader(GenericPetHeaderImage image)
    {
        Validate(image);Game=image.Game;SourceClass=image.SourceClass;OwnerId=image.OwnerId;
        Control=new(image.State,image.Function,image.Field14,false,[]);Memory=new(image.TargetId3c,image.TargetKind40);
        Mode10=image.Mode10;Ai90=image.Ai90;Flags38=image.Flags38;
    }
    internal static GenericPetHeaderImage Initial(GenericPetStartupDraft draft)=>new(draft.OwnerGame,draft.SourceClass,draft.OwnerId,
        draft.State,draft.Function,draft.Mode10,draft.Field14,draft.Flags38,draft.TargetId3c,draft.TargetKind40,draft.Ai90);
    internal GenericPetAiOverrideHeader Override=>new(Control.State,Mode10);
    internal GenericPetHeaderImage Capture()
    {
        // Raw queued functor tokens have no qualified durable native binding.
        // Do not serialize them as portable commands or silently discard them.
        if(Control.Executing||Control.Queue.Count!=0){throw new NotSupportedException("Persistent queued GenericPet action context is not qualified");}
        var image=new GenericPetHeaderImage(Game,SourceClass,OwnerId,Control.State,Control.Function,Mode10,Control.Field14,Flags38,Memory.Id,Memory.Kind,Ai90);
        Validate(image);return image;
    }
    internal static void Validate(GenericPetHeaderImage image)
    {
        if(image==null||image.Game==0||!WarlockDemonPlacement.ClassIdAllowed(image.SourceClass)||!((Serial)(uint)image.OwnerId).IsMobile
            ||image.State!=0||GenericPetAiTables.Monster(image.SourceClass)is not{} monster
            ||GenericPetAiTables.Descriptor(new(false,monster.Ai))is not{Id:148,Type:0} descriptor||image.Function!=descriptor.Think)
        {throw new InvalidDataException("Unqualified normal demon AI header");}
    }
}
