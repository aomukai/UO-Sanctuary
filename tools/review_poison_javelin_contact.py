#!/usr/bin/env python3
"""Bind Poison Javelin weapon parent versus source0 initial-rate/cloud delivery."""
import argparse,hashlib,json
from review_weapon_event_order import ROOT,STAGE,function
from review_magic_arrow_contact import record as parent

def record(check=False):
    parent(True);tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());records={};bodies={}
    for path,names in {'source/D2Game/src/MISSILES/MissMode.cpp':['MISSMODE_SrvDo02_PlagueJavelin_PoisonJavelin_PoisonTrap','MISSMODE_SrvDo03_PoisonCloud_Blizzard_ThunderStorm_HandOfGod','MISSMODE_SetDamageFlags','MISSMODE_SrvDmgHitHandler'],'source/D2Common/src/Units/Missile.cpp':['MISSILE_CalculateDamageData','MISSILE_SetDamageStats'],'source/D2Game/src/UNIT/SUnitDmg.cpp':['SUNITDMG_CalculateTotalDamage','SUNITDMG_ExecuteEvents','SUNITDMG_ExecuteMissileDamage']}.items():
        raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==path)
        for name in names:body=function(raw.decode(),name);bodies[name]=body;records[name]=dict(path=path,sha256=hashlib.sha256(body.encode()).hexdigest())
    assert 'pDamage->dwColdDamage + pDamage->dwPoisDamage' in bodies['SUNITDMG_CalculateTotalDamage']
    cache=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((cache/'sources.json').read_text());p=cache/'json/missiles.json';sha=hashlib.sha256(p.read_bytes()).hexdigest();assert sha==manifest['files']['json/missiles.json']['sha256'];rows=json.loads(p.read_text());runtime={r['key']:r['fields']for r in json.loads((ROOT/'data/Sanctuary/Balance/necromancer-summons.json').read_text())};fields={}
    for name,wanted in {'poisonjav':dict(pSrvDoFunc=2,pSrvHitFunc=0,pSrvDmgFunc=0,ToHit=1,HitClass=6,GetHit=1,ResultFlags=4,HitFlags=0,Range=25,Size=1,CollideKill=1,Pierce=1,LastCollide=1,AlwaysExplode=0),'poisonjavcloud':dict(pSrvDoFunc=3,pSrvHitFunc=0,pSrvDmgFunc=0,ToHit=0,HitClass=6,GetHit=0,ResultFlags=0,HitFlags=0,SrcDamage=-1,Range=60,Size=2,CollideKill=0,Pierce=0,LastCollide=1)}.items():
        assert all(rows[name].get(k,0)==v and runtime[name].get(k,0)==v for k,v in wanted.items());fields[name]=wanted
    evidence={}
    for n in ('factory','damage','routing','trail'):
        p=ROOT/f'tools/reference/poison-javelin-{n}-engine-evidence.json';source=json.loads(p.read_text());probe=ROOT/source['probe']['path'];assert hashlib.sha256(probe.read_bytes()).hexdigest()==source['probe']['sha256'];evidence[str(p.relative_to(ROOT))]=hashlib.sha256(p.read_bytes()).hexdigest()
    assert next(r for r in json.loads((cache/'json/skills.json').read_text()).values()if r['skill']=='Poison Javelin')['SrcDam']==128
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],functions=records,currentMissilesSha256=sha,missileFields=fields,skillSourceDamage=128,retainedSourceEvidence=evidence,established=['Source128 parent uses full weapon/item/skill poison fields and HitFlags32; source ToHit1 applies accuracy PMH before physical blocking. Existing source128 integer rate/length/count packet is unchanged.','Source0 clouds have Successful1,GetHit0,HitClass6,HitFlags0. They commit one mitigated poison-rate HP hit before applying the ordinary poison clock,without attacker/defender weapon events,Crushing Blow,drain,slow,armor,cold or weapon RNG. The complete older total calculator includes poison in immediate damage; later poison ticks remain independent.','Existing previous path-change trail birth,retained parent rank/live hard Plague synergy/mastery,59 contact updates/60-frame expiry,source footprint and pre-selection LastCollide remain. Alternating recipients may receive later initial-rate hits;strongest-poison replacement and cure use the shared receiver.','Weapon-skill Magical Penetration exclusion remains per current policy;user explicitly confirmed that Poison Javelin and Plague Javelin clouds retain that exclusion.'],remaining=['Archived current rows and complete older functions/probes establish scoped event integration,not exact modern initial-rate callback/seed/stat/provider/owner/scheduler/room-order/geometry parity.','Modern depleted providers,exceptional/uninterruptible/death-delay/hireling/PvP remain unverified. Plague Javelin is a separate next consumer.','Whole named acquisition,full class/client and separate effect/audio/icon acceptance remain open.'])
    out=ROOT/'tools/reference/poison-javelin-contact-source-review.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Poison Javelin source128 weapon parent and source0 initial-rate/cloud event flags bound.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
