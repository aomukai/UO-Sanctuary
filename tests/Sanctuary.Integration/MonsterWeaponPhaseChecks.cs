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

public static class MonsterWeaponPhaseChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MonsterWeaponPhaseChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("monster-weapon-phases-check","Real Life Tap/absorb/Thorns over ordered HP and deferred native death",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("monster-weapon-phases-check: passed");}catch(Exception e){Log.Error(e,"monster-weapon-phases-check: failed");}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static int[] Numbers(JsonElement row)=>row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static EncounterCreature Body(PlayerMobile owner)
    {
        var body=new EncounterCreature{Name="Ordered weapon phases [fixture]",CantWalk=true,NoKillAwards=true};body.SetHits(100);
        body.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);body.AIObject.AITimer.Stop();body.SetResistance(ResistanceType.Physical,0);return body;
    }
    private sealed class Contact(PreparedMonsterDamagePacket packet):MonsterContactServices
    {
        internal override PreparedMonsterDamagePacket CapturedPacket()=>packet;
        internal override bool InMeleeRange(int bonus){Require(bonus==3,"NPC contact bonus");return true;}
        internal override bool AttackerInDeathMode()=>false;
        internal override bool Hostile()=>true;
        internal override bool DefenderHasRoom()=>true;
        internal override bool DefenderInTown()=>false;
        internal override bool AttackerAllowedInTown()=>throw new InvalidOperationException("Non-town fixture queried town permission");
        internal override bool DefenderKillable()=>true;
        internal override bool DefenderDead()=>false;
    }
    private sealed class Phases(EncounterCreature source,EncounterCreature target,int[] expected):PreparedMonsterDamageServices
    {
        internal int BeforeHealthActor=-1,AfterHealthTarget=-1,BeforeModeTarget=-1,Free,DeathCalls,KillEvents;
        private void AwaitingDeath()
        {Require(target is{Alive:true,Deleted:false}&&target.Corpse==null,"Incoming native death ran before return");}
        internal override void StoreLastAttacker(){}
        internal override PreparedMonsterDamagePacket DealtDamage(PreparedMonsterDamagePacket p){AwaitingDeath();return p;}
        internal override PreparedMonsterDamagePacket TookDamage(PreparedMonsterDamagePacket p){AwaitingDeath();return p;}
        internal override void BeforeHealth(PreparedMonsterDamagePacket p)
        {AwaitingDeath();BeforeHealthActor=SkillHealth.Balance256(source);Require(BeforeHealthActor==expected[0],"Life Tap did not precede clip/recovery");}
        internal override void ApplyCold(int frames)
        {Require(frames==0,"Unexpected cold in the declared plain packet");AwaitingDeath();AfterHealthTarget=SkillHealth.Balance256(target);Require(AfterHealthTarget==expected[1],"Absorb/once-only HP phase differs");}
        internal override void SurvivedDamage(){AwaitingDeath();}
        internal override PreparedMonsterDamagePacket Killed(PreparedMonsterDamagePacket p){AwaitingDeath();Require(target.Hits==0,"Killed event before zero HP");KillEvents++;return p;}
        internal override PreparedMonsterDamagePacket Kill(PreparedMonsterDamagePacket p){AwaitingDeath();KillEvents++;return p;}
        internal override void PresentOverlay(int overlay)=>throw new InvalidOperationException("No incoming overlay in this packet");
        internal override int BaseHitClass()=>3;
        internal override PreparedMonsterDamagePacket MeleeAttack(PreparedMonsterDamagePacket p){AwaitingDeath();return p;}
        internal override PreparedMonsterDamagePacket AttackedInMelee(PreparedMonsterDamagePacket p){AwaitingDeath();return p;}
        internal override bool AttackerInActiveMode()=>source is{Alive:true,Deleted:false}&&source.Hits>0;
        internal override void BeforeThorns(PreparedMonsterDamagePacket p){AwaitingDeath();}
        internal override void Return(ref PreparedMonsterDamagePacket p,Action death)
        {
            AwaitingDeath();BeforeModeTarget=SkillHealth.Balance256(target);
            Require(BeforeModeTarget==expected[2]&&SkillHealth.Balance256(source)==expected[3],"Retaliation did not follow committed incoming HP");
            var world=new MonsterReturnChecks.WorldServices(target){Death=()=>{DeathCalls++;death();}};
            // Supplied ordinary neutral/non-town/mode metadata deliberately
            // isolates the shared real return policy from native AI mapping.
            PreparedMonsterReturn.Dispatch(ref p,new MonsterHitClassCycle(),world,true);
        }
        internal override void FreePacket()=>Free++;
    }
    private static void Forget(BaseCreature body)
    {if(body==null){return;}MonsterUninterruptibles.Clear(body);PaladinAuras.Clear(body);NecromancerEffects.Profiles.Clear(body);body.Corpse?.Delete();body.Delete();}
    private static void Check()
    {
        var owner=new PlayerMobile{Name="Weapon phase curse/aura owner [fixture]",Body=0x190,Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,99);FixtureSpace.Corridor(owner,6100,24,2100);
        EncounterCreature source=null,target=null;int count=0,deaths=0,sourceDeaths=0;
        try
        {
            using var document=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"monster-weapon-phases-source-probe.json")));
            foreach(var row in document.RootElement.EnumerateArray())
            {
                int[] i=Numbers(row[0]),expected=Numbers(row[1]);
                if(source is not{Alive:true,Deleted:false}){Forget(source);source=Body(owner);}
                if(target is not{Alive:true,Deleted:false}){Forget(target);target=Body(owner);}
                MonsterUninterruptibles.Clear(target);NecromancerEffects.Profiles.Clear(target);PaladinAuras.Clear(target);
                SkillHealth.SetBalance256(source,i[3]);SkillHealth.SetBalance256(target,i[2]);
                if(i[0]!=0){NecromancerEffects.Profiles.Store(target,new(new(CombatAbility.LifeTap,owner.Serial.Value,1,TimedEffects.Now.AddDays(1),owner.X,owner.Y)));}
                if(i[6]!=0||i[7]!=0){PaladinAuras.ElementalWeapon(new(target.Serial.Value,owner.Serial.Value,CombatAbility.Thorns,1,i[6],TimedEffects.Now.AddDays(1),DamagePercent:i[7]));}
                if(i[5]!=0){MonsterUninterruptibles.MarkDeathDelay(target);}
                var packet=new PreparedMonsterDamagePacket(20*256,20*256,0,0,i[1],AbsorbedLife256:i[4]);var phases=new Phases(source,target,expected);
                MonsterContactResult result;
                // The legacy outer attempt scope must not add its fallback
                // flat packet after the ordered attacked-in-melee event.
                using(PaladinThorns.Attempt(source,target,false))
                {result=PreparedMonsterContact.Release(source,target,new Contact(packet),new(false,()=>false,()=>5,()=>false),_=>0,phases,TimedEffects.Now);}
                Require(result.Exit==MonsterContactExit.Finished&&phases.Free==1&&phases.DeathCalls==expected[6]
                    &&result.DeathModeAttempted==(expected[6]!=0),"Deferred death/free dispatch count at "+count);
                Require(phases.BeforeHealthActor==expected[0]&&phases.AfterHealthTarget==expected[1]&&phases.BeforeModeTarget==expected[2]
                    &&(!source.Alive||source.Deleted)==(expected[5]!=0)&&(!target.Alive||target.Deleted)==(expected[6]!=0),"Source/native final life/mode differs at "+count);
                Require(SkillHealth.Balance256(source)==expected[3]&&SkillHealth.Balance256(target)==expected[4]
                    &&result.Packet.ResultFlags==expected[7]&&result.Packet.Physical256==expected[8],"Source/native final packet/HP differs at "+count);
                Require(phases.KillEvents==((expected[6]!=0&&(i[1]&32)==0)?2:0),"Suppressed/ordinary killed/kill callbacks");
                Require(packet.ResultFlags==i[1]&&packet.Physical256==20*256&&packet.AbsorbedLife256==i[4],"Release rewrote captured packet");
                deaths+=phases.DeathCalls;sourceDeaths+=expected[5];count++;
            }
            Require(count==5184&&deaths>0&&sourceDeaths>0,"Incomplete native/source phase compositions");
            Log.Information("Monster weapon phases:5184complete-source/native compositions,actual Life Tap/absorb/flat/percentage consumers,HP-before-return and deferred native death pass;event registry,actor modes and immediate legacy paths remain separate");
        }
        finally{Forget(source);Forget(target);owner.Delete();}
    }
}
