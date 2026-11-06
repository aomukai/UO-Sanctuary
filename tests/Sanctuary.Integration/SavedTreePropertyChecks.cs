// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class SavedTreePropertyChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(SavedTreePropertyChecks));
    private sealed record Held(uint Owner,uint Actor,uint Item,uint Parent,string Gear,string Numeric,int Life,int Mana,int OwnerLife,int OwnerMana,ulong Seed);
    private sealed record Saved(Held[] Actors,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"saved-tree-property-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static uint Parent(Item item)=>item.Parent switch{Mobile m=>m.Serial.Value,Item i=>i.Serial.Value,_=>0};
    private static void Matrix()
    {
        var item=new Dagger();try
        {
            int cases=0;var plan=EquipmentPropertyWriter.Plan("skilltab");
            for(int parameter=0;parameter<24;parameter++)foreach(int value in new[]{0,1,2,3,20,255})
            {
                var values=new EquipmentStatValues(_=>0,_=>null,_=>null,(_,_,_,_,_)=>{});
                var graph=new ItemPropertyCreationContext(item,values,new(0,0,0,0,0,0,0,0),false,false,false,173,[]);
                CurrentTreePropertyWriter.Apply(plan,new(value,value,parameter),graph);var result=graph.Capture();int layer=8*(parameter/3)+parameter%3;
                Require(EquipmentStatValues.Raw(result.Properties,EquipmentStatKey.Pack(188,layer))==value
                    &&result.Properties.All(s=>s.Stat==188&&(ushort)s.Packed==layer)&&result.ItemBase.Length==0&&graph.Draws==0&&result.Seed==173,
                    "Fixed saved tree input lost class/tree layer or drew RNG");cases++;
                string before=Json(result);
                foreach(int invalid in new[]{-1,24})
                {bool refused=false;try{CurrentTreePropertyWriter.Apply(plan,new(value,value,invalid),graph);}catch(NotSupportedException){refused=true;}Require(refused&&Json(graph.Capture())==before,"Invalid tree parameter mutated graph");}
            }
            Require(cases==144&&!CurrentTreePropertyWriter.Supports(EquipmentPropertyWriter.Plan("war-tab-rand")),"Incomplete tree matrix or unapproved random Warlock tree substitution");
        }
        finally{item.Delete();}
    }
    private static void Original(Item item)
    {
        var before=Json(ClassGear.State(item));var numeric=NpcItemStatCapture.Capture(item);var expected=new Dictionary<int,int>();
        foreach(var (property,roll) in EquipmentIntrinsicProperties.For(item).Where(x=>x.Property.Code=="skilltab"))
        {
            int parameter=NamedPropertyRuntime.Parameter(property),layer=8*(parameter/3)+parameter%3;
            expected[layer]=expected.GetValueOrDefault(layer)+roll.Value;
        }
        Require(expected.Count>0&&numeric.Properties.Where(s=>s.Stat==188).All(s=>expected.GetValueOrDefault((ushort)s.Packed)==s.Value)
            &&expected.All(p=>EquipmentStatValues.Raw(numeric.Properties,EquipmentStatKey.Pack(188,p.Key))==p.Value)
            &&Json(ClassGear.State(item))==before,"Whole original item lost or rewrote tree properties");
        if(ClassGear.State(item).NamedItemId==NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Cutthroat1").Id)
        {Require(expected.Count==1&&expected[50]==1&&EquipmentStatValues.Raw(numeric.Properties,EquipmentStatKey.Pack(83,6))==2,"Original Bartuc class/tree layers differ");}
    }
    private static Held Hold(PlayerMobile p,BaseCreature source,Item item)
    {
        Original(item);var target=new NpcItemSlowChecks.Recipient();target.SetHits(20000);target.Hits=target.HitsMax;
        foreach(var kind in Enum.GetValues<ResistanceType>()){target.SetResistance(kind,0);}
        target.MoveToWorld(new(source.X+1,source.Y,source.Z),p.Map);target.AIObject?.AITimer.Stop();
        var block=SourceBlocking.BlockRoll;var choice=SourceBlocking.ChoiceRoll;int ownerLife=SkillHealth.Balance256(p),ownerMana=NecromancerEffects.ManaBalance256(p);
        try
        {
            SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=n=>Math.Min(1,n-1);int before=SkillHealth.Balance256(target);
            var prepared=IronGolemContact.Prepare(source,target,IronGolemContact.Require(source,p,target),()=>5);var seed=NpcEquipmentRandom.State(source);
            IronGolemContact.Release(source,p,target,prepared,_=>true,TimedEffects.Now);
            Require(target.Alive&&SkillHealth.Balance256(target)<before&&NpcEquipmentRandom.State(source)==seed
                &&SkillHealth.Balance256(p)==ownerLife&&NecromancerEffects.ManaBalance256(p)==ownerMana,"Whole tree item actual contact failed or forwarded resources/RNG");
        }
        finally{SourceBlocking.BlockRoll=block;SourceBlocking.ChoiceRoll=choice;target.Delete();}
        SkillHealth.SetBalance256(source,SkillHealth.Maximum256(source)-1237);TimedEffects.PreventHealing(source,TimedEffects.Now);
        var held=new Held(p.Serial.Value,source.Serial.Value,item.Serial.Value,Parent(item),Json(ClassGear.State(item)),Json(NpcItemStatCapture.Capture(item)),SkillHealth.Balance256(source),NecromancerEffects.ManaBalance256(source),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),NpcEquipmentRandom.State(source).Seed);
        PersistentMinions.Suspend(p);p.Internalize();return held;
    }
    private static void Check(NetState net,bool reload)
    {
        Matrix();
        if(!reload)
        {
            var rows=new List<Held>();
            foreach(bool revive in new[]{false,true})foreach(string key in new[]{"Cutthroat1","Jadetalon"})
            {
                var p=NpcItemKnockbackChecks.Owner(new NpcKillRecoveryChecks.RecoveryOwner());Item item;BaseCreature source;
                if(revive){FixtureNetwork.Authenticate(net,p,"saved-tree-");source=NpcFreezeHitChecks.Revive(p,out item,key,"dmg-ac",0);}
                else
                {
                    item=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==key).Id,99,new Random(173),true);
                    source=NpcFreezeHitChecks.Cast(p,item,"dmg-ac",0);((IronGolemBody)source).SuspendActions();
                    if(key=="Jadetalon"){NpcItemProcs.Create((SkeletonGuardian)source);NpcItemProcs.Refresh((SkeletonGuardian)source,NpcCombatEvents.LegacyForSource(source));}
                }
                rows.Add(Hold(p,source,item));
            }
            File.WriteAllText(Manifest,Json(new Saved(rows.ToArray(),0)));
        }
        else
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));FixtureNetwork.Authenticate(net,(PlayerMobile)World.FindMobile((Serial)saved.Actors[0].Owner),"saved-tree-reload-");
            foreach(var row in saved.Actors)
            {
                var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var source=World.FindMobile((Serial)row.Actor)as BaseCreature;var item=World.FindItem((Serial)row.Item);
                Require(source is{Alive:true,Deleted:false}&&source.ControlMaster==p&&Parent(item)==row.Parent&&Json(ClassGear.State(item))==row.Gear&&Json(NpcItemStatCapture.Capture(item))==row.Numeric
                    &&SkillHealth.Balance256(source)==row.Life&&NecromancerEffects.ManaBalance256(source)==row.Mana&&SkillHealth.Balance256(p)==row.OwnerLife&&NecromancerEffects.ManaBalance256(p)==row.OwnerMana
                    &&NpcEquipmentRandom.State(source).Seed==row.Seed,"Original tree item/parent/graph/wounds/resources/RNG changed on reload");Original(item);
            }
            File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
        }
        Log.Information("Saved tree properties:144fixed class/tree/value controls;approved packing/shared writer/no RNG;original Bartuc/Jade legacy/graph Iron and frozen Revive actual contacts;whole gear/numeric layers/parents/wounds/resources/seeds retained;no random Warlock tree substitution");
    }
    public static void Configure()
    {
        foreach(string name in new[]{"saved-tree-property-check","saved-tree-property-reload-check"})ConsoleInputHandler.RegisterCommand(name,"Saved original tree bonus projection",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected(name,net=>
        {try{Check(net,name.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}})));
    }
}
