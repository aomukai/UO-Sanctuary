// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NecromancerPetTargetChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NecromancerPetTargetChecks));
    public static void Configure()=>ConsoleInputHandler.RegisterCommand("necromancer-pet-targets-check","Shared AI67 target adapter on an actual prepared skeleton",_=>Core.LoopContext.Post(()=>
    {try{Check();Log.Information("necromancer-pet-targets-check: passed");}catch(Exception e){Log.Error(e,"necromancer-pet-targets-check: failed");}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Check()
    {
        var owner=PoisonDaggerChecks.Owner();FixtureSpace.Corridor(owner,6980,35,3500);
        var body=new RaisedSkeleton{Frozen=true,CantWalk=true};body.InitializeExact(owner,12,0);body.SetControlMaster(owner);body.MoveToWorld(owner.Location,owner.Map);body.AIObject.AITimer.Stop();
        var targets=new List<GraveyardSkeleton>();
        GraveyardSkeleton Spawn(int dx,int dy){var t=PoisonDaggerChecks.Target(owner);t.MoveToWorld(new(owner.X+dx,owner.Y+dy,owner.Z),owner.Map);targets.Add(t);return t;}
        var a=Spawn(4,0);var b=Spawn(2,0);var c=Spawn(0,2);var outside=Spawn(15,0);
        var distances=new Dictionary<uint,int>{{owner.Serial.Value,0},{a.Serial.Value,10},{b.Serial.Value,2},{c.Serial.Value,2},{outside.Serial.Value,1}};
        var allowed=new HashSet<uint>(targets.Select(t=>t.Serial.Value));uint previous=0;bool combat=false;
        bool Eligible(Mobile t)=>t is{Alive:true,Deleted:false}&&allowed.Contains(t.Serial.Value);
        int Distance(Mobile t)=>distances[t.Serial.Value];
        var requests=new List<NecromancerPetAiRequest>();int commonTarget=-1,commonCombat=-1,moveKind=-1;
        var queries=NecromancerPetTargets.Bind(body,owner,()=>previous,Eligible,Distance,_=>combat,requests.Add,()=>0,
            (target,inCombat,_,_)=>{commonTarget=target;commonCombat=inCombat?1:0;return false;},
            (kind,_,_,_)=>{moveKind=kind;return true;});
        try
        {
            Require(queries.Owner()==(int)owner.Serial.Value&&queries.LookupPlayer((int)owner.Serial.Value)==(int)owner.Serial.Value&&queries.LookupPlayer((int)a.Serial.Value)==0,"Actual owner/player lookup");
            Require(queries.Primary(false,24).Id==(int)b.Serial.Value,"Nearest target/serial tie or native scan radius changed");
            previous=a.Serial.Value;Require(queries.Primary(false,24).Id==(int)a.Serial.Value,"Valid previous primary was replaced by nearer target");
            distances[a.Serial.Value]=24;Require(queries.Primary(false,24).Id==(int)a.Serial.Value,"Previous primary distance24 boundary");
            distances[a.Serial.Value]=25;Require(queries.Primary(false,24).Id==(int)b.Serial.Value,"Previous primary distance25 retained");previous=0;
            distances[b.Serial.Value]=distances[c.Serial.Value]=25;distances[a.Serial.Value]=24;Require(queries.Primary(false,24).Id==(int)a.Serial.Value,"Fresh primary24 boundary");
            distances[a.Serial.Value]=25;Require(queries.Primary(false,24).Id==0,"Fresh primary25 or outside14native scan admitted");
            owner.Combatant=a;ClassCombat.For(owner).Target=b.Serial;
            Require(owner.Combatant==null&&queries.OwnerTarget((int)owner.Serial.Value).Id==(int)b.Serial.Value,"Class-enabled owner session selection fallback");
            ClassCombat.For(owner).Target=a.Serial;Require(queries.OwnerTarget((int)owner.Serial.Value).Id==(int)a.Serial.Value,"Changed owner selection");
            allowed.Remove(a.Serial.Value);Require(queries.OwnerTarget((int)owner.Serial.Value).Id==0,"Ineligible owner selection retained");ClassCombat.For(owner).Target=b.Serial;
            Require(queries.OwnerTarget((int)owner.Serial.Value).Id==(int)b.Serial.Value,"Eligible owner selection fallback");
            Require(queries.PetIgnore((int)a.Serial.Value)&&!queries.ValidTarget((int)a.Serial.Value)&&queries.ValidTarget((int)b.Serial.Value)&&!queries.MissileBarrier((int)b.Serial.Value),"Shared eligibility/LOS query wiring");allowed.Add(a.Serial.Value);
            ClassCombat.For(owner).Target=a.Serial;distances[a.Serial.Value]=10;distances[b.Serial.Value]=7;distances[c.Serial.Value]=25;
            var result=NecromancerPetAi.Think(new(default,8,8,0,0),queries);
            Require(result.RememberedOwner==(int)owner.Serial.Value&&commonTarget==(int)a.Serial.Value&&commonCombat==0&&moveKind==-1
                &&requests.Select(r=>r.Kind).SequenceEqual(new[]{NecromancerPetAiKind.DeleteThink,NecromancerPetAiKind.Velocity,NecromancerPetAiKind.Approach})
                &&requests[^1].A==(int)a.Serial.Value,"Live skeleton bound AI67 owner-target replacement/approach");
            requests.Clear();distances[b.Serial.Value]=2;combat=true;
            NecromancerPetAi.Think(new(default,8,8,0,0),queries);
            Require(requests.Select(r=>r.Kind).SequenceEqual(new[]{NecromancerPetAiKind.DeleteThink,NecromancerPetAiKind.Attack})&&requests[^1].B==(int)b.Serial.Value,"Live skeleton bound AI67 primary combat attack");
            bool rejected=false;try{queries.Primary(true,0);}catch(NotSupportedException){rejected=true;}Require(rejected,"Melee binding silently accepted ranged AI");
            Log.Information("Necromancer pet target adapter:actual prepared skeleton owner/current/nearest/ties/14tile scan/24boundary/fallback/eligibility/LOS and AI67approach/attack requests pass; movement/action delivery remains separate");
        }
        finally{owner.Combatant=null;ClassCombat.For(owner).Target=Serial.Zero;foreach(var t in targets){t.Delete();}body.Delete();owner.Delete();}
    }
}
