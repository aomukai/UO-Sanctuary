#!/usr/bin/env python3
"""Pin Fury's ordinary contact/reaction reuse boundaries before connecting the series."""
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
    pinpath=ROOT/'tools/reference/druid-fury-preflight.json';pin=json.loads(pinpath.read_text());fury=pin['currentRows']['skills']['Fury']
    assert fury['interrupt']=='1' and fury['SrcDam']=='128' and fury['State1']=='wolf' and fury['Param2']=='70'
    forms_path=ROOT/'tools/reference/druid-forms-preflight.json';forms=json.loads(forms_path.read_text());table=ROOT/'.local/research/d2r-3.3.93847'/forms['localTables']['skills']['filename'];assert sha(table)==forms['localTables']['skills']['sha256']
    skills=list(csv.DictReader(table.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t'))
    wolf=next(x for x in skills if x['skill']=='Wearwolf');bear=next(x for x in skills if x['skill']=='Wearbear')
    assert 'skill_concentration' not in [wolf.get('aurastat'+str(i)) for i in range(1,7)]
    assert (bear['aurastat4'],bear['aurastatcalc4'],bear['Param6'])==('skill_concentration','par6','100')
    tree=json.loads((BASE/'d2moo-source-tree.json').read_text());assert tree['sha']==pin['legacyCommit'];hashes={};routines={}
    for relative,names in [
        ('source/D2Game/src/PLAYER/PlrModes.cpp',['D2GAME_PLRMODES_First_6FC7F340','sub_6FC817D0','sub_6FC81890']),
        ('source/D2Game/src/UNIT/SUnitDmg.cpp',['SUNITDMG_GetResultFlags','SUNITDMG_AllocCombat','SUNITDMG_DrainItemDurability','SUNITDMG_ExecuteEvents','SUNITDMG_ApplyBlockOrDodge']),
    ]:
        path=BASE/'d2moo-pinned-source'/relative;raw=path.read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        hashes[relative]=sha(path)
        for name in names:routines[name]=hashlib.sha256(function(raw.decode(),name).encode()).hexdigest()
    previous_path=ROOT/'tools/reference/druid-form-death-interrupt-engine-evidence.json';previous=json.loads(previous_path.read_text())
    assert previous['verified']['interruptCases']==4800 and previous['completeRoutineHashes']['D2GAME_PLRMODES_First_6FC7F340']==routines['D2GAME_PLRMODES_First_6FC7F340']
    fixture=BASE/'druid-form-death-interrupt-probe.cpp';sanitizer=BASE/'druid-form-death-interrupt-sanitizer.log'
    assert sha(fixture)==previous['generatedSourceSha256'] and sha(sanitizer)==previous['sanitizerLogSha256'] and sanitizer.read_bytes()==b''
    result=dict(referenceVersion='3.3.93847',staticReview=True,handlerEnabled=False,nativeAccepted=False,currentExecutableParity=False,newExecutedContexts=0,
        legacyCommit=tree['sha'],preflightSha256=sha(pinpath),licensedSkillsSha256=sha(table),sourceHashes=hashes,completeRoutineHashes=routines,
        reusedExecutedProof=dict(path=str(previous_path.relative_to(ROOT)),sha256=sha(previous_path),interruptCases=4800,generatedSourceSha256=sha(fixture),sanitizerLogSha256=sha(sanitizer)),
        artifacts={str(p.relative_to(ROOT)):sha(p) for p in [Path(__file__).resolve(),pinpath,forms_path,previous_path,fixture,sanitizer]},
        sourceDerivedBaseline=[
            'Do not borrow Fend/Zeal hit-recovery locks solely because Fury shares do13. Current Fury interrupt1and wolf-only restriction combine with the complete older player mode gate: ordinary used interruptible skills allow gethit unless separate Concentration/Concentrate/Uninterruptable state protects the active action. Current Werewolf has no skill_concentration supplier; current Werebear has100but cannot use Fury. The identical First gate already executes4800declared contexts in the reused proof; this review adds no new executions.',
            'The complete XY mode wrapper calls sub_6FC81890then First unless explicitly bypassed. The ordinary A1mode guard admits gethit/block requests before the interruptibility/concentration decision; it does not supply an unconditional Fury-series lock. Modern released block protection is a distinct shared native policy,not evidence for Fury FHR immunity. Exact current game dispatch remains unverified.',
            'Do13 allocates an ordinary Src128combat packet even on miss; DrainItemDurability looks up that packet and rechecks melee contact. Successful result gates health/events,weapon/armor durability and thorns. A miss/block still consumes a series attempt but must not be passed to the successful weapon-strike consumer. Native Fury should use the existing shared AR/typed weapon/critical/leech/proc/resistance pipeline and explicit NPC shield consumer instead of source-zero spell damage.',
            'DrainItemDurability posts DOMELEEATTACK/ATTACKEDINMELEE even for an unsuccessful packet,then applies thorns only on success,and finally executes/free-cleans combat. Those full source events are reviewed,not compiled here. Existing native attempt/packet/health/item-event chronology remains a declared adapter; this static review does not certify complete modern event ordering or reentry.',
        ],nextNativeWork=[
            'Connect a wolf-only2..5attempt series with start37/do13,once-only pinned4mana payment,source-derived ordinary interruptibility,per-contact current-target revalidation/cyclic replacement,actual shield/miss handling and shared typed weapon/charge/drain consumers. Existing Feral charge must not be gained,consumed or refreshed by Fury.',
            'Use the verified shared Ceiling/Rollback70and prepared action7/end13clock. Keep final whole-cycle recovery separate from extra post-hit time. Current progression refresh/rank capture/payment/reentry and full outer executable equivalence remain explicit questions rather than silently certified mechanics.',
            'Stop,accepted movement,weapon changes,unshift/death/history/logout and restart need native/input checks. Preserve already-paid resources and discard unspent series events on reload. Test actual left/right/quick input,hard/effective ranks,real gear,immunity,critical,leech,miss/block,retargeting and low-mana ordinary Attack deviation. No runtime handler/build/save or final asset changes occur in this review.',
        ])
    target=ROOT/'tools/reference/druid-fury-contact-review.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Fury contact review:8complete routine hashes,current row/form contrast and reused4800-case guard proof; no borrowed Fend hit-recovery lock or new native handler.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');build(p.parse_args().check)
