#!/usr/bin/env python3
"""Pin Blood Boil table/tooltip evidence and the unrecovered callback boundary."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def record(check=False):
    ref=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((ref/'sources.json').read_text())
    paths=[ref/'json'/n for n in ('skills.json','skilldesc.json','allstrings-eng.json')]
    for p in paths:assert sha(p)==manifest['files'][str(p.relative_to(ref))]['sha256']
    row=json.loads(paths[0].read_text())['380'];generated=next(r for r in json.loads((ROOT/'data/Sanctuary/Balance/class-skills.json').read_text())if r['sourceId']==380)
    assert generated['sourceFields']=={k:v for k,v in row.items()if not k.startswith('*')and k!='lineNumber'}
    assert all(generated['sourceParameters'][str(i)]==row.get('Param'+str(i),0)for i in range(1,21))
    desc=next(r for r in json.loads(paths[1].read_text()).values()if r.get('skilldesc')=='blood boil');strings=json.loads(paths[2].read_text())
    assert row['srvdofunc']==160 and row.get('srvstfunc',0)==0 and row['calc4']==3 and row['pettype']=='demon'
    assert row['DmgSymPerCalc']=="(skill('Engorge'.blvl))*par8"and row['EDmgSymPerCalc']=="(skill('Blood Oath'.blvl))*par8"and row['Param8']==20
    assert desc['desctexta2']=='StrSkillLifeCostDemon'and strings['StrSkillLifeCostDemon']=="Demon's Life Cost: %d%%"
    capture=ROOT/'.local/class-skills/warlock-runtime-reference/warlock-do-160-20261108/capture.json';saved=json.loads(capture.read_text());candidate=saved['records'][0]
    assert saved['referenceVersion']=='3.3.93847'and candidate['slot']==160 and len(candidate['fragments'])==1 and not candidate['fragments'][0]['captured']
    files=[*paths,ref/'sources.json',ROOT/'data/Sanctuary/Balance/class-skills.json',capture,Path(__file__).resolve()]
    result=dict(referenceVersion='3.3.93847',component='Blood Boil owned suppliers and unavailable do160 boundary',skill=380,start=0,do=160,
        fields={k:v for k,v in row.items()if k.startswith(('aura','calc','Param','Min','Max','EMin','EMax'))or k in ('DmgSymPerCalc','EDmgSymPerCalc','mana','lvlmana','manashift','pettype','requirespettype')},
        displayedRadius=desc['desccalca3'],sourceRadius=row['aurarangecalc'],sourceCandidate=dict(rva=candidate['rva'],fragments=1,readableCodeBytes=0),newLiveReferenceReads=0,
        externalReferences=[dict(url='https://diabloforfans.com/skill/blood-boil?lang=enUS',kind='Author datamined reference',reportedVersion='3.3.93847',observedOn='2026-11-15',scope='Corroborates three-frame recipient delay and demon-count division with5percent floor;does not identify delay scope,life gate or executable order.'),
            dict(url='https://upload3.inven.co.kr/upload/2026/03/02/bbs/WarlockSkillsNotesandFunctionality.pdf',kind='Original Warren/Kabryxis tested notes',version='Unspecified older upload',scope='Reports >5percent life gate,possible lethal edge and older11yard cap;not pinned current executable proof.')],
        established=['Owned row has independent physical/fire endpoints and20percent perhard Engorge/Oath synergy respectively. Physical pre-shift truncation and elemental FP8 evaluation reuse existing getters.',
            'Mana starts3.75and increases0.25per effective rank. Cost curves divide before a5percent floor for1/2/3demons;calc4is3.',
            'Owned displayed5/6/7yard radius differs from raw6/9/12range and older11yard report. Proposed native geometry must explicitly choose raw range through existing shared source-area projection.'],
        unresolved=['Current do160 protected. Maximum/current-life cost basis,strict life gate,lethal cost behavior,roster inclusion,order,roll ownership/count,hit-delay scope,area/LOS/payment and lifecycle require verified evidence or explicit adaptation.',
            'Guardian friendly-hit protection prevents using ordinary pet self-damage as a resource cost. Proposed direct FP8 life setter/native death finisher must not invent incoming attack/proc/aggro callbacks.',
            'No new live memory read or older source compiler matrix is justified. Ordinary native area recipient service is reusable;broader PvP/summon/hireling/property adapters remain outside this prepared proof.'],
        inputs={str(p.relative_to(ROOT)):sha(p)for p in files})
    out=ROOT/'tools/reference/blood-boil-source-review.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Blood Boil source review stale'
    else:out.write_text(encoded)
    print('Blood Boil380:owned endpoints/synergies,life-cost curves and tooltip authenticated;do160unreadable;radius/gate/order conflicts explicit.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
