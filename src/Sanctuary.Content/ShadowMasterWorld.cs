// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Distinct AI106 world bridge. Native16-tile sectors project the existing
//32-source-cell room grid;room/unit order is a named native adapter,not D2
//room-enumeration equivalence. Original skills and transport stay shared.
internal static class ShadowMasterWorld
{
    internal static Action<AssassinShadowBody,ShadowMasterScan> ObservedScan;
    internal static void Think(AssassinShadowBody body,PlayerMobile owner,DateTime at,NecromancerPetAiServices q)
    {
        if(body.Prepared.GroundMotion?.Master is not{} memory||q.Act==null||q.Retreat==null||q.Cell==null||q.CurrentTarget==null||q.InMelee==null)
        {throw new NotSupportedException("Master needs its distinct saved brain and result-bearing world services");}
        int Source(string field)=>NecromancerSummons.Source("shadowmaster",field);
        var primary=q.Primary(false,0);var services=new Services(body,owner,q);
        var c=new ShadowMasterContext(Source("aip1"),Source("aip1(N)"),Source("aip1(H)"),Source("aip2"),Source("aip2(H)"),Source("aip2(N)"),
            Source("aip3"),primary.Combat,primary.Distance,services.Unit(primary.Id));
        body.ChangeMasterMemory(ShadowMasterAi.Think(c,memory,services));
    }
    internal static Mobile Find(ShadowMasterUnit unit)=>unit==null?null:World.FindMobile((Serial)(uint)unit.Id);
    internal static ShadowMasterUnit Handle(Mobile unit)=>unit is{Deleted:false}?new((int)unit.Serial.Value,unit is PlayerMobile?0:1):null;
    internal static IEnumerable<Mobile> Adjacent(AssassinShadowBody body,Point2D cell)
    {
        int cx=cell.X/32,cy=cell.Y/32;var map=body.Map;var result=new List<Mobile>();
        // Reuse the generic native room-grid ordering:neighbor Y then X.
        // Preserve each native sector's unit order rather than sorting by ID.
        for(int y=Math.Max(0,cy-1);y<=Math.Min((map.Height-1)/16,cy+1);y++)
        for(int x=Math.Max(0,cx-1);x<=Math.Min((map.Width-1)/16,cx+1);x++)
        foreach(var m in map.GetMobilesInRange<Mobile>(new Point3D(x*16+8,y*16+8,body.Z),8))
        {if(m.X>=x*16&&m.X<x*16+16&&m.Y>=y*16&&m.Y<y*16+16){result.Add(m);}}return result;
    }
    private static string Source(BaseCreature mob)=>mob switch
    {
        AssassinSentryBody sentry=>SkillMechanics.For(sentry.Prepared.Creation.Ability).Text("summon"),
        BladeSentinelBody=>"bladecreeper",SkeletonGuardian original=>original.SourceMonsterId,
        HydraBody hydra=>hydra.SourceMonsterId,
        _=>CreatureProfiles.Definition(mob).Reference
    };
    private sealed class Services(AssassinShadowBody body,PlayerMobile owner,NecromancerPetAiServices q):ShadowMasterServices
    {
        private int currentSkill;
        internal override ShadowMasterUnit Self=>Handle(body);
        internal override bool HasSkills=>body.Prepared.Skills!=null;
        internal override ShadowMasterUnit Unit(int id)=>id==0?null:Handle(World.FindMobile((Serial)(uint)id));
        internal override ShadowMasterUnit Owner(ShadowMasterUnit unit)=>Handle(Find(unit)is BaseCreature b?b.GetMaster():null);
        internal override void DeleteThink()=>q.Request(new(NecromancerPetAiKind.DeleteThink));
        internal override int Squared(ShadowMasterUnit a,ShadowMasterUnit b)
        {
            var from=Find(a);var to=Find(b);if(from==null||to==null||from.Map!=to.Map){throw new NotSupportedException("Master squared-distance actor is unavailable");}
            var p=from==body?q.Cell():PetOwnerRoutes.Cell(from);var t=to==body?q.Cell():PetOwnerRoutes.Cell(to);
            return checked((p.X-t.X)*(p.X-t.X)+(p.Y-t.Y)*(p.Y-t.Y));
        }
        internal override bool CommonMove(ShadowMasterUnit target,ShadowMasterUnit master,bool combat,int flags,int leash)
            =>Find(master)==owner?q.CommonMove(target?.Id??0,combat,flags,leash):throw new NotSupportedException("Master owner route is unavailable");
        internal override ShadowMasterUnit Target(ShadowMasterUnit unit)=>Find(unit)==body?Unit((int)q.CurrentTarget())
            :Find(unit)==owner?Unit(q.OwnerTarget((int)owner.Serial.Value).Id):Handle(Find(unit)?.Combatant);
        internal override bool TrySkill(int id,bool combat,ShadowMasterUnit target,int x,int y)
        {
            currentSkill=id;
            ShadowAiSkill Lookup(int skill)
            {
                var entry=body.OwnedSkills.Find(skill);if(entry==null){return null;}var r=ItemSkillExecution.Skill(skill);
                return new(skill,ShadowSkillBook.Metadata(skill).Class,entry.Mode,r.Number("aitype"),body.OwnedSkillRank(skill),0,0,false,false,false,ShadowSkillCasting.Range(body,skill));
            }
            try
            {
                return ShadowMasterTactics.Use((int)body.Serial.Value,(int)owner.Serial.Value,target,id,combat,x,y,Lookup,s=>s.Range==1,
                    (t,flags)=>flags==4?q.Act(new(NecromancerPetAiKind.RunTarget,t?.Id??0)):throw new NotSupportedException("Unqualified Master run flags"),
                    (s,t,px,py)=>
                    {
                        if(s.Id==0){return q.Act(new(NecromancerPetAiKind.Attack,s.Mode,t?.Id??0));}
                        if(!(s.Id is 251 or 253 or 255 or 260 or 264 or 270 or 273 or 277||ShadowDefensiveBuffs.Handles(s.Id)||ShadowSentries.Handles(s.Id)||ShadowFinishers.Builder(s.Id)))
                        {body.MasterGap(s.Id,"Intrinsic skill action consumer remains unimplemented or separately gated");return 0;}
                        if(px!=0||py!=0){throw new NotSupportedException("Master coordinate action needs its own saved aim bridge");}
                        return q.Act(new(NecromancerPetAiKind.Skill,s.Mode,s.Id,t?.Id??0));
                    })!=0;
            }
            catch(NotSupportedException e){body.MasterGap(id,e.Message);return false;}
        }
        internal override IEnumerable<int> Skills()=>body.OwnedSkills.Snapshot().Entries.Select(s=>s.Id);
        internal override ShadowMasterSkill Row(int id)
        {
            currentSkill=id;var r=ItemSkillExecution.Skill(id);var meta=GenericPetMetadata.Table.Skill(id);
            int missileA=r.Text("srvmissilea") is "0"or""?-1:ItemSkillExecution.MissileId(r.Text("srvmissilea"));
            if(meta?.AiType is 5 or 11&&r.Missile<0&&missileA>=0)
            {try{_=SkillMissileRanges.Range(missileA);}catch(NotSupportedException e){body.MasterGap(id,e.Message);return null;}}
            return meta==null?null:new(id,meta.AiType,meta.AuraState,meta.AuraTargetState,ShadowSkillBook.Metadata(id).Element,r.Number("reqlevel"),r.Number("aibonus"),
                (meta.Flags36&4)!=0,r.Missile,missileA,r.Do);
        }
        internal override bool State(ShadowMasterUnit unit,int state)
        {
            var actor=Find(unit);if(actor==body)
            {return state==119||ShadowChargeStrikes.State(body,state)||OriginalNpcEquipment.Graph(body).Passives.State(state)
                ||GenericPetMetadata.Table.State(state)?.Name=="bladeshield"&&BladeShields.State(body)!=null;}
            if(GenericPetMetadata.Table.State(state)?.Name=="cloaked")
            {return NecromancerEffects.Curse(actor)is{Ability:CombatAbility.CloakOfShadows,CloakBlind:true};}
            body.MasterGap(currentSkill,"Target state query is not qualified: "+state);return false;
        }
        internal override int? Group(int state)=>GenericPetMetadata.Table.State(state)?.Group;
        internal override int StateCount=>GenericPetMetadata.Table.StateCount;
        internal override int Percentage()=>NpcEquipmentRandom.Limited(body,100);
        internal override int? Left()=>body.OwnedSkills.Left;
        internal override void SetLeft(int id)=>body.ChangeSkills(book=>book.Select(id,false));
        internal override bool Dead(ShadowMasterUnit unit)=>Find(unit)is not{Alive:true,Deleted:false};
        internal override bool Enemy(ShadowMasterUnit unit)=>IronGolemWorld.Eligible(body,owner,Find(unit));
        internal override void Idle(int frames)=>q.Request(new(NecromancerPetAiKind.Idle,frames));
        private ShadowMasterMonster Monster(ShadowMasterUnit u)
        {
            if(Find(u)is not BaseCreature mob){return null;}if(mob is BaseVendor){return new(-1,true,false);}
            if(mob is EncounterCreature){return new(-1,false,true);}string source=Source(mob);if(source==null){return null;}
            var row=NativeMonsterReturn.Identity(source);return new(row.Base,false,row.Killable);
        }
        private byte Flags(ShadowMasterUnit u)=>Find(u)is BaseCreature mob&&GenericPetSearchTables.SourceId(Source(mob))is{} id
            ?GenericPetSearchTables.Bind(1).Monster(1,id)?.Flags60??0:(byte)0;
        private ShadowMasterQueries Queries()=>new(Dead,u=>ClassCombat.Friendly(body,Find(u)),Monster,u=>Find(u)is{Deleted:false}m&&m.CanBeDamaged(),Enemy,
            (id,t)=>Squared(Unit(id),t),u=>Find(u)is BaseCreature m&&(CreatureProfiles.For(m).Traits.Boss==true||(Flags(u)&64)!=0),u=>(Flags(u)&128)!=0,
            (u,mask)=>Find(u)is EncounterCreature e&&(mask&(e.Grade switch{EncounterGrade.Champion=>4,EncounterGrade.Unique=>8,EncounterGrade.SuperUnique=>10,_=>0}))!=0);
        internal override ShadowMasterScan Scan()
        {var scan=ShadowMasterTargets.Scan((int)body.Serial.Value,(int)owner.Serial.Value,Adjacent(body,q.Cell()).Select(Handle),Queries());ObservedScan?.Invoke(body,scan);return scan;}
        internal override bool Special(ShadowMasterUnit target)=>ShadowMasterTargets.Special((int)body.Serial.Value,target,Queries());
        internal override int LifePercent(){int maximum=SkillHealth.Maximum256(body)>>8;return maximum==0?0:checked(100*(SkillHealth.Balance256(body)>>8))/maximum;}
        internal override bool Progressive()=>AssassinCharges.Any(body);
        internal override bool ClearMissileLine(ShadowMasterUnit target)=>Find(target)is{} t&&body.InLOS(t);
        internal override uint Raw()=>NpcEquipmentRandom.Raw(body);
        internal override void Run(ShadowMasterUnit target)=>q.Act(new(NecromancerPetAiKind.RunTarget,target?.Id??0));
        internal override bool Retreat(ShadowMasterUnit target,int flags,int mode)=>flags==8&&mode==1?q.Retreat(target?.Id??0,flags,true):throw new NotSupportedException("Unqualified Master retreat tactic");
        internal override int Resistance(ShadowMasterUnit target,int stat)
        {
            var t=Find(target)??throw new NotSupportedException("Missing Master resistance target");var pair=stat switch
            {0=>("physical",t.PhysicalResistance),1=>("fire",t.FireResistance),2=>("ltng",t.EnergyResistance),3=>("cold",t.ColdResistance),4=>("pois",t.PoisonResistance),5=>("mag",NamedElements.MagicResistance(t)),_=>throw new NotSupportedException("Unknown Master resistance stat")};
            return NamedElements.Resistance(null,t,pair.Item1,pair.Item2,0); // Target effects,without attacker pierce or Sanctuary.
        }
        internal override int Rank(int id)=>body.OwnedSkillRank(id);
        internal override int Limited(int maximum)=>NpcEquipmentRandom.Limited(body,maximum);
        internal override int? Charges(int id)
        {var definition=SkillMechanics.All.FirstOrDefault(s=>s.SourceId==id);return definition!=null&&AssassinCharges.Supports(definition.Ability)?AssassinCharges.State(body,definition.Ability)?.Count:null;}
        internal override int? MissileRange(int id)=>SkillMissileRanges.Range(id);
        internal override int? Drain(ShadowMasterUnit target)=>Find(target)is BaseCreature mob?CreatureProfiles.For(mob).Traits.DrainEffectiveness:null;
        internal override bool Melee(ShadowMasterUnit target)=>Find(target)is{} t&&q.InMelee(t);
    }
}
