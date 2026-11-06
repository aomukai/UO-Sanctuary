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

public static class SingleSkillPropertyChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(SingleSkillPropertyChecks));
    private sealed record Held(uint Owner,uint Actor,uint Item,uint Parent,string Gear,string Numeric,int Life,int Mana,int OwnerLife,int OwnerMana,ulong Seed);
    private sealed record Saved(Held[] Actors,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"single-skill-property-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static uint Parent(Item item)=>item.Parent switch{Mobile m=>m.Serial.Value,Item i=>i.Serial.Value,_=>0};
    private sealed class Target(int layer,bool alloc,ulong seed):EquipmentPropertyServices
    {
        internal readonly SortedDictionary<uint,int> Values=new(){[EquipmentStatKey.Pack(107,unchecked((ushort)layer))]=5};
        internal readonly List<int[]> Trace=[];internal ulong Seed=seed;internal int Draws;
        internal override EquipmentPropertyBase Basis=>new(0,0,0,0,0,0,0,0);
        internal override bool Weapon=>false;internal override bool Armor=>false;internal override bool Throwable=>false;
        internal override int BaseValue(int stat)=>throw new InvalidOperationException();
        internal override void SetBase(int stat,int value)=>throw new InvalidOperationException();
        internal override void SetEthereal()=>throw new InvalidOperationException();
        internal override bool GetOrCreateList(int state,int flags){Trace.Add([3,state,flags,alloc?1:0]);return alloc;}
        internal override int ListValue(int stat,int layer)=>Values.GetValueOrDefault(EquipmentStatKey.Pack(stat,layer));
        internal override void SetList(int stat,int value,int layer){Values[EquipmentStatKey.Pack(stat,layer)]=value;Trace.Add([4,stat,value,layer]);}
        internal override void AddList(int stat,int value,int layer){uint key=EquipmentStatKey.Pack(stat,layer);Values[key]=Values.GetValueOrDefault(key)+value;Trace.Add([5,stat,value,layer]);}
        internal override int Roll(int bound){int value=(int)SourceRandom.Limited(ref Seed,bound);Draws++;Trace.Add([6,bound,value,0]);return value;}
    }
    private static void Matrix()
    {
        using var document=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"single-skill-property-cases.json")));int count=0;
        var original=EquipmentPropertyWriter.Plan("skill");
        foreach(var row in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var args=row[0];int A(int i)=>args[i].GetInt32();var target=new Target(A(3),A(7)!=0,ulong.Parse(args[8].GetString()));
            var plan=original with{Steps=[original.Steps[0]with{Set=A(2)!=0}]};
            int result=ItemSingleSkillProperty.Apply(A(1)!=0?plan:null,new(A(4),A(5),A(3)),target,7,0x40);
            Require(result==row[1].GetInt32()&&Json(target.Values.Select(p=>new long[]{p.Key,p.Value}))==row[2].GetRawText()
                &&target.Seed==ulong.Parse(row[3].GetString())&&target.Draws==row[4].GetInt32()&&Json(target.Trace)==row[5].GetRawText(),
                "Complete single skill callback differs at "+count+" (mode/incoming result are source-only ignored controls)");count++;
        }
        Require(count==13824&&!original.Supported&&!ItemPropertyCreationContext.Admitted(original),"Single skill matrix incomplete or generic numeric admission broadened");
        NamedProperty P(JsonElement? parameter)=>new("1","skill","skill",true,parameter,3,3);
        foreach(int id in new[]{-1,0,69,362,428,65535,65536})
        {Require(ItemSingleSkillProperty.Parameter(P(JsonSerializer.SerializeToElement(id)))==id&&ItemSingleSkillProperty.Parameter(P(JsonSerializer.SerializeToElement(id.ToString())))==id,"Numeric skill parameter changed");}
        int names=0;
        foreach(var skill in ItemSkillExecution.All.Where(s=>s.Name.Length>0))
        {Require(ItemSingleSkillProperty.Parameter(P(JsonSerializer.SerializeToElement(skill.Name.ToUpperInvariant())))==skill.Id,"Current source skill name resolved incorrectly");names++;}
        Require(names>400&&ItemSingleSkillProperty.Parameter(P(null))==0,"Source name/missing parameter controls incomplete");
        foreach(string bad in new[]{"not a skill",""})
        {bool refused=false;try{_=ItemSingleSkillProperty.Parameter(P(JsonSerializer.SerializeToElement(bad)));}catch(NotSupportedException){refused=true;}Require(refused,"Unknown skill text silently became Attack");}
    }
    private static void Original(Item item)
    {
        var before=Json(ClassGear.State(item));var numeric=NpcItemStatCapture.Capture(item);
        Require(EquipmentStatValues.Raw(numeric.Properties,EquipmentStatKey.Pack(107,69))==3&&numeric.Properties.Count(s=>s.Stat==107)==1
            &&Json(ClassGear.State(item))==before,"Whole Undead Crown lost or rewrote its rank3 Skeleton Mastery layer");
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
            foreach(bool revive in new[]{false,true})foreach(string key in new[]{"Undead Crown"})
            {
                var p=NpcItemKnockbackChecks.Owner(new NpcKillRecoveryChecks.RecoveryOwner());Item item;BaseCreature source;
                if(revive){FixtureNetwork.Authenticate(net,p,"single-skill-");source=NpcFreezeHitChecks.Revive(p,out item,key,"dmg-ac",0);}
                else
                {
                    item=NamedGear.Create(NamedGear.Catalog.Items.Single(r=>r.SourceKey==key).Id,99,new Random(173),true);
                    source=NpcFreezeHitChecks.Cast(p,item,"dmg-ac",0);((IronGolemBody)source).SuspendActions();
                    if(key=="Undead Crown"){NpcItemProcs.Create((SkeletonGuardian)source);NpcItemProcs.Refresh((SkeletonGuardian)source,NpcCombatEvents.LegacyForSource(source));}
                }
                rows.Add(Hold(p,source,item));
            }
            File.WriteAllText(Manifest,Json(new Saved(rows.ToArray(),0)));
        }
        else
        {
            var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));FixtureNetwork.Authenticate(net,(PlayerMobile)World.FindMobile((Serial)saved.Actors[0].Owner),"single-skill-reload-");
            foreach(var row in saved.Actors)
            {
                var p=(PlayerMobile)World.FindMobile((Serial)row.Owner);var source=World.FindMobile((Serial)row.Actor)as BaseCreature;var item=World.FindItem((Serial)row.Item);
                Require(source is{Alive:true,Deleted:false}&&source.ControlMaster==p&&Parent(item)==row.Parent&&Json(ClassGear.State(item))==row.Gear&&Json(NpcItemStatCapture.Capture(item))==row.Numeric
                    &&SkillHealth.Balance256(source)==row.Life&&NecromancerEffects.ManaBalance256(source)==row.Mana&&SkillHealth.Balance256(p)==row.OwnerLife&&NecromancerEffects.ManaBalance256(p)==row.OwnerMana
                    &&NpcEquipmentRandom.State(source).Seed==row.Seed,"Original tree item/parent/graph/wounds/resources/RNG changed on reload");Original(item);
            }
            File.WriteAllText(Manifest,Json(saved with{Generation=saved.Generation+1}));
        }
        Log.Information("Single skill property:13824complete source controls;ushort layers/set-add/range RNG;current names/numeric IDs/unknown text refusal;original Undead Crown graph Iron/frozen Revive actual contacts;whole gear/numeric layers/parents/wounds/resources/seeds retained;no NPC skill ownership grant");
    }
    public static void Configure()
    {
        foreach(string name in new[]{"single-skill-property-check","single-skill-property-reload-check"})ConsoleInputHandler.RegisterCommand(name,"Original single skill bonus projection",_=>Core.LoopContext.Post(()=>FixtureNetwork.Connected(name,net=>
        {try{Check(net,name.EndsWith("-reload-check",StringComparison.Ordinal));Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}})));
    }
}
