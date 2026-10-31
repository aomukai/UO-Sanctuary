#!/usr/bin/env python3
"""Execute complete older prefilled NPC allocation/mitigation including initial poison rate."""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1];BASE=ROOT/'.local/class-skills'
def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def build(check=False):
    proof_path=ROOT/'tools/reference/monster-weapon-phases-source-evidence.json';proof=json.loads(proof_path.read_text())
    parent=BASE/'monster-weapon-phases-source-probe.cpp';assert sha(parent)==proof['generatedSourceSha256']
    cpp=parent.read_text().split('int main(){')[0]
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==proof['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    relative='source/D2Game/src/UNIT/SUnitDmg.cpp';source=BASE/'d2moo-pinned-source'/relative;data=source.read_bytes()
    assert hashlib.sha1(b'blob '+str(len(data)).encode()+b'\0'+data).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
    names=['SUNITDMG_AllocCombat','SUNITDMG_CalculateTotalDamage','SUNITDMG_ApplyResistancesAndAbsorb']
    routines={}
    for name in names:
        body=function(data.decode(),name);assert body in cpp;routines[name]=hashlib.sha256(body.encode()).hexdigest()
    cpp+=r'''
int main(){for(int i=0;i<32;i++)gdwBitMasks[i]=1u<<i;printf("[");bool first=true;
for(int flags:{0,1,33,129,8193})for(int rate:{0,1,128,460,1048577})for(int frames:{0,1,75})for(int resist:{-100,0,50,100,150})for(int plr:{0,50,100})for(int hp:{1,1000,10000000})for(int typed:{0,1})for(int variant:{0,1,2,3}){
Reset(416,1);mon={};victimHire=victimBoss=victimUnique=false;tapOn=deathDelay=false;defender.stats[STAT_HITPOINTS]=hp;
for(int stat:{STAT_DAMAGERESIST,STAT_FIRERESIST,STAT_LIGHTRESIST,STAT_COLDRESIST,STAT_MAGICRESIST,STAT_POISONRESIST})defender.stats[stat]=resist;
defender.stats[STAT_ITEM_POISONLENGTHRESIST]=plr;defender.stats[STAT_NORMAL_DAMAGE_REDUCTION]=(variant==1||variant==3);defender.stats[STAT_MAGIC_DAMAGE_REDUCTION]=(variant>=2);defender.stats[STAT_ITEM_ABSORBFIRE]=(variant==3);
D2DamageStrc d={};d.wResultFlags=flags;bool fill=(flags&1)&&!(flags&(128|256|512|32768));int physical=variant==0?0:variant==1?256:257;
if(fill){d.dwPhysDamage=physical;d.dwFireDamage=typed?512:0;d.dwLtngDamage=typed?640:0;d.dwMagDamage=d.dwColdDamage=typed?257:0;d.dwColdLen=typed?31:0;d.dwPoisDamage=rate;d.dwPoisLen=rate?frames:0;d.dwHitFlags=2;}
SUNITDMG_AllocCombat(&game,&attacker,&defender,&d,128);
printf("%s[[%d,%d,%d,%d,%d,%d,%d,%d,%d],[%d,%d,%d,%d,%d,%d,%d,%d,%d,%d,%d]]",first?"":",",flags,physical,typed,rate,frames,resist,plr,variant,hp,d.dwPhysDamage,d.dwFireDamage,d.dwLtngDamage,d.dwMagDamage,d.dwColdDamage,d.dwPoisDamage,d.dwColdLen,d.dwPoisLen,d.dwDmgTotal,d.dwAbsLife,d.wResultFlags);first=false;
}printf("]\n");}
'''
    generated=BASE/'npc-impact-allocation-source-probe.cpp';generated.write_text(cpp);binary=generated.with_suffix('')
    run=subprocess.run(['g++','-std=c++20','-O1','-g','-fsanitize=address,undefined','-fno-sanitize-recover=all',str(generated),'-o',str(binary)],capture_output=True)
    (BASE/'npc-impact-allocation-compiler.log').write_bytes(run.stdout+run.stderr);assert run.returncode==0,run.stderr.decode()[-4000:]
    run=subprocess.run([str(binary)],capture_output=True);(BASE/'npc-impact-allocation-sanitizer.log').write_bytes(run.stderr)
    assert run.returncode==0 and not run.stderr,run.stderr.decode()[-4000:]
    rows=json.loads(run.stdout);assert len(rows)==27000,len(rows)
    for i,o in rows:
        assert o[8]==sum(o[:6])
        if i[0]&1 and not i[0]&128:assert bool(o[10]&2)==((o[8]&~255)>(i[8]&~255))
    fixture=BASE/'npc-impact-allocation-source-probe.json';fixture.write_text(json.dumps(rows,separators=(',',':'))+'\n')
    periodic_path=ROOT/'tools/reference/poison-state-engine-evidence.json';periodic=json.loads(periodic_path.read_text())
    assert periodic['legacyCommit']==tree['sha'] and periodic['probe']['applicationCases']==384 and periodic['probe']['regenCases']==2240
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],sourceFile=relative,sourceSha256=sha(source),unchangedRoutineHashes=routines,cases=len(rows),
        separatePeriodicReference=dict(path=str(periodic_path.relative_to(ROOT)),sha256=sha(periodic_path),scope='Reused independent complete application/regen source evidence,not executed by this allocation probe.'),
        parent=dict(path=str(parent.relative_to(ROOT)),sha256=sha(parent),receipt=str(proof_path.relative_to(ROOT)),receiptSha256=sha(proof_path)),
        fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=sha(fixture)),generatedSourceSha256=sha(generated),sanitizers='ASan+UBSan exit0;empty runtime stderr',
        scope='Complete older AllocCombat,CalculateTotalDamage and resistance/absorb bodies with already-filled declared NPC channels and explicit world/stat/event services. HitFlags2skips FillDamageValues. Does not execute offensive roll preparation,contact/state application/periodic regeneration or current executable.',
        established=['Mitigated poison rate contributes once to immediate total independently of length;zero length can suppress a later state without erasing that impact. Separate periodic application/regen source evidence still schedules its first update on the next frame.',
            'Result32retains mitigation/allocation;miss and source NoFill controls do not calculate damage. WILLDIE prediction uses strict greater-than after masking low8bits of total and current HP.'],
        remaining=['Native packet composition and actual original-item allocation need integration verification. Full actor/event/return/notification bindings and current-build applicability remain separate;do not infer complete periodic lifetime or legacy delivery migration from this proof.'])
    target=ROOT/'tools/reference/npc-impact-allocation-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==text
    else:target.write_text(text)
    print('NPC allocation:27000complete older prefilled mitigation/total/prediction contexts pass ASan+UBSan;initial poison rate is separate from periodic frames.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
