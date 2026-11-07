// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Original graph -> shared ordinary NPC allocation/contact. The caller owns
// allocation/release phase;this does not guess modern A1 startup or enable AI
// attacks/learned casting. Unsupported original registrations refuse intact.
internal static class ValkyrieEquipmentContact
{
    internal const string Profile="valkyrie-original-equipment-contact-v1";
    internal const string OriginalProfile="valkyrie-original-proc-contact-v1";
    private static readonly Dictionary<string,int> Codes=new(StringComparer.Ordinal)
    {
        ["thorns"]=78,["knock"]=81,["heal-kill"]=86,["rip"]=108,["howl"]=112,["stupidity"]=113,["dmg-to-mana"]=114,
        ["dmg-ac"]=120,["light-thorns"]=128,["freeze"]=134,["openwounds"]=135,["crush"]=136,["mana-kill"]=138,["demon-heal"]=139,["slow"]=150,
        ["magic-reduction"]=35,["physical-reduction"]=34,["ac-miss"]=32,["ac-hth"]=33,["res-pois-len"]=110,["nofreeze"]=153,["half-freeze"]=118,
        ["abs-fire%"]=142,["abs-fire"]=143,["abs-ltng%"]=144,["abs-ltng"]=145,["abs-cold%"]=148,["abs-cold"]=149,
        ["pierce-fire"]=333,["pierce-ltng"]=334,["pierce-cold"]=335,["pierce-pois"]=336,["pierce-mag"]=358
    };
    internal static bool Active(BaseCreature actor)=>!World.Loading&&actor is AmazonValkyrieBody{Alive:true,Deleted:false} b
        &&b.Prepared?.EquipmentContactProfile is Profile or OriginalProfile;
    internal static int? Bonus(BaseCreature actor,string code)
    {
        if(!Active(actor)){return null;}
        var body=(AmazonValkyrieBody)actor;_=ValkyrieEquipmentWeapons.Frame(body);
        // The source-profile/native resistance pool already contains stat37.
        // Its additive recipient hook must not count that full graph twice.
        return Number(body.EquipmentNumbers,code);
    }
    internal static int Number(GeneratedNpcStatGraph graph,string code)
    {
        if(code=="res-mag"){return 0;}
        return Codes.TryGetValue(code,out int stat)?graph.Read(stat)
            :throw new NotSupportedException("Unadmitted original Valkyrie scalar consumer: "+code);
    }
    private static NpcCombatEvents ValidateGraph(AmazonValkyrieBody body,bool original=false,bool registration=false)
    {
        _=ValkyrieEquipmentWeapons.Frame(body);_=ValkyrieEquipmentDamage.Passives(body);_=NpcEquipmentRandom.Require(body);
        if(GeneratedNpcLiveResources.State(body)is not{Valkyrie:not null} resources)
        {throw new NotSupportedException("Original Valkyrie contact requires supplied source resources");}
        ValkyrieEquipmentResources.Require(body,resources);
        if(NpcItemProcs.Get(body)!=null){throw new NotSupportedException("Original Valkyrie items cannot use a separate event-only carrier graph");}
        if(original){_=registration?NpcOriginalItemProcs.RequireRegistrationHost(body):NpcOriginalItemProcs.RequireHost(body);}
        return original?NpcCombatEvents.FromOriginalStats(body.EquipmentNumbers.Stats):NpcCombatEvents.FromStats(body.EquipmentNumbers.Stats);
    }
    internal static NpcCombatEvents Registry(AmazonValkyrieBody body)
    {
        if(!Active(body)){throw new NotSupportedException("Original Valkyrie contact is not activated");}
        return ValidateGraph(body,body.Prepared.EquipmentContactProfile==OriginalProfile);
    }
    internal static void Activate(AmazonValkyrieBody body)
    {
        if(body?.Prepared is not{EquipmentContactProfile:null,EquipmentDefenseProfile:ValkyrieEquipmentDefense.Profile} old)
        {throw new NotSupportedException("Original Valkyrie contact needs its qualified original defense/weapon context");}
        _=ValidateGraph(body);body.RestorePrepared(old with{EquipmentContactProfile=Profile});
    }
    internal static void ActivateOriginal(AmazonValkyrieBody body)
    {
        if(body?.Prepared is not{EquipmentContactProfile:null,EquipmentDefenseProfile:ValkyrieEquipmentDefense.Profile} old)
        {throw new NotSupportedException("Original proc contact needs its qualified original defense/weapon context");}
        _=ValidateGraph(body,true,true);body.RestorePrepared(old with{EquipmentContactProfile=OriginalProfile});
    }
    internal static void RequireRegistration(AmazonValkyrieBody body)
    {
        if(body?.Prepared?.EquipmentContactProfile!=OriginalProfile)
        {throw new NotSupportedException("Original Valkyrie registration is not activated");}
        _=ValidateGraph(body,true,true);
    }
    internal static void RequireAllocation(AmazonValkyrieBody body,BaseCreature target)=>OriginalNpcMelee.RequireAllocation(body,target);
    internal static IronGolemContact.Prepared Prepare(AmazonValkyrieBody body,PlayerMobile owner,BaseCreature target,Func<int,bool> range,DateTime at)
        =>OriginalNpcMelee.Prepare(body,owner,target,range,at);
    internal static NpcWeaponImpact Impact(AmazonValkyrieBody body,BaseCreature target,int flags)=>OriginalNpcMelee.Impact(body,target,flags);
}
