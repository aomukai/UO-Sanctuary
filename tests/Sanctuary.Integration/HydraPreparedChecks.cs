// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class HydraPreparedChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(HydraPreparedChecks));
    private sealed record Saved(uint Owner,uint Orb,int Mana,int Life,HydraCreationStats Birth,HydraControlState Control,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"hydra-prepared-fixtures.json");
    public static void Configure()
    {
        Register("hydra-prepared-check",Check);Register("hydra-prepared-reload-check",Reload);
        var prior=ClassGearHooks.CanRegenerateMana;ClassGearHooks.CanRegenerateMana=p=>p.Name!="HydraPreparedFixture"&&prior?.Invoke(p)!=false;
    }
    [CallPriority(int.MaxValue)]public static void Initialize(){var prior=ClassGearHooks.CanRegenerateHits;ClassGearHooks.CanRegenerateHits=p=>p.Name!="HydraPreparedFixture"&&prior?.Invoke(p)!=false;}
    private static void Register(string label,Action run)=>ConsoleInputHandler.RegisterCommand(label,"Hydra source-compared captured birth and ordinary controller components",_=>Core.LoopContext.Post(()=>{try{run();Log.Information("{Name}: passed",label);}catch(Exception e){Log.Error(e,"{Name}: failed",label);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static JsonDocument Cases(string name)=>JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"hydra-"+name+"-source-cases.json")));
    private static HydraCreationStats Birth(int rank,int bolt,int ball,int mastery,int pierce=0,int sunder=0)
    {
        var row=SkillMechanics.For(CombatAbility.Hydra);var values=new SkillValues(row,k=>k==row.SourceKey?rank:0,k=>k=="Fire Bolt"?bolt:k=="Fire Ball"?ball:0,99);
        return HydraCreation.Prepare(values,99,k=>k=="passive_fire_mastery"?mastery:k=="passive_fire_pierce"?pierce:k=="item_pierce_fire_immunity"?sunder:throw new InvalidOperationException(k));
    }
    private static void Source()
    {
        using(var d=Cases("creation"))
        {
            int count=0;foreach(var r in d.RootElement.EnumerateArray())
            {
                int I(int n)=>r[0][n].GetInt32();if(I(4)==0||I(1) is not(0 or 9)||I(2)==0){continue;}
                bool master=I(0) is 1 or 4;var birth=Birth(I(4),master?13:7,master?17:11,master?139:77,master?23:17,master?0:1);
                foreach(var head in r[2].EnumerateArray().Where(h=>h[0].GetInt32()!=0))
                {
                    Require(birth.MaximumHeads==18&&birth.DurationFrames==250&&birth.Rank==head[7].GetInt32()
                        &&birth.Skill("HydraMissile")==head[8].GetInt32()&&birth.Skill("Fire Bolt")==head[9].GetInt32()&&birth.Skill("Fire Ball")==head[10].GetInt32()
                        &&birth.Passive("passive_fire_mastery")==head[11].GetInt32()&&birth.Passive("passive_fire_pierce")==head[12].GetInt32()&&birth.Passive("item_pierce_fire_immunity")==head[13].GetInt32(),"Hydra full-source birth supplier mismatch");count++;
                }
            }
            Require(count==2880,"Incomplete native Hydra birth scope");
        }
        using(var d=Cases("damage"))
        {
            int count=0;foreach(var r in d.RootElement.EnumerateArray())
            {
                int I(int n)=>r[0][n].GetInt32();var birth=Birth(I(1),I(2),I(3),I(4));var ends=HydraCreation.Fire(birth,SlowMissilePayload.Normal);
                Require(ends.Min==r[2][2].GetInt32()&&ends.Max==r[2][3].GetInt32(),"Hydra complete-source numeric capture mismatch");count++;
            }
            Require(count==41310,"Incomplete native Hydra damage scope");
        }
        using(var d=Cases("clock"))
        {
            int count=0;foreach(var r in d.RootElement.EnumerateArray())
            {
                int I(int n)=>r[n].GetInt32();var clock=HydraControl.Clock(I(1)==4,I(2));
                Require(clock.Action==I(7)&&clock.End==I(8)&&AssassinWakeFire.NeutralThink(clock.End,I(3),I(4)!=0)==I(9),"Hydra full-source ordinary clock mismatch");count++;
            }
            Require(count==576,"Incomplete native Hydra clock scope");
        }
        using(var d=Cases("ai"))
        {
            int count=0;foreach(var r in d.RootElement.EnumerateArray())
            {
                int I(int n)=>r[0][n].GetInt32();if(I(7)!=4){continue;}ulong seed=((ulong)r[0][1].GetUInt32()<<32)|r[0][0].GetUInt32();
                var thought=HydraControl.Think(1000,I(2),I(3)==0?null:I(4),seed);var expected=I(2)<1000?HydraDecision.Die:r[1][7].GetInt32()>0?HydraDecision.Shoot:HydraDecision.Idle;
                Require(thought.Seed==r[1][0].GetUInt64()&&thought.Decision==expected&&thought.IdleFrames==(expected==HydraDecision.Idle?10:0),"Hydra complete-source AI percentage/expiry/range mismatch");count++;
            }
            Require(count==7776,"Incomplete native Hydra AI decision scope");
        }
    }
    private static HydraControlState Step(HydraControlState c,Func<uint,bool> enter,Action<uint> shot)=>HydraControl.Advance(c,()=>new SentryTarget(7,0),enter,shot);
    private static ulong Seed()
    {
        for(int i=0;i<1000;i++)
        {
            ulong seed=SourceRandom.InitLow(i);var first=HydraControl.Think(25,250,0,seed);var second=HydraControl.Think(52,250,0,first.Seed);
            if(first.Decision==HydraDecision.Shoot&&second.Decision==HydraDecision.Shoot){return seed;}
        }
        throw new InvalidOperationException("No source seed for consecutive native controls");
    }
    private static void Controller()
    {
        var c=HydraControl.Start(250,Seed());int actions=0,entries=0;var original=c;
        for(int i=0;i<24;i++){c=Step(c,_=>{entries++;return true;},_=>actions++);HydraControl.Validate(c);Require(entries==0&&actions==0,"Hydra acted before initial25deadline");}
        c=Step(c,_=>{entries++;return true;},_=>actions++);Require(c.Mode==4&&c.ActionAt==33&&c.EndAt==37&&entries==1&&actions==0,"Hydra AI25/A1clock");
        for(int i=0;i<8;i++){c=Step(c,_=>true,_=>actions++);HydraControl.Validate(c);}
        Require(actions==1&&c.Dispatches==1&&c.ActionAt==0,"Hydra shot did not dispatch exactly at33");
        for(int i=0;i<4;i++){c=Step(c,_=>true,_=>actions++);}Require(c.Mode==1&&c.NextThink==52,"Hydra ordinary end/neutral AI deadline");
        while(c.Mode!=0)
        {
            c=Step(c,_=>true,_=>actions++);HydraControl.Validate(c);var json=JsonSerializer.Serialize(c);var restored=JsonSerializer.Deserialize<HydraControlState>(json);Require(c==restored,"Hydra controller JSON lost frame/seed/deadline");c=restored;
            Require(c.Frame<=277,"Hydra never requested strict-expiry death");
        }
        Require(c.Frame>250&&actions>1&&c.Dispatches==actions,"Hydra expiry/dispatch accounting");
        var dead=c with{Frame=277};var aged=HydraControl.Inactive(dead,1000);
        Require(aged.Frame==dead.Frame&&aged.Seed==dead.Seed&&aged.Dispatches==dead.Dispatches&&aged.Mode==0,"Inactive expired Hydra rewound its frame or reset history");
        var offline=HydraControl.Inactive(original,20);Require(offline.Frame==20&&offline.Mode==1&&offline.NextThink==25&&offline.Seed==original.Seed&&offline.ExpiryFrame==250,"Offline Hydra reset birth deadline/seed");
        offline=HydraControl.Inactive(offline,231);Require(offline.Mode==0&&offline.Frame==251&&offline.Seed==original.Seed&&offline.Dispatches==0,"Offline Hydra replayed shots or extended life");
        c=original;for(int i=0;i<25;i++){c=Step(c,_=>false,_=>throw new InvalidOperationException("Failed Hydra admission emitted"));}
        Require(c.Mode==1&&c.NextThink==35&&c.Attempts==1&&c.Dispatches==0,"Failed Hydra attack lacks10frame retry");
        bool invalid=false;try{HydraControl.Validate(c with{NextThink=c.Frame});}catch(ArgumentException){invalid=true;}Require(invalid,"Malformed Hydra deadline accepted");
    }
    private static void Check()
    {
        Source();Controller();var p=FireBoltChecks.Owner(CombatAbility.FireBolt,6390);p.Name="HydraPreparedFixture";
        foreach(var ability in new[]{CombatAbility.Warmth,CombatAbility.Fireball,CombatAbility.Enchant,CombatAbility.Hydra,CombatAbility.FireMastery}){EnchantCastChecks.Train(p,ability,1);}
        PartialCap(p);
        var gear=PoisonDaggerChecks.Named(p,"Eschuta's temper");var birth=HydraCreation.Capture(p);var ends=HydraCreation.Fire(birth,SlowMissilePayload.Normal);string old=JsonSerializer.Serialize(birth);
        Require(birth.Rank>1&&birth.Passive("passive_fire_mastery")==SorceressCombat.Passive(p,CombatAbility.FireMastery)+ClassGear.Bonus(p,"extra-fire"),"Hydra capture omitted Sorceress mastery or item stat");
        p.Backpack.DropItem(gear);ClassGear.Refresh(p);EnchantCastChecks.Train(p,CombatAbility.FireBolt,20);Require(JsonSerializer.Serialize(birth)==old&&HydraCreation.Fire(birth,SlowMissilePayload.Normal)==ends,"Existing Hydra birth refreshed from later caster changes");
        p.EquipItem(gear);ClassGear.Refresh(p);birth=HydraCreation.Capture(p);var c=HydraControl.Start(250,Seed());for(int i=0;i<30;i++){c=Step(c,_=>true,_=>throw new InvalidOperationException("Saved Hydra shot too early"));}
        SkillHealth.Damage(p,p,5*256+17);var saved=new Saved(p.Serial.Value,gear.Serial.Value,NecromancerEffects.ManaBalance256(p),SkillHealth.Balance256(p),birth,c,0);p.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));
        Log.Information("Hydra prepared:2880birth suppliers/41310endpoints/576clocks/7776AI decisions,retained gear/mastery/hard stats,JSON frame/seed/deadlines and offline controls pass;world actor/contact/retirement/input remain next");
    }
    private static void PartialCap(PlayerMobile p)
    {
        var point=new Point3D(p.X+7,p.Y,p.Z);var values=SkillMechanics.Values(p,CombatAbility.Hydra);Static blocker=null;
        try
        {
            for(int i=0;i<6;i++){Hydras.Cast(p,point,values);}
            var all=Hydras.Snapshot();var original=all.Heads.Where(h=>h.Owner==p.Serial.Value).OrderBy(h=>h.Id).ToArray();Require(original.Length==18,"Hydra cap fixture did not create18heads");
            original=original.Take(17).ToArray();Hydras.Restore(all with{Heads=all.Heads.Where(h=>h.Owner!=p.Serial.Value).Concat(original).ToArray()});
            int wall=Enumerable.Range(1,0x4000).First(i=>TileData.ItemTable[i].Impassable&&TileData.ItemTable[i].CalcHeight>=16);
            blocker=new Static(wall);blocker.MoveToWorld(new Point3D((int)Math.Round(point.X-.5),(int)Math.Round(point.Y-.5),point.Z),p.Map);
            Require(Hydras.CanPlace(p,point),"Blocked first Hydra head incorrectly refused entire placement");Hydras.Cast(p,point,values);
            var after=Hydras.Snapshot().Heads.Where(h=>h.Owner==p.Serial.Value).ToArray();
            Require(after.Length==18&&!after.Any(h=>h.Id==original[0].Id)&&original.Skip(1).All(old=>after.Any(h=>h.Id==old.Id))
                &&after.Count(h=>original.All(old=>old.Id!=h.Id))==2,"Partial Hydra creation evicted a group instead of one old head");
        }
        finally{blocker?.Delete();Hydras.Cancel(p);}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=World.FindMobile((Serial)s.Owner) as PlayerMobile;var gear=World.FindItem((Serial)s.Orb);
        Require(s.Generation<2&&p!=null&&gear?.Parent==p&&NecromancerEffects.ManaBalance256(p)==s.Mana&&SkillHealth.Balance256(p)==s.Life,"Original prepared Hydra owner/gear/wounds/resources changed");HydraCreation.Validate(s.Birth);HydraControl.Validate(s.Control);
        var c=s.Control;int shots=0;for(int i=0;i<(s.Generation==0?8:24);i++){c=Step(c,_=>true,_=>shots++);HydraControl.Validate(c);}
        Require(shots==1&&c.Dispatches==s.Control.Dispatches+1&&c.Frame>s.Control.Frame&&c.ExpiryFrame==s.Control.ExpiryFrame,"Original saved Hydra component did not continue its pending action");
        File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Control=c,Generation=s.Generation+1}));
    }
}
