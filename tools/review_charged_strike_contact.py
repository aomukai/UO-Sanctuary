#!/usr/bin/env python3
"""Bind Charged Strike source0 bolt packet flags and retained factory/initializer evidence."""
import argparse,hashlib,json
from review_weapon_event_order import ROOT,STAGE,function
from review_magic_arrow_contact import record as parent

def record(check=False):
    parent(True);tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());records={}
    for path,names in {'source/D2Game/src/MISSILES/MissMode.cpp':['MISSMODE_SetDamageFlags','MISSMODE_SrvDmgHitHandler'],'source/D2Game/src/UNIT/SUnitDmg.cpp':['SUNITDMG_ExecuteEvents','SUNITDMG_ExecuteMissileDamage'],'source/D2Game/src/SKILLS/SkillAma.cpp':['SKILLS_SrvDo011_ChargedStrike'],'source/D2Game/src/SKILLS/SkillSor.cpp':['SKILLS_MissileInit_ChargedBolt']}.items():
        raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha']for x in tree['tree']if x['path']==path)
        for name in names:body=function(raw.decode(),name);records[name]=dict(path=path,sha256=hashlib.sha256(body.encode()).hexdigest())
    cache=ROOT/'tools/reference/d2r-named-equipment';manifest=json.loads((cache/'sources.json').read_text());p=cache/'json/missiles.json';sha=hashlib.sha256(p.read_bytes()).hexdigest();assert sha==manifest['files']['json/missiles.json']['sha256'];row=json.loads(p.read_text())['chargedstrikebolt'];runtime=next(r['fields']for r in json.loads((ROOT/'data/Sanctuary/Balance/necromancer-summons.json').read_text())if r['key']=='chargedstrikebolt')
    fields=dict(pSrvDoFunc=1,pSrvHitFunc=0,pSrvDmgFunc=0,ToHit=0,HitClass=0,GetHit=1,ResultFlags=4,HitFlags=0,SrcDamage=0,Range=98,Size=1,CollideKill=1,LastCollide=1,InitSteps=1,Activate=3,CanSlow=1)
    assert all(row.get(k,0)==v and runtime.get(k,0)==v for k,v in fields.items())
    skill=next(r for r in json.loads((cache/'json/skills.json').read_text()).values()if r['skill']=='Charged Strike');assert skill.get('SrcDam',0)==0
    evidence={}
    for n in ('charged-strike-engine-evidence','charged-strike-velocity-source-evidence','amazon-thrust-contact-source-review'):
        p=ROOT/f'tools/reference/{n}.json';evidence[str(p.relative_to(ROOT))]=hashlib.sha256(p.read_bytes()).hexdigest()
    source=json.loads((ROOT/'tools/reference/charged-strike-engine-evidence.json').read_text());probe=ROOT/source['probe']['fixture'];evidence[str(probe.relative_to(ROOT))]=hashlib.sha256(probe.read_bytes()).hexdigest()
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],functions=records,currentMissilesSha256=sha,missileFields=fields,retainedSourceEvidence=evidence,established=['Missing skill SrcDam remains0 in missile creation and excludes weapon stats;the melee FillDamageValues zero-to128 default belongs only to the separately qualified thrust.','Source0 bolt uses Successful1/GetHit4/HitClass0/HitFlags0,so elemental HP/absorb/credit/kill/native return apply without weapon hit/attack callbacks,Crushing Blow,drain,poison,cold,slow,armor or proc RNG allocation. No accuracy or physical shield roll belongs to this packet.','Retain current source initializer seed,curved path,first-collision removal,77frame cap and optional factory-speed snapshots/legacy normal-speed fallback. Per-bolt contacts remain independent. Melee miss/block/lethal emission and startup payment remain unchanged.'],remaining=['This binds archived current flags and complete older functions/probes,not exact modern damage callbacks,geometry/Size/shotgun/activation/global scheduling,seed/stat/owner/provider/PvP equivalence.','Slow Missiles damage capture/order remains explicitly unresolved;this increment preserves existing damage endpoints and fixes their delivery only.','Exceptional/uninterruptible/death-delay/hireling contexts,Lightning Strike chain delivery,full named/class/client and separate effects/audio/icons remain open.'])
    out=ROOT/'tools/reference/charged-strike-contact-source-review.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Charged Strike source0/HitFlags0/GetHit1/HitClass0 delivery bound;existing factory,motion and melee remain separate.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
