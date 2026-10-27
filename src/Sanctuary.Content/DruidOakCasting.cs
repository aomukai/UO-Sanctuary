// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

public static class DruidOakCasting
{
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    internal static Func<PlayerMobile,Point3D,DateTime,ulong,DruidOakBody> Spawn=(p,point,at,seed)=>DruidOakWorld.Create(p,point,at,seed);
    internal static Func<PlayerMobile,Point3D,DateTime,ulong,DruidOakBody> WolverineSpawn=(p,point,at,seed)=>DruidOakWorld.Create(p,point,at,seed,CombatAbility.HeartOfWolverine);
    public static void Initialize()=>ClassMapHooks.LocationChanged+=Moved;
    private static void Moved(Mobile mobile)
    {
        if(World.Loading||mobile is not PlayerMobile owner){return;}var action=ClassCombat.For(owner);
        if((action.PendingAbility is CombatAbility.OakSage or CombatAbility.HeartOfWolverine)&&(owner.Map!=action.ActionMap||owner.Location!=action.ActionOrigin)){ClassCombat.Stop(owner);}
    }
    internal static bool Destination(PlayerMobile owner,Serial selected,Point3D point,bool release,out Point3D result)
        =>DruidSpiritWolfCasting.Destination(owner,selected,point,release,out result);
    public static bool Begin(PlayerMobile owner,Serial selected,Point3D point,long now,CombatAbility ability=CombatAbility.OakSage)
        =>Destination(owner,selected,point,false,out _)
            &&SkillCombat.Begin(owner,ability,selected,point,now,(p,actionAbility,id,where)=>Destination(p,id,where,false,out _));
    internal static bool Commit(PlayerMobile owner,Serial selected,Point3D cached,DateTime at,out DruidOakBody body,CombatAbility ability=CombatAbility.OakSage)
    {
        DruidSpiritStats.Key(ability);var row=SkillMechanics.For(ability);body=null;
        if(owner is not{Alive:true,Deleted:false,Frozen:false,Paralyzed:false}||at.Kind!=DateTimeKind.Utc||!AlliedOverlap.Allowed(owner)
            ||!ClassCreation.Profiles.TryGet(owner.Serial,out var profile)||profile.Class!=DiabloClass.Druid
            ||ClassCreation.Profiles.GetRank(owner.Serial,row.TalentId)<1||SkillMechanics.Rank(owner,ability) is <1 or >255
            ||!Destination(owner,selected,cached,true,out var point)){return false;}
        var values=SkillMechanics.Values(owner,ability);
        if(row.Number("srvdofunc")!=119||row.Number("InTown")!=1||row.Text("anim")!="SC"||row.Text("pettype")!="totem"
            ||values.Mana256!=((ability==CombatAbility.OakSage?14:19)+values.Rank)*256||values.Eval("petmax")!=1){throw new NotSupportedException("Unsupported shared spirit casting route");}
        if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        DruidOakBody created;
        try{created=(ability==CombatAbility.OakSage?Spawn:WolverineSpawn)(owner,point,at,SourceRandom.InitLow(SeedLow()));}
        catch(Exception e) when(e is ArgumentException or NotSupportedException){return false;}
        if(created==null){return false;}
        if(created.SpiritAbility!=ability||created.ControlMaster!=owner){created.Delete();return false;}
        if(!NecromancerEffects.Pay(owner,values.Mana256)){created.Delete();return false;}
        DruidSpirits.Replace(owner,created);body=created;owner.PlaySound(0x1E5); // Identified druid_summon placeholder.
        return true;
    }
    public static bool Release(PlayerMobile owner,CombatAbility ability=CombatAbility.OakSage)
    {
        var action=ClassCombat.For(owner);
        return DruidCombat.Available(owner,ability)&&Commit(owner,action.PendingTarget,action.PendingPoint,TimedEffects.Now,out _,ability);
    }
}

internal static class DruidSpirits
{
    private static readonly CombatAbility[] Skills=[CombatAbility.OakSage,CombatAbility.HeartOfWolverine,CombatAbility.SpiritOfBarbs];
    internal static bool Source(string key)
    {
        foreach(var ability in Skills)
        {
            var row=SkillMechanics.For(ability);
            if(row.Text("pettype")!="totem"||row.Text("petmax")!="1"){throw new NotSupportedException("Unexpected shared spirit category");}
            if(row.Text("summon")==key){return true;}
        }
        return false;
    }
    internal static DruidOakBody[] Oaks(PlayerMobile owner)=>(owner.AllFollowers??[]).OfType<DruidOakBody>()
        .Where(p=>p is{Alive:true,Deleted:false}&&p.SpiritAbility==CombatAbility.OakSage&&p.ControlMaster==owner).OrderBy(BattleCommand.PetOrder).ToArray();
    internal static DruidWolverineBody[] Hearts(PlayerMobile owner)=>(owner.AllFollowers??[]).OfType<DruidWolverineBody>()
        .Where(p=>p is{Alive:true,Deleted:false}&&p.ControlMaster==owner).OrderBy(BattleCommand.PetOrder).ToArray();
    internal static void Replace(PlayerMobile owner,SkeletonGuardian replacement)
    {
        if(replacement is not{Alive:true,Deleted:false}||replacement.ControlMaster!=owner||!Source(replacement.SourceMonsterId))
        {throw new InvalidOperationException("Invalid paid spirit replacement");}
        foreach(var old in (owner.AllFollowers??[]).OfType<SkeletonGuardian>().Where(p=>p!=replacement&&p.ControlMaster==owner&&Source(p.SourceMonsterId)).ToArray())
        {old.Delete();}
    }
}
