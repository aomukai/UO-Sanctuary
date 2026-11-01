#!/usr/bin/env python3
"""Bind older Chain references to canonical pinned files; do not rerun absent probes."""
import argparse
import hashlib
import json
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()

def review(check=False):
    engine=ROOT/'tools/reference/chain-lightning-engine-evidence.json'
    mage=ROOT/'tools/reference/mage-chain-native-evidence.json'
    old=json.loads(engine.read_text());prior=json.loads(mage.read_text())
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text())
    assert old['d2rVersion']=='3.3.93847' and old['engineReferenceCommit']==tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    canonical=['source/D2Game/src/SKILLS/SkillSor.cpp','source/D2Game/src/SKILLS/Skills.cpp','source/D2Game/src/MISSILES/MissMode.cpp','source/D2Game/src/MISSILES/Missiles.cpp','source/D2Common/src/Path/Path.cpp']
    mappings=[]
    for (alias,digest),relative in zip(old['sourceHashes'].items(),canonical,strict=True):
        p=ROOT/'.local/class-skills/d2moo-pinned-source'/relative;raw=p.read_bytes()
        assert sha(p)==digest
        assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        mappings.append(dict(historicalAlias=alias,canonical=str(p.relative_to(ROOT)),sha256=digest))
    bodies={}
    for relative,name in [(canonical[0],'SKILLS_SrvDo026_ChainLightning'),(canonical[2],'MISSMODE_SrvHit12_ChainLightning_LightningStrike')]:
        p=ROOT/'.local/class-skills/d2moo-pinned-source'/relative
        bodies[name]=hashlib.sha256(function(p.read_text(),name).encode()).hexdigest()
    assert bodies==prior['reviewedNotExecuted']
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],kind='Read-only source identity and retained historical evidence; no new compiled emitter/hit/current-executable proof.',
                mappings=mappings,reviewedBodies=bodies,inputHashes={str(p.relative_to(ROOT)):sha(p) for p in (Path(__file__).resolve(),engine,mage)},
                established=['All five historical source aliases map to byte-identical files in the canonical pinned source tree; both do26/hit12 bodies match the retained Mage review.',
                             'The older engine record retains bounded compiled count/cyclic-selection/child outputs and supplementary modern NHD discussion. This check verifies source identity, not re-execution of that historical probe.'],
                remaining=['Historical scratch aliases and original compiled probe are not recreated. Complete modern code, exact room/filter/target ordering and NHD implementation remain unproved; native acceptance is separate.'])
    out=ROOT/'tools/reference/player-chain-source-reuse.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Chain reference:5 canonical source identities/2 exact reviewed bodies; historical outputs retained,not newly compiled.')
if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true');review(parser.parse_args().check)
