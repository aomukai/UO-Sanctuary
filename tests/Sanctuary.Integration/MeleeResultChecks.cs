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

public static class MeleeResultChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MeleeResultChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("melee-result-check","Source melee result flags and actual Iron Golem block delivery",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("melee-result-check: passed");}catch(Exception e){Log.Error(e,"melee-result-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private sealed class Services(int[] input):SourceMeleeResultServices
    {
        internal readonly List<int> Trace=new();
        internal override int? AttackerKind=>input[0]<0?null:input[0];
        internal override int? DefenderKind=>input[1]<0?null:input[1];
        internal override bool Hostile(){Trace.Add(1);return input[2]!=0;}
        internal override bool DefenderRunning()=>input[7]!=0;
        internal override int MeleeReach(){Trace.Add(2);return input[9];}
        internal override bool InRange(int bonus){Trace.Add(100+bonus);return input[3]!=0;}
        internal override bool HitSuccessful(int percent,bool missile){Require(percent==17&&!missile,"Source melee hit arguments");Trace.Add(3);return input[4]!=0;}
        internal override int BlockOrDodge(bool avoid,bool block){Require(!avoid&&block,"Source block/avoid arguments");Trace.Add(4);return input[5];}
        internal override bool Uninterruptible(){Trace.Add(5);return input[6]!=0;}
    }
    private static void Train(PlayerMobile owner,string id)
    {
        Require(TalentCatalog.TryGet(DiabloClass.Necromancer,id,out var row),"Missing Necromancer talent");
        foreach(var prior in row.Prerequisites){Train(owner,prior);}
        if(ClassCreation.Profiles.GetRank(owner.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}
    }
    private static ulong Seed(IronGolemBody body,BaseCreature target,bool hit)
    {
        // A supplied first-roll selection exercises the real AR/defense path;
        // no mock hit callback or bypass is installed on the world controller.
        double chance=ClassStats.HitChance(ClassStats.AttackRatingAgainst(body,target,0),NamedCombat.DefenseFor(body,target,ClassStats.Defense(target)),ClassStats.Level(body),ClassStats.Level(target));
        for(ulong initial=1;initial<100000;initial++)
        {ulong value=initial;if((SourceRandom.Roll(ref value)%100/100.0<chance)==hit){return initial;}}
        throw new InvalidOperationException("No deterministic Iron hit/miss seed");
    }
    private static void World()
    {
        var shields=SourceBlocking.Snapshot();var oldRoll=SourceBlocking.BlockRoll;int scene=0;
        try
        {
            foreach(bool hit in new[]{false,true})foreach(bool block in new[]{false,true})
            {
                var owner=new PlayerMobile{Name="Iron block owner [fixture]",Body=0x190,Player=true};owner.AddItem(new Backpack());
                ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,99);Train(owner,"necromancer.irongolem");
                FixtureSpace.Corridor(owner,6100,24,2300+24*scene++);
                var original=new Broadsword();ClassGear.Profiles.Ensure(original);original.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
                var at=TimedEffects.Now.AddHours(1);var body=IronGolemWorld.Create(owner,original,original.Location,at,1);
                var target=new GraveyardSkeleton{CantWalk=true,NoKillAwards=true};target.MoveToWorld(body.Location,owner.Map);target.AIObject.AITimer.Stop();target.SetHits(20000);target.SetResistance(ResistanceType.Physical,0);
                try
                {
                    string reference=CreatureProfiles.Definition(target).Reference;
                    SourceBlocking.Restore(SourceBlocking.Snapshot().Where(x=>x.Actor!=target.Serial.Value).Append(new(target.Serial.Value,reference,1)).ToArray());
                    Require(SourceBlocking.TryRate(target,out int rate,out _)&&rate>0&&rate<100,"Mapped shield recipient");
                    int rolls=0;SourceBlocking.BlockRoll=bound=>{Require(bound==100,"Block roll bound");rolls++;return block?0:99;};
                    ulong seed=Seed(body,target,hit),expected=seed;SourceRandom.Roll(ref expected);NpcEquipmentRandom.Set(body,seed);
                    var state=body.Prepared.Controller with{Mode=4,Target=target.Serial.Value,NextThink=0,ActionAt=1,EndAt=20,AttackSpeed=256};body.RestoreController(state);
                    int before=SkillHealth.Balance256(target);IronGolemWorld.Advance(body,state.NextUtc,true);
                    Require(!body.Deleted&&body.Prepared.Controller.Fault==null&&body.Prepared.Controller.Attempts==1
                        &&body.Prepared.Controller.Hits==(hit&&!block?1:0),"World hit/block outcome");
                    Require(rolls==(hit?1:0),"Miss consumed a defender block roll or hit skipped it");
                    if(!hit||block)
                    {Require(SkillHealth.Balance256(target)==before&&NpcEquipmentRandom.State(body).Seed==expected,"Miss/block damaged target or rolled weapon damage/critical");}
                    else{Require(SkillHealth.Balance256(target)<before,"Unblocked real weapon attack did no damage");}
                }
                finally{target.Delete();foreach(var pet in(owner.AllFollowers??[]).OfType<BaseCreature>().ToArray()){pet.Delete();}owner.Delete();}
            }
        }
        finally{SourceBlocking.BlockRoll=oldRoll;SourceBlocking.Restore(shields);}
    }
    private static void Check()
    {
        int count=0;
        using(var document=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"melee-result-source-probe.json"))))
        foreach(var row in document.RootElement.EnumerateArray())
        {
            int[] input=row[0].EnumerateArray().Select(x=>x.GetInt32()).ToArray();var services=new Services(input);
            int result=SourceMeleeResult.Resolve(services,17,input[8]);
            Require(result==row[1].GetInt32()&&services.Trace.SequenceEqual(row[2].EnumerateArray().Select(x=>x.GetInt32())),"Source result/trace differs at "+count);count++;
        }
        Require(count==16384,"Incomplete source melee grid");World();

        Log.Information("Melee results:16384complete-source flag/ordered-service comparisons and4actual Iron world miss/block/hit controls pass;shared defender component/RNG adapter and complete HP/death/event chronology remain separate");
    }
}
