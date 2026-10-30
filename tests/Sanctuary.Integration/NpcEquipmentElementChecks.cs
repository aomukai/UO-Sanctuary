// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Logging;
using Server.Mobiles;
namespace Sanctuary.Integration;

public static class NpcEquipmentElementChecks
{
    private static readonly ILogger Log=LogFactory.GetLogger(typeof(NpcEquipmentElementChecks));
    private sealed record Stored(uint Actor,uint Item,string Gear,NpcElementSnapshot Elements,GearDamage First,GearDamage Last,uint ColdTarget,DateTime ColdUntil);
    private sealed record Saved(uint Owner,Stored[] Actors);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"npc-equipment-elements-fixtures.json");
    private static void Require(bool value,string why){if(!value){throw new InvalidOperationException(why);}}
    public static void Configure(){Register("npc-equipment-elements-check",Check);Register("npc-equipment-elements-reload-check",Reload);}
    private static void Register(string name,Action action)=>ConsoleInputHandler.RegisterCommand(name,"Original NPC equipment elements and buff composition",_=>Core.LoopContext.Post(()=>
    {try{action();Log.Information("{Name}: passed",name);}catch(Exception error){Log.Error(error,"{Name}: failed",name);}}));
    private static void SourceCases()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"npc-equipment-elements-source-cases.json")));int count=0;
        foreach(var row in doc.RootElement.EnumerateArray())
        {
            int[] i=row[0].EnumerateArray().Select(x=>x.GetInt32()).ToArray(),o=row[1].EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            int bound=0;int Roll(int n){bound=n;return i[5]==0?0:n-1;}
            Require(SourceElementRoll.Roll256(i[0],i[1],i[2],i[3],i[4],Roll)==o[0]&&bound==o[1],"Complete source element range differs at "+count);count++;
        }
        Require(count==864,"Missing complete source range contexts");
        var order=new List<int>();var raw=new MissileSourceStats(FireMin:256,FireMax:512,LightningMin:512,LightningMax:1024,ColdMin:768,ColdMax:1536,MagicMin:1024,MagicMax:2048);
        var packet=NpcEquipmentElements.Roll(new(raw,0,0,0,0),n=>{order.Add(n);return 0;});
        Require(order.SequenceEqual(new[]{256,512,768,1024})&&packet.Fire==1&&packet.Lightning==2&&packet.Cold==3&&packet.Magic==4,"Source fire/lightning/cold/magic order or units changed");
        Require(NpcEquipmentElements.Roll(new(new(FireMin:1,FireMax:7),0,0,0,0),_=>throw new InvalidOperationException("Subthreshold roll" )).Total==0,"Fill threshold below8 was ignored");
    }
    private static SkeletonGuardian Pet(PlayerMobile owner)
    {
        var actor=new SkeletonGuardian{Name="NPC element holder [prepared]"};actor.Initialize(owner,7,2);actor.ControlSlots=0;actor.NoKillAwards=true;
        Require(actor.SetControlMaster(owner),"Prepared holder owner failed");actor.MoveToWorld(new(owner.X+2,owner.Y,owner.Z),owner.Map);
        PersistentMinions.Profiles.Track(actor);actor.AIObject.AITimer.Stop();return actor;
    }
    private static Stored Bind(PlayerMobile owner,Item item,string kind)
    {
        var actor=Pet(owner);item.MoveToWorld(new(owner.X+1,owner.Y,owner.Z),owner.Map);
        Require(IronGolemItems.TryAttach(owner,actor,item,out string why),"Original binding failed for "+kind+": "+why);
        if(kind=="mastery")
        {
            // Prepared external leases isolate recipient mastery/ownership;
            // this fixture does not claim a new learned aura/Enchant cast.
            PaladinAuras.ElementalWeapon(new(actor.Serial.Value,owner.Serial.Value,CombatAbility.HolyShock,1,0,TimedEffects.Now.Add(FixtureSpace.Retention),WeaponMin:16,WeaponMax:32));
            SorceressEffects.Profiles.Store(actor,new(Enchantment:new(TimedEffects.Now.Add(FixtureSpace.Retention),1,1001,2001,20,owner.Serial.Value)));
        }
        var itemContext=IronGolemItems.Equipment(actor,CreatureProfiles.Level(actor));var raw=itemContext.Elements();var snapshot=NpcEquipmentElements.Capture(actor);
        if(kind=="fire"){Require(raw.FireMin==25*256&&raw.FireMax==75*256,"Hellplague range differs");}
        if(kind=="magic"){Require(raw.MagicMin==108*256&&raw.MagicMax==108*256,"Ghostflame magic range differs");}
        if(kind=="combined"){Require(raw.FireMin==256&&raw.FireMax==200*256&&raw.LightningMin==256&&raw.ColdMin==256&&raw.ColdFrames==0,"Baranar range or absent cold length differs");}
        if(kind=="cold"){Require(raw.ColdMin==3*256&&raw.ColdMax==5*256&&raw.ColdFrames==50,"Gleamscythe range/frames differ");}
        if(kind=="gems"){Require(raw.FireMin==15*256&&raw.FireMax==20*256&&raw.ColdMin==10*256&&raw.ColdMax==14*256&&raw.ColdFrames==75,"Actual installed Ruby/Sapphire range or units differ");}
        if(kind=="mastery")
        {
            Require(snapshot.LightningMastery is >=10 and <=15&&snapshot.Stats.LightningMin==16*256&&snapshot.Stats.LightningMax==32*256
                &&snapshot.Stats.FireMin==1001&&snapshot.Stats.FireMax==2001&&itemContext.Bonus("pierce-ltng") is >=15 and <=20,"Original Griffon or separate buff ownership/units differ");
        }
        string gear=JsonSerializer.Serialize(ClassGear.State(item));var prior=ClassGear.State(item);
        ClassGear.Profiles.Store(item,prior with{Identified=false});Require(new ItemStatContext(item,CreatureProfiles.Level(actor)).Elements()==new MissileSourceStats(),"Unidentified original supplied item elements");ClassGear.Profiles.Store(item,prior);
        if(item is BaseWeapon weapon)
        {int durability=weapon.HitPoints;weapon.HitPoints=0;Require(itemContext.Elements()==new MissileSourceStats(),"Broken original supplied item elements");weapon.HitPoints=durability;}
        var first=NpcEquipmentElements.Roll(snapshot,_=>0);var last=NpcEquipmentElements.Roll(snapshot,n=>n-1);
        var target=new Rat{Name="NPC element recipient",NoKillAwards=true};target.SetHits(20000);
        foreach(var type in new[]{ResistanceType.Physical,ResistanceType.Fire,ResistanceType.Cold,ResistanceType.Energy}){target.SetResistance(type,0);}
        target.MoveToWorld(new(actor.X+1,actor.Y,actor.Z),actor.Map);target.AIObject.AITimer.Stop();uint coldTarget=0;DateTime coldUntil=default;
        try
        {
            int physical=NpcEquipmentDamage.Roll256(NpcEquipmentDamage.Capture(actor,target).Value,_=>0),before=SkillHealth.Balance256(target);
            int expected=checked(physical+NamedElements.To256(first.Total));
            // Griffon pierce lowers zero resistance below0 on the first hit.
            if(kind=="mastery"){expected=checked(expected+SourcePercentage.Calculate(NamedElements.To256(first.Lightning),itemContext.Bonus("pierce-ltng"),100));}
            Require(NpcEquipmentDamage.TryResolve(actor,target,out _,_=>0)&&before-SkillHealth.Balance256(target)==expected,"Original item/buff elements did not reach actual life exactly once: "+kind);
            before=SkillHealth.Balance256(target);typeof(SkeletonGuardian).GetMethod("Strike",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(actor,[target]);
            Require(before>SkillHealth.Balance256(target),"Actual summon Strike bypassed element packet");
            if(raw.ColdFrames>0)
            {
                Require(TimedEffects.TryActor(target,out var state)&&state.Cold?.Source==actor.Serial.Value&&state.ChillUntil>TimedEffects.Now,"Item cold frames did not reach the NPC-owned cold state");
                Require((state.ChillUntil-TimedEffects.Now).TotalMilliseconds<=raw.ColdFrames*40+100,"Cold duration exceeded source frames");
                // Preserve inspected state only after checking its real source
                // duration. Shared retained cold tests cover natural expiry.
                state.ChillUntil=TimedEffects.Now.Add(FixtureSpace.Retention);coldUntil=state.ChillUntil;coldTarget=target.Serial.Value;
            }
            target.SetResistance(ResistanceType.Fire,50);target.SetResistance(ResistanceType.Energy,kind=="mastery"?100:0);
            before=SkillHealth.Balance256(target);
            int fireLoss=checked((int)SourcePercentage.Damage(NamedElements.To256(first.Fire),50));
            expected=checked(physical+fireLoss+NamedElements.To256(first.Cold+first.Magic)+(kind=="mastery"?0:NamedElements.To256(first.Lightning)));
            Require(NpcEquipmentDamage.TryResolve(actor,target,out _,_=>0)&&before-SkillHealth.Balance256(target)==expected,"Element resistance/immunity or pierce immunity gate failed: "+kind);
        }
        finally{if(coldTarget==0){target.Delete();}else{target.Internalize();}}
        return new(actor.Serial.Value,item.Serial.Value,gear,snapshot,first,last,coldTarget,coldUntil);
    }
    private static void Check()
    {
        SourceCases();var owner=new PlayerMobile{Name="NpcEquipmentElementOwner",Body=0x190,Player=true};owner.AddItem(new Backpack());
        ClassCreation.Profiles.Assign(owner,DiabloClass.Necromancer);ClassCreation.Profiles.AdvanceLevel(owner,47);
        Require(ClassCreation.Profiles.TryTrain(owner.Serial,"necromancer.raiseskeleton",out _),"Holder history prerequisite failed");FixtureSpace.Place(owner,new(6100,2300,0),Map.Trammel);
        // Socket capacity is prepared; insertion itself uses the existing real
        // quote/commit path and consumes each dropped gem exactly once.
        var gemmed=RuneSocketChecks.Host(owner,typeof(Longsword),2);
        GemActivationChecks.Install(owner,GemActivationChecks.Gem(owner,"Ruby"),gemmed);
        GemActivationChecks.Install(owner,GemActivationChecks.Gem(owner,"Sapphire"),gemmed);
        Item Named(string id){Require(NamedGear.Available(id),"Named fixture unavailable: "+id);return NamedGear.Create(id,99,new Random(317),true);}
        var rows=new[]{Bind(owner,Named("unique/31"),"fire"),Bind(owner,Named("unique/333"),"magic"),Bind(owner,Named("unique/256"),"combined"),Bind(owner,Named("unique/28"),"cold"),Bind(owner,Named("unique/336"),"mastery"),Bind(owner,gemmed,"gems")};
        File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(owner.Serial.Value,rows)));PersistentMinions.Suspend(owner);owner.Internalize();Report();
    }
    private static void Reload()
    {
        SourceCases();var saved=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));
        foreach(var row in saved.Actors)
        {
            var actor=(SkeletonGuardian)World.FindMobile((Serial)row.Actor);var item=World.FindItem((Serial)row.Item);
            Require(actor is{Alive:true,Deleted:false}&&actor.ControlMaster?.Serial.Value==saved.Owner&&actor.Map==Map.Internal&&item.Parent==actor&&JsonSerializer.Serialize(ClassGear.State(item))==row.Gear,"Reload lost original identity/parent/full rolls");
            var snapshot=NpcEquipmentElements.Capture(actor);Require(snapshot==row.Elements&&NpcEquipmentElements.Roll(snapshot,_=>0)==row.First&&NpcEquipmentElements.Roll(snapshot,n=>n-1)==row.Last,"Reload changed actual element packet/leases/mastery");
            if(row.ColdTarget!=0){var target=World.FindMobile((Serial)row.ColdTarget);Require(TimedEffects.TryActor(target,out var state)&&state.Cold?.Source==actor.Serial.Value&&state.ChillUntil==row.ColdUntil,"Reload lost retained NPC cold source/timer");}
        }
        Report();
    }
    private static void Report()=>Log.Information("NPC original elements:864complete source range cases;fire/lightning/cold/magic order,actual named/socket ranges,prepared Enchant/aura with own mastery/pierce once,life loss,resistance/immunity,cold state source and exact reload packets pass;poison,critical/procs/full item projection and learned Iron Golem remain unfinished");
}
