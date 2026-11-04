// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;

namespace Sanctuary.Integration;
public static class ShadowEquipmentChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(ShadowEquipmentChecks));
    private sealed record Saved(uint Owner,uint Helm,uint Armor,int Rank,SummonEquipmentRequest[] Requests,ulong Seed);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"shadow-equipment-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("shadow-equipment-check",Check);Register("shadow-equipment-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Shadow/Valk shared equipment requests; no generated NPC items or actor",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static string Code(int packed)=>new string(Enumerable.Range(0,4).Select(i=>(char)((uint)packed>>(i*8)&255)).ToArray()).TrimEnd();
    private static void Model()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-equipment-cases.json")));int count=0;
        foreach(var row in source.RootElement.EnumerateArray())
        {
            int V(int i)=>row[0][i].GetInt32();int profile=V(0),rank=V(1),pick=V(2),mode=V(3),mask=V(4),failure=V(5),level=V(6);
            var rows=profile==2?new SummonEquipRow[]{new(0,[new(4,"ces",8)]),new(99,[new(3,"",4)])}:ShadowEquipment.Rows(profile==0?CombatAbility.ShadowWarrior:CombatAbility.ShadowMaster);
            var actual=new List<SummonEquipmentRequest>();var draws=new List<int>();ulong seed=173;
            uint Draw(int bound){draws.Add(bound);return (uint)(pick%bound);}
            bool Create(SummonEquipmentRequest r){actual.Add(r);return failure!=1&&!(failure==2&&r.Quality==6);}
            string OwnerBase(int slot)=>mode==3?slot==1?"cap":slot==3?"qui":null:null;
            SummonEquipment.Select(rows,rank,level,ref seed,Create,Enumerable.Range(1,10).Where(i=>(mask&(1<<i))!=0).ToArray(),OwnerBase,mode!=0,mode!=1,Draw);
            Require(seed==173&&actual.Count==row[1].GetArrayLength()&&draws.SequenceEqual(row[2].EnumerateArray().Select(x=>x.GetInt32())),"Complete selector draw/slot/request count");
            for(int i=0;i<actual.Count;i++)
            {
                var e=row[1][i];Require(actual[i]==new SummonEquipmentRequest(e[0].GetInt32(),Code(e[1].GetInt32()),e[2].GetInt32(),e[3].GetInt32()),"Complete shared selector owner-base/quality/failure/order");
            }
            count++;
        }
        Require(count==4752,"Incomplete shared source selection contexts");
        for(int rank=1;rank<=255;rank++)
        {
            Require(ShadowEquipment.ItemLevel(CombatAbility.ShadowWarrior,rank)==Math.Clamp(18+(rank-1)*2,1,99)
                &&ShadowEquipment.ItemLevel(CombatAbility.ShadowMaster,rank)==Math.Clamp(24+(rank-1)*3,1,99),"Pinned Shadow item-level supplier");
        }
        var detached=ShadowEquipment.Rows(CombatAbility.ShadowMaster);detached[0].Choices[0]=new(2,"bad",0);
        Require(ShadowEquipment.Rows(CombatAbility.ShadowMaster)[0].Choices[0].Code=="amu","Mutable Shadow profile escaped");
        Require(!AssassinCombat.Implemented(CombatAbility.ShadowWarrior)&&!AssassinCombat.Implemented(CombatAbility.ShadowMaster),"Equipment requests enabled incomplete actors");
        Log.Information("Shadow equipment:4752 complete shared selector controls/510 pinned item-level suppliers;Valkyrie reuse;no generated NPC items or learned Shadow actor");
    }
    private static (SummonEquipmentRequest[] Requests,ulong Seed) Capture(PlayerMobile owner,Item helm,Item armor,int rank)
    {
        ulong seed=SourceRandom.InitLow(173);var result=new List<SummonEquipmentRequest>();
        ShadowEquipment.Select(CombatAbility.ShadowMaster,rank,ref seed,r=>{result.Add(r);return true;},slot=>slot==1?ClassGear.Base(helm).Code:slot==3?ClassGear.Base(armor).Code:null);
        Require(result.Any(r=>r.Slot==1&&r.Code==ClassGear.Base(helm).Code&&r.Quality==6)
            &&result.Any(r=>r.Slot==3&&r.Code==ClassGear.Base(armor).Code&&r.Quality==6),"Actual owner bases copied as new rare requests");
        ulong expected=SourceRandom.InitLow(173);foreach(var row in ShadowEquipment.Rows(CombatAbility.ShadowMaster)){SourceRandom.Limited(ref expected,row.Choices.Length);}
        Require(seed==expected&&result.Count==8&&result.All(r=>r.ItemLevel==ShadowEquipment.ItemLevel(CombatAbility.ShadowMaster,rank)),"Production pet seed/filled-slot draw consumption");
        return(result.ToArray(),seed);
    }
    private static void Check()
    {
        Model();var owner=ClassSkillChecks.Player(DiabloClass.Assassin);owner.Name="ShadowEquipmentFixture";owner.AddItem(new Backpack());
        while(ClassCreation.Profiles.GetRank(owner.Serial,"assassin.shadowmaster")<16){Require(ClassCreation.Profiles.TryTrain(owner.Serial,"assassin.shadowmaster",out _),"Shadow rank supplier");}
        var helm=PoisonDaggerChecks.Named(owner,"Tarnhelm");var armor=new LeatherChest();ClassGear.Profiles.Convert(armor,99,GearRarity.Normal,new Random(173));Require(owner.EquipItem(armor),"Actual copied owner armor");ClassGear.Refresh(owner);
        int rank=NamedSkills.Rank(owner,"assassin.shadowmaster");Require(rank==17,"Tarnhelm effective Shadow rank");var capture=Capture(owner,helm,armor,rank);
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,helm.Serial.Value,armor.Serial.Value,rank,capture.Requests,capture.Seed)));
    }
    private static void Reload()
    {
        Model();var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var owner=(PlayerMobile)World.FindMobile((Serial)s.Owner);var helm=World.FindItem((Serial)s.Helm);var armor=World.FindItem((Serial)s.Armor);
        Require(helm.Parent==owner&&armor.Parent==owner&&NamedSkills.Rank(owner,"assassin.shadowmaster")==s.Rank,"Original owner gear/rank changed");
        var capture=Capture(owner,helm,armor,s.Rank);Require(capture.Seed==s.Seed&&capture.Requests.SequenceEqual(s.Requests),"Original equipment request plan/seed changed");
    }
}
