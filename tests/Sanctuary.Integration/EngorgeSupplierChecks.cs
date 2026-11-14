// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class EngorgeSupplierChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(EngorgeSupplierChecks));
    private sealed record Pet(uint Serial,string Birth,int Life,NpcEquipmentSeed Random);
    private sealed record Saved(int Generation,uint Owner,EngorgeSuppliers Frozen,EngorgeSuppliers Current,Pet[] Pets);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"engorge-suppliers-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static string Birth(SkeletonGuardian b)=>JsonSerializer.Serialize(b switch
    {WarlockGoatmanBody g=>(object)g.Prepared.Snapshot,WarlockTaintedBody t=>t.Prepared.Snapshot,WarlockDefilerBody d=>d.Prepared.Snapshot,_=>throw new NotSupportedException()});
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"engorge-suppliers-reload-check":"engorge-suppliers-check";
            ConsoleInputHandler.RegisterCommand(name,"Engorge owned numerical capture,real gear and saved birth controls",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}: passed",name+"-result");}catch(Exception e){Log.Error(e,"{Name}: failed",name+"-result");}});}));}
    }
    private static void Numbers()
    {
        var row=SkillMechanics.For(CombatAbility.Engorge);
        for(int rank=1;rank<=255;rank++)
        foreach(int hard in new[]{0,1,20})
        {
            var v=new SkillValues(row,key=>key=="Engorge"?rank:key=="Blood Oath"?255:0,key=>key=="Blood Oath"?hard:0,99);
            var captured=EngorgePrepared.Capture(v,hard);
            Require(captured==new EngorgeSuppliers(rank,hard,(12+rank-1)*64,125+75*(rank-1),30+rank-1,
                5,15+2*(rank-1),15+2*(rank-1),Math.Min(15+rank-1,35),25*hard,5*hard),"Pinned supplier/soft-vs-hard formula mismatch");
            EngorgePrepared.Validate(captured);
        }
        var one=EngorgePrepared.Capture(new SkillValues(row,_=>1,_=>0,99),0);
        Require(one is{Mana256:768,DurationFrames:125,HealPercent:30,MinimumDrainStat:15,AttackSpeed:15},"Rank1 supplier control");
        Require(EngorgePrepared.Healing256(one,1001,0)==300&&EngorgePrepared.Healing256(one,1001,900)==101
            &&EngorgePrepared.Healing256(one,1001,1001)==0,"FP8 healing fraction or missing-life cap");
        foreach(var malformed in new[]{one with{HealPercent=31},one with{HardBloodOath=21},one with{Rank=0},one with{MaximumDrainStat=16}})
        {bool rejected=false;try{EngorgePrepared.Validate(malformed);}catch(InvalidDataException){rejected=true;}Require(rejected,"Malformed captured suppliers accepted");}
        bool mismatch=false;try{EngorgePrepared.Capture(new SkillValues(row,_=>1,_=>0,99),1);}catch(NotSupportedException){mismatch=true;}Require(mismatch,"Contradictory hard-rank context accepted");
        Log.Information("Engorge suppliers:765pinned rank/hard-point contexts;FP8 healing cap and malformed capture controls pass;no raw drain consumer inferred");
    }
    private static void Scene(NetState network)
    {
        Numbers();var p=ClassSkillChecks.Player(DiabloClass.Warlock);p.Female=true;p.Name="EngorgeFixture";p.AddItem(new Backpack());
        ClassSkillChecks.Train(p,"warlock.engorge",19);ClassSkillChecks.Train(p,"warlock.bloodoath",7);ClassSkillChecks.Train(p,"warlock.demonicmastery",10);
        foreach(var key in new[]{"warlock.summongoatman","warlock.summontainted","warlock.summondefiler"}){ClassSkillChecks.Train(p,key,6);}
        FixtureSpace.Corridor(p,6900,34,3300);var account=FixtureNetwork.Authenticate(network,p,"engorge-");SkeletonGuardian[] pets=[];bool kept=false;
        try
        {
            Require(!WarlockCombat.Implemented(CombatAbility.Engorge)&&!WarlockCombat.Handles(CombatAbility.Engorge),"Supplier capture unexpectedly enabled an incomplete skill");
            pets=new[]{CombatAbility.SummonGoatman,CombatAbility.SummonTainted,CombatAbility.SummonDefiler}
                .Select((ability,i)=>WarlockGoatmanWorld.Create(p,new(p.X+2+i*2,p.Y,p.Z),TimedEffects.Now,ability)).ToArray();
            foreach(var b in pets){WarlockGoatmanWorld.Admit(b);}var births=pets.Select(Birth).ToArray();
            var before=EngorgePrepared.Capture(p);Require(before is{Rank:19,HardBloodOath:7,Mana256:1920,DurationFrames:1475,HealPercent:48,
                PhysicalResistance:5,MinimumDrainStat:51,MaximumDrainStat:51,AttackSpeed:33,FlatArmor:175,RegenerationStat:35},"Real rank19 capture");
            var helm=PoisonDaggerChecks.Named(p,"Tarnhelm");ClassGear.Refresh(p);var after=EngorgePrepared.Capture(p);
            Require(after is{Rank:20,HardBloodOath:7,Mana256:1984,DurationFrames:1550,HealPercent:49,AttackSpeed:34,MinimumDrainStat:53,FlatArmor:175,RegenerationStat:35}
                &&SkillMechanics.Rank(p,CombatAbility.BloodOath)==8&&before.Rank==19,"Tarnhelm did not capture soft Engorge independently of hard Blood Oath");
            p.AddToBackpack(helm);ClassGear.Refresh(p);Require(EngorgePrepared.Capture(p)==before&&after.Rank==20,"Equipment removal rewrote immutable suppliers");
            p.EquipItem(helm);ClassGear.Refresh(p);Require(EngorgePrepared.Capture(p)==after,"Equipment re-equip capture changed");
            int mana=NecromancerEffects.ManaBalance256(p);
            for(int i=0;i<pets.Length;i++)
            {
                Require(Birth(pets[i])==births[i],"Numerical capture rewrote original summon birth");
                int maximum=SkillHealth.Maximum256(pets[i]);Require(EngorgePrepared.Healing256(after,maximum,maximum-513)==513,"Actual demon maximum-life healing cap");
                SkillHealth.SetBalance256(pets[i],maximum-513);
            }
            Require(NecromancerEffects.ManaBalance256(p)==mana,"Supplier capture spent mana");
            PersistentMinions.Suspend(p);var saved=new Saved(0,p.Serial.Value,before,after,pets.Select(b=>new Pet(b.Serial.Value,Birth(b),SkillHealth.Balance256(b),NpcEquipmentRandom.State(b))).ToArray());
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved));kept=true;
            Log.Information("Engorge suppliers:actual Witch/Tarnhelm19to20;soft Oath7to8leaves175flat armor/35raw regen;three original births unchanged;no mana/healing/state installed");
        }
        finally
        {network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){foreach(var b in pets){b.Delete();}p.Delete();}}
    }
    private static void Reload(NetState network)
    {
        var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(saved.Generation<2,"Too many Engorge supplier reloads");
        EngorgePrepared.Validate(saved.Frozen);EngorgePrepared.Validate(saved.Current);
        var p=(PlayerMobile)World.FindMobile((Serial)saved.Owner);
        foreach(var pet in saved.Pets)
        {
            var b=(SkeletonGuardian)World.FindMobile((Serial)pet.Serial);
            Require(b.Map==Map.Internal&&Birth(b)==pet.Birth&&SkillHealth.Balance256(b)==pet.Life&&NpcEquipmentRandom.State(b)==pet.Random
                &&DemonNativeState.Controller(b)is{Fault:null},"Original demon birth/wound/RNG/controller changed during parked reload");
        }
        var account=FixtureNetwork.Authenticate(network,p,"engorge-reload-");
        try
        {
            Require(EngorgePrepared.Capture(p)==saved.Current&&saved.Frozen.Rank==19&&saved.Current.Rank==20&&p.Female,"Saved actual gear or frozen capture changed");
            File.WriteAllText(Manifest,JsonSerializer.Serialize(saved with{Generation=saved.Generation+1}));
            Log.Information("Engorge suppliers:original Witch/gear and three parked demon births,wounds,RNG/controllers survive reload;fixture payload remains frozen;no automatic Engorge state claimed");
        }
        finally{network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
}
