#!/usr/bin/env python3
"""Bind complete older Whirlwind contact,state and item-trigger functions."""
import argparse,hashlib,json
from review_weapon_event_order import ROOT,STAGE,function

def record(check=False):
    tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    files={'source/D2Game/src/SKILLS/SkillBar.cpp':['SKILLS_SrvSt38_Whirlwind','SKILLS_RemoveWhirlwindStats','SKILLS_SrvDo076_Whirlwind'],
        'source/D2Game/src/UNIT/SUnit.cpp':['sub_6FCBDE90'],
        'source/D2Game/src/UNIT/SUnitDmg.cpp':['SUNITDMG_DrainItemDurability'],
        'source/D2Game/src/SKILLS/SkillItem.cpp':['SKILLITEM_EventFunc20_SkillOnAttackHitKill','SKILLITEM_CastSkillOnTarget','SKILLITEM_CastSkillOnPosition']}
    bodies={};records={}
    for path,names in files.items():
        raw=(STAGE/'d2moo-pinned-source'/path).read_bytes();blob=hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest();assert blob==next(x['sha'] for x in tree['tree'] if x['path']==path)
        for name in names:
            body=function(raw.decode(),name);bodies[name]=body;records[name]=dict(path=path,gitBlob=blob,sha256=hashlib.sha256(body.encode()).hexdigest())
    start=bodies['SKILLS_SrvSt38_Whirlwind'];assert 'sub_6FCBDE90(pUnit, 1)' in start
    do=bodies['SKILLS_SrvDo076_Whirlwind'];anchors=['SUNITDMG_GetResultFlags(','damage.dwEnDmgPct =','SUNITDMG_AllocCombat(','SUNITDMG_DrainItemDurability('];assert [do.index(x)for x in anchors]==sorted(do.index(x)for x in anchors)
    callback=bodies['SKILLITEM_EventFunc20_SkillOnAttackHitKill'];assert callback.index('ITEMS_RollRandomNumber(')<callback.index('SKILLITEM_CastSkillOnTarget(')
    for name in ('SKILLITEM_CastSkillOnTarget','SKILLITEM_CastSkillOnPosition'):assert 'STATE_UNINTERRUPTABLE' in bodies[name]
    result=dict(referenceVersion='3.3.93847',legacyCommit=tree['sha'],scope='Complete pinned older source review,not current executable parity. Existing released2.4.3 cadence remains authoritative over older speed code.',functions=records,
        established=['Whirlwind sets the uninterruptible source state and toggles selected hands around each serial-selected contact.','Do76 resolves ordinary accuracy/block,applies row flags/ED,allocates source128 weapon lanes and drains the contact.','Damage/attack item callbacks gate on successful HitFlags32 and roll source RNG before the item-cast helpers reject an uninterruptible caster. A rejected cast allocates no projectile and consumes no world child seed.','Source death mode is distinct from current HP:Whirlwind retains its active mode at zero life,then settles its pending death when its source state ends.','Shared source poison keeps one mitigated initial rate plus its independent strongest-poison clock;ordinary contacts retain the approved noninterrupting proc rule outside this controller.'],
        remaining=['Current D2R exact cadence/geometry,stat-node/RNG and mode identity remain scoped by existing Whirlwind receipts.','Player recipients retain their previous scalar compatibility route;unsupported NPC guards remain explicit. No other named-item/proc,missile,incoming or whole-class qualification follows.'])
    p=ROOT/'tools/reference/whirlwind-contact-source-review.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert p.read_text()==text
    else:p.write_text(text)
    print('Whirlwind complete older contact/state/proc review bound;modern limitations retained.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
