// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Server;
using Server.Items;
using Server.Mobiles;
using Server.Network;
namespace Sanctuary.Integration;

public static class HellslayerChecks
{
    private static readonly Server.Logging.ILogger Log=Server.Logging.LogFactory.GetLogger(typeof(HellslayerChecks));
    private sealed record Saved(uint First,uint Second,uint Necro,uint PlayerItem,uint GolemItem,uint Body,Point3D FirstPoint,Point3D SecondPoint,Point3D NecroPoint,string PlayerGear,string GolemGear,string PlayerProfile,string Recipe,string Events,NpcEquipmentSeed Random,int[] Life,int[] Mana,int Generation);
    private static string Manifest=>Path.Join(Core.BaseDirectory,"hellslayer-fixtures.json");
    private static string Json<T>(T value)=>JsonSerializer.Serialize(value);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    public static void Configure()
    {
        foreach(bool again in new[]{false,true})
        {bool reload=again;string name=reload?"hellslayer-reload-check":"hellslayer-check";
            ConsoleInputHandler.RegisterCommand(name,"Complete original Hellslayer player and learned Iron Golem",_=>Core.LoopContext.Post(()=>
            {try{Check(reload);Log.Information("{Name}: passed",name);}catch(Exception e){Log.Error(e,"{Name}: failed",name);}}));
            string audit=reload?"hellslayer-readiness-reload-check":"hellslayer-readiness-check";
            ConsoleInputHandler.RegisterCommand(audit,"Whole catalogue exact Hellslayer admission delta",_=>Core.LoopContext.Post(()=>
            {try{Readiness();Log.Information("{Name}: passed",audit);}catch(Exception e){Log.Error(e,"{Name}: failed",audit);}}));}
        foreach(string mode in new[]{"player-left","player-right","golem-right","golem-quick"})
        {string label="hellslayer-"+mode;ConsoleInputHandler.RegisterCommand(label+"-check","Complete original named equipment/item-target input",_=>Core.LoopContext.Post(()=>
            {try{FixtureNetwork.Connected(label+"-result",n=>{if(mode.StartsWith("golem",StringComparison.Ordinal)){IronGolemCastChecks.Input(n,mode.EndsWith("quick",StringComparison.Ordinal),label,Original,(p,b,item)=>{Canonical(item);Require(NpcItemProcs.Get(b)?.Capture().Items.Single().Item==item.Serial.Value,"Learned input did not automatically enroll its original");GolemAttack(p,b);});}else{Input(n,label);}});Log.Information("{Label}-check: passed",label);}catch(Exception e){Log.Error(e,"{Label}-check: failed",label);}}));}
    }
    internal static PlayerMobile Owner(DiabloClass type,int row)
    {
        var p=new PlayerMobile{Body=0x190,Player=true,Name="PlayerItemFireBallFixture"};p.AddItem(new Backpack());ClassCreation.Profiles.Assign(p,type);ClassCreation.Profiles.AdvanceLevel(p,99);
        foreach(var stat in new[]{CharacterStat.Strength,CharacterStat.Dexterity,CharacterStat.Energy})
        {int goal=stat==CharacterStat.Strength?220:100;while((stat==CharacterStat.Strength?p.RawStr:stat==CharacterStat.Dexterity?p.RawDex:p.RawInt)<goal){Require(ClassCreation.Profiles.TrySpendStat(p,stat),"Allocate complete-original requirements");}}
        if(type==DiabloClass.Necromancer){ClassSkillChecks.Train(p,"necromancer.irongolem");Require(SkillBindings.Instance.Assign(p,false,CombatAbility.IronGolem)&&SkillBindings.Instance.AssignQuick(p,7,CombatAbility.IronGolem),"Learned original item bindings");}
        FixtureSpace.Corridor(p,6240,30,row);NecromancerEffects.SetMana256(p,p.ManaMax*256);return p;
    }
    internal static Item Original(PlayerMobile p)
    {
        var item=NamedGear.Generate("7bt",GearRarity.Unique,99,new Random(173),true);Require(item!=null,"Normal unique selection did not acquire Hellslayer");Canonical(item);
        item.MoveToWorld(new(p.X+2,p.Y,p.Z),p.Map);return item;
    }
    private static void Canonical(Item item)
    {
        var s=ClassGear.State(item);Require(s is{NamedItemId:"unique/254",BaseCode:"7bt",Identified:true,NamedProperties.Length:7}&&item.Origin==ItemOrigin.Drop&&s.Roll.ItemLevel==99&&s.Roll.Rarity==GearRarity.Unique,"Complete original identity/origin/level/ledger changed");
        NamedGear.Validate(item,s);var captured=OriginalItemProcProjection.Capture(item);
        var expected=IronGolemBodyRecipes.Fields((7,6400),(17,100),(18,100),(48,150),(49,250),(219,24),(220,4),(223,4)).Append(new(EquipmentStatKey.Pack(195,(47<<6)|20),10)).OrderBy(v=>v.Packed).ToArray();
        Require(captured.Full.Properties.SequenceEqual(expected)&&captured.Events.Properties.SequenceEqual(expected.Where(v=>v.Stat==195)),"Whole original lost one of its nine numeric/event fields");
        var context=new ItemStatContext(item,99);var elements=context.Elements();
        Require(context.Bonus("str")==49&&context.Bonus("vit")==49&&context.Bonus("hp")==25&&context.Bonus("dmg%")==100&&context.Bonus("dmg-max%")==297
            &&elements.FireMin==150*256&&elements.FireMax==250*256&&context.WeaponDamage.Min>0&&context.WeaponDamage.Max>context.WeaponDamage.Min,"Whole original live numeric suppliers differ: "+Json(new{Str=context.Bonus("str"),Vit=context.Bonus("vit"),Hp=context.Bonus("hp"),Damage=context.Bonus("dmg%"),Maximum=context.Bonus("dmg-max%"),elements.FireMin,elements.FireMax,Weapon=context.WeaponDamage}));
        var prop=NamedGear.Catalog.Item("unique/254").Properties.Single(p=>p.Code=="att-skill");
        Require(NamedGear.PropertyText(prop,s.NamedProperties.Single(r=>r.Key=="p/7"),1,s.Roll.ItemLevel)=="10% Chance to Cast Level 20 Fire Ball on Attack","Proc description used recipient level or lost source rank/chance");
    }
    private static PlayerProcSave Own(PlayerMobile p,PlayerMobile q,Item item)
    {var all=PlayerItemProcs.Capture();return all with{Items=all.Items.Where(i=>i.Item==item.Serial.Value).ToArray(),Owners=all.Owners.Where(o=>o.Actor==p.Serial.Value||o.Actor==q.Serial.Value).ToArray()};}
    internal static IronGolemFireBallChecks.Recipient Target(Mobile from,int dx=1)
    {var t=new IronGolemFireBallChecks.Recipient();t.SetHits(60000);t.MoveToWorld(new(from.X+dx,from.Y,from.Z),from.Map);t.AIObject.AITimer.Stop();foreach(var r in Enum.GetValues<ResistanceType>()){t.SetResistance(r,0);}TimedEffects.PreventHealing(t,TimedEffects.Now);return t;}
    private static SorceressSpellFlight[] Flights(uint actor,bool npc)=>SorceressSpellMissiles.Snapshot().Flights.Where(f=>npc?f.Npc?.Actor==actor:f.Owner==actor&&f.PlayerItemClass!=null).ToArray();
    internal static void Finish(PlayerMobile p,Mobile actual,IronGolemFireBallChecks.Recipient target,SorceressSpellFlight f)
    {
        int before=SkillHealth.Balance256(target),mana=NecromancerEffects.ManaBalance256(p);Require(f.Rank==20&&f.Minimum256==51072&&f.Maximum256==57984,"Complete-original proc rank/damage borrowed weapon elements or owner skills");
        for(int i=0;i<80&&FireBoltChecks.State(f.Id)is{} live;i++){SorceressSpellMissiles.Tick(live.NextUtc,true,live.Id);}
        Require(FireBoltChecks.State(f.Id)==null&&SkillHealth.Balance256(target)<before&&target.Source==actual.Serial.Value&&NecromancerEffects.ManaBalance256(p)==mana,"Complete original spell failed native damage,ownership or free resources");
    }
    internal static void PlayerAttack(PlayerMobile p,Item item)
    {
        var t=Target(p);var hit=PlayerBasicContact.HitRoll;var element=PlayerBasicContact.ElementRoll;var block=SourceBlocking.BlockRoll;var observed=PlayerItemProcWorld.Observed;
        try
        {
            PlayerBasicContact.HitRoll=()=>0;PlayerBasicContact.ElementRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;
            using(PlayerItemProcWorld.Candidate(p))
            {
                Require(PlayerItemProcs.Provider(item.Serial.Value)?.State.Properties.Single().Value==10,"Whole original did not enroll automatically or changed its real chance");
                for(int i=0;i<1000;i++)
                {
                    SkillHealth.SetBalance256(t,t.HitsMax*256);var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
                    ulong expectedWorld=NpcWorldAllocation.RequireStream().Value;var actor=PlayerItemProcs.Capture().Owners.SingleOrDefault(o=>o.Actor==p.Serial.Value);
                    ulong expectedActor=actor?.Seed??SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref expectedWorld)));bool success=SourceRandom.Roll(ref expectedActor)%100<10;if(success){SourceRandom.Roll(ref expectedWorld);}
                    int mana=NecromancerEffects.ManaBalance256(p);ClassCombat.Stop(p);var s=ClassCombat.For(p);s.NextAction=s.RecoveryUntil=Core.TickCount;
                    Require(ClassCombat.Perform(p,CombatAbility.BasicAttack,t,t.Location,Core.TickCount)&&ClassCombat.ResolveAction(p,s.EffectAt),"Complete-original basic action failed");
                    Require(visits.Count==2&&visits[0].Phase=="domeleedamage"&&visits[0].Packet.Fire256==150*256&&visits[0].Outcomes.Length==0
                        &&visits[1].Outcomes.Single().CastAttempts==(success?1:0)&&PlayerItemProcs.Capture().Owners.Single(o=>o.Actor==p.Serial.Value).Seed==expectedActor
                        &&NpcWorldAllocation.Capture().Seed==expectedWorld&&NecromancerEffects.ManaBalance256(p)==mana,"Complete item changed event phase/chance/separate streams/weapon fire or mana");
                    var flights=Flights(p.Serial.Value,false);Require(flights.Length==(success?1:0),"Complete source chance and actual allocation differ");
                    if(success){Finish(p,p,t,flights.Single());return;}
                }
                throw new InvalidOperationException("No successful proc in1000real source draws");
            }
        }
        finally{ClassCombat.Stop(p);PlayerBasicContact.HitRoll=hit;PlayerBasicContact.ElementRoll=element;SourceBlocking.BlockRoll=block;PlayerItemProcWorld.Observed=observed;SorceressSpellMissiles.Cancel(p);t.Delete();}
    }
    private static void GolemAttack(PlayerMobile p,IronGolemBody body)
    {
        var original=IronGolemItems.Equipment(body,CreatureProfiles.Level(body)).Item;Canonical(original);
        var graph=NpcItemProcs.Get(body);Require(graph!=null&&graph.Capture().Items.Single().Item==original.Serial.Value,"Original golem graph was not enrolled by birth");
        string events=Json(graph.Capture());var random=NpcEquipmentRandom.State(body);Require(NpcItemProcs.RegisterOriginal(body)&&Json(graph.Capture())==events&&NpcEquipmentRandom.State(body)==random,"Repeated original enrollment reset RNG or duplicated chance");
        var t=Target(body);int mana=NecromancerEffects.ManaBalance256(p);
        try
        {
            for(int i=0;i<20000;i++)
            {
                if(SkillHealth.Balance256(t)<t.HitsMax*128){SkillHealth.SetBalance256(t,t.HitsMax*256);}
                var state=body.Prepared.Controller;Require(state.Fault==null,"Whole-original golem AI refused: "+state.Fault);
                IronGolemWorld.Advance(body,state.NextUtc,true);var flights=Flights(body.Serial.Value,true);
                if(flights.Length>0){Require(flights.Length==1,"Original on-attack proc duplicated its emission");Finish(p,body,t,flights.Single());Require(NecromancerEffects.ManaBalance256(p)==mana,"Golem proc spent owner mana");return;}
            }
            throw new InvalidOperationException("No original golem AI proc within20000source updates;mode="+body.Prepared.Controller.Mode+" fault="+body.Prepared.Controller.Fault);
        }
        finally{SorceressSpellMissiles.Cancel(body);t.Delete();}
    }
    private static void Check(bool reload)
    {
        Require(NamedGear.Available("unique/254"),"Complete-original proc return is missing");PlayerMobile p,q,n;Item playerItem,golemItem;IronGolemBody body;int generation=0;
        if(!reload)
        {
            p=Owner(DiabloClass.Paladin,3400);q=Owner(DiabloClass.Barbarian,3450);n=Owner(DiabloClass.Necromancer,3500);
            playerItem=Original(p);int strength=p.Str,hits=p.HitsMax;p.AddToBackpack(playerItem);Require(p.EquipItem(playerItem),"Equip complete acquired original");ClassGear.Refresh(p);
            Require(p.Str==strength+49&&p.HitsMax>hits&&!PlayerItemProcs.Initialized(p)&&PlayerItemProcs.Provider(playerItem.Serial.Value)!=null,"Full item numeric activation or automatic provider enrollment failed");
            var gear=ClassGear.State(playerItem);ClassGear.Profiles.Store(playerItem,gear with{Identified=false});ClassGear.Refresh(p);Require(p.Str==strength,"Unidentified complete item retained attributes");ClassGear.Profiles.Store(playerItem,gear);ClassGear.Refresh(p);
            PlayerAttack(p,playerItem);var fields=Json(PlayerItemProcs.Provider(playerItem.Serial.Value));p.AddToBackpack(playerItem);q.AddToBackpack(playerItem);Require(q.EquipItem(playerItem),"Trade same complete original to second class");ClassGear.Refresh(p);ClassGear.Refresh(q);Require(p.Str==strength&&Json(PlayerItemProcs.Provider(playerItem.Serial.Value))==fields,"Trade retained stats or rerolled original fields");PlayerAttack(q,playerItem);
            golemItem=Original(n);ClassCombat.Stop(n);ClassCombat.For(n).NextAction=0;int mana=NecromancerEffects.ManaBalance256(n);
            Require(SkillBindingNetwork.Use(n,DiabloClass.Necromancer,7,CombatAbility.IronGolem,golemItem.Serial,golemItem.Location),"Learned complete-original golem failed to start");
            Require(golemItem.Parent==null&&NecromancerEffects.ManaBalance256(n)==mana,"Golem windup consumed original or mana");Require(ClassCombat.ResolveAction(n,ClassCombat.For(n).EffectAt),"Learned complete-original golem failed release");
            body=NecromancerGolems.For(n).OfType<IronGolemBody>().Single();Require(golemItem.Parent==body&&NecromancerEffects.ManaBalance256(n)==mana-SkillMechanics.Values(n,CombatAbility.IronGolem).Mana256,"Learned golem lost original/payment");
        }
        else
        {
            var s=JsonSerializer.Deserialize<Saved>(File.ReadAllText(Manifest));generation=s.Generation+1;Require(generation<=2,"Extra complete-original reload");p=(PlayerMobile)World.FindMobile((Serial)s.First);q=(PlayerMobile)World.FindMobile((Serial)s.Second);n=(PlayerMobile)World.FindMobile((Serial)s.Necro);playerItem=World.FindItem((Serial)s.PlayerItem);golemItem=World.FindItem((Serial)s.GolemItem);body=(IronGolemBody)World.FindMobile((Serial)s.Body);
            Require(playerItem.Parent==q&&golemItem.Parent==body&&Json(ClassGear.State(playerItem))==s.PlayerGear&&Json(ClassGear.State(golemItem))==s.GolemGear&&Json(Own(p,q,playerItem))==s.PlayerProfile
                &&Json(body.Prepared.Recipe)==s.Recipe&&Json(NpcItemProcs.Get(body).Capture())==s.Events&&NpcEquipmentRandom.State(body)==s.Random
                &&new Mobile[]{p,q,n,body}.Select(SkillHealth.Balance256).SequenceEqual(s.Life)&&new[]{p,q,n}.Select(NecromancerEffects.ManaBalance256).SequenceEqual(s.Mana),"Complete originals,recipe,streams,wounds or payment changed after reload");
            p.MoveToWorld(s.FirstPoint,Map.Trammel);q.MoveToWorld(s.SecondPoint,Map.Trammel);n.MoveToWorld(s.NecroPoint,Map.Trammel);PersistentMinions.Resume(n);PlayerAttack(q,playerItem);
        }
        Canonical(playerItem);Canonical(golemItem);GolemAttack(n,body);SkillHealth.SetBalance256(body,SkillHealth.Balance256(body)-257);TimedEffects.PreventHealing(body,TimedEffects.Now);
        var pp=p.Location;var qp=q.Location;var np=n.Location;PersistentMinions.Suspend(n);p.Internalize();q.Internalize();n.Internalize();
        File.WriteAllText(Manifest,Json(new Saved(p.Serial.Value,q.Serial.Value,n.Serial.Value,playerItem.Serial.Value,golemItem.Serial.Value,body.Serial.Value,pp,qp,np,Json(ClassGear.State(playerItem)),Json(ClassGear.State(golemItem)),Json(Own(p,q,playerItem)),Json(body.Prepared.Recipe),Json(NpcItemProcs.Get(body).Capture()),NpcEquipmentRandom.State(body),new Mobile[]{p,q,n,body}.Select(SkillHealth.Balance256).ToArray(),new[]{p,q,n}.Select(NecromancerEffects.ManaBalance256).ToArray(),generation)));
    }
    private static void Input(NetState network,string label)
    {
        var p=Owner(DiabloClass.Paladin,3600);var item=Original(p);p.AddToBackpack(item);Require(p.EquipItem(item),"Wire equip complete original");ClassGear.Refresh(p);Require(SkillBindings.Instance.Assign(p,true,CombatAbility.BasicAttack)&&SkillBindings.Instance.Assign(p,false,CombatAbility.BasicAttack),"Wire basic bindings");var account=FixtureNetwork.Authenticate(network,p,"hellslayer-input-");var t=Target(p);
        var hit=PlayerBasicContact.HitRoll;var element=PlayerBasicContact.ElementRoll;var block=SourceBlocking.BlockRoll;var observed=PlayerItemProcWorld.Observed;PlayerBasicContact.HitRoll=()=>0;PlayerBasicContact.ElementRoll=()=>0;SourceBlocking.BlockRoll=n=>n-1;
        ulong expectedWorld=NpcWorldAllocation.RequireStream().Value;ulong expectedActor=SourceRandom.InitLow(unchecked((int)SourceRandom.Roll(ref expectedWorld)));bool success=SourceRandom.Roll(ref expectedActor)%100<10;if(success){SourceRandom.Roll(ref expectedWorld);}int mana=NecromancerEffects.ManaBalance256(p);bool began=false;long deadline=Core.TickCount+5000;var visits=new List<PlayerProcWorldVisit>();PlayerItemProcWorld.Observed=v=>{if(v.Owner==p.Serial.Value){visits.Add(v);}};
        void Cleanup(){ClassCombat.Stop(p);PlayerBasicContact.HitRoll=hit;PlayerBasicContact.ElementRoll=element;SourceBlocking.BlockRoll=block;PlayerItemProcWorld.Observed=observed;SorceressSpellMissiles.Cancel(p);t.Delete();item.Delete();p.Delete();account.Delete();}
        void Inspect()
        {
            try
            {
                Require(Core.TickCount<deadline,"Whole original input timeout");var state=ClassCombat.For(p);
                if(!began&&state.PendingAbility==CombatAbility.BasicAttack){Require(!PlayerItemProcs.Initialized(p)&&visits.Count==0&&NecromancerEffects.ManaBalance256(p)==mana,"Whole original windup initialized/spent resources");began=true;Log.Information("{Label}: windup",label);}
                if(visits.Count<2){Timer.StartTimer(TimeSpan.FromMilliseconds(10),Inspect);return;}
                Require(began&&visits[0].Outcomes.Length==0&&visits[1].Outcomes.Single().CastAttempts==(success?1:0)&&PlayerItemProcs.Capture().Owners.Single(o=>o.Actor==p.Serial.Value).Seed==expectedActor&&NpcWorldAllocation.Capture().Seed==expectedWorld&&NecromancerEffects.ManaBalance256(p)==mana&&state.PendingTarget==t.Serial,"Whole original wire changed real chance,stream or target/mana");
                ClassCombat.Stop(p);var flights=Flights(p.Serial.Value,false);Require(flights.Length==(success?1:0),"Whole original wire allocation differs from real chance");if(success){Finish(p,p,t,flights.Single());}Cleanup();Log.Information("{Label}-result: passed",label);
            }
            catch(Exception e){Cleanup();Log.Error(e,"{Label}-result: failed",label);}
        }
        Timer.StartTimer(TimeSpan.FromMilliseconds(30),()=>{Log.Information("{Label}: ready ability={Ability} class={Class} target={Target} x={X} y={Y} z={Z}",label,(int)CombatAbility.BasicAttack,(int)DiabloClass.Paladin,t.Serial.Value,t.X,t.Y,t.Z);Inspect();});
    }
    private static void Readiness()
    {
        using var before=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"hellslayer-before-readiness.json")));var old=before.RootElement.GetProperty("Definitions").EnumerateArray().ToDictionary(x=>x.GetProperty("Id").GetString());
        var report=NamedGear.Catalog.Items.Select(d=>new{d.Id,d.Name,d.Rarity,d.BaseCode,d.SetId,d.SourceAvailability,Available=NamedGear.Available(d.Id),Reasons=NamedGear.Unavailable.GetValueOrDefault(d.Id)??[]}).ToArray();
        var changed=report.Where(r=>r.Available!=old[r.Id].GetProperty("Available").GetBoolean()).Select(r=>r.Id).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
        // The immutable audit predates the11accepted direct-grant returns.
        // Their source/readiness proof is retained in the parent checkpoints.
        var prior=new[]{"unique/186","unique/56","unique/57","unique/58","unique/54","unique/185","piece/76","piece/77","piece/78","piece/79","piece/80"};
        Require(report.Length==573&&changed.SequenceEqual(prior.Append("unique/254").OrderBy(x=>x,StringComparer.Ordinal))
            &&changed.All(id=>!old[id].GetProperty("Available").GetBoolean()&&NamedGear.Available(id)),"Unexpected catalogue delta from immutable audit: "+string.Join(',',changed));
        string root=Environment.GetEnvironmentVariable("SANCTUARY_PROJECT_ROOT");string Hash(string path)=>Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        var inputs=new[]{"src/Sanctuary.Content","shared","data/Sanctuary/Balance"}.SelectMany(dir=>Directory.GetFiles(Path.Join(root,dir),"*",SearchOption.TopDirectoryOnly)).Where(p=>p.EndsWith(".cs",StringComparison.Ordinal)||p.EndsWith(".json",StringComparison.Ordinal))
            .Concat(Directory.GetFiles(Path.Join(root,"patches/ModernUO"),"*.patch")).Concat(Directory.GetFiles(Path.Join(root,"patches/ClassicUO"),"*.patch"))
            .Concat(new[]{"NamedGearChecks.cs","NamedLifecycleChecks.cs","NamedVisualUiChecks.cs"}.Select(n=>Path.Join(root,"tests/Sanctuary.Integration",n))).OrderBy(p=>p,StringComparer.Ordinal).ToDictionary(p=>Path.GetRelativePath(root,p).Replace('\\','/'),Hash);
        File.WriteAllText(Path.Join(Core.BaseDirectory,"hellslayer-readiness-fixtures.json"),Json(new{SchemaVersion=1,ContentAssemblySha256=Hash(typeof(NamedGear).Assembly.Location),NativeAssemblySha256=Hash(typeof(BaseWeapon).Assembly.Location),InputHashes=inputs,Definitions=report}));
    }
}
