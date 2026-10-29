#!/usr/bin/env python3
"""Corroborate real maximum-scaling weapon bases against preserved owned data."""
import argparse
import csv
import hashlib
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
BASE=ROOT/'.local/class-skills'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    priorpath=ROOT/'tools/reference/iron-golem-local-items-evidence.json';prior=json.loads(priorpath.read_text())
    assert prior['referenceVersion']=='3.3.93847' and prior['localReadOnly']
    row=next(x for x in prior['tables'] if x['archivePath'].endswith('/weapons.txt'));ownedpath=ROOT/row['path'];assert sha(ownedpath)==row['sha256']
    owned={r['code']:r for r in csv.DictReader(ownedpath.read_text(encoding='utf-8-sig').splitlines(),delimiter='\t') if r.get('code')}
    cachepath=ROOT/'tools/reference/d2r-named-equipment/json/weapons.json';cache={r['code']:r for r in json.loads(cachepath.read_text()).values()};assert sha(cachepath)==prior['inputs'][str(cachepath.relative_to(ROOT))]
    fields=['mindam','maxdam','2handmindam','2handmaxdam','minmisdam','maxmisdam','StrBonus','DexBonus','2handed','1or2handed','reqstr','reqdex','levelreq']
    labels=['Minimum','Maximum','TwoMinimum','TwoMaximum','ThrowMinimum','ThrowMaximum','StrengthBonus','DexterityBonus','TwoHanded','EitherHanded','RequiredStrength','RequiredDexterity','RequiredLevel']
    bases=[];compared=0
    for code in ['7ga','6l7','bsd']:
        values=[]
        for field in fields:
            value=int(cache[code].get(field) or 0);assert value==int(owned[code].get(field) or 0),(code,field);values.append(value);compared+=1
        bases.append(dict(Code=code,**dict(zip(labels,values))))
    assert compared==39
    fixture=BASE/'item-maximum-bases-source-probe.json';encoded=json.dumps(bases,indent=2)+'\n'
    if check:assert fixture.read_text()==encoded
    else:fixture.write_text(encoded)
    result=dict(referenceVersion='3.3.93847',component='Owned real-item maximum-damage base controls',processMemoryRead=False,processInput=False,newGameReads=False,
        inputs={str(p.relative_to(ROOT)):sha(p) for p in [priorpath,ownedpath,cachepath,Path(__file__).resolve()]},verified=dict(bases=3,ownedComparedFields=39,unexplainedDifferences=0),
        artifacts={str(fixture.relative_to(ROOT)):sha(fixture)},remaining=['Current tables corroborate base/requirements/attribute factors;this is not runtime item initialization,ethereal or complete NPC stat-graph acceptance.'])
    path=ROOT/'tools/reference/item-maximum-bases-source-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded
    else:path.write_text(encoded)
    print('Maximum item bases:3real bases/39owned fields match;preserved read-only reference.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
