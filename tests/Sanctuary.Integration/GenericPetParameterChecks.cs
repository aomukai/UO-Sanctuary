// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
namespace Sanctuary.Integration;
public static class GenericPetParameterChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GenericPetParameterChecks));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static void Register(string n)=>ConsoleInputHandler.RegisterCommand(n,"Current GenericPet raw parameter decoder",_=>Core.LoopContext.Post(()=>{try{Check();Log.Information("{Name}: passed",n);}catch(Exception e){Log.Error(e,"{Name}: failed",n);}}));
    public static void Configure(){Register("generic-pet-parameters-check");Register("generic-pet-parameters-reload-check");}
    private static int[] Values(GenericPetParameters p)=>[p.LeashSquared,p.WarpSquared,p.CloseRaw,p.WanderRaw,p.SwarmRaw,p.Flags,p.GatherType,p.AllowSkillBonuses?1:0,p.ChooseType,p.ModeAttack,p.ModeAiType,p.ModeBonus,p.MeleeMinSquared,p.MeleeMaxSquared,p.MeleeBonus,p.RangedMinSquared,p.RangedMaxSquared,p.RangedBonus,p.RandomBonus,p.ProgressiveBonus];
    private static void Check()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generic-pet-parameters-source-probe.json")));int count=0;
        foreach(var row in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            var input=new short[24];input[row[2].GetInt32()]=checked((short)row[3].GetInt32());var output=GenericPetParameters.Decode(row[0].GetInt32()==0?null:input);
            Require(Values(output).SequenceEqual(row[4].EnumerateArray().Select(x=>x.GetInt32())),"Native parameter prefix differs from captured current x64 initializer");count++;
        }
        Require(count==2016,"Current initializer source matrix missing");int profiles=0;
        foreach(var row in doc.RootElement.GetProperty("profiles").EnumerateArray())
        {
            string key=row.GetProperty("key").GetString();Require(Values(GenericPetProfiles.Raw(key)).SequenceEqual(row.GetProperty("values").EnumerateArray().Select(x=>x.GetInt32())),"Owned current monster parameters differ from decoder profile");profiles++;
        }
        Require(profiles==3,"Expected three current GenericPet monster profiles");var goat=GoatmanPrepared.RawAiParameters;
        Require(goat==GenericPetProfiles.Raw(CombatAbility.SummonGoatman)&&goat is{LeashSquared:900,WarpSquared:900,CloseRaw:60,WanderRaw:10,SwarmRaw:0,Flags:0,GatherType:0,AllowSkillBonuses:false,ChooseType:1,ModeAttack:4,ModeAiType:4,ModeBonus:0,MeleeMinSquared:0,MeleeMaxSquared:225,MeleeBonus:1,RangedMinSquared:0,RangedMaxSquared:625,RangedBonus:0,RandomBonus:0,ProgressiveBonus:0},"Goatman current triplet profile");
        Require(GenericPetProfiles.Raw(CombatAbility.SummonTainted) is{ModeAttack:0,RangedMinSquared:1,RangedMaxSquared:625,RangedBonus:10}&&GenericPetProfiles.Raw(CombatAbility.SummonDefiler) is{Flags:2,ModeAttack:4,ModeAiType:2,RangedBonus:10},"Tainted/Defiler current source routes");
        var unusual=new short[24];unusual[3]=255;unusual[5]=-1;unusual[9]=-1;unusual[12]=-17;unusual[13]=3;var raw=GenericPetParameters.Decode(unusual);
        Require(raw is{WanderRaw:255,Flags:65535,ModeAttack:-1,MeleeMinSquared:289,MeleeMaxSquared:9},"Source raw values or inverted range were silently normalized");
        Require(GenericPetParameters.Decode(null) is{LeashSquared:784,WarpSquared:2500,ModeAttack:16,RangedMinSquared:25,RangedMaxSquared:625}&&GenericPetParameters.Decode(new short[24]).LeashSquared==0,"Missing monster record versus zero source fields");
        try{GenericPetParameters.Decode(new short[23]);throw new InvalidOperationException("Malformed source parameter row accepted");}catch(ArgumentException){}
        try{GenericPetProfiles.Raw(CombatAbility.BindDemon);throw new InvalidOperationException("Unverified bound-demon profile guessed");}catch(NotSupportedException){}
        Require(!WarlockCombat.Implemented(CombatAbility.SummonGoatman)&&!WarlockCombat.Implemented(CombatAbility.SummonTainted)&&!WarlockCombat.Implemented(CombatAbility.SummonDefiler),"Parameter work enabled incomplete summons");
        Log.Information("GenericPet parameters:2016captured-current initializer cases,3owned demon profiles;raw decoder only,full AI/owner callbacks remain gated");
    }
}
