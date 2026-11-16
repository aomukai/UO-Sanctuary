#!/usr/bin/env python3
"""Bind Strafe full-source weapon impacts and unchanged sequence/NHD evidence."""
import argparse,hashlib,json
from review_weapon_event_order import ROOT,STAGE
from review_magic_arrow_contact import record as parent

def record(check=False):
    parent(True);cache=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((cache/'sources.json').read_text());p=cache/'json/missiles.json';sha=hashlib.sha256(p.read_bytes()).hexdigest();assert sha==manifest['files']['json/missiles.json']['sha256'];rows=json.loads(p.read_text());runtime={r['key']:r['fields']for r in json.loads((ROOT/'data/Sanctuary/Balance/necromancer-summons.json').read_text())};fields={}
    for name in ('strafearrow','strafebolt'):
        f=dict(pSrvDoFunc=1,pSrvHitFunc=0,pSrvDmgFunc=0,ToHit=1,HitClass=10,GetHit=1,NextHit=1,NextDelay=4,SrcDamage=128)
        assert all(rows[name].get(k,0)==v and runtime[name].get(k,0)==v for k,v in f.items());fields[name]=f
    retained={}
    for name in ('engine','mode-engine','timing-engine','action-engine'):
        p=ROOT/f'tools/reference/strafe-{name}-evidence.json';r=json.loads(p.read_text());assert r['referenceVersion']=='3.3.93847';probe=ROOT/r['probe']['path'];assert hashlib.sha256(probe.read_bytes()).hexdigest()==r['probe']['sha256'];retained[str(p.relative_to(ROOT))]=hashlib.sha256(p.read_bytes()).hexdigest()
    p=ROOT/'tools/reference/magic-arrow-contact-source-review.json';r=json.loads(p.read_text());functions={k:v for k,v in r['functions'].items()if k in ('MISSMODE_SetDamageFlags','MISSMODE_SrvDmgHitHandler','MISSILE_CalculateDamageData','MISSILE_SetDamageStats','SUNITDMG_ExecuteEvents','SUNITDMG_ExecuteMissileDamage','SUNITDMG_IsHitSuccessful','SUNITDMG_PreventMonsterHeal')}
    result=dict(referenceVersion='3.3.93847',legacyCommit=r['legacyCommit'],functions=functions,sharedImpactReviewSha256=hashlib.sha256(p.read_bytes()).hexdigest(),currentMissilesSha256=sha,missileFields=fields,retainedSourceEvidence=retained,established=['Current arrows/bolts use full SrcDamage128,AR,GetHit1/HitClass10 and no damage callback. Reuse the released weapon packet,source poison/drain/cold,captured armor,successful accuracy PMH and pre-HP missile item hit events.','Keep existing Strafe start/quantity/mana,per-emission current stat/rank/AR capture,50percent rollback and identity selector. One series reuses its cast identity across arrows.','The existing series-local four-frame lease survives an empty flight gap and saved reload. Contacts refused by that lease must not allocate proc streams,roll chances,deal damage or dispatch weapon events. Independent casts retain the current separate scope.','Source mode-entry restrictions do not by themselves establish STATE_UNINTERRUPTABLE. Retain approved noninterrupting item casts in this route;do not equate its movement/input lock with the separate Whirlwind state.'],remaining=['Archived current rows plus complete older damage/sequence functions and original calculator evidence are scoped evidence,not exact modern executable parity.','Modern cross-cast NHD/provider/owner/stat/seed/geometry,input queuing/restart and exceptional/uninterruptible/death-delay/hireling/PvP contexts remain explicit.','Whole named acquisition,remaining javelin/Blade Fury/Mirrored/NPC missile consumers,full class/client and separate effect/audio/icon assets remain open.'])
    out=ROOT/'tools/reference/strafe-contact-source-review.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Strafe full-source rows bound to shared weapon impacts;existing sequence,NHD and mode boundaries retained.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
