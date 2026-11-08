// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record ShadowMasterGap(int Skill,string Reason);
internal sealed record ShadowGroundState(SkeletonWorldState World,ShadowWarriorMemory Warrior)
{
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public ShadowMasterMemory Master{get;init;}
    [JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)]public ShadowMasterGap[] Gaps{get;init;}
}
internal sealed record ShadowGroundAnimation(string ReferenceVersion,int WalkVelocity,int RunVelocity,ValkyrieAnimationRow[] Rows);
// Own source motion/brain over the existing equipped-summon ground transport.
// Master's distinct AI106 is not replaced by Warrior or NecroPet behavior.
internal static class ShadowGroundMotion
{
    internal const string Profile="shadow-warrior-original-ground-v1";
    internal const string MasterProfile="shadow-master-original-ground-v1";
    internal static string ProfileFor(AssassinShadowBody body)=>body.Prepared.Capture.Ability==CombatAbility.ShadowMaster?MasterProfile:Profile;
    private static readonly Lazy<ShadowGroundAnimation> Animation=new(()=>
    {
        var data=BalanceCatalog.ReadObject<ShadowGroundAnimation>("shadow-ground-animation");
        if(data is not{ReferenceVersion:"3.3.93847",WalkVelocity:6,RunVelocity:9,Rows.Length:2}
            ||!data.Rows.Select(r=>r.Code).Order().SequenceEqual(new[]{"AIRNHT2","AIWLHT2"})
            ||data.Rows.Any(r=>r.Frames!=8||r.Speed!=256||r.Events.Length!=0))
        {throw new InvalidDataException("Unsupported owned Shadow locomotion rows");}
        return data;
    });
    internal static bool Active(AssassinShadowBody body)=>body?.Prepared is{} saved&&saved.GroundMotion?.World?.Controller?.Profile==(saved.Capture.Ability==CombatAbility.ShadowMaster?MasterProfile:Profile);
    internal static ShadowGroundState Copy(ShadowGroundState s)=>s==null?null:s with{World=s.World with{Controller=s.World.Controller with{
        Motion=s.World.Controller.Motion with{Points=(Point2D[])s.World.Controller.Motion.Points.Clone(),SavedSteps=(Point2D[])s.World.Controller.Motion.SavedSteps.Clone()}}},Gaps=s.Gaps?.ToArray()};
    internal static void Validate(ShadowGroundState s,uint owner,ShadowOwnerSnapshot capture)
    {
        if(s==null){return;}
        bool master=capture.Ability==CombatAbility.ShadowMaster;
        if(capture.Ability is not(CombatAbility.ShadowMaster or CombatAbility.ShadowWarrior)||s.World?.Owner!=owner||s.World.Controller?.Profile!=(master?MasterProfile:Profile)||s.Warrior==null
            ||s.Warrior.AllowedFrame<0||s.Warrior.Budget<0||!master&&(s.Master!=null||s.Gaps!=null)
            ||master&&(s.Master==null||s.Master.Repeat is<0 or>25||s.Master.Skill is<0 or>428||s.Master.Rank!=capture.Rank
                ||s.Warrior.AllowedFrame!=0||s.Warrior.Budget!=0||s.Gaps is{Length:>429}
                ||s.Gaps!=null&&(s.Gaps.Any(g=>g==null||g.Skill is<0 or>428||string.IsNullOrEmpty(g.Reason)||g.Reason.Length>512)||s.Gaps.Select(g=>g.Skill).Distinct().Count()!=s.Gaps.Length)))
        {throw new InvalidDataException("Invalid original Shadow ground/brain context");}
        IronGolemWorld.Validate(s.World.Controller with{Profile="prepared-necropet-ground-fit-equipment-clock-v1"},false,true);
    }
    internal static void Activate(AssassinShadowBody body,PlayerMobile owner,DateTime at)
    {
        if(body?.Prepared is not{GroundMotion:null} old||old.Capture.Ability is not(CombatAbility.ShadowWarrior or CombatAbility.ShadowMaster)||!body.CantWalk
            ||owner is not{Alive:true,Deleted:false}||body.ControlMaster!=owner||owner.Map==null||owner.Map==Map.Internal||body.Map!=owner.Map
            ||OriginalNpcEquipment.Owner(body)!=owner.Serial.Value)
        {throw new NotSupportedException("Ground activation needs its original qualified Shadow with a distinct brain profile");}
        _=Animation.Value;_=ShadowEquipmentDefense.Registry(body);
        try{body.WorldGround.Initialize(owner,at,()=>NpcEquipmentRandom.Require(body),adopt:true,initialThink:20);body.CantWalk=false;}
        catch{IronGolems.Forget(body);body.RestorePrepared(old);body.WorldGround.Invalidate();body.CantWalk=true;throw;}
    }
    internal static int Mode(AssassinShadowBody body)
    {
        if(!Active(body)){throw new NotSupportedException("Shadow has no owned original ground mode");}
        var controller=body.WorldGround.State.Controller;
        if(body.Map!=Map.Internal&&(controller.Map!=body.Map?.MapID||body.X!=(IronGolemWorld.Cell(controller).X+1)/2||body.Y!=(IronGolemWorld.Cell(controller).Y+1)/2))
        {throw new NotSupportedException("Shadow native pose changed before source controller repair");}
        return ShadowActions.State(body)is{} action?body.OwnedSkills.Find(action.Cast.Skill).Mode:controller.Mode;
    }
    internal static (int Velocity,int Animation) Speed(AssassinShadowBody body,int bonus,bool run)
    {
        if(!Active(body)||body.CantWalk){throw new NotSupportedException("Shadow ground motion is not active");}
        var graph=OriginalNpcEquipment.Graph(body);
        // Source initializer supplies velocitypercent75;converted Assassin
        // run adds100*9/6-100. Converted locomotion bases are213walk/101run.
        int percent=checked(graph.Read(67)+(run?50:0)+bonus+PaladinVigor.VelocityPercent(body)-GearEffects.MovementSlowTotal(body));
        return SourceActorMotion.Speed(Animation.Value.WalkVelocity,run?101:213,percent,graph.Read(96));
    }
    private static int Source(AssassinShadowBody body,string name)=>NecromancerSummons.Fields("shadowwarrior").TryGetValue(name,out var value)
        &&value.TryGetInt32(out int number)?number:throw new InvalidDataException("Missing Shadow AI parameter: "+name);
    internal static void Think(AssassinShadowBody body,PlayerMobile owner,int frame,DateTime at,NecromancerPetAiServices q)
    {
        if(body.Prepared.Capture.Ability==CombatAbility.ShadowMaster){ShadowMasterWorld.Think(body,owner,at,q);return;}
        var primary=q.Primary(false,0);var state=body.Prepared.GroundMotion;
        var c=new ShadowWarriorContext(frame,primary.Id,primary.Distance,primary.Combat,q.Distance((int)owner.Serial.Value),body.Prepared.Capture.Rank,
            Source(body,"aip1"),Source(body,"aip2"),Source(body,"aip3"),Source(body,"aip4"),Source(body,"aip8(N)"),Source(body,"aip8(H)"));
        int? Selected(bool right)
        {
            var binding=SkillBindings.Instance.Get(owner);var ability=right?binding.Right:binding.Left;
            return ability==CombatAbility.None?null:ability==CombatAbility.BasicAttack?0:SkillMechanics.For(ability).SourceId;
        }
        ShadowAiSkill Skill(int id)
        {
            var entry=body.OwnedSkills.Find(id);if(entry==null){return null;}
            var row=ItemSkillExecution.Skill(id);int rank=body.OwnedSkillRank(id);
            var definition=id==0?null:SkillMechanics.All.FirstOrDefault(s=>s.SourceId==id);
            SkillValues values=definition==null?null:new(definition,key=>body.OwnedSkillRank(SkillMechanics.Source(key).SourceId),
                key=>body.OwnedSkills.Find(SkillMechanics.Source(key).SourceId)?.Base??0,body.Prepared.Capture.Level);
            if(row.HasDelay("delay")){throw new NotSupportedException("Shadow current delay needs its own source field context");}
            return new(id,ShadowSkillBook.Metadata(id).Class,entry.Mode,row.Number("aitype"),rank,Math.Max(0,(values?.Mana256??0)>>8),0,
                row.Text("aurastate")!="0",row.Text("auratargetstate")!="0",row.Number("progressive")!=0,ShadowSkillCasting.Range(body,id));
        }
        bool Pet(ShadowAiSkill skill)
        {
            var row=ShadowSkillBook.Metadata(skill.Id);
            return row.Summon!=417&&(row.PetType<0||row.PetType!=16);
        }
        bool State(ShadowAiSkill skill)=>ShadowDefensiveBuffs.Handles(skill.Id)
            ?OriginalNpcEquipment.Graph(body).Passives.State(ShadowDefensiveBuffs.State(skill.Id))
                :skill.Id==277?BladeShields.State(body)!=null
                :ShadowChargeStrikes.Handles(skill.Id)?ShadowChargeStrikes.Charge(body,skill.Id)!=null
                :throw new NotSupportedException("Shadow skill state query needs its NPC consumer: "+skill.Id);
        var s=new ShadowWarriorServices(()=>true,()=>Selected(false),()=>Selected(true),
            (left,right)=>body.ChangeSkills(book=>book.Mirror(body.Prepared.Capture,left,right)),Skill,Pet,State,State,
            skill=>ShadowChargeStrikes.Handles(skill.Id)?ShadowChargeStrikes.Charge(body,skill.Id)?.Count>=3
                :throw new NotSupportedException("Shadow progressive count needs its own consumer"),
            ()=>NpcEquipmentRandom.Raw(body),n=>NpcEquipmentRandom.Limited(body,n),()=>NpcEquipmentRandom.Limited(body,100),q.CommonMove,
            n=>q.Request(new(NecromancerPetAiKind.Idle,n)),target=>q.Request(new(NecromancerPetAiKind.RunTarget,target)),
            (skill,target)=>q.Request(skill.Id==0?new(NecromancerPetAiKind.Attack,4,target):new(NecromancerPetAiKind.Skill,skill.Mode,skill.Id,target)),
            ()=>body.ChangeSkills(book=>book.Assign(0,1)));
        var next=ShadowWarriorAi.Think(c,state.Warrior,s);
        body.ChangeGroundMemory(next);
    }
}
