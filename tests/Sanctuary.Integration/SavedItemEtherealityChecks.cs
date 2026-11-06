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

public static class SavedItemEtherealityChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(SavedItemEtherealityChecks));
    private static readonly int[] Physical=[21,22,23,24,159,160,31];
    private sealed record Held(uint Owner,uint Actor,uint Item,uint Parent,string Gear,string Numeric,int Life,int Mana,int OwnerLife,int OwnerMana,ulong Seed);
    private sealed record Saved(Held[] Actors,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"saved-item-ethereality-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static uint Parent(Item item)=>item.Parent switch{Mobile m=>m.Serial.Value,Item i=>i.Serial.Value,_=>0};
    private sealed class Target(bool weapon,int odd,bool ethereal):EquipmentPropertyServices
    {
        internal bool Ethereal=ethereal;internal int Flags, Writes,MaxReads,IndReads;
        internal readonly Dictionary<int,int> Values=Physical.Select((stat,i)=>(stat,value:i*10+odd)).ToDictionary(x=>x.stat,x=>x.value);
        internal override EquipmentPropertyBase Basis=>new(0,0,0,0,0,0,0,0);
        internal override bool Weapon=>weapon;internal override bool Armor=>!weapon;internal override bool Throwable=>false;
        internal override int BaseValue(int stat)=>Values.GetValueOrDefault(stat);
        internal override void SetBase(int stat,int value){Require(Ethereal,"Ethereal flag did not precede base writes");Values[stat]=value;Writes++;}
        internal override void SetEthereal(){Ethereal=true;Flags++;}
        internal override bool GetOrCreateList(int state,int flags)=>throw new InvalidOperationException("Ethereality allocated a property list");
        internal override int ListValue(int stat,int layer)=>throw new InvalidOperationException();
        internal override void SetList(int stat,int value,int layer)=>throw new InvalidOperationException();
        internal override void AddList(int stat,int value,int layer)=>throw new InvalidOperationException();
        internal override int Roll(int bound)=>throw new InvalidOperationException("Ethereality drew item RNG");
        internal int[] State(bool result)=>new[]{result?1:0,Ethereal?0x400000:0,MaxReads,IndReads,Flags,Writes}.Concat(Physical.Select(BaseValue)).ToArray();
    }
    private static void SourceCases()
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"saved-item-ethereality-cases.json")));int count=0;
        foreach(var row in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            int[] a=row[0].EnumerateArray().Select(v=>v.GetInt32()).ToArray();var target=new Target(a[9]!=0,a[10],a[3]!=0);
            bool Durable()=>SavedItemEthereality.HasDurability(a[0]!=0,a[1],a[4]!=0,a[5]!=0,a[6],()=>{target.MaxReads++;return a[7];},()=>{target.IndReads++;return a[8];});
            foreach(int phase in new[]{1,2})
            {
                bool result=SavedItemEthereality.Apply(a[2]!=0,a[0]!=0,a[1],target.Ethereal,Durable,target);
                Require(target.State(result).SequenceEqual(row[phase].EnumerateArray().Select(v=>v.GetInt32())),"Complete ethereal guard/order/base trace differs at "+count+"/"+phase);
            }
            count++;
        }
        Require(count==13824,"Incomplete ethereal source matrix");int bases=0;
        foreach(var row in document.RootElement.GetProperty("bases").EnumerateArray())
        {var facts=SavedItemEthereality.Facts(row[0].GetString());Require(facts==(row[1].GetInt32(),row[2].GetInt32()!=0),"Owned durability metadata differs");bases++;}
        Require(bases==692,"Incomplete owned durability catalogue");
    }
    private static void GuardQueries()
    {
        var item=new Dagger();try
        {
            var values=new EquipmentStatValues(_=>0,_=>null,_=>null,(_,_,_,_,_)=>{});int count=0;
            foreach(int basis in new[]{0,1,40})foreach(int flat in new[]{0,1,5})foreach(int percent in new[]{-100,-50,0,100})foreach(int indestruct in new[]{-1,0,1})
            {
                var properties=IronGolemBodyRecipes.Fields((73,flat),(75,percent),(152,indestruct));
                var graph=ItemPropertyCreationContext.Restore(item,values,new(0,0,0,0,0,0,0,0),false,false,false,new(173,false,properties.Length>0,IronGolemBodyRecipes.Fields((73,basis)),properties));
                string before=Json(graph.Capture());var guard=SavedItemEthereality.Guard(item,graph.Unit,values,basis);
                Require(values.Query(guard.Unit.Stats,EquipmentStatKey.Pack(73))==basis+flat+(int)(basis*(percent/100.0))
                    &&values.Query(guard.Unit.Stats,EquipmentStatKey.Pack(152))==indestruct&&Json(graph.Capture())==before&&graph.Draws==0,
                    "Private guard lost base/flat/op13/indestructible or mutated original graph");count++;
            }
            Require(count==108,"Incomplete durability guard query matrix");
        }
        finally{item.Delete();}
    }
    private static void Original(Item item)
    {
        string before=Json(ClassGear.State(item));var numeric=NpcItemStatCapture.Capture(item);var basis=ClassGear.Base(item);
        Require(NamedGear.Ethereal(item),"Original ethereal identity missing");
        int[] raw=[basis.MinDamage,basis.MaxDamage,basis.TwoHandMin,basis.TwoHandMax,basis.MissileMin,basis.MissileMax,0];
        for(int i=0;i<Physical.Length;i++)
        {Require(EquipmentStatValues.Raw(numeric.ItemBase,EquipmentStatKey.Pack(Physical[i]))==3*raw[i]/2,"Original ethereal physical base was omitted or applied twice: "+Physical[i]);}
        Require(Json(ClassGear.State(item))==before,"Projection mutated original gear/rolls");
        if(ClassGear.State(item).NamedItemId!=NamedGear.Catalog.Items.Single(r=>r.SourceKey=="Wraithflight").Id)
        {Require(numeric.Properties.Any(s=>s.Stat==152&&s.Value==1),"Later indestructible property was stripped");}
    }
    private static Held Hold(PlayerMobile p,BaseCreature source,Item item)
    {
        Original(item);var target=new NpcItemSlowChecks.Recipient();target.SetHits(20000);target.Hits=target.HitsMax;
        foreach(var type in Enum.GetValues<ResistanceType>()){target.SetResistance(type,0);}target.MoveToWorld(new(source.X+1,source.Y,source.Z),p.Map);target.AIObject?.AITimer.Stop();
        var block=SourceBlocking.BlockRoll;var choice=SourceBlocking.ChoiceRoll;int ownerLife=SkillHealth.Balance256(p),ownerMana=NecromancerEffects.ManaBalance256(p);
        try
        {
            SourceBlocking.BlockRoll=n=>n-1;SourceBlocking.ChoiceRoll=n=>Math.Min(1,n-1);int life=SkillHealth.Balance256(target);
            var prepared=IronGolemContact.Prepare(source,target,IronGolemContact.Require(source,p,target),()=>5);var seed=NpcEquipmentRandom.State(source);
            IronGolemContact.Release(source,p,target,prepared,_=>true,TimedEffects.Now);
            Require(target.Alive&&SkillHealth.Balance256(target)<life&&NpcEquipmentRandom.State(source)==seed
                &&SkillHealth.Balance256(p)==ownerLife&&NecromancerEffects.ManaBalance256(p)==ownerMana,"Original ethereal contact failed or forwarded resources/RNG");
        }
        finally{SourceBlocking.BlockRoll=block;SourceBlocking.ChoiceRoll=choice;target.Delete();}
        SkillHealth.SetBalance256(source,SkillHealth.Maximum256(source)-1237);TimedEffects.PreventHealing(source,TimedEffects.Now);
        var result=new Held(p.Serial.Value,source.Serial.Value,item.Serial.Value,Parent(item),Json(ClassGear.State(item)),Json(NpcItemStatCapture.Capture(item)),SkillHealth.Balance256(source),NecromancerEffects.ManaBalance256(source),SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),NpcEquipmentRandom.State(source).Seed);
        PersistentMinions.Suspend(p);p.Internalize();return result;
    }
    private static void Check(NetState net,bool reload)
    {
        SourceCases();GuardQueries();
        if(!reload)
        {
            var saved=new List<Held>();
            foreach(bool revive in new[]{false,true})foreach(string key in new[]{"Ethereal Edge","Ghostflame","Wraithflight"})
            {
                var p=NpcItemKnockbackChecks.Owner(new NpcKillRecoveryChecks.RecoveryOwner());Item item;BaseCreature actor;
                if(revive){FixtureNetwork.Authenticate(net,p,"saved-eth-");actor=NpcFreezeHitChecks.Revive(p,out item,key,"dmg-ac",0);}
                else
                {
                    item=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==key).Id,99,new Random(173),true);actor=NpcFreezeHitChecks.Cast(p,item,"dmg-ac",0);((IronGolemBody)actor).SuspendActions();
                    if(key=="Ghostflame"){NpcItemProcs.Create((SkeletonGuardian)actor);NpcItemProcs.Refresh((SkeletonGuardian)actor,NpcCombatEvents.LegacyForSource(actor));}
                }
                saved.Add(Hold(p,actor,item));
            }
            File.WriteAllText(Manifest,Json(new Saved(saved.ToArray(),0)));
        }
        else
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));FixtureNetwork.Authenticate(net,(PlayerMobile)World.FindMobile((Serial)saved.Actors[0].Owner),"saved-eth-reload-");
            foreach(var row in saved.Actors)
            {
                var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var actor=World.FindMobile((Serial)row.Actor)as BaseCreature;var item=World.FindItem((Serial)row.Item);
                Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster==p&&Parent(item)==row.Parent&&Json(ClassGear.State(item))==row.Gear&&Json(NpcItemStatCapture.Capture(item))==row.Numeric
                    &&SkillHealth.Balance256(actor)==row.Life&&NecromancerEffects.ManaBalance256(actor)==row.Mana&&SkillHealth.Balance256(p)==row.OwnerLife&&NecromancerEffects.ManaBalance256(p)==row.OwnerMana
                    &&NpcEquipmentRandom.State(actor).Seed==row.Seed,"Original ethereal actor/item/parent/base graph/wounds/resources/RNG changed on reload");Original(item);
            }
            File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
        }
        Log.Information("Saved item ethereality:13824complete source contexts/692owned bases/108guard queries;ordered property23/shared physical writer;original Edge/Ghostflame/Wraithflight legacy/graph Iron/frozen Revive contact and exact parent/gear/base/wound/resource/RNG reload;random/native flags and current executable remain separate");
    }
    public static void Configure()
    {
        foreach(string name in new[]{"saved-item-ethereality-check","saved-item-ethereality-reload-check"})ConsoleInputHandler.RegisterCommand(name,"Original forced-ethereal property projection",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected(name,net=>
        {try{Check(net,name.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}})));
    }
}
