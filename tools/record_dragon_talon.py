#!/usr/bin/env python3
"""Seal the separately approved learned Dragon Talon action and retained checks."""
import argparse
import json
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation

ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'dragon-talon-action'
PARENT='98966ca4960bf1487e9813530543e32856bb6569'


def record(check=False):
    broad=BASE/'dragon-talon-action-broader';iso=BASE/'dragon-talon-action-isolate'
    born=e.inventory(STAGE,broad,STAGE/'source-snapshot-1.json')
    previous=e.load(ROOT/'tools/reference/dragon-talon-clock-native-evidence.json')
    added=Counter({'dragon-talon-check':1,'dragon-talon-reload-check':2,'dragon-talon-mechanics-check':3})
    added.update({f'dragon-talon-{mode}{suffix}':3 for mode in ['left','right','quick'] for suffix in ['-check','-result']})
    for old,stage,count in [('dragon-talon-clock',STAGE,87),('dragon-talon-clock-broader',broad,303),('dragon-talon-clock-isolate',iso,36)]:
        oldlog=BASE/old/'birth-native-checks.log';assert e.sha(oldlog)==previous['files'][str(oldlog.relative_to(ROOT))]
        e.cohort(stage,markers(oldlog)+added,count)
        assert 'pass across3boots/2reloads' in(stage/'run-1.log').read_text()
        snapshot(STAGE/'source-snapshot-1.json',stage/'birth-inputs.json')
        tested=e.load(stage/'birth-inputs.json');assert tested['artifacts']==born['artifacts'] and tested['copiedData']==born['copiedData']
        for p,h in tested['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
        saved=e.load(stage/'dragon-talon-fixtures.json')
        assert saved['Charge']['Count']==2 and saved['Mana']==(100-6)*256+17 and saved['PendingMana']==saved['Mana']
        assert saved['Owner']!=saved['PendingOwner'] and saved['Target']!=saved['PendingTarget']
        assert saved['Health']>0 and saved['PendingHealth']==50000*256
    for name in ['content-build-4.log','integration-build-5.log']:
        assert all(s in(STAGE/name).read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    commands=e.load(STAGE/'verification-commands.json');assert len(commands)==6 and all(r['exitCode']==0 for r in commands)
    assert 'Ran 71 tests' in(STAGE/'python-tests.log').read_text() and '\nOK\n' in(STAGE/'python-tests.log').read_text()
    fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation()
    assert protected==e.load(STAGE/'preservation-before.json')
    client=e.load(ROOT/'tools/reference/blade-fury-held-input-client-evidence.json');assert len(client['patchHashes'])==43
    for group in ['patchHashes','sharedInputs']:
        for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
    source=e.load(ROOT/'tools/reference/dragon-talon-clock-source-evidence.json')
    assert not source['currentExecutableTimingVerified'] and not source['olderRoutinesExecuted']
    for group in ['inputs','artifacts']:
        for p,h in source[group].items():assert e.sha(ROOT/p)==h,p
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert sum(r['implementation']=='handler-present' for r in status['skills'])==206 and not status['completedClasses']
    assert sum(r['implementation']=='handler-present' and r['characterClass']=='Assassin' for r in status['skills'])==26
    talon=next(r for r in status['skills'] if r['talentId']=='assassin.dragontalon')
    assert talon['implementation']=='handler-present' and talon['verification']=='partial-native-coverage' and not talon['complete']
    effect=next(r for r in e.load(ROOT/'data/Sanctuary/Balance/skill-effects.json') if r['talentId']==talon['talentId'])
    icon=next(r for r in e.load(ROOT/'data/Sanctuary/Balance/skill-icons.json') if r['talentId']==talon['talentId'])
    assert effect['effects']==icon['talentIcon']==icon['hotkeyIcon']=='needed' and 'Wrestle' in effect['placeholder']
    files=e.stage_files(STAGE,broad,iso)+[Path(__file__).resolve(),ROOT/'tests/check_dragon_talon.py',ROOT/'docs/dragon-talon-approved-action-rule.md']
    files += [ROOT/'docs'/name for name in ['dragon-talon-action-checkpoint.md','dragon-talon-action-proposal.md',
        'class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-effects.md','skill-icons.md','skill-mechanical-reference.md']]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,handlerEnabled=True,installed=False,
        approvedAdaptation='Approve this Dragon Talon rule',currentExecutableTimingVerified=False,
        clockProfile='original-calculator-b7d7910-human-talon-v1',handlers=206,assassinHandlers=26,completedClasses=[],
        contentBuild=4,integrationBuild=5,sourceSnapshot=1,focusedMarkers=87,broaderMarkers=303,isolatedMarkers=36,
        bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,pythonTests=71,verificationCommands=6,
        auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=43,
        artifacts=born['artifacts'],copiedData=born['copiedData'],
        scope=['Separately approved human KK clock/count,one6mana entry debit,fixed target and charged accuracy-only/per-successful-contact consume-one policy now reach learned left/right/quick input.',
               'Prepared boot/body/skill-ED,Tiger/Cobra,typed poison/charge and source contact/knockback services are reused;Tail keeps its original payment/timing and retained tests.',
               'Native cases cover4contacts/final recovery,one-time fractional debit,miss/charged hit/shield block/fully absorbed contact,per-contact Tiger/Cobra,single/final knockback,target loss/death,Stop/movement/weapon/boot/recovery/frozen/map cancellation and underfunded basic fallback.',
               'Actual original Tarnhelm/Gorerider/ranks,bindings,wounds/fractional mana and retained charges persist through two reloads. A paid queued series never replays or refunds. Real raw left/right/quick casts use the native update loop.',
               'Original clock/kick/Tail/Flight/charge/trap components remain tested. All573item decisions and installed artifacts/saves/configuration/client patches remain unchanged.'],
        candidateHistory=['First isolated candidate rejected a mana assertion that ignored Cobra mana leech;the strict entry debit test now uses Tiger alone and absorbed contacts independently check Tiger+Cobra.',
                          'Second candidate reached the single-kick case,but its fixture tried to lower rank through the increment-only training helper;rank1now uses its own fresh actor.',
                          'Third candidate passed mechanics/input/initial save but the reload fixture omitted restoring the offline owner to its saved world position;it now restores that position before new input.',
                          'Fourth isolated candidate passed36markers;final source/data freeze and clean Content4/Integration5 rebuild precede all three accepted cohorts. Candidate runs do not count as final package evidence.'],
        limits=['Exact modern executable timing/charge boundaries,full item/proc/owner/PvP and original geometry/RNG/event/client parity remain unverified.',
                'Unclassified native NPC knockback/recovery contexts are refused before entry payment. No invented source profile or silent loss of innate knockback.',
                'Firelizard individual-skill catalogue admission and later empty-ground melee assistance remain separate gaps.',
                'Final kick/series/contact/progressive effects/audio and talent/hotkey icons remain identified placeholders. No class completion,deployment or overall goal completion.'])
    e.write('dragon-talon-action-native-evidence.json',result,files,check)
    print('Approved learned Talon:87focused/303retained/36isolated,3boots/2reloads/3saves each;206handlers/Assassin26;installed build preserved.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
