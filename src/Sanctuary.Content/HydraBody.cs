// SPDX-License-Identifier: GPL-3.0-or-later
using ModernUO.Serialization;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Untargetable/nonblocking emitter identity. Combat/lifetime live in the
// existing Hydra persistence, not the native AI or UO summon timer.
[SerializationGenerator(0)]
public partial class HydraBody:BaseCreature
{
    [SerializableField(0,setter:"private")] private long _headId;
    internal HydraHead Head=>Hydras.Head(_headId);
    internal string SourceMonsterId=>"hydra"+((Head?.Index??throw new System.InvalidOperationException("Hydra identity is detached"))+1);
    internal int Passive(string name)=>Head?.Source?.Birth.Passive(name)??0;
    internal void Attach(long headId,Mobile owner){_headId=headId;SummonMaster=owner;}
    internal bool TargetAllowed(PlayerMobile owner,Mobile target)=>AssassinTrapTargets.Eligible(this,owner,target,()=>base.CanBeHarmful(target,false,true));
    public override Mobile GetDamageMaster(Mobile damagee)
        =>Head is{Source.NpcOwner:not null} head&&World.FindMobile((Serial)head.Owner) is PlayerMobile{Deleted:false} owner
            ?owner:base.GetDamageMaster(damagee);
    public override bool BlocksFit=>false;
    public override bool UsesNativeSummonTimer=>false;
    public override bool CanBeDamaged()=>false;
    public override bool Commandable=>false;
    public override bool IsBondable=>false;
    public override bool IsDispellable=>false;
    public override bool CanRegenHits=>false;
    public override bool CanRegenMana=>false;
    public override bool CanRegenStam=>false;
    public override bool DeleteCorpseOnDeath=>true;
    public override bool OnMoveOver(Mobile mobile)=>true;
    public override bool IsEnemy(Mobile mobile)=>false;
    public override bool CanBeHarmful(Mobile mobile,bool message,bool ignoreOurBlessedness)=>false;
    public HydraBody():base(AIType.AI_Melee,FightMode.None,0)
    {
        Name="Hydra head [placeholder]";Body=0x9C;Hue=0x489;Blessed=true;CantWalk=true;
        Summoned=true;ControlSlots=0;NoKillAwards=true;SetStr(0);SetDex(0);SetInt(0);SetStam(0);SetMana(0);SetDamage(0,0);AIObject?.AITimer.Stop();
    }
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    public override void GenerateLoot(bool spawning){}
    public override bool OnBeforeDeath(){Delete();return false;}
    public override void OnAfterDelete(){Hydras.Forget(this,_headId);base.OnAfterDelete();}
    [AfterDeserialization]private void AfterLoad(){AIObject?.AITimer.Stop();}
}
