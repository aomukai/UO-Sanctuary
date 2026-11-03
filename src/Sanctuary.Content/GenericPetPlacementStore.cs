// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
namespace Sanctuary;

// Opt-in saved registry. No timer, automatic movement or learned cast is enabled.
// Existing bodies/saves have no store until a controller explicitly creates one.
[SerializationGenerator(0)]
public partial class GenericPetPlacementStore:Item
{
    [SerializableField(0,setter:"private")]private string _saved="{}";
    private GenericPetPlacementWorld _world;
    internal GenericPetPlacementWorld State=>_world??=GenericPetPlacementWorld.Restore(JsonSerializer.Deserialize<GenericPetPlacementImage>(_saved));
    internal string SavedText=>_saved;
    public GenericPetPlacementStore():base(1){Name="prepared pet placement registry";Movable=false;Visible=false;}
    internal static GenericPetPlacementStore Create(Map map)
    {
        var world=new GenericPetPlacementWorld(map);var store=new GenericPetPlacementStore{_world=world};
        store.Capture();store.Internalize();return store;
    }
    internal void Capture()=>_saved=JsonSerializer.Serialize(State.Capture());
    internal void Register(WarlockGoatmanBody body,GenericPetMonsterPlacementUnit unit,GenericPetCommitPath path)
    {
        if(Deleted){throw new NotSupportedException("Deleted pet placement store");}
        try{State.Register(body,unit,path);}finally{Capture();}
    }
    internal int Place(int handle,Point2D point,GenericPetPathTargetView targetMetadata=null,bool skipSearch=false)
    {
        if(Deleted){throw new NotSupportedException("Deleted pet placement store");}
        var body=Server.World.FindMobile((Serial)unchecked((uint)handle)) as WarlockGoatmanBody;
        var unit=State.Unit(handle);var path=State.Member(handle).Path;
        try{return new GenericPetNativePlacement(body,unit,path,point,targetMetadata,State).Place(skipSearch);}
        finally{Capture();}
    }
    internal void Remove(int handle)
    {
        if(Deleted){throw new NotSupportedException("Deleted pet placement store");}
        try{State.Remove(handle);}finally{Capture();}
    }
    [AfterDeserialization]private void AfterPlacementLoad()
    {_world=GenericPetPlacementWorld.Restore(JsonSerializer.Deserialize<GenericPetPlacementImage>(_saved));}
}
