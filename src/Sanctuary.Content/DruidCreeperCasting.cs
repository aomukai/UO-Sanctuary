// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Linq;
using Server;
using Server.Mobiles;
namespace Sanctuary;

// Prepared do115release transaction. Deliberately not registered with
// DruidCombat while underground exposure/current transport remain unfinished.
internal static class DruidCreeperCasting
{
    internal static Func<int> SeedLow=()=>unchecked((int)Random.Shared.NextInt64(1L<<32));
    internal static Func<PlayerMobile,Point3D,DateTime,ulong,DruidCreeperBody> Spawn=(p,point,at,seed)=>DruidCreeperWorld.Create(p,point,at,seed);
    internal static bool Destination(PlayerMobile owner,Serial selected,Point3D cached,bool release,out Point3D point)
    {
        if(!DruidRavenCasting.Destination(owner,selected,cached,release,out var aim,out _)){point=default;return false;}
        bool Legal(Point3D p)=>Math.Abs(p.Z-owner.Z)<16 && owner.InRange(p,CombatProtocol.TeethRange) && owner.InLOS(p)
            && PetGroundWorld.ZeroFootprint(owner.Map,p);
        if(Legal(aim)){point=aim;return true;}
        // Reuse the declared two-native-tile summon retry projection. Source
        // parameter4room/search ordering is not certified by this adapter.
        for(int radius=1;radius<=2;radius++)for(int x=-radius;x<=radius;x++)for(int y=-radius;y<=radius;y++)
        {
            if(Math.Abs(x)!=radius&&Math.Abs(y)!=radius){continue;}
            var trial=new Point3D(aim.X+x,aim.Y+y,aim.Z);
            if(trial.X<=0||trial.Y<=0||trial.X>=owner.Map.Width||trial.Y>=owner.Map.Height){continue;}
            if(Legal(trial)){point=trial;return true;}
            int z=owner.Map.GetAverageZ(trial.X,trial.Y);
            if(Math.Abs(z-aim.Z)<16&&Legal(new(trial.X,trial.Y,z))){point=new(trial.X,trial.Y,z);return true;}
        }
        point=default;return false;
    }
    internal static bool Commit(PlayerMobile owner,Serial selected,Point3D cached,DateTime at,out DruidCreeperBody body)
    {
        body=null;
        if(owner is not{Alive:true,Deleted:false,Frozen:false,Paralyzed:false} || at.Kind!=DateTimeKind.Utc || !AlliedOverlap.Allowed(owner)
            || !ClassCreation.Profiles.TryGet(owner.Serial,out var profile) || profile.Class!=DiabloClass.Druid
            || ClassCreation.Profiles.GetRank(owner.Serial,"druid.poisoncreeper")<1 || SkillMechanics.Rank(owner,CombatAbility.PoisonCreeper) is <1 or >255
            || !Destination(owner,selected,cached,true,out var point)){return false;}
        var values=SkillMechanics.Values(owner,CombatAbility.PoisonCreeper);var row=SkillMechanics.For(CombatAbility.PoisonCreeper);
        if(row.Number("srvdofunc")!=115 || row.Number("InTown")!=1 || row.Text("anim")!="SC" || row.Text("pettype")!="vine"
            || values.Mana256!=8*256 || values.Eval("petmax")!=1){throw new NotSupportedException("Unsupported Creeper casting route");}
        if(!NecromancerEffects.CanPay(owner,values.Mana256)){return false;}
        DruidCreeperBody created;
        try{created=Spawn(owner,point,at,SourceRandom.InitLow(SeedLow()));}
        catch(Exception e) when(e is NotSupportedException or ArgumentException){return false;}
        if(created==null){return false;}
        if(!NecromancerEffects.Pay(owner,values.Mana256)){created.Delete();return false;}
        DruidVines.Replace(owner,created);body=created;
        owner.PlaySound(0x1E5); // Identified druid_summon audio placeholder.
        return true;
    }
}

internal static class DruidVines
{
    private static readonly CombatAbility[] Skills=[CombatAbility.PoisonCreeper,CombatAbility.CarrionVine,CombatAbility.SolarCreeper];
    internal static bool Source(string key)
    {
        foreach(var ability in Skills)
        {
            var row=SkillMechanics.For(ability);
            if(row.Text("pettype")!="vine"){throw new NotSupportedException("Unexpected shared vine category");}
            if(row.Text("summon")==key){return true;}
        }
        return false;
    }
    internal static void Replace(PlayerMobile owner,SkeletonGuardian replacement)
    {
        if(replacement is not{Alive:true,Deleted:false} || replacement.ControlMaster!=owner || !Source(replacement.SourceMonsterId))
        {throw new InvalidOperationException("Invalid paid vine replacement");}
        foreach(var old in (owner.AllFollowers??[]).OfType<SkeletonGuardian>().Where(p=>p!=replacement&&p.ControlMaster==owner&&Source(p.SourceMonsterId)).ToArray())
        {old.Delete();}
    }
}
