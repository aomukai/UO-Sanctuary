#!/usr/bin/env python3
"""Bind existing frozen/shatter corpse policy to native animation and Lich revalidation."""
import argparse,hashlib,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]

def record(check=False):
    base=ROOT/'.local/class-skills/unresisted-shields/native/Projects/UOContent';p=base/'Spells/Necromancy/AnimateDeadSpell.cs';text=p.read_text();assert text.count('CorpseRecoveryHooks.CanAnimate?.Invoke(')==2
    assert text.index('CorpseRecoveryHooks.CanAnimate?.Invoke(c) == false')<text.index('else if (CheckSequence())')
    callback=text[text.index('private static void SummonDelay_Callback'):];assert callback.index('CorpseRecoveryHooks.CanAnimate?.Invoke(corpse) == false')<callback.index('var owner = corpse.Owner;')<callback.index('toSummon.CreateInstance<Mobile>()')
    evidence={}
    for n in ('freeze-state-engine-evidence','holy-freeze-engine-evidence','cold-corpse-reference-gap'):
        source=ROOT/f'tools/reference/{n}.json';evidence[str(source.relative_to(ROOT))]=hashlib.sha256(source.read_bytes()).hexdigest()
    result=dict(referenceVersion='3.3.93847',nativeAnimateSource=dict(path=str(p.relative_to(ROOT)),sha256=hashlib.sha256(p.read_bytes()).hexdigest(),hookCalls=2),retainedSourceEvidence=evidence,established=['Existing freeze and Holy Freeze markers are captured as CorpseSkillRecord.Shattered. Shared D2 corpse availability already rejects them;NoSelect is a separate one-consumer reservation.','Native Animate Dead consults the public CanAnimate hook at initial targeting and again before delayed allocation. The old content hook checked NoSelect only. Wild Lich CanRaise likewise omitted Shattered;controlled helper policy has an additional availability check.','Use the existing captured Shattered/NoSelect flags for both bridge guards. Preserve the previous native hook veto,unknown-metadata compatibility,ordinary unshattered raising,loot and all producer chances. No ordinary-cold chance or new shatter marker is introduced.'],primarySources=[dict(url='https://classic.battle.net/diablo2exp/monsters/basics.shtml',scope='Blizzard legacy guide describes corpse loss from some chilled kills and all frozen kills;does not specify current3.3.93847chance.'),dict(url='https://classic.battle.net/diablo2exp/classes/meleeequipment.shtml',scope='Blizzard legacy melee guide confirms ordinary cold can remove revival corpses;does not establish exact chance/timing.')],remaining=['The older ordinary-cold deadCol/raw-SHATTER branch remains individually unsupported. Neither legacy guide is proof of modern3.3.93847chance or executable state routing.','Native UO spell conversion,all corpse consumers/current class/client/final effects and icons remain separate. These bridge guards do not enable new UO spells for D2 classes.'])
    out=ROOT/'tools/reference/corpse-shatter-guard-source-review.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Existing frozen/shatter policy and both actual native animation hook sites bound;ordinary cold chance unchanged.')
if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');record(p.parse_args().check)
