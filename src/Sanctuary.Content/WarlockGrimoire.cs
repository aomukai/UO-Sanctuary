// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Items;
namespace Sanctuary;

// The eventual equip path keeps this saved shield subtype. Only validated
// captured input is available now; no incomplete random item is offered.
[SerializationGenerator(0)]
public partial class WarlockGrimoire:BaseShield
{
    [SerializableField(0,setter:"private")]private string _saved="{}";
    internal string SavedText=>_saved;
    internal GrimoireSnapshot Capture=>_saved=="{}"?null:GrimoirePrepared.Copy(JsonSerializer.Deserialize<GrimoireSnapshot>(_saved));
    [Constructible]public WarlockGrimoire():base(0xEFA)
    {Name="Grimoire [prepared placeholder]";Layer=Layer.TwoHanded;HitPoints=MaxHitPoints=20;}
    public override bool CanEquip(Mobile from)=>false; // Native layer/stat binding is still gated.
    internal void RestorePrepared(GrimoireSnapshot snapshot)
    {
        var copy=GrimoirePrepared.Copy(snapshot);var encoded=JsonSerializer.Serialize(copy);
        _saved=encoded;Name=GrimoirePrepared.Base(copy.Code).Name+" [prepared placeholder]";InvalidateProperties();
    }
    internal static WarlockGrimoire CreatePrepared(GrimoireSnapshot snapshot)
    {
        GrimoirePrepared.Validate(snapshot);var item=new WarlockGrimoire();
        try{item.RestorePrepared(snapshot);return item;}catch{item.Delete();throw;}
    }
    [AfterDeserialization]private void AfterGrimoireLoad()
    {
        if(_saved=="{}"){return;}
        try{GrimoirePrepared.Validate(Capture);}
        catch(JsonException e){throw new InvalidDataException("Invalid saved Grimoire capture",e);}
    }
}
