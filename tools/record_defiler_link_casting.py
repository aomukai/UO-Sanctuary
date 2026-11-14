#!/usr/bin/env python3
"""Authenticate own Defiler S1 casting and real connected pending-action saves."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/defiler-link-casting'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    first=pin(STAGE/'source-snapshot-1.json')['inputs'];last=pin(STAGE/'source-snapshot-2.json')['inputs']
    assert first['projectInputs']==last['projectInputs']and first['copiedData']==last['copiedData']
    assert {n for n in first['sourceInputs']if first['sourceInputs'][n]!=last['sourceInputs'][n]}=={'tests/Sanctuary.Integration/DefilerLinkCastingChecks.cs'}
    for kind,num in (('content',1),('integration',2)):
        log=STAGE/f'{kind}-build-{num}.log';pin(log);assert all(s in log.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    source=pin('tools/reference/defiler-link-cast-source-evidence.json');assert source['cases']==401 and source['baseline']==[100,128,[[1,24,1,0],[2,32,0,0]]]
    for kind in ('inputs','artifacts'):
        for name,h in source[kind].items():assert e.sha(ROOT/name)==h,name;pin(name)
    base='.local/class-skills/d2moo-pinned-source/source/D2Game/src/MONSTER/Monster.cpp';pin(base)
    assert 'STATLIST_SetUnitStat(pUnit, STAT_OTHER_ANIMRATE, 100, 0);'in(ROOT/base).read_text()
    approval=pin('tools/reference/defiler-flight-world-approval.json');assert approval['approved']
    assert e.sha(ROOT/approval['proposal'])==approval['proposalAtApprovalSha256']
    assert e.sha(ROOT/approval['testedCandidateEvidence'])==approval['testedCandidateEvidenceSha256']
    pin('tools/reference/defiler-health-link-rule-approval.json')
    parent=pin('tools/reference/defiler-flying-world-native-evidence.json')
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter(parent['markers']);expected.update({'defiler-link-casting-check':1,'defiler-link-casting-reload-check':2,
        'defiler-link-casting-check-result':1,'defiler-link-casting-reload-check-result':2,'defiler-link-casting-save-result':3})
    text=e.cohort(STAGE,expected,96);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    assert text.count('Defiler S1:401source clocks,24/32baseline,')==3
    assert text.count('Lost Defiler S1 target;current path-point fallback remains unverified')==3
    assert text.count('Client: 127.0.0.1: Connected.')==36
    saved=pin(STAGE/'defiler-link-casting-fixtures.json');assert saved['Generation']==2 and saved['Pending']!=0 and saved['Source']['Mode']==8
    for path in sorted((STAGE/'diagnostics-1').iterdir()):pin(path)
    for name in ('src/Sanctuary.Content/SourceOtherAnimation.cs','src/Sanctuary.Content/DefilerLinkCasting.cs',
        'src/Sanctuary.Content/DefilerHealthLink.cs','src/Sanctuary.Content/DemonNativeCombat.cs',
        'src/Sanctuary.Content/WarlockDefilerBody.cs','src/Sanctuary.Content/DeathMarkWorld.cs',
        'tests/Sanctuary.Integration/DefilerLinkCastingChecks.cs','tests/check_defiler_link_casting.py',
        'tools/probe_defiler_link_cast_clock.py','tools/record_defiler_link_casting.py','data/Sanctuary/Balance/defiler-link-cast.json',
        'docs/defiler-link-casting-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md',
        'docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Own Defiler NPC405 S1 cast and transient action lifecycle',handlers=223,warlockHandlers=14,completedClasses=[],
        learnedDefilerEnabled=False,automaticDefilerAiEnabled=False,healthLinkRuleApproved=True,flightWorldApproved=True,
        sourceClockCases=401,nativeMarkers=96,markers=dict(expected),actualInputs=12,authenticatedSessions=36,
        boots=3,reloads=2,saves=3,actualConnectedMidWindupSaves=3,originalPendingSaveReloads=2,
        contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=2,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,newLiveReferenceReads=0,
        artifacts=born['artifacts'],preservation={**protected,'installed':False},
        established=['Ownedactive405S1 selects unchanged source Other branch;401signed rate/predicate/scheduler cases match;baseline128speed/release24/end32.',
            'Own Other fields use cold/HolyFreeze/Impale/Ward receivers;entryclock excludes masteryIAS,ownerFCR and attack-only modifiers.',
            'Shared demon action owner binds targetedS1;own live405/377 ranks captured at release,full-capacity refused,no payment/damage/retry.',
            'Fractional movement/freeze/mode/logout cancel;lost-target fallback explicitly refused and logged,not invented.',
            'Three actual connected original mid-windup world saves;two reloads normalize discarded mode8,park offline body and preserve original group/body/path/header/wounds/RNG without late link replay.',
            'Accepted Health Link/world flight and learned Goatman/Tainted actual-input/automatic host/source/cap/mana/save regressions retain.'],
        limits=['Normal active used-skill flags0 and complete older source predicates are qualified;current live executable/global frame parity is not claimed.',
            'Lost-target path-point fallback remains explicitly unsupported;automatic type2AI/full saved host and learned Defiler route remain unfinished.',
            'Other-mode consumer is qualified for this S1,not every native mode or legacy slow producer;whole-class certification remains open.',
            'Final summon/S1/link/expiry effects/audio and talent/hotkey icons remain separately pending.'],inputs=inputs)
    path=ROOT/'tools/reference/defiler-link-casting-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Defiler S1 cast evidence stale'
    else:path.write_text(encoded)
    print('Defiler S1:401sourceclocks/96native markers/12currentpetinputs/2actualpending-save reloads;no learned AI route;installed files protected.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
