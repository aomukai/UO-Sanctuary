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

public static class ValkyrieDamageChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(ValkyrieDamageChecks));
    private sealed record Held(int Case,uint Body,string Text,uint[] Items,string[] Payloads,NpcEquipmentSeed Random,int[] Pools,string Stats);
    private sealed record Saved(uint Owner,Held[] Bodies,int Generation=0);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"valkyrie-damage-fixtures.json");
    private static string Json<T>(T v)=>JsonSerializer.Serialize(v);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static int[] Array(JsonElement a)=>a.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static int[] Fields(NpcFillPacket p)=>[p.Physical,p.Fire,p.Lightning,p.Cold,p.Magic,p.Poison,p.ColdFrames,p.PoisonFrames,p.StunFrames,p.LifeDrain,p.ManaDrain,p.StaminaDrain,p.AdditionalPercent,(int)p.HitFlags,p.ResultFlags,p.ConversionType,p.ConversionPercent,p.FreezeFrames,p.BurnFrames,p.HitClass,p.Overlay];
    private static NpcFillPacket Packet(int[] a)=>new(a[0],a[1],a[2],a[3],a[4],a[5],a[6],a[7],a[8],a[9],a[10],a[11],a[12],(uint)a[13],a[14],a[15],a[16],a[17],a[18],a[19],a[20]);
    private static int[] Pools(AmazonValkyrieBody b)=>[SkillHealth.Balance256(b),NecromancerEffects.ManaBalance256(b),BattleOrders.Stamina256(b)];
    public static void Configure(){Register("valkyrie-damage-check",false);Register("valkyrie-damage-reload-check",true);}
    private static void Register(string command,bool reload)=>ConsoleInputHandler.RegisterCommand(command,"Original Valkyrie damage allocation and inherited skill consumers",_=>Core.LoopContext.Post(()=>
    {try{Check(reload);Log.Information("{Name}: passed",command);}catch(Exception e){Log.Error(e,"{Name}: failed",command);}}));
    private static void RankCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-innate-skill-rank-cases.json")));int count=0;
        foreach(var c in doc.RootElement.EnumerateArray())
        {
            var a=Array(c.GetProperty("Args"));int actual=NpcInnateSkillRank.Read(a[0],a[1],a[2],a[3],a[4],a[5],a[6]!=0,a[7]!=0,a[8]!=0,a[9]!=0,a[10],a[11]!=0,a[12]!=0,a[13]!=0,a[14]!=0);
            Require(actual==c.GetProperty("Rank").GetInt32(),"Complete NPC skill getter mismatch at "+count);count++;
        }
        Require(count==8192,"Missing complete NPC skill getter controls");
        var d=SkillMechanics.For(CombatAbility.CriticalStrike);
        Require(new SkillValues(d,_=>2,_=>0,99).Eval("passivecalc1")==25&&new SkillValues(d,_=>12,_=>0,99).Eval("passivecalc1")==59,"Current user Critical Strike2/12 observations differ");
    }
    internal static AmazonValkyrieBody Body(PlayerMobile p,JsonElement c)
    {
        int rank=c.GetProperty("Rank").GetInt32();var difficulty=(MonsterDifficulty)c.GetProperty("Difficulty").GetInt32();var b=new AmazonValkyrieBody();
        var birth=ValkyrieBirthNumbers.Capture(p,400*256)with{Rank=rank,Critical=c.GetProperty("Critical").GetInt32(),Dodge=1,Avoid=3,Evade=20};
        var state=new ValkyrieBodyState(p.Serial.Value,birth,difficulty,TimedEffects.Now.AddDays(2));b.RestorePrepared(state);var facets=SummonDifficulties.Facets();
        try{SummonDifficulties.SetFacets(facets.Where(f=>f.Map!=p.Map.MapID).Append(new(p.Map.MapID,difficulty)).ToArray());b.Initialize(p,rank,0);}finally{SummonDifficulties.SetFacets(facets);}
        b.RestorePrepared(state);b.SetStr(rank*25);b.SetDex(rank*12);Require(b.SetControlMaster(p),"Original damage owner refused");PersistentMinions.Profiles.Track(b);
        b.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);b.AIObject?.AITimer.Stop();SkillHealth.SetBalance256(b,b.MaximumLife256-257);
        ValkyrieEquipment.Materialize(b,new(SourceRandom.InitLow(100+rank)),new(SourceRandom.InitLow(900+(int)difficulty)));ValkyrieEquipmentStats.Activate(b);ValkyrieEquipmentWeapons.Activate(b,ulong.Parse(c.GetProperty("Seed").GetString()));return b;
    }
    private static Held Store(AmazonValkyrieBody b,int index)
    {var items=ValkyrieEquipment.Originals(b);return new(index,b.Serial.Value,b.SavedText,items.Select(i=>i.Serial.Value).ToArray(),items.Select(i=>i.SavedText).ToArray(),NpcEquipmentRandom.State(b),Pools(b),Json(b.EquipmentNumbers.Stats));}
    private static GeneratedNpcFillTarget Target(int variant,Func<int> armor,Action<int> set)=>new(variant!=4,true,variant is 1 or 3,variant is 2 or 3,variant==3?7:0,armor,set,(layer,type)=>layer%3==type%3);
    private static void Inspect(AmazonValkyrieBody b,JsonElement c,int generation,PlayerMobile supplier)
    {
        var before=Store(b,-1);int count=0;
        foreach(int command in new[]{0,1})
        {
            if(command==1){BattleCommand.Apply(supplier,b,1,TimedEffects.Now);Require(BattleCommand.Bonus(b)==1,"Source Command rank1 context");}
            foreach(var r in c.GetProperty("Packets").EnumerateArray().Where(r=>r.GetProperty("Generation").GetInt32()==generation&&r.GetProperty("Command").GetInt32()==command))
            {
                int variant=r.GetProperty("Variant").GetInt32(),armor=20,share=new[]{0,64,128,255,128}[variant];var seed=NpcEquipmentRandom.State(b);var pools=Pools(b);string stats=Json(b.EquipmentNumbers.Stats);
                Require(seed.Seed==ulong.Parse(r.GetProperty("Before").GetString()),"Original packet RNG start differs case="+c.GetProperty("Case")+" generation="+generation);
                var passive=ValkyrieEquipmentDamage.Passives(b);var expected=r.GetProperty("Passives").EnumerateArray().Select(Array).Where(a=>a[1]>0).ToArray();
                // Captured grant order is Dodge/Avoid/Evade/Critical. The
                // independent allocation fixture lists Critical first. This
                // read-only value view has no ordering-sensitive side effects.
                var actual=passive.Select(v=>new[]{v.Skill,v.Rank,v.Stat,v.Value}).OrderBy(a=>a[0]).ToArray();
                Require(Json(actual)==Json(expected.OrderBy(a=>a[0]).ToArray()),"Inherited own-item/Command passive rank/value mismatch case="+c.GetProperty("Case")+" hard="+c.GetProperty("Critical")+" command="+command+" actual="+Json(actual)+" expected="+Json(expected));
                var packet=ValkyrieEquipmentDamage.Allocate(b,Target(variant,()=>armor,value=>armor=value),Packet(Array(r.GetProperty("Initial"))),share);
                Require(Fields(packet).SequenceEqual(Array(r.GetProperty("Output")))&&armor==r.GetProperty("Armor").GetInt32(),"Full source original damage packet mismatch case="+c.GetProperty("Case")+" hard="+c.GetProperty("Critical")+" command="+command+" variant="+variant+" got="+Json(Fields(packet))+" expected="+r.GetProperty("Output").GetRawText());
                Require(NpcEquipmentRandom.State(b)==seed with{Seed=ulong.Parse(r.GetProperty("After").GetString())},"Full source exact RNG continuation or binding mismatch");
                Require(Pools(b).SequenceEqual(pools)&&Json(b.EquipmentNumbers.Stats)==stats,"Read-only allocation changed native pools or original graph/passive state");count++;
            }
            BattleCommand.Clear(b);
        }
        Require(count==10&&Pools(b).SequenceEqual(before.Pools)&&b.SavedText==before.Text&&Json(b.EquipmentNumbers.Stats)==before.Stats,"Command/allocation changed original birth,wounds,graph or expected packet count");
        Require(!ClassCombat.Available((PlayerMobile)b.ControlMaster,CombatAbility.Valkyrie)&&b.SkillWeaponDamage==(0,0),"Packet allocation enabled unfinished AI/contact or learned cast");
    }
    private static void Refusals(AmazonValkyrieBody b)
    {
        var old=Store(b,-1);int armor=20;var target=Target(0,()=>armor,value=>armor=value);bool refused=false;
        try{ValkyrieEquipmentDamage.Allocate(b,target,sourceDamage:256);}catch(ArgumentOutOfRangeException){refused=true;}
        Require(refused&&Json(Store(b,-1))==Json(old)&&armor==20,"Invalid packet share consumed RNG or wrote armor/state");
        var all=NpcEquipmentRandom.Snapshot();var low=NpcEquipmentRandom.SeedLow;NpcEquipmentRandom.SeedLow=()=>throw new InvalidOperationException("Unexpected implicit seed");
        try
        {
            NpcEquipmentRandom.Forget(b);refused=false;try{ValkyrieEquipmentDamage.Allocate(b,target);}catch(NotSupportedException){refused=true;}
            Require(refused&&NpcEquipmentRandom.State(b)==null&&armor==20,"Missing explicit RNG state wrote target or auto-seeded");NpcEquipmentRandom.Restore(all);
            var original=b.Prepared;b.RestorePrepared(original with{Birth=original.Birth with{Critical=original.Birth.Critical==20?19:20}});refused=false;
            try{ValkyrieEquipmentDamage.Allocate(b,target);}catch(NotSupportedException){refused=true;}Require(refused&&NpcEquipmentRandom.State(b)==old.Random&&armor==20,"Stale inherited birth consumed RNG or wrote target");b.RestorePrepared(original);
        }
        finally{NpcEquipmentRandom.SeedLow=low;NpcEquipmentRandom.Restore(all);}
        Require(Json(Store(b,-1))==Json(old),"Allocation refusal changed original item/birth/pool/RNG witness");
    }
    private static void Check(bool reload)
    {
        RankCases();using var source=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-damage-cases.json")));var cases=source.RootElement.EnumerateArray().ToArray();Require(cases.Length==171,"Damage original context matrix");
        using var raw=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"valkyrie-numeric-cases.json")));
        var supplier=ClassSkillChecks.Player(DiabloClass.Barbarian);PlayerMobile p;int generation=0;var bodies=new List<AmazonValkyrieBody>();
        try
        {
            if(reload)
            {
                var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Damage reload count");generation=saved.Generation+1;p=World.FindMobile((Serial)saved.Owner)as PlayerMobile;Require(p is{Alive:true,Deleted:false},"Original damage owner lost");
                foreach(var h in saved.Bodies){var b=World.FindMobile((Serial)h.Body)as AmazonValkyrieBody;Require(b is{Alive:true,Deleted:false}&&b.ControlMaster==p&&Json(Store(b,h.Case))==Json(h),"Original packet actor/items/birth/wounds/RNG changed on reload");bodies.Add(b);}
            }
            else
            {
                p=new PlayerMobile{Name="ValkyrieDamageFixture",Body=0x190,Player=true};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,DiabloClass.Amazon);ClassCreation.Profiles.AdvanceLevel(p,99);ClassSkillChecks.Train(p,"amazon.valkyrie",20);FixtureSpace.Corridor(p,6380,20,3620);
                foreach(var c in cases)
                {
                    var b=Body(p,c);var expected=raw.RootElement[c.GetProperty("Case").GetInt32()].GetProperty("Items");var items=ValkyrieEquipment.Originals(b);Require(items.Length==expected.GetArrayLength(),"Unfiltered original count");
                    for(int i=0;i<items.Length;i++){var s=items[i].Snapshot;Require(Json(s.Graph)==expected[i].GetProperty("Graph").GetRawText()&&Json(s.Metadata)==expected[i].GetProperty("Metadata").GetRawText()&&Json(s.Request)==expected[i].GetProperty("Request").GetRawText()&&Json(s.Allocation)==expected[i].GetProperty("Allocation").GetRawText(),"Original damage graph/metadata/seed request was filtered/rerolled");}bodies.Add(b);
                }
                Refusals(bodies[0]);
            }
            for(int i=0;i<bodies.Count;i++){Inspect(bodies[i],cases[i],generation,supplier);if(i%57==56){Log.Information("Valkyrie damage: completed {Count}/171 original contexts",i+1);}}
            if(!reload){PoisonDaggerChecks.Named(p,"Tarnhelm");}ClassGear.Refresh(p);
            var held=bodies.Select((b,i)=>Store(b,i)).ToArray();PersistentMinions.Suspend(p);p.Internalize();
            for(int i=0;i<bodies.Count;i++){Require(Json(Store(bodies[i],i))==Json(held[i]),"Parking changed original packet context/wounds/RNG");}
            File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,held,generation)));
        }
        finally{supplier.Delete();}
        Log.Information("Valkyrie damage:8192complete NPC rank controls;171original bodies/990items;1710packets per boot/5130across actual reloads;21fields,exact source RNG,armor,own-gear/Command/inherited passives,no graph/pool mutation,owner gear/parking/atomic refusal;HP/status/proc/AI/initializer/learned cast remain gated");
    }
}
