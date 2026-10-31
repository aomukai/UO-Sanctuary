// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class GolemRosterChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(GolemRosterChecks));
    private static readonly CombatAbility[] Kinds=[CombatAbility.ClayGolem,CombatAbility.BloodGolem,CombatAbility.IronGolem,CombatAbility.FireGolem];
    private sealed record Saved(uint Owner,uint Body,uint Item,bool Member,long Order,string Gear);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"golem-roster-fixtures.json");
    private static int _scene;
    public static void Configure(){Register("golem-roster-check",Check);Register("golem-roster-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Shared golem replacement/cap/original lifecycle;Iron learned cast remains disabled",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    private static void Train(PlayerMobile owner,string id)
    {
        ClassCreation.Profiles.TryGet(owner.Serial,out var profile);Require(TalentCatalog.TryGet(profile.Class,id,out var row),"Missing talent "+id);
        foreach(var prior in row.Prerequisites){Train(owner,prior);}
        if(ClassCreation.Profiles.GetRank(owner.Serial,id)==0){Require(ClassCreation.Profiles.TryTrain(owner.Serial,id,out _),"Train "+id);}
    }
    private static PlayerMobile Fresh(DiabloClass kind=DiabloClass.Necromancer)
    {
        var owner=new PlayerMobile{Body=0x190,Player=true,Name="GolemRosterOwner"};owner.AddItem(new Backpack());ClassCreation.Profiles.Assign(owner,kind);ClassCreation.Profiles.AdvanceLevel(owner,99);
        if(kind==DiabloClass.Necromancer){Train(owner,"necromancer.firegolem");Train(owner,"necromancer.raiseskeleton");Train(owner,"necromancer.skeletalmage");}
        else{Train(owner,"barbarian.battlecommand");}
        FixtureSpace.Corridor(owner,6100,24,3100+24*_scene++);return owner;
    }
    private static void End(PlayerMobile owner)
    {foreach(var pet in(owner.AllFollowers??[]).OfType<BaseCreature>().ToArray()){pet.Delete();}owner.Delete();}
    private static IronGolemBody Iron(PlayerMobile owner,int dx=1)
    {
        var item=new Broadsword();ClassGear.Profiles.Ensure(item);item.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);
        return IronGolemBody.CreatePrepared(owner,item,item.Location);
    }
    private static NecromancerMinion Raw(PlayerMobile owner,CombatAbility ability,int dx=1)
    {
        var body=new NecromancerMinion{CantWalk=true};body.Prepare(owner,ability);Require(body.SetControlMaster(owner),"Raw minion owner");
        body.MoveToWorld(new(owner.X+dx,owner.Y,owner.Z),owner.Map);body.AIObject.AITimer.Stop();BattleCommand.Track(body);PersistentMinions.Profiles.Track(body);return body;
    }
    private static SkeletonGuardian Spawn(PlayerMobile owner,CombatAbility ability,int dx)
    {
        if(ability==CombatAbility.IronGolem){var body=Iron(owner,dx);NecromancerGolems.Commit(owner,body);return body;}
        Require(NecromancerSummons.Create(owner,ability,Serial.Zero,new(owner.X+dx,owner.Y,owner.Z)),"Public golem creation "+ability);
        var result=NecromancerGolems.For(owner).Single();result.AIObject.AITimer.Stop();return result;
    }
    private static void Matrix()
    {
        int count=0;
        foreach(var from in Kinds)foreach(var to in Kinds)
        {
            var owner=Fresh();var skeleton=new SkeletonGuardian{CantWalk=true};skeleton.Initialize(owner,1,0);Require(skeleton.SetControlMaster(owner),"Skeleton owner");
            skeleton.MoveToWorld(new(owner.X+4,owner.Y,owner.Z),owner.Map);skeleton.AIObject.AITimer.Stop();var mage=Raw(owner,CombatAbility.RaiseSkeletalMage,5);
            try
            {
                var old=Spawn(owner,from,1);var original=IronGolemItems.State(old) is{} binding?World.FindItem((Serial)binding.Item):null;
                var next=Spawn(owner,to,2);var roster=NecromancerGolems.For(owner);
                Require(old.Deleted&&roster.Length==1&&roster[0]==next&&next.ControlMaster==owner&&!next.Deleted,
                    "Cross-type replacement failed "+from+" -> "+to);
                Require(original==null||original.Deleted,"Replaced Iron Golem returned or retained its consumed original");
                Require(skeleton.Alive&&!skeleton.Deleted&&mage.Alive&&!mage.Deleted&&!NecromancerGolems.Member(skeleton)&&!NecromancerGolems.Member(mage),"Golem replacement consumed a skeleton/mage");
                Require(NecromancerGolems.Limit(owner)==1&&BattleCommand.PetOrder(next)>0,"Shared cap/creation order");count++;
            }
            finally{End(owner);}
        }
        Require(count==16,"Incomplete four-type replacement matrix");
    }
    private static void Failures()
    {
        var owner=Fresh();var old=(IronGolemBody)Spawn(owner,CombatAbility.IronGolem,1);var original=World.FindItem((Serial)IronGolemItems.State(old).Item);
        try
        {
            Require(!NecromancerSummons.Create(owner,CombatAbility.ClayGolem,Serial.Zero,Point3D.Zero)&&!old.Deleted&&!original.Deleted,"Invalid placement consumed existing golem");
            Require(!NecromancerSummons.Create(owner,CombatAbility.IronGolem,Serial.Zero,new(owner.X+2,owner.Y,owner.Z))&&!old.Deleted,"Legacy factory silently substituted Iron Golem");
            var failed=new Broadsword();ClassGear.Profiles.Ensure(failed);failed.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);var point=failed.Location;bool refused=false;
            try{IronGolemBody.CreatePrepared(owner,failed,point,_=>throw new NotSupportedException("Injected pre-commit failure"));}
            catch(NotSupportedException e){refused=e.Message=="Injected pre-commit failure";}
            Require(refused&&!failed.Deleted&&failed.Parent==null&&failed.Location==point&&failed.Movable&&!old.Deleted&&!original.Deleted
                &&NecromancerGolems.For(owner).Single()==old,"Failed candidate replaced old golem or consumed failed original");failed.Delete();
            var unrelated=Fresh();try{var other=Spawn(unrelated,CombatAbility.ClayGolem,1);Spawn(owner,CombatAbility.BloodGolem,2);Require(!other.Deleted&&NecromancerGolems.For(unrelated).Single()==other,"Cross-owner roster replacement");}finally{End(unrelated);}
        }
        finally{End(owner);}
    }
    private static void Refresh()
    {
        var owner=Fresh();var supplier=Fresh(DiabloClass.Barbarian);var old=(IronGolemBody)Spawn(owner,CombatAbility.IronGolem,1);
        var oldItem=World.FindItem((Serial)IronGolemItems.State(old).Item);var storage=Iron(owner,3);var storageItem=World.FindItem((Serial)IronGolemItems.State(storage).Item);
        try
        {
            // Simulate a historical missing-order entry through the real ledger
            // restore API. It must be older than a recorded subsequent birth.
            var ledger=BattleCommand.Snapshot();BattleCommand.Restore(ledger with{Pets=ledger.Pets.Where(p=>p.Pet!=old.Serial.Value).ToArray()});
            var newer=Raw(owner,CombatAbility.ClayGolem,2);PersistentMinions.Suspend(owner);
            Require(old.Map==Map.Internal&&newer.Map==Map.Internal&&NecromancerGolems.For(owner).Length==2,"Parked roster fixture");
            BattleCommand.Apply(supplier,owner,1,TimedEffects.Now.AddHours(1));
            Require(old.Deleted&&oldItem.Deleted&&!newer.Deleted&&NecromancerGolems.For(owner).Single()==newer
                &&!storage.Deleted&&!storageItem.Deleted&&!storage.Prepared.RosterMember,"Cap refresh missed Iron/original or pruned storage-only body");
        }
        finally{End(supplier);End(owner);}
    }
    private static void Stage()
    {
        var owner=Fresh();var joined=(IronGolemBody)Spawn(owner,CombatAbility.IronGolem,1);var storage=Iron(owner,3);
        var rows=new System.Collections.Generic.List<Saved>();
        foreach(var body in new[]{joined,storage})
        {
            var item=World.FindItem((Serial)IronGolemItems.State(body).Item);
            rows.Add(new(owner.Serial.Value,body.Serial.Value,item.Serial.Value,body.Prepared.RosterMember,BattleCommand.PetOrder(body),JsonSerializer.Serialize(ClassGear.State(item))));
        }
        PersistentMinions.Suspend(owner);owner.Internalize();File.WriteAllText(Manifest,JsonSerializer.Serialize(rows));
    }
    private static void Check()
    {
        Require(!NecromancerCombat.Implemented(CombatAbility.IronGolem),"Roster enabled unfinished learned Iron Golem");
        Matrix();Failures();Refresh();Stage();
        Log.Information("Golem roster:16four-type replacements,unrelated skeleton/mage/owner preservation,original consumption,failed candidate rollback,parked Command cap refresh,historical-order fallback and excluded storage bodies pass;learned Iron/payment/event parity remain separate");
    }
    private static void Reload()
    {
        var rows=JsonSerializer.Deserialize<Saved[]>(File.ReadAllText(Manifest));Require(rows.Length==2,"Saved roster fixtures");
        foreach(var row in rows)
        {
            var body=World.FindMobile((Serial)row.Body) as IronGolemBody;var item=World.FindItem((Serial)row.Item);
            Require(body is{Alive:true,Deleted:false}&&body.ControlMaster?.Serial.Value==row.Owner&&body.Map==Map.Internal&&body.Prepared.RosterMember==row.Member
                &&BattleCommand.PetOrder(body)==row.Order&&item?.Parent==body&&JsonSerializer.Serialize(ClassGear.State(item))==row.Gear,"Saved roster/order/original identity changed");
        }
        var owner=(PlayerMobile)World.FindMobile((Serial)rows[0].Owner);NecromancerGolems.Enforce(owner);
        Require(NecromancerGolems.For(owner).Single().Serial.Value==rows.Single(x=>x.Member).Body&&rows.All(r=>World.FindMobile((Serial)r.Body) is{Deleted:false}),"Reload cap pruned storage or lost membership");
    }
}
