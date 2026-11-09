// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
namespace Sanctuary.Integration;

public static class GenericPetAiSelectionChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GenericPetAiSelectionChecks));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int N(JsonElement e,string k)=>e.GetProperty(k).GetInt32();
    private static bool B(JsonElement e,string k)=>e.GetProperty(k).GetBoolean();
    private static int[] Ints(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("pet-ai-selection-check","Current descriptor selection and AI switch predicate",_=>Core.LoopContext.Post(()=>
    {try{Source();Tables();Log.Information("pet-ai-selection-check: passed");}catch(Exception e){Log.Error(e,"pet-ai-selection-check: failed");}}));
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"pet-ai-selection-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var p=row.GetProperty("input");var expected=row.GetProperty("output");var trace=new List<string>();
            var first=new GenericPetAiPrimary(checked((short)N(p,"ai")));var primary=first;int kind=N(p,"unitKind"),mode=N(p,"mode");uint flags=p.GetProperty("flags124").GetUInt32();
            var switchServices=new GenericPetAiSwitchServices(_=>new(kind,742,checked((byte)N(p,"selector")),mode,flags,p.GetProperty("flags128").GetUInt32(),B(p,"data"),checked((byte)N(p,"flags1a")),N(p,"count"),B(p,"hasRoot"),N(p,"root1c"),p.GetProperty("rootb34").GetUInt32(),p.GetProperty("rootb40").GetUInt32()),
                (selector,id)=>
                {
                    trace.Add($"A:{selector}:{id}");var mutate=p.GetProperty("mutateAi");if(mutate.ValueKind!=JsonValueKind.Null){first.Ai=checked((short)mutate.GetInt32());}
                    if(B(p,"replacePrimary")){primary=new(7);}
                    var late=p.GetProperty("lateFlags");if(late.ValueKind!=JsonValueKind.Null){flags=late.GetUInt32();}
                    late=p.GetProperty("lateKind");if(late.ValueKind!=JsonValueKind.Null){kind=late.GetInt32();}
                    late=p.GetProperty("lateMode");if(late.ValueKind!=JsonValueKind.Null){mode=late.GetInt32();}
                    return B(p,"rowA")?new(checked((byte)N(p,"flags60")),checked((byte)N(p,"flags62"))):null;
                },(selector,id)=>{trace.Add($"B:{selector}:{id}");return B(p,"rowB")?B(p,"walk"):null;});
            var services=new GenericPetAiSelectionServices(_=>new(kind,B(p,"data"),B(p,"primary")?primary:null),
                id=>GenericPetAiSwitch.Check(id,false,false,true,true,switchServices));
            bool unsupported=false;bool allowed=false;GenericPetAiSelection selection=null;int actor=B(p,"actor")?11:0;
            try
            {
                if(p.GetProperty("kind").GetString()=="selector"){selection=GenericPetAiSelectionPolicy.Select(actor,N(p,"state"),services);}
                else{var options=Ints(p.GetProperty("options"));allowed=GenericPetAiSwitch.Check(actor,options[0]!=0,options[1]!=0,options[2]!=0,options[3]!=0,switchServices);}
            }
            catch(NotSupportedException){unsupported=true;}
            Require(unsupported==B(expected,"unsupported")&&trace.SequenceEqual(expected.GetProperty("trace").EnumerateArray().Select(v=>v.GetString())), $"AI selection boundary/trace differs at{count}");
            if(!unsupported)
            {
                if(selection!=null)
                {
                    ulong address=unchecked((ulong)(0x140000000L+(selection.Special?0x1d19b70:0x1d18810)+(long)selection.Index*32));
                    Require(address==expected.GetProperty("value").GetUInt64(),$"Current descriptor pointer arithmetic differs at{count}");
                }
                else{Require(allowed==expected.GetProperty("value").GetBoolean(),$"Current switch predicate differs at{count}");}
            }
            count++;
        }
        Require(count==66,"Missing composed selector/predicate controls");
        Log.Information("AI selection:66current contexts/686bytes;conditional state10..12predicate,primary pointer cache,signed index/default bounds,raw special arithmetic and full switch flags match. Live status projection/special descriptors remain explicit.");
    }
    private static void Tables()
    {
        foreach(var row in BalanceCatalog.Read<GenericPetAiMonster>("generic-pet-ai-monsters"))
        {Require(GenericPetAiTables.Monster(row.Id)==row,"Compiled AI monster row changed on load");}
        foreach(var row in BalanceCatalog.Read<GenericPetAiDescriptor>("generic-pet-ai-descriptors"))
        {Require(GenericPetAiTables.Descriptor(new(false,row.Id))==row,"AI descriptor changed on load");}
        Require(GenericPetAiTables.Monster(-1)==null&&GenericPetAiTables.Monster(751)==null,"Unknown monster silently mapped");
        bool refused=false;try{GenericPetAiTables.Descriptor(new(true,10));}catch(NotSupportedException){refused=true;}
        Require(refused,"Unpublished special AI was invented");
    }
    internal static int Original(SkeletonGuardian body,int state)
    {
        var descriptor=GenericPetAiTables.Original(body,state,_=>throw new NotSupportedException("Live switch/status projection is not yet bound"));
        Require(descriptor.Id==148&&descriptor.Name=="GenericPet"&&descriptor.Type==0&&descriptor.Think==0x4b60c0,"Original demon did not resolve its actual compiled AI descriptor");
        return descriptor.Type;
    }
}
