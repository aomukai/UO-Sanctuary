// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.Linq;
using Server.Mobiles;
namespace Sanctuary;

// Numeric attack/defense/resistance consumers only. Every original item and
// raw layer is replayed; retained resource,skill,event and timing fields are
// not permission to execute those separate consumers or the learned cast.
internal static class ValkyrieEquipmentStats
{
    internal const string Profile="valkyrie-equipment-numeric-v1";
    private static readonly Lazy<Dictionary<string,int>> Names=new(()=>Enumerable.Range(0,368)
        .Select(EquipmentStatValues.Definition).ToDictionary(d=>d.Name,d=>d.Id,StringComparer.Ordinal));
    private static int Id(string name)=>Names.Value.TryGetValue(name,out int id)?id
        :throw new NotSupportedException("Unknown captured Valkyrie source stat: "+name);
    internal static EquipmentStatValue[] Basis(AmazonValkyrieBody body,MonsterDifficulty difficulty)
    {
        var n=ValkyrieBirthNumbers.Calculate(body.Prepared.Birth,difficulty);
        var fields=new Dictionary<int,int>();
        void Add(string name,int value){int id=Id(name);fields[id]=checked(fields.GetValueOrDefault(id)+value);}
        Add("level",n.Level);Add("maxhp",n.Life256);Add("armorclass",n.Defense);Add("tohit",n.Attack);
        foreach(var field in new[]{("damageresist","ResDm"),("magicresist","ResMa"),("fireresist","ResFi"),
            ("lightresist","ResLi"),("coldresist","ResCo"),("poisonresist","ResPo")})
        {Add(field.Item1,NecromancerSummons.Source("valkyrie",field.Item2,difficulty));}
        foreach(var field in n.Auras.Concat(n.Passives)){Add(field.Name,field.Value);}
        // No source mana/stamina/rate/current-pool bases are inferred from UO
        // attributes. Those consumers require their own creation context.
        return fields.Where(p=>p.Value!=0).Select(p=>new EquipmentStatValue(EquipmentStatKey.Pack(p.Key),p.Value)).OrderBy(v=>v.Packed).ToArray();
    }
    internal static GeneratedNpcStatGraph Build(AmazonValkyrieBody body,MonsterDifficulty difficulty)
    {
        _=ValkyrieEquipment.Originals(body);
        return new(body,Basis(body,difficulty),_=>null,(_,_,_,_,_)=>{},false,_=>{},_=>{},null,
            body.Prepared.Equipment.Attempts.Where(a=>a.Success).Select(a=>a.Item).ToArray());
    }
    internal static void Activate(AmazonValkyrieBody body)
    {
        if(body?.Prepared is not{Equipment:not null,EquipmentStatProfile:null} state)
        {throw new NotSupportedException("Valkyrie numeric delivery needs its original unbound generated loadout");}
        // Build/validate the complete unfiltered frame before changing any
        // saved admission. Unknown time contexts refuse without partial state.
        _=Build(body,state.Difficulty);
        body.RestorePrepared(state with{EquipmentStatProfile=Profile});
        body.ApplyEquipmentResistances();
    }
    internal static int? AttackPercent(BaseCreature actor)=>actor is AmazonValkyrieBody body&&body.EquipmentNumbers is{} graph?graph.Read(119):null;
}
