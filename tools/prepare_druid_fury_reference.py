#!/usr/bin/env python3
"""Pin Fury's shared Zeal/Fend routes and distinct wolf rollback requirements."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    predecessor=ROOT/'tools/reference/druid-shock-wave-preflight.json';pin=json.loads(predecessor.read_text())
    public_root=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((public_root/'sources.json').read_text())
    assert pin['referenceVersion']==manifest['mechanicsReferenceVersion']=='3.3.93847'
    rows={};tables={};compared=0
    for table,key,name in [('skills','skill','Fury'),('states','state','wolf')]:
        table_pin=pin['localTables'][table];local=ROOT/'.local/research/d2r-3.3.93847'/table_pin['localFilename'];assert sha(local)==table_pin['sha256']
        licensed=list(csv.DictReader(local.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t'))
        public=public_root/'json'/(table+'.json');assert sha(public)==manifest['files']['json/'+table+'.json']['sha256']
        row=next(x for x in licensed if x.get(key)==name);other=next(x for x in json.loads(public.read_text()).values() if x.get(key)==name)
        for field,value in other.items():
            if field not in row or field=='lineNumber' or field.startswith('*') and field not in ['*Id','*ID']:continue
            text=str(value)
            if table=='skills' and text.startswith('"') and text.endswith('"'):text=text[1:-1]
            assert row[field]==text,(table,field,row[field],text);compared+=1
        rows[table]={name:{field:value for field,value in row.items() if field and value!=''}}
        tables[table]=dict(localFilename=table_pin['localFilename'],sha256=sha(local),publicSha256=sha(public))
    skill=rows['skills']['Fury']
    assert [skill[k] for k in ['*Id','srvstfunc','srvdofunc','anim','seqtrans','UseAttackRate','mana','restrict','State1','Param2','Param3','Param4','Param5','Param6','ToHit','LevToHit','SrcDam']]==['248','37','13','A1','A1','1','4','2','wolf','70','100','17','2','5','50','10','128']
    assert skill['calc1']=='min((par5 + lvl -1), par6)' and skill['calc2']=='ln34' and not skill.get('DmgSymPerCalc')
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==pin['legacyCommit'];hashes={};routines={}
    for relative,names in [
        ('source/D2Game/src/SKILLS/SkillAma.cpp',['SKILLS_SrvSt37_Zeal_Fury_BloodLordFrenzy','SKILLS_SrvDo013_Fend_Zeal_Fury']),
        ('source/D2Game/src/SKILLS/Skills.cpp',['sub_6FD10880','sub_6FD107F0','sub_6FD15080','sub_6FD15460','D2GAME_SKILLS_Handler_6FD12BA0']),
        ('source/D2Game/src/UNIT/SUnit.cpp',['sub_6FCBCE70','sub_6FCBCFD0']),
        ('source/D2Common/src/D2Skills.cpp',['SKILLS_GetToHitFactor','SKILLS_GetManaCosts']),
    ]:
        path=BASE/'d2moo-pinned-source'/relative;raw=path.read_bytes()
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=sha(path)
        for name in names:routines[name]=hashlib.sha256(function(raw.decode(),name).encode()).hexdigest()
    forms_path=ROOT/'tools/reference/druid-forms-preflight.json';forms=json.loads(forms_path.read_text())
    animation=ROOT/'.local/research/d2r-3.3.93847/AnimData.d2';assert sha(animation)==forms['animation']['sha256']
    attacks=[x for x in forms['animation']['records'] if x['code'] in ['40A1HTH','40A2HTH']]
    assert len(attacks)==2 and all(x['frames']==13 and x['speed']==256 and x['events']==[[7,1]] for x in attacks)
    reuse=['PaladinZeal.cs','AmazonFend.cs','DruidFormAttacks.cs','DruidFormRates.cs','DruidFormInterruption.cs','GearEffects.cs','WeaponHands.cs','SkillCombat.cs']
    result=dict(referenceVersion='3.3.93847',sourcePreparation=True,handlerEnabled=False,nativeAccepted=False,executedContexts=0,currentExecutableParity=False,
        legacyCommit=tree['sha'],publicCommit=manifest['commit'],predecessorSha256=sha(predecessor),localTables=tables,currentRows=rows,publicFieldsCompared=compared,
        animation=dict(sha256=sha(animation),wolfAttacks=attacks,formPreflightSha256=sha(forms_path)),sourceHashes=hashes,completeRoutineHashes=routines,
        reuseCommit='8949002',reuseCandidates={name:hashlib.sha256(subprocess.check_output(['git','show','8949002:src/Sanctuary.Content/'+name],cwd=ROOT)).hexdigest() for name in reuse},
        reusableEvidence=['tools/reference/zeal-engine-evidence.json','tools/reference/fend-engine-evidence.json','tools/reference/modern-form-progress-source-evidence.json','tools/reference/druid-form-attack-clock-engine-evidence.json'],
        established=[
            'Current skill248 is wolf-only ordinary A1 with start37/do13 shared by Zeal/Fend. Count is min(rank+1,5),4mana,SrcDam128,50+10/rank AR and100+17/rank enhanced physical damage with no hard-skill synergy. SearchEnemyXY/TargetableOnly retain targeted melee acquisition; do not substitute the free-aim Shock Wave fan.',
            'Complete older start37 stores the chosen target identity and count. Each do13 revalidates/reselects current melee contact,allocates an ordinary Src128 packet and drains wear,then decrements count and chooses the next cyclic identity. The shared next-target selector can wrap to a sole surviving monster. Actual source/native composition,hit/block/charge/drain/item callbacks and modern target policy still require verification.',
            'Param2=70 is a rollback argument passed through sub_6FD15080 to the full SUnit animation scheduler. It differs from Zeal100 and Fend30. Current wolf A1/A2 both have13frames,speed256 and action7; transformed modern weapon-normalized progress is already prepared separately. Do not copy a human Paladin clock or assume a constant shortened follow-up period.',
        ],remaining=[
            'Compose complete shared start/do13,selection,ordinary weapon packet/payment and wolf rate-scaled rollback with outer animation-anchor/event scheduling. Existing Zeal/Fend proofs declare transformed or incomplete outer-clock limits; their bounded outputs do not certify Fury timing.',
            'Then connect a saved or explicitly discarded pending series,actual left/right/quick inputs,rank/gear/resources,per-hit retargeting,interruption/unshift/Stop/movement and real reloads. Current initial miss/block payment,low/lost mana,extra transformed AR and current packet/callback chronology must be resolved or marked uncertain. Approved Sanctuary melee fallback takes precedence where applicable.',
            'Final wolf multi-swing poses/contact/series audio and talent/left/right/quick icons remain separate placeholders. No Fury handler,full acceptance,class completion or installed build changes are claimed by this source preparation.',
        ])
    for name in result['reusableEvidence']:assert (ROOT/name).is_file(),name
    path=ROOT/'tools/reference/druid-fury-preflight.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print(f'Fury preflight:{compared}licensed/public fields,{len(routines)}complete routines,wolf A1/A2 and shared Zeal/Fend candidates; learned casting remains disabled.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
