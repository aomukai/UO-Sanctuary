#!/usr/bin/env python3
"""Compare uncapped slowing inputs with unchanged source/current-family rates."""
import argparse,hashlib,json,subprocess
from pathlib import Path
from probe_weapon_sequence_rate import ROOT,STAGE,build as weapon_reference
from probe_assassin_claw_modern_rate_reference import build as claw_reference

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()

def build(check=False):
    # Reconstruct verified originals, preserving their earlier matrices/evidence.
    weapon_reference(True);claw_reference(True)
    original=STAGE/'weapon-sequence-rate-probe.cpp'
    prefix=original.read_text().split('int main(){')[0]
    cpp=prefix+r'''
int main(){bool first=true;std::printf("[");
for(int item:{0,40,120})for(int haste:{0,20,60,100})for(int wsm:{-30,0,20})
for(int slow:{0,50,85,100,150,200})for(int sequence:{0,1})for(int base:{128,208,256}){
ias=item;rate=100+haste-wsm-slow;owner.dwUnitType=0;owner.dwAnimMode=sequence;anim.dwAnimSpeed=base;
UNITS_UpdateAttackAnimRateAndVelocity(&owner);
if(!first)std::printf(",");first=false;
std::printf("[%d,%d,%d,%d,%d,%d,%d,%d]",item,haste,wsm,slow,sequence,base,owner.dwSeqSpeed,owner.wAnimSpeed);
}std::printf("]\n");}
'''
    source=STAGE/'player-attack-slow-probe.cpp';source.write_text(cpp)
    binary=STAGE/'player-attack-slow-probe'
    subprocess.run(['g++','-std=c++20','-O2','-fsanitize=address,undefined','-fno-omit-frame-pointer',str(source),'-o',str(binary)],check=True)
    engine=json.loads(subprocess.check_output([str(binary)]));assert len(engine)==1296
    # Only declared UI inputs change; original calculator functions stay intact.
    js_original=STAGE/'assassin-claw-modern-rate-probe.js'
    js=js_original.read_text()
    old="for(let bonus of [0,20])for(let slow of [0,40,85])"
    new="for(let bonus of [0,20,60,100])for(let slow of [0,40,85,100,150,200])"
    assert js.count(old)==1;js=js.replace(old,new)
    script=STAGE/'player-attack-slow-modern-probe.js';script.write_text(js)
    modern=json.loads(subprocess.check_output(['node',str(script)]));assert len(modern)==18432
    fixture=STAGE/'player-attack-slow-probe.json';fixture.write_text(json.dumps(dict(engine=engine,modern=modern),separators=(',',':'))+'\n')
    differs=sum(out!=base*max(15,min(175,100+120*item//(120+item)+haste-wsm-min(slow,85)-(30 if seq else 0)))//100
        for item,haste,wsm,slow,seq,base,out,_ in engine)
    assert differs>0
    evidence=dict(referenceVersion='3.3.93847',legacyEvidence='tools/reference/weapon-sequence-rate-evidence.json',
        currentFamilyEvidence='tools/reference/assassin-claw-modern-rate-evidence.json',
        unchangedSourcePrefixSha256=hashlib.sha256(prefix.encode()).hexdigest(),baseCppSha256=sha(original),baseJsSha256=sha(js_original),
        probeSha256=sha(source),scriptSha256=sha(script),fixture=dict(path=str(fixture.relative_to(ROOT)),sha256=sha(fixture)),
        engineContexts=len(engine),currentFamilyContexts=len(modern),prematureClampDisagreements=differs,
        established=['The unchanged non-dual player engine branch combines supplied attackrate with diminished IAS and player sequence adjustment before clamping15..175.',
            'Unchanged current-family calculator Fists and Dragon Claw branches subtract supplied slowing above85 before their final clamp; haste and WSM can leave the total above the minimum.',
            'Individual slow producers retain their own eligibility/caps. Only the aggregate player projection changes; native NPC divisor projections remain bounded.'],
        limits=['Legacy source and original-author current-family calculator are supplementary evidence, not execution of the pinned modern client.',
            'Engine stat access supplies aggregate attackrate; dual merging/melee-only override are inactive. Modern controls supply explicit UI values and hidden unrelated buffs.',
            'This does not resolve modern finisher hit/block/charge timing, mid-animation event rescheduling, full stat ordering or client presentation.'])
    target=ROOT/'tools/reference/player-attack-slow-source-evidence.json';encoded=json.dumps(evidence,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print(f'Player attack slows:{len(engine)} source/{len(modern)} current-family contexts;{differs} premature-clamp disagreements; sanitizers passed.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
