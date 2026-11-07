#!/usr/bin/env python3
"""Seal the explicitly prepared Talon calculator profile without enabling it."""
import argparse
import json
from collections import Counter
from pathlib import Path
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from record_poison_dagger_contact import markers
from record_inferno_held_input import preservation

ROOT=e.ROOT;BASE=ROOT/'.local/class-skills';STAGE=BASE/'dragon-talon-clock'
PARENT='8d352b650d73a4bc301928255fe4697860108abb'


def record(check=False):
    broad=BASE/'dragon-talon-clock-broader';iso=BASE/'dragon-talon-clock-isolate'
    born=e.inventory(STAGE,broad,STAGE/'source-snapshot-2.json')
    source=e.load(ROOT/'tools/reference/dragon-talon-clock-source-evidence.json')
    assert source['rateCases']==490 and source['rankPlanCases']==3315 and source['rollbackRows']==13
    assert source['currentAnimationProfiles']==10 and not source['currentExecutableTimingVerified'] and not source['olderRoutinesExecuted']
    for group in ['inputs','artifacts']:
        for p,h in source[group].items():assert e.sha(ROOT/p)==h,p
    legacy=e.load(ROOT/'tools/reference/dragon-talon-sequence-source-evidence.json')
    assert e.sha(ROOT/'tools/reference/d2r-named-equipment/json/skills.json')==legacy['currentSkillsSha256']
    previous=e.load(ROOT/'tools/reference/dragon-flight-mode-native-evidence.json')
    added=Counter({'dragon-talon-clock-check':1,'dragon-talon-clock-reload-check':2})
    for old,stage,count in [('dragon-flight-mode-native',STAGE,63),('dragon-flight-mode-broader',broad,279)]:
        oldlog=BASE/old/'birth-native-checks.log';assert e.sha(oldlog)==previous['files'][str(oldlog.relative_to(ROOT))]
        e.cohort(stage,markers(oldlog)+added,count)
    e.cohort(iso,Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
                         'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})+added,12)
    for stage in [STAGE,broad,iso]:
        assert 'pass across3boots/2reloads' in(stage/'run-1.log').read_text()
        snapshot(STAGE/'source-snapshot-2.json',stage/'birth-inputs.json')
        tested=e.load(stage/'birth-inputs.json')
        assert tested['artifacts']==born['artifacts'] and tested['copiedData']==born['copiedData']
        for p,h in tested['sourceInputs'].items():assert e.sha(ROOT/p)==h,p
        saved=e.load(stage/'dragon-talon-clock-fixtures.json')
        assert saved['Generation']==2 and saved['Mana256']==20*256+31 and saved['Life256']>0
        assert saved['Primary']!=saved['Secondary'] and saved['Helm'] not in [saved['Primary'],saved['Secondary']]
        state=json.loads(saved['SecondaryState']);assert len(state['Runes'])==1
        assert saved['Plan']['Profile']=='original-calculator-b7d7910-human-talon-v1' and saved['Plan']['Rank']==5
    for name in ['content-build-1.log','integration-build-1.log']:
        assert all(s in(STAGE/name).read_text() for s in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    commands=e.load(STAGE/'verification-commands.json');assert len(commands)==6 and all(r['exitCode']==0 for r in commands)
    assert 'Ran 71 tests' in(STAGE/'python-tests.log').read_text() and '\nOK\n' in(STAGE/'python-tests.log').read_text()
    fingerprints=e.audits(STAGE,PARENT,born['artifacts']);protected=preservation()
    client=e.load(ROOT/'tools/reference/blade-fury-held-input-client-evidence.json');assert len(client['patchHashes'])==43
    for group in ['patchHashes','sharedInputs']:
        for p,h in client[group].items():assert e.sha(ROOT/p)==h,p
    status=e.load(ROOT/'data/Sanctuary/Balance/class-skill-status.json')
    assert sum(r['implementation']=='handler-present' for r in status['skills'])==205 and not status['completedClasses']
    talon=next(r for r in status['skills'] if r['talentId']=='assassin.dragontalon')
    assert talon['implementation']!='handler-present' and talon['verification']=='partial-native-coverage'
    rows=e.load(STAGE/'named-item-runtime-audit.json')['Definitions']
    if isinstance(rows,dict):rows=rows.values()
    fire=next(r for r in rows if r['Id']=='unique/368')
    assert not fire['Available'] and fire['Reasons']==['Effect: skill']
    files=e.stage_files(STAGE,broad,iso)+[Path(__file__).resolve(),ROOT/'tools/probe_dragon_talon_clock.py',
        ROOT/'tests/check_dragon_talon_clock.py',ROOT/'tools/reference/dragon-talon-clock-source-evidence.json',
        ROOT/'src/Sanctuary.Content/DragonTalonTiming.cs',ROOT/'tests/Sanctuary.Integration/DragonTalonClockChecks.cs']
    files += [ROOT/'docs'/name for name in ['dragon-talon-clock-checkpoint.md','dragon-talon-action-proposal.md',
        'class-skill-roadmap.md','class-skills-resume-checkpoint.md','skill-effects.md','skill-icons.md']]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,handlerEnabled=False,installed=False,
        currentExecutableTimingVerified=False,clockProfile='original-calculator-b7d7910-human-talon-v1',
        handlers=205,assassinHandlers=25,completedClasses=[],contentBuild=1,integrationBuild=1,sourceSnapshot=2,
        focusedMarkers=63,broaderMarkers=279,isolatedMarkers=12,bootsPerCohort=3,reloadsPerCohort=2,savesPerCohort=3,
        originalAuthorRateCases=490,originalRollbackRows=13,rankPlanExtensions=3315,ownedKickProfiles=10,
        pythonTests=71,verificationCommands=6,auditFingerprints=fingerprints,protection=protected,clientPatchesUnchanged=43,
        artifacts=born['artifacts'],copiedData=born['copiedData'],
        scope=['Explicit prepared current-author calculator profile reuses existing primary-hand scope,rate and ceiling helpers. It validates KK13/base256/action4 metadata,primary IAS/WSM,zero initial frame,100percent rollback and final full-cycle recovery.',
               'Unmodified selected calculator functions/full rollback block execute within declared metadata/UI/SIAS services. The3315rank extensions are declared extensions of13original rows,not3315actual current game executions.',
               'Actual Tarnhelm crosses rank5to6;whole rune-IAS offhand cannot leak into primary timing and selected scopes restore. Original actor,primary/secondary/helmet,full socketed gear state and exact wounded resources survive two reloads.',
               'Original kick/Tail/Flight and broader component/input checks remain. Preparing clocks consumes no mana,health,durability or active talent binding. All573item decisions and installed artifacts/saves/configs/client patches remain unchanged.'],
        limits=['No learned Talon,actual series,input,target,payment,hit/block/charge consumption or complete typed/leech/proc delivery is enabled. Tail approval doesnot silently extend to Talon.',
                'Older ordinary-entry/rate/rewind bodies are statically reviewed and hash-bound;none are newly executed by this probe. Exact modern executable/frame-origin/rewind and alternate forms remain unverified.',
                'Firelizard individual-skill catalogue admission remains unavailable;no properties are trimmed. A complete existing rune socket route supplies the IAS control instead.',
                'Final kick/series/contact/progressive effects/audio and talent/hotkey icons remain pending placeholders. No class certificate,deployment or overall goal completion.'])
    e.write('dragon-talon-clock-native-evidence.json',result,files,check)
    print('Prepared Talon clock:63focused/279broader/12isolated,3boots/2reloads/3saves each;205handlers unchanged;action policy/learned route remain open.')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
