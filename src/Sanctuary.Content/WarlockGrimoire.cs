// SPDX-License-Identifier: GPL-3.0-or-later
using System.IO;
using System.Text.Json;
using ModernUO.Serialization;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary;

// Validated captured input keeps the same saved shield subtype. Random
// generation and acquisition remain separate from original-item equip policy.
[SerializationGenerator(0)]
public partial class WarlockGrimoire:BaseShield
{
    [SerializableField(0,setter:"private")]private string _saved="{}";
    private GrimoireSnapshot _capture;
    private (NamedProperty Property,NamedPropertyRoll Roll)[] _properties;
    internal bool HasCapture=>_saved!="{}";
    internal string SavedText=>_saved;
    internal GrimoireSnapshot Captured=>_capture??=GrimoirePrepared.Copy(JsonSerializer.Deserialize<GrimoireSnapshot>(_saved));
    internal GrimoireSnapshot Capture=>HasCapture?GrimoirePrepared.Copy(Captured):null;
    internal System.Collections.Generic.IEnumerable<(NamedProperty Property,NamedPropertyRoll Roll)> CapturedProperties
        =>_properties??=GrimoirePrepared.Properties(Captured);
    [Constructible]public WarlockGrimoire():base(0xEFA)
    {Name="Grimoire [prepared placeholder]";Layer=Layer.TwoHanded;HitPoints=MaxHitPoints=20;}
    public override bool CanEquip(Mobile from)
    {
        if(!HasCapture||from is not PlayerMobile owner||!ClassCombat.Enabled(owner)||!WarlockHands.Innate(owner)){return false;}
        GrimoireEquipment.Bind(this);return base.CanEquip(from);
    }
    internal void RestorePrepared(GrimoireSnapshot snapshot)
    {
        var copy=GrimoirePrepared.Copy(snapshot);var encoded=JsonSerializer.Serialize(copy);
        if(ClassGear.Profiles?.Find(Serial)!=null&&encoded!=_saved){throw new InvalidDataException("A bound Grimoire cannot replace its original capture");}
        _saved=encoded;_capture=copy;_properties=null;Name=GrimoirePrepared.Base(copy.Code).Name+" [placeholder]";
        if(ClassGear.Profiles!=null){GrimoireEquipment.Bind(this);}InvalidateProperties();
    }
    internal static WarlockGrimoire CreatePrepared(GrimoireSnapshot snapshot)
    {
        GrimoirePrepared.Validate(snapshot);var item=new WarlockGrimoire();
        try{item.RestorePrepared(snapshot);return item;}catch{item.Delete();throw;}
    }
    [AfterDeserialization]private void AfterGrimoireLoad()
    {
        if(_saved=="{}"){return;}
        try
        {
            _capture=null;_properties=null;GrimoirePrepared.Validate(Capture);
            Timer.StartTimer(System.TimeSpan.Zero,()=>{if(!Deleted){GrimoireEquipment.Bind(this);}});
        }
        catch(JsonException e){throw new InvalidDataException("Invalid saved Grimoire capture",e);}
    }
}
