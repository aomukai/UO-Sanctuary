#!/usr/bin/env python3
"""Authenticate the approved Health Link component and current pet regressions."""
import argparse,json
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/defiler-health-link'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-7.json',STAGE/'birth-inputs.json')
    for name,h in born['sourceInputs'].items():assert e.sha(ROOT/name)==h,name
    first=pin(STAGE/'source-snapshot-5.json')['inputs'];last=pin(STAGE/'source-snapshot-7.json')['inputs']
    assert first['copiedData']==last['copiedData']and first['projectInputs']==last['projectInputs']
    assert {n for n in first['sourceInputs']if first['sourceInputs'][n]!=last['sourceInputs'][n]}=={'tests/Sanctuary.Integration/DefilerHealthLinkChecks.cs'}
    for kind,build in (('content',3),('integration',5)):
        path=STAGE/f'{kind}-build-{build}.log';pin(path);assert all(s in path.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    parent=pin('tools/reference/defiler-link-numbers-native-evidence.json')
    approval=pin('tools/reference/defiler-health-link-rule-approval.json');assert approval['approved']
    pin(approval['proposal']);assert e.sha(ROOT/approval['proposal'])==approval['proposalAtApprovalSha256']
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][name]==parent['artifacts'][name]==e.sha(STAGE/'native/Distribution'/name)
    expected=Counter(parent['markers']);expected.update({'defiler-body-check':1,'defiler-body-reload-check':2,
        'defiler-health-link-check':1,'defiler-health-link-reload-check':2,'defiler-health-link-check-result':1,'defiler-health-link-reload-check-result':2})
    text=e.cohort(STAGE,expected,78);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-5.log')
    assert text.count('Defiler links:own target/cap/expiry,')==3
    assert text.count('Direct damage lacks settled typed components')==3
    assert text.count('Client: 127.0.0.1: Connected.')==27
    assert pin(STAGE/'defiler-health-link-fixtures.json')['Generation']==2
    for index in (1,2,3,4):
        for path in sorted((STAGE/f'diagnostics-{index}').iterdir()):pin(path)
    for path in sorted((STAGE/'diagnostics-build-1').iterdir()):pin(path)
    for name in ('src/Sanctuary.Content/DefilerHealthLink.cs','src/Sanctuary.Content/DirectDamageParts.cs',
        'src/Sanctuary.Content/SkillDamageEvents.cs','src/Sanctuary.Content/SkillHealth.cs','src/Sanctuary.Content/SkillCombat.cs',
        'src/Sanctuary.Content/PreparedMonsterDamage.cs','src/Sanctuary.Content/NpcEquipmentRandom.cs',
        'src/Sanctuary.Content/WarlockDefilerBody.cs','src/Sanctuary.Content/WarlockGoatmanWorld.cs',
        'tests/Sanctuary.Integration/DefilerHealthLinkChecks.cs','tests/Sanctuary.Integration/DefilerBodyChecks.cs',
        'tests/check_defiler_health_link.py','tools/record_defiler_health_link.py','docs/defiler-health-link-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md',
        'docs/skill-effects.md','docs/skill-icons.md'):pin(name)
    protected=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Approved Defiler Health Link groups and typed sharing',handlers=223,warlockHandlers=14,
        completedClasses=[],learnedDefilerEnabled=False,automaticLinkingEnabled=False,healthLinkRuleApproved=True,
        nativeMarkers=78,markers=dict(expected),actualInputs=12,authenticatedSessions=27,boots=3,reloads=2,saves=3,
        originalSavedGroupReactivations=2,contentBuild=3,integrationBuild=5,sourceSnapshot=7,nativeRun=5,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceIds=0,serializationVersionChanged=False,newLiveReferenceReads=0,
        artifacts=born['artifacts'],preservation={**protected,'installed':False},
        established=['Original per-Defiler saved groups;target-first capacity/expiry,no refresh and cross-group overwrite prevention.',
            'Full direct hit retained;per-component FP8 copies use recipient defenses/immunity and actual Defiler/owner credit,without recursion or attack events.',
            'Actual shared fire route,range retention,periodic exclusion,offline suppression,malformed ownership and explicit untyped refusal pass.',
            'Lethal secondary damage keeps death/credit;original saved group resumes after each reload without healing or deadline reset.',
            'Current learned Goatman/Tainted actual-input,automatic host,pool/mana/source/RNG/save regressions remain green.'],
        limits=['Current do162/event38 equivalence is not claimed;complete policy is an approved Sanctuary adaptation.',
            'Scalar-only direct producers fault sharing explicitly;remaining typed producer and recipient coverage needs completion before certification.',
            'Defiler flying movement,automatic AI/link release and learned casting are unfinished;no additional handler or certified class.',
            'Final summon/link/expiry effects,audio and talent/right/quick icons remain separately pending.'],inputs=inputs)
    path=ROOT/'tools/reference/defiler-health-link-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Defiler Health Link evidence stale'
    else:path.write_text(encoded)
    print('Defiler Health Link:78markers/12currentpetinputs/2reloads;original groups and typed sharing retained;installed build/saves protected.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
