// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

internal sealed record BloodOathTransfer(uint Demon,int Amount256);
internal sealed record BloodOathRedirect(int Original256,int Remaining256,int Rank,int Percent,BloodOathTransfer[] Transfers,string Fault=null);

// Approved Sanctuary reaction,not a recovered event35. See the complete
// original/bound rule in warlock-final-talents-approval.json.
internal static class BloodOathDamageProposal
{
    internal static bool Approved=>true;
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(BloodOathDamageProposal));
    private static readonly HashSet<uint> Delivering=new();
    internal static bool ManagedPrepared(Mobile target)=>target is PlayerMobile{Alive:true,Deleted:false,NetState:{Running:true}} owner
        &&Approved&&ClassCreation.Profiles.TryGet(owner.Serial,out var profile)
        &&profile.Class==DiabloClass.Warlock&&SkillMechanics.Rank(owner,CombatAbility.BloodOath)>0;
    internal static (int Percent,int Maximum) Numbers(int rank)
    {
        var row=SkillMechanics.For(CombatAbility.BloodOath);
        if(row.SourceId!=378||!row.Passive||row.Number("srvstfunc")!=66||row.Text("auraevent1")!="absorbresisteddamage"
            ||row.Number("auraeventfunc1")!=35||row.Text("calc1")!="dm12"||row.Number("calc2")!=3||row.Text("pettype")!="demon"||rank is<0 or>255)
        {throw new NotSupportedException("Unqualified Blood Oath reaction data");}
        var v=new SkillValues(row,key=>key==row.SourceKey?rank:0,_=>0,99);int percent=checked((int)v.Eval("calc1"));
        if(percent is<0 or>100){throw new NotSupportedException("Unqualified Blood Oath redirect percentage");}
        return(percent,row.Number("calc2"));
    }
    private static bool Recipient(PlayerMobile owner,BaseCreature body)=>body is{Alive:true,Deleted:false,Blessed:false}
        &&body.ControlMaster==owner&&body.Map==owner.Map&&body.Map!=Map.Internal&&PersistentMinions.Permitted(owner,body,DiabloClass.Warlock)&&PersistentMinions.Active(owner,body)
        &&!MinionSafeAreas.Parked(body)&&body.CanBeDamaged()&&SkillHealth.Balance256(body)>0;
    private static void Qualified(PlayerMobile owner,BaseCreature body,Mobile source)
    {
        if(!WarlockDemonRecipients.Owned(owner,body))
        {throw new NotSupportedException("Blood Oath needs its original qualified demon host");}
        if(NpcEquipmentCombat.Context(body)!=null){throw new NotSupportedException("Blood Oath equipment modifiers need their own settled receiver");}
        if(BoundDemonHosts.Active(body))
        {
            // Reuse the stateless standard Damage/OnDamage override check;
            // this does not install a Revive identity or incoming writer.
            ReviveEquipmentIncoming.RequireBody(body);
            PreparedMonsterNativeDamage.RequireBaseModifiers(source,body,controlledIdentity:true);
        }
        else
        {
            if(body is not SkeletonGuardian original||DeathMarkWorld.State(original)is not{Selector:1})
            {throw new NotSupportedException("Blood Oath needs the original learned demon's qualified world");}
            PreparedMonsterNativeDamage.RequireIncoming(owner,body,source);
        }
    }
    internal static BloodOathRedirect ReducePrepared(Mobile source,PlayerMobile owner,int damage256,SkillDamageKind kind)
    {
        if(damage256<0){throw new ArgumentOutOfRangeException(nameof(damage256));}
        var empty=new BloodOathRedirect(damage256,damage256,0,0,[]);
        if(damage256==0||kind!=SkillDamageKind.Direct||!ManagedPrepared(owner)||source==null||source==owner
            ||SkillHealth.IsSelfCost(source,owner)||ClassCombat.Friendly(source,owner)||owner.Map==null||owner.Map==Map.Internal
            ||MinionSafeAreas.Contains(owner)||Delivering.Contains(owner.Serial.Value)){return empty;}
        var transfers=new List<BloodOathTransfer>();int rank=SkillMechanics.Rank(owner,CombatAbility.BloodOath),percent=0;
        Delivering.Add(owner.Serial.Value);
        try
        {
            var n=Numbers(rank);percent=n.Percent;
            // Membership precedes qualification so an unsupported legacy or
            // faulted member still refuses instead of silently granting cover.
            var pets=WarlockDemonRecipients.Members(owner).Where(b=>Recipient(owner,b)).Take(n.Maximum).ToArray();
            if(pets.Length==0||percent==0){return empty with{Rank=rank,Percent=percent};}
            int budget=checked((int)((long)damage256*percent/100));
            if(budget==0){return empty with{Rank=rank,Percent=percent};}
            foreach(var body in pets){Qualified(owner,body,source);}
            // One total budget,not one full budget per pet. Preserve FP8
            // remainders in original creation order;unabsorbed shares stay
            // on the player and are not reassigned to another recipient.
            int each=budget/pets.Length,remainder=budget%pets.Length,redirected=0;
            for(int i=0;i<pets.Length;i++)
            {
                var body=pets[i];if(!Recipient(owner,body)){continue;}Qualified(owner,body,source);
                int share=Math.Min(checked(each+(i<remainder?1:0)),SkillHealth.Balance256(body));if(share<=0){continue;}
                int before=SkillHealth.Balance256(body);
                // Already-settled damage:bypass pet defenses,retain actual
                // attacker/native death credit,and do not emit a new attack.
                SkillHealth.Damage(source,body,share,SkillDamageKind.Return);
                int lost=before-Math.Max(0,SkillHealth.Balance256(body));
                if(lost<share){throw new NotSupportedException("Blood Oath native receiver changed its settled life loss");}
                redirected=checked(redirected+share);transfers.Add(new(body.Serial.Value,share));
            }
            return new(damage256,damage256-redirected,rank,percent,transfers.ToArray());
        }
        catch(Exception e)when(e is NotSupportedException or System.IO.InvalidDataException or OverflowException)
        {
            // Native callbacks may already have completed a transfer. Keep
            // those effects;do not invent rollback or grant unverified cover.
            Log.Warning("Blood Oath refused unsupported incoming context for {Owner}: {Reason}",owner.Serial,e.Message);
            return new(damage256,damage256,rank,percent,transfers.ToArray(),e.Message);
        }
        finally{Delivering.Remove(owner.Serial.Value);}
    }
}
