#!/usr/bin/env python3
"""Bind Nova's identical finder/router fields to already-executed Teeth controls.

This is evidence reuse, not a new executable simulation or a poison-health proof.
It only reads the preserved source/fixtures and writes its separate review receipt.
"""
import argparse,hashlib,json
from pathlib import Path
from review_weapon_event_order import function
ROOT=Path(__file__).resolve().parents[1]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def build(check=False):
    ref=ROOT/'tools/reference/teeth-source-evidence.json';old=json.loads(ref.read_text());assert old['referenceVersion']=='3.3.93847' and old['routeCases']==192
    for group in ['inputs','artifacts']:
        for name,digest in old[group].items():assert sha(ROOT/name)==digest,name
    path=ROOT/'tools/reference/d2r-named-equipment/json/missiles.json';rows=json.loads(path.read_text());teeth,nova=rows['teeth'],rows['poisonnova']
    fields=['pSrvDoFunc','pSrvHitFunc','pSrvDmgFunc','CollideType','Size','CollideKill','LastCollide','NextHit','NextDelay','ToHit','CollideFriend','AlwaysExplode','Pierce','Explosion']
    matched={k:nova.get(k,0) for k in fields};assert matched=={k:teeth.get(k,0) for k in fields}
    assert nova['*ID']==118 and teeth['*ID']==114 and matched['pSrvDoFunc']==1 and matched['NextDelay']==4
    mode=ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Game/src/MISSILES/MissMode.cpp';text=mode.read_text();bodies={}
    for name in ['MISSMODE_SrvDmgHitHandler','MISSMODE_UnitFindCallback_CanCollideWithPlayerOrMonster']:
        body=function(text,name);digest=hashlib.sha256(body.encode()).hexdigest();assert old['completeRoutines'][name]==digest;bodies[name]=digest
    fixture=ROOT/'.local/class-skills/teeth-source/route-cases.json';assert len(json.loads(fixture.read_text()))==192
    differing={k:dict(teeth=teeth.get(k,0),poisonNova=nova.get(k,0)) for k in ['Vel','MaxVel','Range','InitSteps','CanSlow','GetHit','ResultFlags','HitShift','HitClass'] if teeth.get(k,0)!=nova.get(k,0)}
    out=dict(referenceVersion='3.3.93847',kind='Reuse of already-executed row-parameterized source finder/router evidence',legacyCommit=old['legacyCommit'],newSourceCasesExecuted=0,reusedLegacyCases=192,handlerAccepted=False,currentExecutableParity=False,
        matchedRoutingFields=matched,outsideThisReview=differing,completeRoutineHashes=bodies,inputs={str(p.relative_to(ROOT)):sha(p) for p in [ref,path,mode,fixture,Path(__file__).resolve()]},
        established=['The current Teeth and Poison Nova rows have identical fields used by the complete generic finder/router controls:ordinary do1/Size1/CollideType3,LastCollide,contact removal,four-frame NextHit,no AR/custom hit/damage function/friend collision/Pierce/Explosion.',
        'Reuse the192already-executed historical delayed/repeated/hostile/target-kind/lease-allocation/terminal controls for this common routing contract. Their declared packet/event/defense services are unchanged;this review executes no new source cases.'],
        remaining=['Poison Nova has distinct velocity/range/initial-step/CanSlow/GetHit/ResultFlags/HitShift/HitClass data. Movement and factory are covered separately;actual poison health/defense/hit-recovery/events are not proved by matching routing fields.',
        'Historical JUSTHIT does not establish modern caster/cast identity. Native rays,next-hit scope,initial poison delivery,ongoing poison/tick potion bonuses,owner/lifecycle/payment/input/reloads and current executable/client parity remain acceptance work.'])
    target=ROOT/'tools/reference/poison-nova-routing-review.json';rendered=json.dumps(out,indent=2)+'\n'
    if check:assert target.read_text()==rendered
    else:target.write_text(rendered)
    print('Poison Nova routing:identical current generic-routing fields reuse192preserved Teeth contexts;no new execution or health acceptance claimed.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
