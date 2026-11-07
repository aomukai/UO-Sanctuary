#!/usr/bin/env python3
"""Bind Arctic's approved potion additions to existing spell delivery."""
import argparse
import json
from pathlib import Path
import subprocess
import skill_checkpoint_evidence as e

PARENT='ea328567f6ded3ab08b1602eef87a045b3915dc1'


def review(check=False):
    inputs={}
    def pin(path):
        inputs[str(path.relative_to(e.ROOT))]=e.sha(path)
        return e.load(path) if path.suffix=='.json' else None
    proof=pin(e.ROOT/'tools/reference/arctic-stream-source-evidence.json')
    assert proof['referenceVersion']=='3.3.93847' and not proof['currentExecutableParity']
    assert proof['verified']['damageContexts']==4590 and proof['verified']['factoryContexts']==14280
    for name,h in proof['pinnedInputHashes'].items():
        p=e.ROOT/'tools/reference/d2r-named-equipment/json'/f'{name}.json'
        assert e.sha(p)==h,p
        pin(p)
    for relative,h in proof['sourceHashes'].items():
        p=e.ROOT/'.local/class-skills/d2moo-pinned-source'/relative
        assert e.sha(p)==h,p
        pin(p)
    unavailable=[]
    for relative,h in proof['artifacts'].items():
        p=e.ROOT/relative
        if not p.is_file():unavailable.append(relative);continue
        assert e.sha(p)==h,p
        pin(p)
    for relative in ['src/Sanctuary.Content/DruidArcticStream.cs','src/Sanctuary.Content/SorceressSpellMissiles.cs',
                     'src/Sanctuary.Content/DruidMoltenBoulder.cs','src/Sanctuary.Content/DruidFirestorm.cs',
                     'src/Sanctuary.Content/SkillCombat.cs','src/Sanctuary.Content/CombatPotions.cs',
                     'src/Sanctuary.Content/DefensiveArmors.cs','src/Sanctuary.Content/NamedElements.cs',
                     'src/Sanctuary.Content/TimedEffects.cs','src/Sanctuary.Content/ShadowEquipmentResources.cs',
                     'tests/Sanctuary.Integration/ArcticPotionChecks.cs','tests/Sanctuary.Integration/ArcticStreamChecks.cs',
                     'tests/check_arctic_potion.py','tools/build_skill_roadmap.py',
                     'docs/arctic-approved-input-rule.md',
                     'data/Sanctuary/Balance/class-skill-status.json','data/Sanctuary/Balance/restoration-potions.json']:
        pin(e.ROOT/relative)
    before=json.loads(subprocess.check_output(['git','show',PARENT+':data/Sanctuary/Balance/class-skill-status.json'],cwd=e.ROOT))
    after=e.load(e.ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    old=next(r for r in before['skills'] if r['talentId']=='druid.arcticblast')
    new=next(r for r in after['skills'] if r['talentId']=='druid.arcticblast')
    assert old['remaining']!=new['remaining']
    old['remaining']=new['remaining']
    assert before==after,'Unrelated class status changed'
    assert new['implementation']=='blocked' and sum(r['implementation']=='handler-present'for r in after['skills'])==203
    pin(Path(__file__).resolve())
    result=dict(referenceVersion='3.3.93847',parent=PARENT,inputs=inputs,
        reusedDamageContexts=4590,reusedFactoryContexts=14280,unavailableHistoricalArtifacts=unavailable,
        sourceProbeRerun=False,handlerEnabled=False,currentExecutableParityVerified=False,
        arcticTimingApprovalInferred=False,arcticTimingRuleApproved=True,modifiersCapturedAtBirth=True,potionReadAtContact=True,
        legacyModifiersInvented=False,scope=[
            'Arctic uses the existing elemental spell receiver for defenses, independent resistance/absorb/DamageRate41, cold length, recovery and completion. Its original mastered cold is retained; T29 adds fire/cold/lightning/poison components through the existing approved potion helper.',
            'New emissions capture elemental masteries once, while the potion bonus is read at each contact timestamp. Taking off the original mastery item cannot rewrite an in-flight packet; potion application/expiry can change its later additions.',
            'The optional V0 modifier extension leaves historical emissions readable. Without captured masteries, a legacy emission retains pure cold when no potion is active and explicitly refuses potion contact. Restore rejects a frozen potion bonus before replacing the active pool.',
            'The existing cold/factory case files and pinned source hashes are rechecked. Historical ABI/C++/compiler/sanitizer files listed as unavailable are absent; the old C++ probe is not rerun here. Native comparisons retain those historical oracle cases as regression controls. The Sanctuary potion addition is not represented as a D2R mechanic or as current executable parity.'
        ],limits=[
            'The user explicitly approved the proposed Arctic held-input rule. Its integration is still separate; no learned Arctic handler,binding or hotkey is enabled by this potion change.',
            'Modern Slow Missiles damage order and owner/death/facet/global-event/client acceptance retain their existing boundaries.',
            'Shadow Fire Blast was deferred after the parked-only original resource guard was confirmed. Real Shadow world admission, incoming combat/events/AI and learned casting must precede a live NPC spell host.',
            'Existing stream/audio placeholders and tree/hotkey icon work remain separate; this reuse adds no asset.'
        ])
    path=e.ROOT/'tools/reference/arctic-potion-review-evidence.json';rendered=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==rendered,'Arctic potion review stale'
    else:path.write_text(rendered)
    print('Arctic potion reuse reviewed;203handlers,legacy mastery not invented,held timing and Shadow world admission remain separate.')


if __name__=='__main__':
    p=argparse.ArgumentParser(description=__doc__);p.add_argument('--check',action='store_true');review(p.parse_args().check)
