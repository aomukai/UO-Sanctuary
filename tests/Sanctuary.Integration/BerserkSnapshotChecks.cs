// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;

// Exercise the real worker serializer while the main loop is held. Expired
// payloads must be omitted without queuing live equipment-validation timers.
public static class BerserkSnapshotChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(BerserkSnapshotChecks));
    private static void Require(bool value,string reason){if(!value){throw new InvalidOperationException(reason);}}
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("berserk-snapshot-check","Worker serialization is read-only for expired Berserk states",_=>Core.LoopContext.Post(Start));
    private static PlayerMobile Actor()
    {
        var owner=new PlayerMobile{Body=0x190,Player=true,Name="BerserkSnapshotFixture"};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(owner,99);
        owner.MoveToWorld(new(1438,1696,0),Map.Felucca);return owner;
    }
    private static void Start()
    {
        var expired=Actor();var retained=Actor();
        // Let ordinary construction/equipment validation finish first.
        Timer.StartTimer(TimeSpan.FromMilliseconds(200),()=>
        {
            var prior=BarbarianBerserk.Snapshot();
            try
            {
                var field=typeof(PlayerMobile).GetField("m_NoDeltaRecursion",BindingFlags.Instance|BindingFlags.NonPublic);
                Require(field!=null && !(bool)field.GetValue(expired) && !(bool)field.GetValue(retained),"Native equipment validation has not settled");
                var now=TimedEffects.Now;var old=new BerserkState(expired.Serial.Value,1,-100,now.AddMilliseconds(-1));
                var kept=new BerserkState(retained.Serial.Value,7,-100,now.AddHours(1));
                BarbarianBerserk.Restore(prior.Append(old).Append(kept).ToArray());
                int defense=ClassStats.Defense(expired);Require(defense>0 && ClassStats.Defense(retained)==0,"Expired/live defense control");
                var bytes=Task.Run(()=>
                {
                    var writer=new BufferWriter(new byte[1024*1024],true);BarbarianBerserk.Profiles.Serialize(writer);
                    return writer.Buffer[..(int)writer.Position];
                }).GetAwaiter().GetResult();
                Require(!(bool)field.GetValue(expired) && !(bool)field.GetValue(retained),"Worker serialization queued live equipment validation");
                var reader=new BufferReader(bytes);Require(reader.ReadInt()==0,"Berserk save format changed");
                var rows=JsonSerializer.Deserialize<BerserkState[]>(reader.ReadString());
                Require(rows.All(x=>x.Owner!=expired.Serial.Value) && rows.Single(x=>x.Owner==retained.Serial.Value)==kept,"Expired/live serialized state control");
                Require(ClassStats.Defense(expired)==defense && BarbarianBerserk.State(retained)==kept,"Serialization changed live defense or the retained deadline");
                Log.Information("berserk-snapshot-result: passed");
            }
            catch(Exception e){Log.Error(e,"berserk-snapshot-result: failed");}
            finally{BarbarianBerserk.Restore(prior);expired.Delete();retained.Delete();}
        });
    }
}
