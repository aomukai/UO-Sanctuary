// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using ModernUO.Serialization;
using Server;
using Server.Mobiles;
using Server.Movement;
namespace Sanctuary;

// Untargetable/nonblocking emitter identity. Combat/lifetime live in the
// existing Hydra persistence, not the native AI or UO summon timer.
[SerializationGenerator(0)]
public partial class HydraBody:BaseCreature
{
    [SerializableField(0,setter:"private")] private long _headId;
    private ushort _visualFrame;
    private DateTime _visualRefresh;
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
        Name="Hydra head";Body=0x9C;Hue=0x489;Blessed=true;CantWalk=true;
        Summoned=true;ControlSlots=0;NoKillAwards=true;SetStr(0);SetDex(0);SetInt(0);SetStam(0);SetMana(0);SetDamage(0,0);AIObject?.AITimer.Stop();
    }
    public override void OnThink(){AIObject?.AITimer.Stop();Combatant=null;FocusMob=null;Warmode=false;}
    internal void Present(HydraHead head, DateTime now)
    {
        var control = head.Source.Control;
        int elapsed = control.Mode == 0
            ? (int)Math.Clamp(400 - (head.Source.RetireUtc.GetValueOrDefault() - now).TotalMilliseconds, 0, 400)
            : 0;
        int offset = control.Mode switch
        {
            9 => control.Frame / 3,
            1 => SpellEffectGraphics.HydraPoseFrames + (control.Frame / 5) % SpellEffectGraphics.HydraPoseFrames,
            4 => 2 * SpellEffectGraphics.HydraPoseFrames + (control.Frame + 12 - control.EndAt) / 3,
            _ => 3 * SpellEffectGraphics.HydraPoseFrames + elapsed / 100
        };
        int forwardX = 0, forwardY = 0;
        Movement.Offset(Direction, ref forwardX, ref forwardY);
        ushort graphic = elapsed == 400
            ? SpellEffectGraphics.HydraHeadGone
            : (ushort)((forwardX < forwardY ? SpellEffectGraphics.HydraHeadLeft : SpellEffectGraphics.HydraHeadRight) + offset);
        if (graphic == _visualFrame && now < _visualRefresh) { return; }

        // Project the retained half-tile origin and spread the three necks so
        // they remain readable even when the native emitter tiles coincide.
        double x = head.X - X, y = head.Y - Y;
        int screenX = (int)Math.Round((x - y) * 22) + (head.Index == 0 ? -14 : 0);
        int screenY = (int)Math.Round((x + y) * 22);
        FixedEffect(graphic, screenX + 64, screenY + 64);
        _visualFrame = graphic;
        _visualRefresh = now.AddMilliseconds(220);
    }
    public override void GenerateLoot(bool spawning){}
    public override bool OnBeforeDeath(){Delete();return false;}
    public override void OnAfterDelete(){Hydras.Forget(this,_headId);base.OnAfterDelete();}
    [AfterDeserialization]private void AfterLoad()
    {
        if (Name == "Hydra head [placeholder]") { Name = "Hydra head"; }
        AIObject?.AITimer.Stop();
    }
}
