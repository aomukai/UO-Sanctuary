// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;
public static class MonsterContactChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(MonsterContactChecks));
    private static void Require(bool b,string why){if(!b){throw new InvalidOperationException(why);}}
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("monster-contact-check","Captured NPC melee contact/target gates",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("monster-contact-check: passed");}catch(Exception e){Log.Error(e,"monster-contact-check: failed");}}));
    private sealed class Contact:MonsterContactServices
    {
        internal PreparedMonsterDamagePacket Packet;internal int Gate,Life,Bonus=-1;internal readonly List<string> Calls=new();
        internal override PreparedMonsterDamagePacket CapturedPacket(){Calls.Add("packet");return Packet;}
        internal override bool InMeleeRange(int bonus){Calls.Add("range");Bonus=bonus;return Gate!=1;}
        internal override bool AttackerInDeathMode(){Calls.Add("mode");return Gate==2;}
        internal override bool Hostile(){Calls.Add("hostile");return Gate!=5;}
        internal override bool DefenderHasRoom(){Calls.Add("room");return Gate!=4;}
        internal override bool DefenderInTown(){Calls.Add("town");return Gate==6;}
        internal override bool AttackerAllowedInTown(){Calls.Add("town-row");return false;}
        internal override bool DefenderKillable(){Calls.Add("killable");return Gate!=7;}
        internal override bool DefenderDead(){Calls.Add("dead");return Life<=0;}
    }
    private static string[] ExpectedCalls(int flags,int gate)
    {
        if(gate==3){return [];}
        var calls=new List<string>{"packet","range"};if(gate==1||(flags&1)==0){return calls.ToArray();}
        calls.Add("mode");if(gate==2){return calls.ToArray();}
        calls.Add("hostile");if(gate==5){return calls.ToArray();}
        calls.Add("room");if(gate==4){return calls.ToArray();}
        calls.Add("town");if(gate==6){calls.Add("town-row");return calls.ToArray();}
        calls.Add("killable");if(gate!=7){calls.Add("dead");}return calls.ToArray();
    }
    private static void Check()
    {
        var actor=MonsterOrderedReleaseChecks.Body();var body=MonsterOrderedReleaseChecks.Body();var prior=CombatStuns.Snapshot();
        int count=0,commits=0,missing=0,freed=0,retained=0,finished=0;
        using(var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"goatman-npc-release-source-probe.json"))))
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            var i=N(row[0]);var o=N(row[1]);var s=N(row[2]);var state=N(row[3]);var trace=row[4].EnumerateArray().Select(N).ToArray();
            if(body.Deleted||!body.Alive){body=MonsterOrderedReleaseChecks.Body();}
            SkillHealth.SetBalance256(body,i[3]);var at=TimedEffects.Now;var began=at.AddMilliseconds(-i[11]*40);GoatmanBerserkEffects.Clear(actor);
            CombatStuns.Restore(i[10]!=0?prior.Append(new CombatStunState(body.Serial.Value,at.AddMilliseconds((1000-i[11])*40))).ToArray():prior);
            int resistance=i[12];int Reduce(int n)=>NamedElements.Reduce256(n,resistance,0,0,0).Damage256;
            var captured=GoatmanNpcMelee.BeginFor(actor,i[0],i[1],i[2],10000000,new(30,50,210,512,1536,768,1792,1280,2048,31),
                p=>new(Reduce(p.Physical256),Reduce(p.Magic256),Reduce(p.Fire256),Reduce(p.Lightning256),Reduce(p.Cold256),p.ColdFrames*(100-resistance)/100,p.StunFrames),_=>0,()=>99,began);
            var packet=new PreparedMonsterDamagePacket(captured.Physical256,captured.Total256,captured.StunFrames,captured.ColdFrames,captured.ResultFlags,
                captured.HitClass,false,captured.CriticalOverlay,captured.Magic256,captured.Fire256,captured.Lightning256,captured.Cold256);
            bool eligible=(i[2]&1)!=0&&i[4]==0&&i[3]>0;
            int change=eligible&&packet.Total256>0?1:0;if(s[2]!=0){change=2;}if(s[3]!=0){change=3;}
            var services=new MonsterOrderedReleaseChecks.Services{Body=body,At=at,Frame=1000+i[11],ExpectedLife=o[2],ExpectedUntil=s[1],ExpectedStun=s[0]!=0,
                ExpectedCold=trace.FirstOrDefault(t=>t[0]==6)?[1]??0,StunChange=change,TryTwice=true,HealthWrite=eligible&&packet.Total256>0};
            var contact=new Contact{Packet=packet,Gate=i[4],Life=i[3]};var draws=new List<int>();int rank=i[1]==255?254:i[1]+1;
            var result=GoatmanNpcMelee.ReleaseContact(actor,i[0],rank,()=>
            {
                if(i[0]==418){Require(GoatmanBerserkEffects.At(actor,at)?.Rank==rank,"Target lookup preceded release self-state refresh");}
                return i[4]==3?null:body;
            },contact,new(i[5]!=0,()=>i[6]!=0,()=>i[7],()=>i[8]!=0),bound=>{draws.Add(bound);return i[9];},services,at);
            Require(result.Action==(o[1]!=0)&&contact.Calls.SequenceEqual(ExpectedCalls(i[2],i[4])),"Contact target/guard short-circuit differs at "+count);
            Require(contact.Bonus==o[17]&&services.Trace.Count==trace.Length&&services.Trace.Zip(trace).All(x=>x.First.SequenceEqual(x.Second)),"Complete contact service trace differs at "+count);
            Require(contact.Packet==packet&&draws.SequenceEqual(N(row[5])),"Contact rewrote stored start packet or redrew damage");
            Require(SkillHealth.Balance256(body)==o[2]||body.Deleted&&o[2]==0,"Contact changed rejected health or missed source commitment");
            if(result.Contact==null){Require(i[4]==3,"Unexpected missing contact");missing++;}
            else
            {
                var r=result.Contact;Require((r.Commit!=null)==eligible,"Wrong contact-to-health eligibility");
                if(r.Commit!=null){commits++;Require(r.Commit.AfterHealth256==o[2],"Wrong committed health");}
                if(r.Exit==MonsterContactExit.OutOfRangeFreed){freed++;Require(i[4]==1&&o[16]==1,"Out-of-range packet not freed");}
                else if(r.Exit==MonsterContactExit.DeathModeRetained){retained++;Require(i[4]==2&&(i[2]&1)!=0&&o[18]==1,"Death-mode packet not retained");}
                else
                {
                    finished++;Require(r.Exit==MonsterContactExit.Finished&&r.Packet.ResultFlags==o[10]&&r.Packet.Physical256==o[11]&&r.Packet.Total256==o[12]&&r.Packet.HitClass==o[13],"Completed contact output differs at "+count);
                    Require(r.Packet.HitFlags==((i[2]&1)!=0?32u:0u),"Contact hit flags differ");
                    Require(r.DeathModeAttempted==(services.DeathCalls!=0),"Contact death result differs");
                }
            }
            if(i[0]==418)
            {
                var actual=GoatmanBerserkEffects.At(actor,at);Require(actual.Flags==state[1]&&actual.Rank==state[3]&&actual.UntilUtc==(state[2]==0?null:began.AddMilliseconds((state[2]-1000)*40)),"Outer-guard Berserk self-state differs");
            }
            CombatStuns.Clear(body);if(body.Deleted||!body.Alive){SkillHealth.Clear(body);}count++;
        }
        Require((count,commits,missing,freed,retained,finished)==(3840,1616,288,288,96,3168),"Full contact source grid incomplete");
        CombatStuns.Restore(prior);GoatmanBerserkEffects.Clear(actor);actor.Delete();body.Delete();MissingPacket();Presentation();
        Log.Information("NPC contact:3840complete-source releases/1616health commits;missing target/range/death-mode/room/hostility/town/killability/dead guards,exact traces and immutable captured packets pass;real metadata/range mapping remains explicit");
    }
    private static void MissingPacket()
    {
        var c=new Contact();var services=new MonsterOrderedReleaseChecks.Services();
        var r=PreparedMonsterContact.Release(null,null,c,null,null,services,default);
        Require(r.Exit==MonsterContactExit.MissingPacket&&c.Calls.SequenceEqual(new[]{"packet"})&&services.Trace.Count==0,"Missing packet reached range or release services");
    }
    private sealed class PresentationServices:MonsterOrderedReleaseChecks.Services
    {
        internal int Overlays,Classes;internal override void PresentOverlay(int value){Require(value==54,"Wrong overlay");Overlays++;}
        internal override int BaseHitClass(){Classes++;return 3;}
    }
    private static void Presentation()
    {
        var actor=MonsterOrderedReleaseChecks.Body();var body=MonsterOrderedReleaseChecks.Body();int life=SkillHealth.Balance256(body);
        var metadata=new PreparedMonsterRecipient(false,()=>false,()=>5,()=>false);
        foreach(bool hit in new[]{false,true})
        {
            var packet=new PreparedMonsterDamagePacket(0,0,0,0,hit?1:0,Overlay:54,HitFlags:4096);
            var c=new Contact{Packet=packet,Gate=5,Life=life};var services=new PresentationServices{Body=body,ExpectedLife=life};
            var r=PreparedMonsterContact.Release(actor,body,c,metadata,_=>0,services,TimedEffects.Now);
            Require(services.Overlays==(hit?1:0)&&services.Classes==(hit?1:0)&&r.Packet.HitClass==(hit?3:0),"Miss versus rejected-success presentation ordering differs");
            Require(r.Packet.ResultFlags==0&&r.Packet.HitFlags==(hit?32u:4096u)&&packet.HitFlags==4096,"Contact did not overwrite only the copied hit flags");
            Require(SkillHealth.Balance256(body)==life,"Rejected presentation control changed HP");
        }
        actor.Delete();body.Delete();
    }
}
