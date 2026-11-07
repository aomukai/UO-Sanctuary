#!/usr/bin/env python3
"""Seal the separately approved learned Dragon Claw action and retained checks."""
import argparse
import json
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation

ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'dragon-claw-action'
PARENT='950be533d4877a31dd17dcc330cdda28fa8142cc'


def record(check=False):
    broad=BASE/'dragon-claw-action-broader';iso=BASE/'dragon-claw-action-isolate'
    born=e.inventory(STAGE,broad,STAGE/'source-snapshot-2.json')
    previous=e.load(ROOT/'tools/reference/dragon-talon-action-native-evidence.json')
    added=Counter({'dragon-claw-preparation-check':1,'dragon-claw-preparation-reload-check':2,
                   'dragon-claw-check':1,'dragon-claw-reload-check':2,'dragon-claw-mechanics-check':3})
    added.update({f'dragon-claw-{mode}{suffix}':3 for mode in ['left','right','quick'] for suffix in ['-check','-result']})
    for old,stage,count in [('dragon-talon-action',STAGE,114),('dragon-talon-action-broader',broad,330)]:
        oldlog=BASE/old/'birth-native-checks.log';assert e.sha(oldlog)==previous['files'][str(oldlog.relative_to(ROOT))]
        e.cohort(stage,markers(oldlog)+added,count)
    base=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    e.cohort(iso,base+added,36)
    for stage in [STAGE,broad,iso]:
        assert 'pass across3boots/2reloads' in(stage/'run-2.log').read_text()
        snapshot(STAGE/'source-snapshot-2.json',stage/'birth-inputs.json')
        tested=e.load(stage/'birth-inputs.json');assert tested['artifacts']==born['artifacts'] and tested['copiedData']==born['copiedData']
        for p,h in tested['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
        saved=e.load(stage/'dragon-claw-fixtures.json')
        assert saved['Charge']['Count']==2 and saved['Mana']==(100-2)*256+17 and saved['PendingMana']==saved['Mana']
        assert saved['Owner']!=saved['PendingOwner'] and saved['Target']!=saved['PendingTarget'] and saved['Primary']!=saved['Secondary']
        assert saved['Health']>0 and saved['PendingHealth']==50000*256
        prepared=e.load(stage/'dragon-claw-preparation-fixtures.json')
        assert prepared['Generation']==2 and prepared['Mana256']==20*256+31 and prepared['PhysicalPercent']>0
        assert prepared['Plan']['Profile']=='original-calculator-b7d7910-dual-dragon-claw-v1'
    for name in ['content-build-3.log','integration-build-3.log']:
        assert all(s in(STAGE/name).read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    commands=e.load(STAGE/'verification-commands.json');assert len(commands)==8 and all(r['exitCode']==0 for r in commands)
    assert 'Ran 71 tests' in(STAGE/'python-tests.log').read_text() and '\nOK\n' in(STAGE/'python-tests.log').read_text()
    fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation()
    assert protected==e.load(STAGE/'preservation-before.json')
    client=e.load(ROOT/'tools/reference/blade-fury-held-input-client-evidence.json');assert len(client['patchHashes'])==43
    for group in ['patchHashes','sharedInputs']:
        for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
    source=e.load(ROOT/'tools/reference/assassin-claw-modern-rate-evidence.json')
    assert source['fixture']['cases']==4608 and source['fixture']['dragonControls']==2304
    assert e.sha(ROOT/source['fixture']['path'])==source['fixture']['sha256']
    legacy=e.load(ROOT/'tools/reference/dragon-claw-source-evidence.json')
    assert legacy['fixture']['cases']==1152 and legacy['fixture']['contacts']==2304
    assert e.sha(ROOT/legacy['fixture']['path'])==legacy['fixture']['sha256']
    for name in ['modern-rate-source-check.log','contact-source-check.log']:
        assert 'pass' in(STAGE/name).read_text()
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert sum(r['implementation']=='handler-present' for r in status['skills'])==207 and not status['completedClasses']
    assert sum(r['implementation']=='handler-present' and r['characterClass']=='Assassin' for r in status['skills'])==27
    claw=next(r for r in status['skills'] if r['talentId']=='assassin.dragonclaw')
    assert claw['implementation']=='handler-present' and claw['verification']=='partial-native-coverage' and not claw['complete']
    effect=next(r for r in e.load(ROOT/'data/Sanctuary/Balance/skill-effects.json') if r['talentId']==claw['talentId'])
    icon=next(r for r in e.load(ROOT/'data/Sanctuary/Balance/skill-icons.json') if r['talentId']==claw['talentId'])
    assert effect['effects']==icon['talentIcon']==icon['hotkeyIcon']=='needed' and 'dual-claw' in effect['placeholder']
    files=e.stage_files(STAGE,broad,iso)+[Path(__file__).resolve(),ROOT/'tests/check_dragon_claw.py',ROOT/'tests/check_dragon_claw_preparation.py',ROOT/'tools/reference/dragon-claw-preflight.json',ROOT/'docs/dragon-claw-approved-action-rule.md']
    files += [p for p in (STAGE/'frozen-c2-i2').rglob('*') if p.is_file() and p.suffix in ['.json','.log']]
    files += [ROOT/'docs'/name for name in ['dragon-claw-action-checkpoint.md','dragon-claw-action-proposal.md','dragon-claw-preparation-checkpoint.md',
        'class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-effects.md','skill-icons.md','skill-mechanical-reference.md']]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,handlerEnabled=True,installed=False,
        approvedAdaptation='Approve this Dragon Claw rule',currentExecutableTimingVerified=False,
        clockProfile='original-calculator-b7d7910-dual-dragon-claw-v1',handlers=207,assassinHandlers=27,completedClasses=[],
        contentBuild=3,integrationBuild=3,sourceSnapshot=2,focusedMarkers=114,broaderMarkers=330,isolatedMarkers=36,
        bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,pythonTests=71,verificationCommands=8,
        auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=43,
        artifacts=born['artifacts'],copiedData=born['copiedData'],
        candidateArtifacts={str(p.relative_to(ROOT)):e.sha(p) for p in (STAGE/'frozen-c2-i2').rglob('*.dll')},
        scope=['Separately approved two-claw human sequence captures its distinct dualOnly clock,pays2mana once,retains the initial target and rolls each selected-hand contact independently.',
               'Actual primary/secondary weapon packets use current skill ED/hard Mastery,Tiger/Cobra,ordinary critical,typed/ordered poison,source result/contact/return and progressive helpers. Accuracy-only charge bypass retains block/dodge;each successful unblocked contact consumes one per family.',
               '1152eligible original-author dual rates/192complete source dual clocks,real whole IAS claw/Bartuc/Tarnhelm,hard-only damage synergy,hand restoration and unchanged original rolls/resources survive both reloads.',
               'Native action checks cover2contacts/final recovery,once-only fractional debit,correct success2/miss0hitflags,independent selected-hand physical packets,charge exhaustion/miss/block/absorb,ordinary critical,Ice freeze,6interruptions,target loss/death,basic fallback and real raw left/right/quick input.',
               'Original actual effective24equipment/bindings/wounds/mana/charges persist;separately paid pending series never replay or refund. Learned Fists/Thunder/Ice,Talon/Tail,Flight components and broader traps remain retained. All573item decisions and installed artifacts/saves/configs/client patches stay unchanged.'],
        candidateHistory=['Preparation C2/I2passed an isolated12marker cohort before the separate action approval;the final learned package reruns its clock/stat/persistence suppliers.',
                          'Initial action C2/I2/snapshot1passed114focused/330retained/36isolated and71Python/eightcommands. Complete source review then caught hitflags2being supplied on misses as well as successes.',
                          'Original C2/I2binaries/data/input/output evidence is archived in frozen-c2-i2. Claw now allocates hitflags0on misses and2on successes,with direct packet assertions. Clean C3/I3/snapshot2and all three final cohorts qualify the correction.'],
        limits=['Exact modern executable timing/stat mutation/charge boundaries/primary-roll sharing and full item/proc/owner/PvP,original geometry/RNG/event/client parity remain unverified.',
                'Player recipients and unsupported shared item/recipient modes are explicitly refused. No borrowed kick-only critical suppression or Fists primary-only timing.',
                'Final directional dual-claw/contacts/progressive/miss/block/interruption/recovery effects/audio and talent/hotkey icons remain identified placeholders. No class completion,deployment or overall goal completion.'])
    e.write('dragon-claw-action-native-evidence.json',result,files,check)
    print('Approved learned Dragon Claw:114focused/330retained/36isolated,3boots/2reloads/3saves each;207handlers/Assassin27;installed build preserved.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
