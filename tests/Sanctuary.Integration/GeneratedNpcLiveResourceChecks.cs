// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
using Server.Items;
using Server.Regions;
namespace Sanctuary.Integration;

public static class GeneratedNpcLiveResourceChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(GeneratedNpcLiveResourceChecks));
    private sealed record Held(int Case,uint Actor,uint[] Items,string[] Payloads,int Life,int Mana,int Stamina,int MaximumLife,int MaximumMana,int MaximumStamina,string State);
    private sealed record Saved(uint Owner,Held[] Actors,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"generated-npc-live-resources-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    public static void Configure()
    {
        Register("generated-npc-live-resources-check",false);Register("generated-npc-live-resources-reload-check",true);
    }
    [CallPriority(int.MaxValue)] public static void Initialize()
    {
        // Stamina recovery cadence is a separate consumer. Freeze this
        // inspection roster so saved fractional wounds have exact witnesses.
        var prior=ClassGearHooks.CanRegenerateStamina;
        ClassGearHooks.CanRegenerateStamina=actor=>actor.Name?.StartsWith("GeneratedLiveResourceFixture",StringComparison.Ordinal)!=true&&prior?.Invoke(actor)!=false;
        EventSink.WorldLoad+=()=>{if(File.Exists(Manifest)){foreach(var h in JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)).Actors){if(World.FindMobile((Serial)h.Actor)is BaseCreature b){b.AIObject?.AITimer.Stop();}}}};
    }
    private static void Register(string command,bool reload)=>ConsoleInputHandler.RegisterCommand(command,"Generated NPC live resource persistence",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",command);}catch(Exception e){Log.Error(e,"{Name}: failed",command);}}));
    private static EquipmentStatValue[] Basis(int level)=>new[]{(0,100),(1,20),(2,50),(3,30),(6,123457),(7,256000),(8,34567),(9,76800),(10,12799),(11,25600),(12,level),(21,7),(22,14),(31,80),(74,17)}
        .Select(p=>new EquipmentStatValue(EquipmentStatKey.Pack(p.Item1),p.Item2)).ToArray();
    private static PreparedItemRequest Request(JsonElement a)=>new(a[0].GetString(),a[1].GetInt32(),a[2].GetInt32(),a[3].GetInt32(),ulong.Parse(a[4].GetString()),ulong.Parse(a[5].GetString()),a[6].GetUInt32());
    private static SkeletonGuardian Actor(PlayerMobile owner)
    {
        var actor=new SkeletonGuardian{Name="GeneratedLiveResourceFixture",Frozen=true,CantWalk=true};actor.Initialize(owner,2,0);Require(actor.SetControlMaster(owner),"Live resource owner");
        PersistentMinions.Profiles.Track(actor);actor.MoveToWorld(owner.Location,owner.Map);actor.AIObject?.AITimer.Stop();return actor;
    }
    private static int Max(BaseCreature actor,int channel)=>GeneratedNpcLiveResources.Maximum256(actor,channel)??throw new InvalidOperationException("Missing live maximum");
    private static Held Store(BaseCreature actor,int source)
    {
        var items=GeneratedNpcEquipment.Items(actor);return new(source,actor.Serial.Value,items.Select(i=>i.Serial.Value).ToArray(),items.Select(i=>i.SavedText).ToArray(),
            SkillHealth.Balance256(actor),NecromancerEffects.ManaBalance256(actor),BattleOrders.Stamina256(actor),Max(actor,0),Max(actor,2),Max(actor,1),Json(GeneratedNpcLiveResources.State(actor)));
    }
    private static void Partial(BaseCreature actor)
    {
        SkillHealth.SetBalance256(actor,Max(actor,0)/3+17);NecromancerEffects.SetMana256(actor,Max(actor,2)/3+11);BattleOrders.SetStamina256(actor,Max(actor,1)/2+31);
    }
    private static void Ratio(BaseCreature actor,BattleOrders.Pools before,string why)
    {
        Require(SkillHealth.Balance256(actor)==BattleOrders.Rescale256(before.Life256,before.ExactLifeMaximum256,Max(actor,0))
            &&NecromancerEffects.ManaBalance256(actor)==BattleOrders.Rescale256(before.Mana256,before.ExactManaMaximum256,Max(actor,2))
            &&BattleOrders.Stamina256(actor)==BattleOrders.Rescale256(before.Stamina256,before.ExactStaminaMaximum256,Max(actor,1)),why);
        Require(actor.HitsMax==(Max(actor,0)+255)/256&&actor.ManaMax==(Max(actor,2)+255)/256&&actor.StamMax==(Max(actor,1)+255)/256,"Native maximum hooks disagree");
    }
    private static void Transitions(BaseCreature actor,PlayerMobile orders)
    {
        Partial(actor);var state=GeneratedNpcLiveResources.State(actor);int life=state.Basis.Single(v=>v.Stat==7).Value;
        var before=BattleOrders.Capture(actor);GeneratedNpcLiveResources.ChangeBase(actor,7,life*2);Ratio(actor,before,"Source base increase rescaled twice/lost fractions");
        Require(GeneratedNpcLiveResources.RegenerationBase(actor)==3*(Max(actor,0)>>8)>>4,"Source life callback regeneration");
        before=BattleOrders.Capture(actor);GeneratedNpcLiveResources.ChangeBase(actor,7,life);Ratio(actor,before,"Source base decrease");
        uint item=GeneratedNpcEquipment.Items(actor)[0].Serial.Value;
        before=BattleOrders.Capture(actor);GeneratedNpcLiveResources.SetItemActive(actor,item,false);Ratio(actor,before,"Logical original item removal");
        before=BattleOrders.Capture(actor);GeneratedNpcLiveResources.SetItemActive(actor,item,true);Ratio(actor,before,"Logical original item reattachment");
        before=BattleOrders.Capture(actor);GeneratedNpcLiveResources.SetItemActive(actor,item,true);Require(before==BattleOrders.Capture(actor),"Repeated attachment changed pools");
        before=BattleOrders.Capture(actor);BattleOrders.Apply(orders,actor,1,TimedEffects.Now);Ratio(actor,before,"Orders gain double-applied source callback");
        before=BattleOrders.Capture(actor);BattleOrders.Clear(actor);Ratio(actor,before,"Orders removal double-applied source callback");
        var auras=PaladinAuras.Snapshot();
        try
        {
            // Exercise the public lease persistence/service boundary and the
            // same Refresh producer used by actual Vigor application/removal.
            var lease=new PaladinAuraLease(actor.Serial.Value,orders.Serial.Value,CombatAbility.Vigor,1,100,TimedEffects.Now.AddHours(1),StaminaPercent:100);
            before=BattleOrders.Capture(actor);PaladinAuras.Restore(auras with{Leases=auras.Leases.Append(lease).ToArray()});PaladinVigor.Refresh(actor);Ratio(actor,before,"Vigor gain");
            before=BattleOrders.Capture(actor);PaladinAuras.Restore(auras);PaladinVigor.Refresh(actor);Ratio(actor,before,"Vigor removal");
        }
        finally{PaladinAuras.Restore(auras);PaladinVigor.Refresh(actor);}
        var oak=DruidOakAuras.Snapshot();
        try
        {
            var lease=new OakLease(actor.Serial.Value,orders.Serial.Value,orders.Serial.Value,1,30,TimedEffects.Now.AddHours(1),false);
            before=BattleOrders.Capture(actor);DruidOakAuras.Restore(oak with{Leases=oak.Leases.Append(lease).ToArray()});BattleOrders.Change(actor,before);Ratio(actor,before,"Oak gain");
            before=BattleOrders.Capture(actor);DruidOakAuras.Restore(oak);BattleOrders.Change(actor,before);Ratio(actor,before,"Oak removal");
        }
        finally{DruidOakAuras.Restore(oak);var prior=BattleOrders.Capture(actor);BattleOrders.Change(actor,prior);}
        SkillHealth.SetBalance256(actor,1);NecromancerEffects.SetMana256(actor,0);BattleOrders.SetStamina256(actor,0);
        GeneratedNpcLiveResources.ChangeBase(actor,7,life/2);
        Require(SkillHealth.Balance256(actor)==1&&NecromancerEffects.ManaBalance256(actor)==0&&BattleOrders.Stamina256(actor)==0,"Tiny/empty pool became recovery");
        GeneratedNpcLiveResources.ChangeBase(actor,7,life);Partial(actor);
        var stable=Store(actor,-1);bool refused=false;
        try{GeneratedNpcLiveResources.ChangeBase(actor,7,16640001);}catch(InvalidDataException){refused=true;}
        Require(refused&&Json(Store(actor,-1))==Json(stable),"Out-of-domain profile changed live state");
        // Array equality in records is reference equality, so use JSON for
        // all retained inventory/state witness comparisons below.
        var copy=GeneratedNpcLiveResources.State(actor);copy.Basis[0]=new(0,99999);copy.ActiveItems[0]=0;
        Require(Json(GeneratedNpcLiveResources.State(actor))==stable.State,"Mutable recipe alias changed saved source state");
        int old=SkillHealth.Balance256(actor),rate=GeneratedNpcLiveResources.RegenerationBase(actor).Value+NpcEquipmentRegeneration.ItemRate(actor);
        NpcEquipmentRegeneration.Reset(actor);NpcEquipmentRegeneration.Recover(actor,10000);NpcEquipmentRegeneration.Recover(actor,10040);
        Require(SkillHealth.Balance256(actor)==old+rate,"Live source regeneration was omitted or doubled");
        Partial(actor);
    }
    private static void Check(bool reload)
    {
        using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"generated-npc-resources-cases.json")));
        using var raw=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"item-quality-current-cases.json")));
        var root=source.RootElement;var saved=reload?JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest)):new Saved(0,[],0);PlayerMobile owner;
        if(reload){owner=World.FindMobile((Serial)saved.Owner)as PlayerMobile;Require(owner is{Alive:true,Deleted:false},"Original live owner missing");FixtureSpace.Place(owner,new(6150,2900,0),Map.Trammel);}
        else
        {
            owner=new PlayerMobile{Name="GeneratedLiveResourceOwner",Body=0x190,Player=true};ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,47);
            Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _)&&ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Live holder prerequisite");FixtureSpace.Place(owner,new(6150,2900,0),Map.Trammel);
        }
        var supplier=ClassSkillChecks.Player(DiabloClass.Barbarian);var rows=new List<Held>();
        try
        {
            int[] shapes=[8,587,576,578,602];
            foreach(int shape in shapes)
            {
                BaseCreature actor;var input=root.GetProperty("numericInputs")[shape];var output=root.GetProperty("numericOutputs")[shape][0][0];
                if(reload)
                {
                    var held=saved.Actors.Single(h=>h.Case==shape);actor=World.FindMobile((Serial)held.Actor)as BaseCreature;
                    Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster==owner,"Original live recipient lost");
                    Require(Json(Store(actor,shape))==Json(held),"Reload reconstructed wounds,regeneration,item identity or source recipe: "+shape+" actual="+Json(Store(actor,shape))+" expected="+Json(held));
                    actor.MoveToWorld(owner.Location,owner.Map);actor.AIObject?.AITimer.Stop();
                }
                else
                {
                    actor=Actor(owner);
                    foreach(var index in input.GetProperty("Items").EnumerateArray())
                    {
                        var item=root.GetProperty("items")[index.GetInt32()];var request=Request(raw.RootElement.GetProperty("inputs")[item.GetProperty("Case").GetInt32()]);
                        Require(GeneratedNpcEquipment.TryCreate(actor,owner,(EquipMonster)input.GetProperty("Kind").GetInt32(),request,item.GetProperty("Body").GetInt32(),out _),"Original live equipment delivery");
                    }
                    GeneratedNpcLiveResources.Activate(actor,Basis(input.GetProperty("Level").GetInt32()),3);
                    var expected=output[2].EnumerateArray().ToDictionary(v=>v[0].GetUInt32(),v=>v[1].GetInt32());int E(int stat)=>expected.GetValueOrDefault(EquipmentStatKey.Pack(stat));
                    Require(Max(actor,0)==E(7)&&Max(actor,2)==E(9)&&Max(actor,1)==E(11)&&SkillHealth.Balance256(actor)==E(6)
                        &&NecromancerEffects.ManaBalance256(actor)==E(8)&&BattleOrders.Stamina256(actor)==E(10),"Native birth differs from source item/resource composition");
                }
                Transitions(actor,supplier);rows.Add(Store(actor,shape));
            }
            // Park the original roster for exact offline/reload witnesses.
            PersistentMinions.Suspend(owner);owner.Internalize();
            File.WriteAllText(Manifest,Json(new Saved(owner.Serial.Value,rows.ToArray(),reload?saved.Generation+1:0)));
        }
        finally{supplier.Delete();}
        Log.Information("Generated NPC live resources:5original recipients;source-matched birth;native fractional maxima/pools,base/item/Orders/Vigor transitions,tiny/empty guards and regeneration;source recipes/items/wounds persist without replay recovery");
    }
}
