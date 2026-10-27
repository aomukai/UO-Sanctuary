#!/usr/bin/env python3
"""Pin current Fire Claws and review complete older routines; no execution claim."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def build(check=False):
    forms_path=ROOT/'tools/reference/druid-forms-preflight.json'
    forms=json.loads(forms_path.read_text())
    manifest=json.loads((ROOT/'tools/reference/d2r-named-equipment/sources.json').read_text())
    assert manifest['mechanicsReferenceVersion']==forms['referenceVersion']=='3.3.93847'
    rows,pins,compared={},{},0
    for table,key,names in [('skills','skill',['Fire Claws']),('states','state',['wolf','bear'])]:
        pin=forms['localTables'][table]
        local=ROOT/'.local/research/d2r-3.3.93847'/pin['filename'];assert sha(local)==pin['sha256']
        current=list(csv.DictReader(local.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t'))
        public=ROOT/'tools/reference/d2r-named-equipment/json'/(table+'.json')
        assert sha(public)==manifest['files']['json/'+table+'.json']['sha256'];cached=json.loads(public.read_text())
        rows[table]={}
        for name in names:
            row=next(x for x in current if x.get(key)==name);other=next(x for x in cached.values() if x.get(key)==name)
            for field,value in other.items():
                if field not in row or field=='lineNumber':continue
                text=str(value)
                if table=='skills' and text.startswith('"') and text.endswith('"'):text=text[1:-1]
                assert text==row[field],(table,name,field,text,row[field]);compared+=1
            rows[table][name]={k:v for k,v in row.items() if k and v!=''}
        pins[table]=dict(filename=pin['filename'],sha256=sha(local),publicSha256=sha(public))
    row=rows['skills']['Fire Claws']
    assert [row[k] for k in ['*Id','srvstfunc','srvdofunc','anim','seqtrans','UseAttackRate','restrict','State1','State2','mana','SrcDam','EType','ToHit','LevToHit']]==['239','58','2','A1','A1','1','2','wolf','bear','4','128','fire','50','15']
    assert row['EDmgSymPerCalc']=="(skill('Firestorm'.blvl)+skill('Molten Boulder'.blvl))*par8" and row['Param8']=='22'
    assert row['calc1']=='0' and not row.get('aurastate')
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==forms['legacyCommit']
    hashes,bodies,routines={},{},{}
    groups=[('source/D2Game/src/SKILLS/SkillDruid.cpp',['SKILLS_SrvSt58_FireClaws']),
        ('source/D2Game/src/SKILLS/Skills.cpp',['sub_6FD155E0','D2GAME_RollElementalDamage_6FD14DD0','sub_6FD11E40','SKILLS_SrvDo002_Kick_PowerStrike_MonIceSpear_Impale_Bash_Stun_Concentrate_BearSmite_Vengeance_Berserk_FireClaws']),
        ('source/D2Game/src/UNIT/SUnitDmg.cpp',['SUNITDMG_FillDamageValues','SUNITDMG_AllocCombat','SUNITDMG_DrainItemDurability'])]
    for relative,names in groups:
        path=BASE/'d2moo-pinned-source'/relative;raw=path.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=sha(path)
        for name in names:
            body=function(raw.decode(),name);bodies[name]=body;routines[name]=hashlib.sha256(body.encode()).hexdigest()
    start=bodies['SKILLS_SrvSt58_FireClaws']
    order=['sub_6FD155E0','damage.dwHitFlags |= 2','SUNITDMG_FillDamageValues','D2GAME_RollElementalDamage_6FD14DD0','SUNITDMG_AllocCombat']
    assert [start.index(x) for x in order]==sorted(start.index(x) for x in order)
    assert 'if (!(pDamage->dwHitFlags & DAMAGEHITFLAG_2))' in bodies['SUNITDMG_AllocCombat']
    roller=bodies['D2GAME_RollElementalDamage_6FD14DD0']
    assert 'SKILLS_GetMinElemDamage(pUnit, nSkillId, nSkillLevel, 1)' in roller and 'nMaxDamage - nMinDamage' in roller
    release=bodies['SKILLS_SrvDo002_Kick_PowerStrike_MonIceSpear_Impale_Bash_Stun_Concentrate_BearSmite_Vengeance_Berserk_FireClaws']
    assert 'SUNITDMG_DrainItemDurability' in release
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert next(x for x in status['skills'] if x['talentId']=='druid.fireclaws')['implementation']=='pending'
    result=dict(referenceVersion='3.3.93847',sourcePreparation=True,nativeAccepted=False,handlerEnabled=False,executedContexts=0,
        sourceScope='Byte-pinned current licensed/public data and eight complete older routine hashes. Static checks review Fire Claws call ordering; they do not exhaustively review or execute every shared damage branch, nor certify the current executable.',
        legacyCommit=tree['sha'],publicCommit=manifest['commit'],formsPreflightSha256=sha(forms_path),localTables=pins,publicFieldsCompared=compared,currentRows=rows,
        sourceHashes=hashes,pinnedRoutineHashes=routines,
        reuseCandidates={name:sha(ROOT/'src/Sanctuary.Content'/name) for name in ['DruidChargedAttacks.cs','DruidFormAttacks.cs','AmazonMelee.cs','GearEffects.cs','SkillMechanics.cs','WeaponLeech.cs']},
        reviewed=[
            'Current Fire Claws239uses start58/do2,A1/attack-rate,4mana and source128weapon damage. Restrict2allows both wolf and bear. It creates no new charge aura. Current hard-only damage synergy is22percent per Firestorm and Molten Boulder point; do not copy an older four-fire-skill synergy list or apply the summon-tree effective-rank exception here.',
            'Complete start58 obtains result flags,then on success sets hitflag2,adds row hit flags/class,sets calc1physical ED,current0,fills weapon damage,rolls skill fire and allocates combat. AllocCombat respects hitflag2and does not refill weapon values. Fill-once ordering prevents duplicate weapon damage/properties. The elemental roller uses mastered endpoints,an exclusive max-minus-min RNG span,and the shared elemental writer. Critical/mitigation/allocation ordering still needs composed execution.',
            'Complete do2handles configured impact overlay/optional target state before shared durability/combat drain. Final overlay is fire_hit. The existing saved-charge start/impact/timing/cancellation and prepared weapon services are reuse candidates,not proof that do120gain belongs here. Maul damage/stun in bear and Feral pre-gain life drain in wolf must remain shared stat channels; Fire Claws must not add or consume those charge states.',
            'Use existing actual SkillValues hard-synergy endpoints and shared weapon packet/mastery/critical/leech/recovery consumers rather than introducing another independent damage engine. AmazonMelee has the existing endpoint/mastery pattern,with a lightning-specific adapter that must not be blindly reused as fire. Current form human-weapon normalization/A1clocks and melee-only hands/range remain reusable with their explicit parity gaps.'
        ],remaining=[
            'Execute start58/result/weapon-fill/element/mastery/AllocCombat/do2composition with declared service boundaries,then build the native learned route. Verify both forms,mana,miss/block/immunity,physical versus fire/critical/leech,hard-versus-soft synergy,gear,state coexistence,Stop/range/weapon/form cancellation,right/quick and save/reload. Exact modern geometry/RNG/event/reentry,unmapped recipients/PvP and item-only permission remain unverified.',
            'Keep the current canonical Feral full-regression candidate frozen. Fire Claws follows its acceptance. Its shared pre-gain leech and Maul charge channels are now live reuse candidates; do not add do120charge gain to Fire Claws. This static preparation changes no handler/runtime data/DLLs or installed build/saves. Human form pose/contact and generic effects remain identified placeholders; fire_hit/cast/contact/audio and talent/left/right/quick icons remain separate pending assets.'
        ])
    path=ROOT/'tools/reference/druid-fire-claws-preflight.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==rendered
    else:path.write_text(rendered)
    print(f'Fire Claws preflight:{compared}current public fields/eight complete older routine hashes; static ordering only, execution/native handler not claimed.')

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');args=parser.parse_args();build(args.check)
