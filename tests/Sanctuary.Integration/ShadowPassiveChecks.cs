// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Server;
using Server.Mobiles;
namespace Sanctuary.Integration;

internal static class ShadowPassiveChecks
{
    private static string Json<T>(T x)=>JsonSerializer.Serialize(x);
    private static void Require(bool ok,string why){if(!ok){throw new InvalidOperationException(why);}}
    private static void Reject(Action run)
    {try{run();}catch(Exception e)when(e is InvalidDataException or NotSupportedException or InvalidOperationException or ArgumentException){return;}throw new Exception("Unsupported passive cache accepted");}
    private static ShadowSkillBook Book(int id,int rank,bool own=true)
    {var entries=new[]{new ShadowOwnedSkill(0,1,0,4)}.Concat(own?new[]{new ShadowOwnedSkill(id,rank,0,7)}:[]).ToArray();return ShadowSkillBook.Restore(Json(new ShadowSkillBookState(entries)));}
    private static GeneratedNpcStatGraph Graph(AssassinShadowBody b)=>new(b,[],_=>null,(_,_,_,_,_)=>{},false,_=>{},_=>{},activeOrder:[]);
    internal static void SourceControls()
    {
        var actor=World.Mobiles.Values.OfType<AssassinShadowBody>().First(b=>b.Alive&&!b.Deleted);var world=NpcWorldAllocation.Capture();var rng=NpcEquipmentRandom.Require(actor);
        using var doc=JsonDocument.Parse(File.ReadAllText(Path.Join(Core.BaseDirectory,"shadow-passive-source-cases.json")));int count=0;
        foreach(var c in doc.RootElement.EnumerateArray())
        {
            int id=c[0].GetInt32(),rank=c[1].GetInt32(),cached=c[2].GetInt32();bool own=c[3].GetInt32()!=0;
            var graph=Graph(actor);var first=Book(id,Math.Max(0,cached),cached>=0);
            graph.Passives=ShadowPassiveGraph.Initialize(graph,first.Save(),()=>0);var manager=graph.Passives;
            var before=graph.SourceListOrder().FirstOrDefault();int writes=manager.Writes,gfx=manager.GraphicsNotifications;
            manager.Refresh(Book(id,rank,own).Save());var after=graph.SourceListOrder().FirstOrDefault();
            Require((before==null&&after!=null?1:0)==c[4].GetInt32()&&(before!=null&&after==null?1:0)==c[5].GetInt32(),"Whole source passive allocation/removal differs");
            Require(manager.GraphicsNotifications-gfx==c[6].GetInt32()&&manager.Writes-writes==c[7].GetInt32(),"Whole source cached-level rewrite/gfx behavior differs");
            var actual=manager.Capture().Lists.SelectMany(l=>l.Values).Select(v=>new[]{(int)v.Stat,(int)(v.Packed&65535),v.Value});
            Require(Json(actual)==c[8].GetRawText(),"Whole source passive stat/layer/value order differs");
            Require(manager.State(id==252?152:155)==own,"Intrinsic passive state differs from owned entry presence");count++;
        }
        Require(count==1600,"Missing complete source passive cases");
        var g=Graph(actor);var book=Book(252,10);book.Assign(263,22);g.Passives=ShadowPassiveGraph.Initialize(g,book.Save(),()=>0);
        Require(g.Read(342,68)==120&&g.Read(343,68)==71&&g.Read(348,68)==58&&g.Read(342)==0&&g.Read(348)==0,"Current claw layers/Weapon Block22tooltip or actual posted graph values");
        int old=g.Read(342,68),w=g.Passives.Writes;g.Values.SetBase(g.Actor.Stats,EquipmentStatKey.Pack(107,252),5,g.Actor);
        Require(book.Rank(252,g.Read)==15&&g.Read(342,68)==old&&g.Passives.Writes==w,"NPC single-skill callback refreshed a passive immediately");
        g.Values.SetBase(g.Actor.Stats,EquipmentStatKey.Pack(127),1,g.Actor);
        Require(g.Read(342,68)==180&&g.Passives.GraphicsNotifications>2,"All-skills callback did not flush changed effective passive ranks");
        w=g.Passives.Writes;g.Values.SetBase(g.Actor.Stats,EquipmentStatKey.Pack(126,1),1,g.Actor);Require(g.Passives.Writes==w,"Unchanged passive rank rewrote cached lists");
        var frame=g.Passives.Capture();var clone=ShadowPassiveGraph.Copy(frame);clone.Lists[0].Values[0]=new(1,999);Require(Json(g.Passives.Capture())==Json(frame),"Passive frame exposed live cached arrays");
        Reject(()=>ShadowPassiveGraph.Validate(clone,[]));Reject(()=>ShadowPassiveGraph.Validate(frame with{Order=[new(0,152),new(0,152)]},[]));
        Reject(()=>g.RegisterPassiveList(new(){OwnerKind=1,State=152}));
        var unknown=new EquipmentStatNode{OwnerKind=1,State=152};EquipmentStatGraph.Post(g.Actor,unknown,true,g.Values);Reject(()=>{_=g.ActiveItems;});EquipmentStatGraph.Detach(unknown,g.Values);
        Require(NpcWorldAllocation.Capture()==world&&NpcEquipmentRandom.Require(actor)==rng,"Passive source controls consumed world/actor RNG");
    }
    private static PlayerMobile Supplier()
    {
        var existing=World.Mobiles.Values.OfType<PlayerMobile>().FirstOrDefault(p=>p.Name=="ShadowPassiveCommandSupplier");if(existing!=null){return existing;}
        var p=new PlayerMobile{Name="ShadowPassiveCommandSupplier",Body=0x190,Player=true};ClassCreation.Profiles.Assign(p,DiabloClass.Barbarian);ClassCreation.Profiles.AdvanceLevel(p,99);
        ClassSkillChecks.Train(p,SkillMechanics.For(CombatAbility.BattleCommand).TalentId,20);p.Internalize();return p;
    }
    internal static void Body(AssassinShadowBody b)
    {
        string original=b.Prepared.Skills;var state=GeneratedNpcLiveResources.State(b);Require(state.Passives!=null,"Fresh Shadow did not own source passive state");
        var world=NpcWorldAllocation.Capture();var random=NpcEquipmentRandom.Require(b);var items=ShadowLoadout.Originals(b);var payloads=items.Select(i=>i.SavedText).ToArray();
        b.ChangeSkills(book=>{book.Assign(252,10);book.Assign(263,22);});var graph=b.EquipmentNumbers;
        int Rank(int id)=>b.OwnedSkills.Rank(id,b.EquipmentNumbers.Read,BattleCommand.Bonus(b));
        int expected=SkillMechanics.For(CombatAbility.ClawMastery).Parameter(1)+10*(Rank(252)-1);
        Require(graph.Read(342,68)==expected&&graph.Passives.State(152)&&graph.Passives.State(155),"Actual NPC passive did not reach the original source graph");
        b.ChangeSkills(book=>{book.Assign(252,0);book.SetBonus(252,-1000);});Require(!b.EquipmentNumbers.Passives.Capture().Lists.Any(l=>l.Skill==252)&&b.EquipmentNumbers.Passives.State(152),"Base-zero intrinsic passive state/list behavior");
        b.ChangeSkills(book=>{book.Assign(252,10);book.SetBonus(252,0);});
        SkillHealth.SetBalance256(b,SkillHealth.Maximum256(b)*2/3-1);int life=SkillHealth.Balance256(b);var supplier=Supplier();
        int before=Rank(252);BattleCommand.Apply(supplier,b,20,TimedEffects.Now.AddHours(1));
        Require(BattleCommand.Bonus(b)==1&&Rank(252)==before+1&&b.EquipmentNumbers.Passives.Capture().Lists.Single(l=>l.Skill==252).Level==Rank(252),"Battle Command application skipped real passive refresh");
        BattleCommand.Clear(b);Require(Rank(252)==before&&b.EquipmentNumbers.Passives.Capture().Lists.Single(l=>l.Skill==252).Level==before&&SkillHealth.Balance256(b)==life,"Command removal refilled life or left passive rank stale");
        int maximum=SkillHealth.Maximum256(b);GeneratedNpcLiveResources.SetItemActive(b,items[0].Serial.Value,false);
        int nextMaximum=SkillHealth.Maximum256(b);life=BattleOrders.Rescale256(life,maximum,nextMaximum);
        Require(SkillHealth.Balance256(b)==life,"Passive item removal lost exact proportional FP256 wounds");
        GeneratedNpcLiveResources.SetItemActive(b,items[0].Serial.Value,true);
        life=BattleOrders.Rescale256(life,nextMaximum,SkillHealth.Maximum256(b));
        Require(SkillHealth.Balance256(b)==life,"Passive item reattachment lost exact proportional FP256 wounds");
        var order=GeneratedNpcLiveResources.State(b).Passives.Order;Require(order.Last().Item==items[0].Serial.Value,"Reattached original item lost order after passive lists");
        b.ChangeSkills(book=>
        {
            var desired=ShadowSkillBook.Restore(original).Snapshot();foreach(var current in book.Snapshot().Entries.Where(s=>!desired.Entries.Any(d=>d.Id==s.Id)).ToArray()){book.Assign(current.Id,0,true);}
            foreach(var s in desired.Entries){book.Assign(s.Id,s.Base);book.SetBonus(s.Id,s.Bonus);book.SetMode(s.Id,s.Mode);}book.Select(desired.Left.Value,false);book.Select(desired.Right.Value,true);book.Use(desired.Used);
        });
        Require(b.Prepared.Skills==original,"Passive tests changed original owned skill order/selection");
        BattleCommand.Apply(supplier,b,20,TimedEffects.Now.AddHours(1));
        var snapshot=GeneratedNpcLiveResources.Snapshot();var saved=snapshot.Single(s=>s.Actor==b.Serial.Value);string valid=Json(saved);
        var bad=ShadowPassiveGraph.Copy(saved.Passives);
        if(bad.Lists.Length>0){bad.Lists[0].Values[0]=new(bad.Lists[0].Values[0].Packed,9999);}
        else{bad=bad with{Order=[new(0,152)]};}
        Reject(()=>GeneratedNpcLiveResources.Restore(snapshot.Select(s=>s.Actor==b.Serial.Value?s with{Passives=bad}:s).ToArray()));
        Require(Json(GeneratedNpcLiveResources.State(b))==valid,"Malformed passive restoration partially committed");
        Require(NpcWorldAllocation.Capture()==world,"Passive transitions consumed world RNG");
        Require(NpcEquipmentRandom.Require(b)==random,"Passive transitions consumed actor RNG");
        Require(items.Select(i=>i.SavedText).SequenceEqual(payloads),"Passive transitions changed original item payloads");
        Require(SkillHealth.Balance256(b)==life,"Passive transitions refilled or reset proportional FP256 wounds");
    }
}
