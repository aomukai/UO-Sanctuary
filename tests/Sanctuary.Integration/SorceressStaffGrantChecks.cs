// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class SorceressStaffGrantChecks
{
    private sealed record Staff(string Key,string Id,string Base,int Properties,CombatAbility[] Skills,int[] Ranks);
    private sealed record InputCase(string Key,string Staff,CombatAbility Ability,int Rank,bool Ground);
    private static readonly Staff[] Staves=[
        new("lazarus","unique/56","cst",9,[CombatAbility.ChainLightning,CombatAbility.Lightning,CombatAbility.StaticField],[2,3,4]),
        new("salamander","unique/57","bst",6,[CombatAbility.FireWall,CombatAbility.Fireball,CombatAbility.Warmth],[3,4,5]),
        new("jang","unique/58","wst",8,[CombatAbility.Nova,CombatAbility.FrostNova,CombatAbility.Blaze],[4,5,4]),
        new("bane","unique/54","sst",7,[CombatAbility.FireBolt,CombatAbility.Warmth],[5,2]),
        new("chromatic","unique/185","8cs",8,[CombatAbility.FireMastery,CombatAbility.LightningMastery,CombatAbility.ColdMastery],[4,4,4]),
        new("tal","piece/78","oba",7,[CombatAbility.FireMastery,CombatAbility.LightningMastery,CombatAbility.ColdMastery],[])];
    private static readonly InputCase[] Inputs=[
        new("nova-right","jang",CombatAbility.Nova,4,true),new("frost-quick","jang",CombatAbility.FrostNova,5,true),
        new("blaze-quick","jang",CombatAbility.Blaze,4,true),new("wall-right","salamander",CombatAbility.FireWall,3,true),
        new("ball-right","salamander",CombatAbility.Fireball,4,false),new("chain-quick","lazarus",CombatAbility.ChainLightning,2,false),
        new("lightning-right","lazarus",CombatAbility.Lightning,3,false),new("static-quick","lazarus",CombatAbility.StaticField,4,true),
        new("firebolt-right","bane",CombatAbility.FireBolt,5,false),new("firebolt-quick","bane",CombatAbility.FireBolt,5,false)];
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(SorceressStaffGrantChecks));
    private sealed record SavedItem(string Key,uint Item,string Gear);
    private sealed record Saved(int Generation,uint Owner,SavedItem[] Items,uint Equipped,int Life,int Mana,SavedItem[] Partners);
    private static bool MeasuringRecovery;
    private static string Manifest=>Path.Join(Core.BaseDirectory,"sorceress-staff-grants-fixtures.json");
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    [CallPriority(int.MaxValue)]public static void Initialize()
    {
        var hp=ClassGearHooks.CanRegenerateHits;var mp=ClassGearHooks.CanRegenerateMana;
        ClassGearHooks.CanRegenerateHits=m=>m.Name!="SorceressStaffGrantFixture"&&hp?.Invoke(m)!=false;
        ClassGearHooks.CanRegenerateMana=m=>(m.Name!="SorceressStaffGrantFixture"||MeasuringRecovery)&&mp?.Invoke(m)!=false;
    }
    public static void Configure()
    {
        ConsoleInputHandler.RegisterCommand("sorceress-staff-readiness-check","Bound whole named-catalogue admission delta",_=>Core.LoopContext.Post(()=>
        {try{Readiness();Log.Information("sorceress-staff-readiness-check: passed");}catch(Exception e){Log.Error(e,"sorceress-staff-readiness-check: failed");}}));
        foreach(bool again in new[]{false,true})
        {
            bool reload=again;string name=reload?"sorceress-staff-grants-reload-check":"sorceress-staff-grants-check";
            ConsoleInputHandler.RegisterCommand(name,"Complete three-staff grants/equip/passive/binding/original-save checks",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}: passed",name);FixtureNetwork.Connected(name+"-result",n=>
            {try{if(reload){Reload(n);}else{Scene(n);}Log.Information("{Name}-result: passed",name);}catch(Exception e){Log.Error(e,"{Name}-result: failed",name);}});}));
        }
        foreach(var c in Inputs)
        {
            var spec=c;string name="sorc-staff-"+c.Key;
            ConsoleInputHandler.RegisterCommand(name+"-check","Actual complete-item granted untrained spell and retained effect",_=>Core.LoopContext.Post(()=>
            {Log.Information("{Name}-check: passed",name);FixtureNetwork.Connected(name+"-result",n=>Input(n,spec));}));
        }
    }
    private static void Readiness()
    {
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"sorceress-staff-before-readiness.json")));
        var rows=doc.RootElement.GetProperty("Definitions").EnumerateArray().ToArray();Require(rows.Length==573,"Incomplete baseline definitions");
        var changed=rows.Where(r=>r.GetProperty("Available").GetBoolean()!=NamedGear.Available(r.GetProperty("Id").GetString())).ToArray();
        var expected=new[]{"unique/186","unique/56","unique/57","unique/58","unique/54","unique/185","piece/76","piece/77","piece/78","piece/79","piece/80"}.Order(StringComparer.Ordinal);
        Require(changed.All(r=>!r.GetProperty("Available").GetBoolean())&&changed.Select(r=>r.GetProperty("Id").GetString()).Order(StringComparer.Ordinal).SequenceEqual(expected),"Unexpected named readiness delta: "+string.Join(',',changed.Select(r=>r.GetProperty("Id").GetString())));
        Require(!NamedGear.Available("unique/254"),"Staff direct grants enabled unfinished Hellslayer proc");
    }
    private static PlayerMobile Owner(DiabloClass type=DiabloClass.Sorceress)
    {
        var p=new PlayerMobile{Body=0x191,Female=true,Player=true,Name="SorceressStaffGrantFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,type);ClassCreation.Profiles.AdvanceLevel(p,99);
        foreach(var stat in new[]{CharacterStat.Strength,CharacterStat.Dexterity,CharacterStat.Energy})
        {while((stat==CharacterStat.Strength?p.RawStr:stat==CharacterStat.Dexterity?p.RawDex:p.RawInt)<(stat==CharacterStat.Strength?225:100)){Require(ClassCreation.Profiles.TrySpendStat(p,stat),"Ordinary stat allocation");}}
        FixtureSpace.Corridor(p,6900,34,2250);ClassGear.Refresh(p);p.Hits=p.HitsMax;p.Mana=p.ManaMax;return p;
    }
    private static void Equip(PlayerMobile p,Item item)
    {var current=CombatTiming.Equipped(p);if(current!=null&&current!=item){p.AddToBackpack(current);}if(item.Parent!=p){p.AddToBackpack(item);Require(p.EquipItem(item),"Equip complete original staff");}ClassGear.Refresh(p);}
    private static Item Make(PlayerMobile p,Staff spec)
    {
        var item=Generate(spec.Id);Require(NamedGear.IntrinsicProperties(item).Count()==spec.Properties,"Complete named generation lost a property: "+spec.Key);Equip(p,item);return item;
    }
    private static Item Generate(string id)
    {
        Require(NamedGear.Available(id),"Complete item is unavailable: "+id);var definition=NamedGear.Catalog.Item(id);
        for(int seed=173;seed<1173;seed++)
        {
            var item=NamedGear.Generate(definition.BaseCode,Enum.Parse<GearRarity>(definition.Rarity),99,new Random(seed),true);
            if(item!=null&&ClassGear.State(item).NamedItemId==id){return item;}
            item?.Delete();
        }
        throw new InvalidOperationException("No fixture seed generated "+id);
    }
    private static int[] Ranks(PlayerMobile p,Staff spec)=>spec.Key!="tal"?spec.Ranks:spec.Skills.Select(a=>NamedGear.IntrinsicProperties(CombatTiming.Equipped(p)).Single(x=>x.Property.Code=="skill"&&ItemSingleSkillProperty.Parameter(x.Property)==SkillMechanics.For(a).SourceId).Roll.Value).ToArray();
    private static double RecoveryRate(PlayerMobile p)
    {
        // Native timers cannot interleave this synchronous main-loop probe.
        // Keep the exact input/save mana freeze outside this one getter call.
        MeasuringRecovery=true;
        try{return GearEffects.RegenerationRate(p,TimeSpan.FromSeconds(2),"mana-per-five").TotalSeconds;}
        finally{MeasuringRecovery=false;}
    }
    private static void Grants(PlayerMobile p,Staff spec)
    {
        var ranks=Ranks(p,spec);Require(spec.Key!="tal"||ranks.All(r=>r is >=1 and <=2),"Orb independent mastery rolls escaped range");
        for(int i=0;i<spec.Skills.Length;i++)
        {
            var ability=spec.Skills[i];Require(SkillMechanics.Rank(p,ability)==ranks[i]&&ClassCreation.Profiles.GetRank(p.Serial,SkillMechanics.For(ability).TalentId)==0,"Specific/general effective grant or hard rank: "+spec.Key+"/"+ability);
            if(ClassAbilities.Find(ability) is not {Passive:true}){Require(ClassCombat.Available(p,ability)&&SkillBindings.Instance.AssignQuick(p,i,ability),"Active grant/binding unavailable");}
            else{Require(!SkillBindings.Instance.Assign(p,false,ability)&&!SkillBindings.Instance.AssignQuick(p,7,ability),"Passive skill acquired active binding");}
        }
        int before=NecromancerEffects.ManaBalance256(p);
        foreach(int amount in new[]{p.ManaMax*256,p.ManaMax*256-17})
        {
            NecromancerEffects.SetMana256(p,amount);
            for(int n=0;n<3;n++){ClassGear.Refresh(p);Require(NecromancerEffects.ManaBalance256(p)==amount,"Unchanged equipment refresh spent whole/fractional mana: "+spec.Key);}
        }
        NecromancerEffects.SetMana256(p,before);
        double wanted=spec.Key=="salamander"?2/1.78:spec.Key=="lazarus"?2/1.43:spec.Key=="bane"?2/1.42:2;
        Require(Math.Abs(RecoveryRate(p)-wanted)<.000001,"Actual Warmth/item recovery rate differs: "+spec.Key);
        if(spec.Key=="salamander"){Require(SorceressCombat.Passive(p,CombatAbility.Warmth)==78&&p.FireResistance==30&&GearEffects.Damage(p,0).Fire==15&&GearEffects.Damage(p,1).Fire==32,"Complete Salamander nonspell/passive effects");}
        else if(spec.Key=="lazarus"){Require(p.EnergyResistance==75&&ClassGear.Bonus(p,"physical-reduction")==5&&ClassGear.Bonus(p,"enr")==15&&ClassGear.Bonus(p,"mana-regen%")==43,"Complete Lazarus nonspell effects");}
        else if(spec.Key=="jang"){Require(CombatTiming.Fcr(p)==20&&ClassGear.Bonus(p,"dmg%")==100&&ClassGear.Bonus(p,"att%")==50&&ClassGear.Bonus(p,"ac")==30,"Complete Jang Bong nonspell effects");}
        else if(spec.Key=="bane"){Require(CombatTiming.Fcr(p)==20&&p.FireResistance==50&&ClassGear.Bonus(p,"mana")==30&&GearEffects.Damage(p,0).Fire==4&&GearEffects.Damage(p,1).Fire==6,"Complete Bane Ash nonspell effects");}
        else if(spec.Key=="chromatic")
        {
            Require(CombatTiming.Fcr(p)==20&&p.FireResistance is >=20 and <=40&&p.ColdResistance==p.FireResistance&&p.EnergyResistance==p.FireResistance&&p.PoisonResistance==p.FireResistance
                &&ClassGear.Bonus(p,"hp%") is >=20 and <=25&&ClassGear.Bonus(p,"light-thorns")==20,"Complete Chromatic Ire nonspell effects");
            Masteries(p,ranks);
        }
        else
        {
            Require(ClassGear.Bonus(p,"hp")==57&&ClassGear.Bonus(p,"mana")==77&&ClassGear.Bonus(p,"enr")==10&&CombatTiming.Fcr(p)==20,"Complete Lidless Eye base properties");Masteries(p,ranks);
        }
    }
    private static void Masteries(PlayerMobile p,int[] ranks)
    {
        int fire=30+7*(ranks[0]-1),lightning=50+12*(ranks[1]-1),cold=20+5*(ranks[2]-1);
        Require(SorceressCombat.Passive(p,CombatAbility.FireMastery)==fire&&SorceressCombat.Passive(p,CombatAbility.LightningMastery)==lightning&&SorceressCombat.Passive(p,CombatAbility.ColdMastery)==cold,"Zero-hard mastery values");
        Require(SkillMechanics.Rank(p,CombatAbility.FireBolt)==0&&!ClassCombat.Available(p,CombatAbility.FireBolt),"General Sorceress bonus unlocked an unrelated active spell");
        var target=FireBoltChecks.Target(p,2);
        try
        {
            target.SetHits(50000);target.Hits=target.HitsMax;
            foreach(var c in new[]{("fire",100+fire), ("ltng",100+lightning), ("cold",50+cold)})
            {
                target.SetResistance(ResistanceType.Cold,50);int before=SkillHealth.Balance256(target);
                SkillCombat.Hit(p,target,100,c.Item1,recover:false);
                Require(before-SkillHealth.Balance256(target)==c.Item2*256,"Item mastery failed actual typed life damage: "+c.Item1);
            }
            target.SetResistance(ResistanceType.Cold,100);int life=SkillHealth.Balance256(target);SkillCombat.Hit(p,target,100,"cold",recover:false);
            Require(life==SkillHealth.Balance256(target),"Item Cold Mastery broke immunity");
        }
        finally{target.Delete();}
    }
    private static void Suppressed(PlayerMobile p,Staff spec)
    {foreach(var a in spec.Skills){Require(SkillMechanics.Rank(p,a)==0&&(ClassAbilities.Find(a) is {Passive:true}||!ClassCombat.Available(p,a)),"Inactive/wrong-class staff retained grant: "+a);}}
    private static void Lifecycle(PlayerMobile p,Item item,Staff spec)
    {
        Equip(p,item);Grants(p,spec);p.AddToBackpack(item);ClassGear.Refresh(p);Suppressed(p,spec);Equip(p,item);
        var original=ClassGear.State(item);ClassGear.Profiles.Store(item,original with{Identified=false});ClassGear.Refresh(p);Suppressed(p,spec);ClassGear.Profiles.Store(item,original);ClassGear.Refresh(p);
        var weapon=(BaseWeapon)item;int life=weapon.HitPoints;weapon.HitPoints=0;ClassGear.Refresh(p);Suppressed(p,spec);weapon.HitPoints=life;ClassGear.Refresh(p);Grants(p,spec);
        var other=Owner(DiabloClass.Necromancer);p.AddToBackpack(item);other.AddToBackpack(item);
        if(spec.Key=="tal"){Require(ClassGear.CanEquip(item,other)==false&&!other.EquipItem(item)&&item.Parent==other.Backpack,"Sorceress-only orb equipped on Necromancer");}
        else{Equip(other,item);}
        Suppressed(other,spec);other.AddToBackpack(item);p.AddToBackpack(item);Equip(p,item);other.Delete();Grants(p,spec);
    }
    private static void TalSet(PlayerMobile p,Item orb,Item[] partners)
    {
        foreach(var item in partners){p.AddToBackpack(item);}Equip(p,orb);var ranks=Ranks(p,Staves.Single(x=>x.Key=="tal"));
        try
        {
            for(int n=1;n<=5;n++)
            {
                if(n>1){Require(p.EquipItem(partners[n-2]),"Equip original Tal Rasha partner");}ClassGear.Refresh(p);
                int general=n>=2?3:0;if(n==5){general+=3;}
                for(int i=0;i<3;i++){Require(SkillMechanics.Rank(p,Staves.Last().Skills[i])==ranks[i]+general,"Partial/full Tal mastery rank threshold: "+n);}
                Require(ClassGear.Bonus(p,"pierce-fire")== (n>=3?15:0)&&ClassGear.Bonus(p,"pierce-ltng")== (n>=4?15:0)&&ClassGear.Bonus(p,"extra-cold")== (n==5?15:0),"Tal orb partial damage threshold: "+n);
                Require(DruidFirestorm.Modifiers(p).FireMastery==30+7*(ranks[0]+general-1)&&DruidFirestorm.Modifiers(p).LightningMastery==50+12*(ranks[1]+general-1),"Tal mastery missile supplier");
            }
            var weapon=(BaseWeapon)orb;int life=weapon.HitPoints;weapon.HitPoints=0;ClassGear.Refresh(p);Suppressed(p,Staves.Last());
            Require(ClassGear.Bonus(p,"pierce-fire")==0&&ClassGear.Bonus(p,"extra-cold")==0,"Broken orb retained its partial item modifiers");weapon.HitPoints=life;ClassGear.Refresh(p);
        }
        finally{foreach(var item in partners){p.AddToBackpack(item);}ClassGear.Refresh(p);}
        Grants(p,Staves.Last());
    }
    private static void Scene(NetState network)
    {
        var p=Owner();var account=FixtureNetwork.Authenticate(network,p,"sorc-staff-grants-");Item[] items=[],partners=[];bool kept=false;
        try
        {
            foreach(var spec in Staves){Suppressed(p,spec);}items=Staves.Select(s=>Make(p,s)).ToArray();
            for(int i=0;i<Staves.Length;i++){Lifecycle(p,items[i],Staves[i]);}
            partners=new[]{"piece/77","piece/76","piece/79","piece/80"}.Select(Generate).ToArray();foreach(var part in partners){p.AddToBackpack(part);}TalSet(p,items.Last(),partners);
            SkillHealth.SetBalance256(p,SkillHealth.Maximum256(p)-513);NecromancerEffects.SetMana256(p,37*256+17);ClassCombat.Stop(p);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(new Saved(0,p.Serial.Value,Staves.Select((s,i)=>new SavedItem(s.Key,items[i].Serial.Value,JsonSerializer.Serialize(ClassGear.State(items[i])))).ToArray(),CombatTiming.Equipped(p).Serial.Value,SkillHealth.Balance256(p),NecromancerEffects.ManaBalance256(p),partners.Select(i=>new SavedItem(ClassGear.State(i).NamedItemId,i.Serial.Value,JsonSerializer.Serialize(ClassGear.State(i)))).ToArray())));kept=true;
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();if(!kept){foreach(var i in items.Concat(partners)){i.Delete();}p.Delete();}}
    }
    private static void Reload(NetState network)
    {
        var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));Require(s.Generation<2,"Too many staff reloads");var p=(PlayerMobile)World.FindMobile((Serial)s.Owner);
        Require(CombatTiming.Equipped(p)?.Serial.Value==s.Equipped&&SkillHealth.Balance256(p)==s.Life&&NecromancerEffects.ManaBalance256(p)==s.Mana,"Original equipped staff/fractional resources changed");
        foreach(var saved in s.Items.Concat(s.Partners)){var item=World.FindItem((Serial)saved.Item);Require(item?.RootParent==p&&JsonSerializer.Serialize(ClassGear.State(item))==saved.Gear,"Original staff identity/rolls/container changed");}
        var account=FixtureNetwork.Authenticate(network,p,"sorc-staff-grants-reload-");
        try
        {
            foreach(var spec in Staves){Equip(p,World.FindItem((Serial)s.Items.Single(i=>i.Key==spec.Key).Item));Grants(p,spec);}
            TalSet(p,World.FindItem((Serial)s.Items.Last().Item),s.Partners.Select(i=>World.FindItem((Serial)i.Item)).ToArray());
            Equip(p,World.FindItem((Serial)s.Equipped));SkillHealth.SetBalance256(p,s.Life);NecromancerEffects.SetMana256(p,s.Mana);
            File.WriteAllText(Manifest,JsonSerializer.Serialize(s with{Generation=s.Generation+1}));
        }
        finally{ClassCombat.Stop(p);network.Mobile=null;p.NetState=null;account[0]=null;network.Account=null;account.Delete();}
    }
    private static void Clean(PlayerMobile p,Mobile target=null)
    {ClassCombat.Stop(p);SorceressSpellMissiles.Cancel(p);ChainLightning.Cancel(p);FireGround.Cancel(p);target?.Delete();p.Delete();}
    private static void Input(NetState network,InputCase c)
    {
        string label="sorc-staff-"+c.Key;var p=Owner();var spec=Staves.Single(s=>s.Key==c.Staff);var item=Make(p,spec);var target=FireBoltChecks.Target(p,c.Ability==CombatAbility.Blaze?0:2);target.SetHits(50000);target.Hits=target.HitsMax;
        var account=FixtureNetwork.Authenticate(network,p,"sorc-staff-wire-");ClassCombat.Stop(p);ClassCombat.For(p).NextAction=ClassCombat.For(p).RecoveryUntil=Core.TickCount;NecromancerEffects.SetMana256(p,200*256+17);
        Require(SkillBindings.Instance.Assign(p,false,c.Ability)&&SkillBindings.Instance.AssignQuick(p,7,c.Ability),"Untrained item spell binding");int hp=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(p),cost=SkillMechanics.Values(p,c.Ability).Mana256;
        bool began=false,detached=false,done=false,moved=false;long deadline=Core.TickCount+10000;
        void Cleanup(){Clean(p,target);account.Delete();}
        bool Captured()
        {
            if(c.Ability==CombatAbility.StaticField){return SkillHealth.Balance256(target)<hp;}
            if(c.Ability==CombatAbility.Blaze){return FireGround.Blaze(p)is{} b&&b.Rank==c.Rank;}
            if(c.Ability==CombatAbility.FireWall)
            {var state=FireGround.Snapshot();return state.Makers.Any(m=>m.Owner==p.Serial.Value&&m.Rank==c.Rank)&&state.Patches.Any(f=>f.Owner==p.Serial.Value&&f.Ability==c.Ability&&f.SkillRank==c.Rank);}
            if(c.Ability==CombatAbility.ChainLightning){return ChainLightning.Snapshot().Any(f=>f.Owner==p.Serial.Value&&f.Rank==c.Rank);}
            return SorceressSpellMissiles.Snapshot().Flights.Any(f=>f.Owner==p.Serial.Value&&f.Ability==c.Ability&&f.Rank==c.Rank);
        }
        void Inspect()
        {
            if(done){return;}
            try
            {
                Require(Core.TickCount<deadline,"Item spell timeout: "+c.Key+" pending="+ClassCombat.For(p).PendingAbility+" detached="+detached+" hp="+SkillHealth.Balance256(target));var s=ClassCombat.For(p);
                if(!began&&s.PendingAbility==c.Ability)
                {began=true;Log.Information("{Label}: windup",label);Require(SkillMechanics.Rank(p,c.Ability)==c.Rank&&NecromancerEffects.ManaBalance256(p)==mana&&SkillHealth.Balance256(target)==hp,"Item spell rank/unpaid windup: rank="+SkillMechanics.Rank(p,c.Ability)+"/"+c.Rank+" mana="+NecromancerEffects.ManaBalance256(p)+"/"+mana+" hp="+SkillHealth.Balance256(target)+"/"+hp);}
                if(!detached&&Captured())
                {
                    Require(began&&NecromancerEffects.ManaBalance256(p)==mana-cost,"Release did not use one mana payment");p.AddToBackpack(item);ClassGear.Refresh(p);Suppressed(p,spec);detached=true;
                }
                if(detached&&c.Ability==CombatAbility.Blaze&&!moved)
                {
                    Require(FireGround.Blaze(p)is{Rank:4},"Removing staff erased captured Blaze");p.Direction=Direction.East;
                    Require(p.Move(Direction.East),"Real movement for item Blaze");var patches=FireGround.Snapshot().Patches.Where(f=>f.Owner==p.Serial.Value&&f.Ability==CombatAbility.Blaze).ToArray();Require(patches.Length==2&&patches.All(f=>f.SkillRank==4),"Item Blaze lost captured rank/trail after removal");moved=true;
                }
                if(detached&&SkillHealth.Balance256(target)<hp)
                {
                    Require(NecromancerEffects.ManaBalance256(p)==mana-cost&&ClassCreation.Profiles.GetRank(p.Serial,SkillMechanics.For(c.Ability).TalentId)==0,"Released item spell replayed mana or granted hard points");
                    if(c.Ability==CombatAbility.StaticField){Require(hp-SkillHealth.Balance256(target)==hp/4,"Rank4 item Static did not apply ordinary quarter-life loss");}
                    done=true;Cleanup();Log.Information("{Label}-result: passed",label);return;
                }
                Timer.StartTimer(TimeSpan.FromMilliseconds(10),()=>Core.LoopContext.Post(Inspect));
            }
            catch(Exception e){done=true;Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(20),()=>Core.LoopContext.Post(()=>
        {
            var aim=target.Location;
            if(c.Ground){if(ClassAbilities.SelfTarget(c.Ability)){aim=p.Location;}Log.Information("{Label}: ready ability={Ability} class={Class} x={X} y={Y} z={Z}",label,(byte)c.Ability,(byte)DiabloClass.Sorceress,aim.X,aim.Y,aim.Z);}
            else{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(byte)c.Ability,(byte)DiabloClass.Sorceress,target.Serial.Value,aim.X,aim.Y,aim.Z);}Inspect();
        }));
    }
}
