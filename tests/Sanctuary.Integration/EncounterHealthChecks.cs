// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Text.Json;
using Server;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class EncounterHealthChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(EncounterHealthChecks));
    private sealed record Saved(uint Owner,uint Enemy,Point3D Point,int Map,int Balance,int Hits,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"encounter-health-fixtures.json");
    public static void Configure(){Register("encounter-health-check",Check);Register("encounter-health-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Encounter FP256 damage and saved fractional life",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Reset(PlayerMobile p)=>SkillHealth.SetBalance256(p,p.HitsMax*256);
    private static void Save(PlayerMobile p,EncounterCreature c,int generation,Point3D point,int map)
    {
        var row=new Saved(p.Serial.Value,c.Serial.Value,point,map,SkillHealth.Balance256(p),p.Hits,generation);
        p.Internalize();c.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(row));
    }
    private static void Check()
    {
        var p=EncounterShieldChecks.Owner(DiabloClass.Necromancer);var c=EncounterShieldChecks.Enemy(p);
        try
        {
            foreach(bool magic in new[]{false,true})
            {
                Reset(p);int hp=SkillHealth.Balance256(p),hits=p.Hits;
                for(int i=1;i<=4;i++)
                {
                    int lost=magic?EliteCombat.Damage(c,p,magic:.25):EliteCombat.Damage(c,p,physical:.25);
                    Require(SkillHealth.Balance256(p)==hp-i*64&&lost==(i==4?1:0),"Encounter discarded fractional "+(magic?"magic":"physical")+" life");
                }
                Require(p.Hits==hits-1&&SkillHealth.Remainder256(p)==0,"Accumulated native life debit");
            }
            Reset(p);int start=SkillHealth.Balance256(p);
            for(int i=1;i<=256;i++)
            {
                EliteCombat.Damage(c,p,physical:1/256.0,fire:1/256.0,cold:1/256.0,lightning:1/256.0,magic:1/256.0);
                Require(SkillHealth.Balance256(p)==start-i*5,"Encounter mixed FP256 packet lost low bits");
            }
            Require(SkillHealth.Remainder256(p)==0,"Mixed FP remainder conservation");
            Reset(p);NecromancerEffects.BoneArmor(p,new(SkillMechanics.For(CombatAbility.BoneArmor),_=>1,_=>0,99));
            NecromancerEffects.AbsorbPhysical(p,19.75);start=SkillHealth.Balance256(p);
            Require(EliteCombat.Damage(c,p,physical:.5)==0&&NecromancerEffects.Armor256(p)==0&&SkillHealth.Balance256(p)==start-64,"Breaking shield discarded fractional overflow");
            Reset(p);var armor=PoisonDaggerChecks.Named(p,"Shaftstop");start=SkillHealth.Balance256(p);
            for(int i=1;i<=10;i++){EliteCombat.Damage(c,p,physical:.25);Require(SkillHealth.Balance256(p)==start-44*i,"Post-resistance FP256 settlement");}
            p.AddToBackpack(armor);armor.Delete();ClassGear.Refresh(p);
            SkillHealth.SetBalance256(p,256);Require(EliteCombat.Damage(c,p,physical:.25)==1&&!p.Alive&&p.LastKiller==c,"Shared native sub-one-life death/owner bridge");
            p.Corpse?.Delete();p.Resurrect();Reset(p);
            var point=p.Location;int map=p.Map.MapID;start=SkillHealth.Balance256(p);int hitsBefore=p.Hits;
            Require(EliteCombat.Damage(c,p,physical:.25)==0&&SkillHealth.Balance256(p)==start-64&&p.Hits==hitsBefore,"Saved initial fractional encounter hit");
            Save(p,c,0,point,map);
            Log.Information("Encounter health:physical/magic quarters,256mixed packets,shield overflow,real30percent resistance and native fractional death pass; saved quarter-point injury staged");
        }
        catch{c.Delete();p.Corpse?.Delete();p.Delete();throw;}
    }
    private static void Reload()
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);var c=(EncounterCreature)World.FindMobile((Serial)s.Enemy);
        Require(p is{Alive:true}&&c is{Alive:true}&&p.Map==Map.Internal&&c.Map==Map.Internal
            &&SkillHealth.Balance256(p)==s.Balance&&p.Hits==s.Hits,$"Saved encounter injury/actor changed:life={SkillHealth.Balance256(p)}/{s.Balance},hits={p.Hits}/{s.Hits},maps={p.Map}/{c.Map},alive={p.Alive}/{c.Alive}");
        // Frozen is a transient fixture control, not a persistent encounter
        // identity field. Restore it before bringing the actor into the scene.
        c.Frozen=true;
        p.MoveToWorld(s.Point,Map.Maps[s.Map]);c.MoveToWorld(new(p.X+1,p.Y,p.Z),p.Map);c.AIObject.AITimer.Stop();
        Require(EliteCombat.Damage(c,p,physical:.25)==0&&SkillHealth.Balance256(p)==s.Balance-64,"Loaded encounter injury failed to accumulate");
        if(s.Generation==1)
        {Require(EliteCombat.Damage(c,p,physical:.25)==1&&SkillHealth.Balance256(p)==s.Balance-128&&SkillHealth.Remainder256(p)==0,"Four quarters across reloads did not debit one native hit point");}
        Save(p,c,s.Generation+1,s.Point,s.Map);
    }
}
