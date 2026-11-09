// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class CleaveDirectionChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(CleaveDirectionChecks));
    private sealed record Saved(uint Owner,uint Weapon,int X,int Y,CleaveSweepRequest Request,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"cleave-direction-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        void Register(string name,bool reload)=>ConsoleInputHandler.RegisterCommand(name,"Current direction wrapper and mapped Cleave request",_=>Core.LoopContext.Post(()=>
        {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
        Register("cleave-direction-check",false);Register("cleave-direction-reload-check",true);
    }
    private static void Source()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"cleave-direction-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var i=row.GetProperty("input");int I(string key)=>i.GetProperty(key).GetInt32();float F(string key)=>BitConverter.UInt32BitsToSingle(i.GetProperty(key).GetUInt32());
            var calls=new List<(string Name,uint[] Args)>();float Service(string name,string result,params float[] args){calls.Add((name,args.Select(BitConverter.SingleToUInt32Bits).ToArray()));return F(result);}
            var result=SourceDirection64.Calculate(new(I("fromX"),I("fromY")),new(I("toX"),I("toY")),new((x,y)=>Service("angle","angleBits",x,y),(v,a,b)=>Service("span","spanBits",v,a,b),v=>Service("round","roundBits",v)));
            var o=row.GetProperty("output");Require(result==new SourceDirectionResult(o.GetProperty("x").GetInt32(),o.GetProperty("y").GetInt32(),o.GetProperty("heading").GetInt32()),"Current direction output case "+count);
            var expected=o.GetProperty("calls").EnumerateArray().ToArray();Require(calls.Count==expected.Length&&calls.Where((c,n)=>c.Name!=expected[n][0].GetString()||!c.Args.SequenceEqual(expected[n][1].EnumerateArray().Select(x=>x.GetUInt32()))).Any()==false,"Current direction float32 call bits/order case "+count);count++;
        }
        Require(count==48&&WarlockCombat.Implemented(CombatAbility.Cleave)==CleaveActionProposal.Approved,"Missing direction cases or unfinished Cleave enabled");
    }
    // Explicit supplied math results,not a live approximation of protected calls.
    private static readonly SourceDirectionServices Supplied=new((_,_)=>0,(_,_,_)=>0,_=>32);
    private static CleaveSweepRequest Plan(PlayerMobile p)
    {
        int mana=NecromancerEffects.ManaBalance256(p),life=SkillHealth.Balance256(p);
        var request=CleaveSweep.PrimaryPrepared(p,new(p.X+8,p.Y+2,p.Z),Supplied);var cell=PetOwnerRoutes.Cell(p);
        Require(request is{Skill:391,Rank:1,SourceDamage:128}&&request.X==cell.X&&request.Y==cell.Y&&request.CenterX==cell.X&&request.CenterY==cell.Y
            &&request.Radius==PlayerMeleeGeometry.Reach(p)+4&&PlayerMeleeGeometry.Reach(p)>=2,"Mapped Cleave position/rank/source reach binding");
        Require(mana==NecromancerEffects.ManaBalance256(p)&&life==SkillHealth.Balance256(p),"Prepared request consumed resources");return request;
    }
    private static void Check(bool reload)
    {
        Source();Saved saved;PlayerMobile p;
        if(reload)
        {
            saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;
            Require(p!=null&&CombatTiming.Equipped(p)?.Serial.Value==saved.Weapon,"Original mapped Cleave weapon missing");
            FixtureSpace.Place(p,new(saved.X,saved.Y,0),Map.Trammel);Require(Plan(p)==saved.Request,"Original mapped Cleave plan changed on reload");saved=saved with{Generation=saved.Generation+1};
        }
        else
        {
            p=ClassSkillChecks.Player(DiabloClass.Warlock);ClassSkillChecks.Train(p,"warlock.cleave");while(p.RawStr<100){Require(ClassCreation.Profiles.TrySpendStat(p,CharacterStat.Strength),"Direction fixture Strength");}
            FixtureSpace.Corridor(p,6560,16,3500);var weapon=new Broadsword();p.AddToBackpack(weapon);Require(p.EquipItem(weapon),"Mapped Cleave sword");ClassGear.Refresh(p);var request=Plan(p);
            p.AddToBackpack(weapon);ClassGear.Refresh(p);
            try{CleaveSweep.PrimaryPrepared(p,p.Location,Supplied);throw new InvalidOperationException("Unarmed prepared Cleave accepted");}catch(NotSupportedException){}
            Require(p.EquipItem(weapon),"Original Cleave sword re-equip");ClassGear.Refresh(p);Require(Plan(p)==request,"Mapped Cleave re-equip changed request");
            saved=new(p.Serial.Value,weapon.Serial.Value,p.X,p.Y,request,0);
        }
        File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
        Log.Information("Cleave direction:48current wrapper contexts;unsigned packed deltas,float32 normalization/trace bits,zero/nonfinite/truncation/mask paths;original mapped Warlock weapon reach and request persist. Three protected math services remain supplied;no learned cast.");
    }
}
