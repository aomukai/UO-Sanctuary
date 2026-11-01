#!/usr/bin/env python3
"""Bind player Static Field acceptance; preserve explicit modern receiver gaps."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import subprocess
import sys

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from review_weapon_event_order import function
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/sorceress-static-field'
BUILD=5
sha=inventory.sha


def historical(path):return subprocess.check_output(['git','show','53e84cf:'+path],cwd=ROOT)


def record(check=False):
    inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
    counts=markers(STAGE/'birth-native-checks.log');log=(STAGE/'birth-native-checks.log').read_text()
    assert sum(counts.values())==245 and ' ERR]' not in log and ': failed' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
    sys.path.insert(0,str(ROOT/'tests'))
    from check_static_field import configuration
    config=configuration(True);assert len(config['extra_families'])==22 and len(config['extra_checks'])==6
    for name in config['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
    for name in config['extra_checks']:assert counts[name]==3
    for spell in ('fire-bolt','ice-bolt','ice-blast','glacial-spike','fire-ball','nova','frost-nova','static-field'):
        assert counts[spell+'-reload-result']==2
        for mode in ('right','quick'):
            assert counts[spell+'-'+mode+'-check']==counts[spell+'-'+mode+'-result']==3
            assert log.count(' '+spell+'-'+mode+': windup')==3
    for name in ('mage-glacial-live','glacial-player-live','mage-static-live'):assert counts[name+'-result']==3
    assert 'Player Static:16200old wrapper/callback/percentage cases' in log
    saved=json.loads((STAGE/'static-field-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['Life']==192000 and saved['PendingAt']>0 and saved['Mana']==saved['OriginalMana']-4608
    full=ROOT/'tools/reference/d2r-named-equipment/json/skills.json'
    data=ROOT/'data/Sanctuary/Balance/necromancer-summons.json';assert data.read_bytes()==historical(str(data.relative_to(ROOT)))
    status=ROOT/'data/Sanctuary/Balance/class-skill-status.json';now=json.loads(status.read_text());before=json.loads(historical(str(status.relative_to(ROOT))))
    updated=next(x for x in now['skills'] if x['talentId']=='sorceress.staticfield');old=next(x for x in before['skills'] if x['talentId']=='sorceress.staticfield')
    assert old['verification']==updated['verification']=='partial-native-coverage'
    old.update({k:updated[k] for k in ('verification','remaining')});assert before==now and not now['completedClasses']
    parent=ROOT/'tools/reference/sorceress-nova-native-evidence.json';mage=ROOT/'tools/reference/mage-static-native-evidence.json'
    source=ROOT/'tools/reference/static-field-source-evidence.json';metadata=ROOT/'tools/reference/skill-do-table-candidate-evidence.json'
    for p in (parent,mage):assert p.read_bytes()==historical(str(p.relative_to(ROOT)))
    evidence=json.loads(source.read_text());candidate=json.loads(metadata.read_text())
    assert evidence['cases']==16200 and evidence['delivered']==2565 and evidence['negativeResistanceCases']==1026 and not evidence['modernExecutableVerified']
    assert candidate['staticCorroboration']['tableCandidateRva']=='0x163a8b0' and candidate['bytesRead']==0 and candidate['processMemoryRead'] is False
    assert all(not candidate[k] for k in ('processInput','ptraceAttach','securitySettingsChanged','gameMemoryWritten','dispatcherIndexLinkVerified','callbackSemanticsVerified'))
    assert next(x for x in candidate['observedPermissions'] if x['slot']==20)['completeCandidateReadable'] is False
    for name,digest in {**evidence['artifacts'],**candidate['artifacts']}.items():assert sha(ROOT/name)==digest,name
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text());assert tree['sha']==evidence['legacyCommit']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    reviewed={};sources=[]
    for relative,names in (
        ('source/D2Game/src/SKILLS/SkillSor.cpp',('SKILLS_SrvDo020_StaticField','SKILLS_AuraCallback_StaticField')),
        ('source/D2Game/src/MONSTER/MonsterUnique.cpp',('MONSTERUNIQUE_CalculatePercentage',)),
        ('source/D2Common/include/D2Combat.h',())):
        p=ROOT/'.local/class-skills/d2moo-pinned-source'/relative;blob=p.read_bytes();sources.append(p)
        assert hashlib.sha1(b'blob '+str(len(blob)).encode()+b'\0'+blob).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==relative)
        assert sha(p)==evidence['sourceHashes'][relative]
        for name in names:reviewed[name]=hashlib.sha256(function(blob.decode(),name).encode()).hexdigest()
    assert reviewed==evidence['completeFunctionHashes']
    prior=json.loads((ROOT/'.local/class-skills/sorceress-nova/birth-inputs.json').read_text())
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert tested['artifacts'][name]==prior['artifacts'][name]
    assert tested['copiedData'].keys()==prior['copiedData'].keys()
    assert {p for p in prior['copiedData'] if prior['copiedData'][p]!=tested['copiedData'][p]}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for kind in ('content','integration'):
        build=(STAGE/f'{kind}-build-{BUILD}.log').read_text();assert 'Build succeeded.' in build and '0 Warning(s)' in build and '0 Error(s)' in build
    audited=inventory.validate_inventory(STAGE/'audit-inputs.json');assert audited['artifacts']==tested['artifacts'] and audited['copiedData']==tested['copiedData']
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text();sizes=[]
    for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
        audit=json.loads((STAGE/name).read_text());assert audit==json.loads((ROOT/'tools/reference'/name).read_text())
        assert audit['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in audit['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(historical('tools/reference/'+name));identity=('InputHashes','ContentAssemblySha256','ContentAssemblyPath')
        assert {k:v for k,v in audit.items() if k not in identity}=={k:v for k,v in old.items() if k not in identity}
        if 'ContentAssemblyPath' in audit:assert (ROOT/audit['ContentAssemblyPath']).resolve()==(STAGE/'content/Sanctuary.Content.dll').resolve()
        sizes.append(len(audit['InputHashes']))
    python=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in python and '\nOK\n' in python and 'FAILED' not in python
    checks=(STAGE/f'checks-{BUILD}.log').read_text()
    for message in ('Named readiness: 573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert message in checks
    assert 'Traceback' not in checks
    assert '16200complete old wrapper/callback/percentage cases' in (STAGE/'source-recheck.log').read_text()
    assert 'Traceback' not in (STAGE/'source-recheck.log').read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    files=[Path(__file__).resolve(),ROOT/'tests/check_static_field.py',ROOT/'tools/probe_static_field_source.py',ROOT/'tools/probe_skill_do_candidates.py',ROOT/'tools/build_skill_roadmap.py',
        ROOT/'tools/review_weapon_event_order.py',ROOT/'.local/class-skills/d2moo-source-tree.json',full,data,parent,mage,source,metadata,*sources,
        *(ROOT/'docs'/name for name in ('sorceress-static-field-checkpoint.md','sorceress-passives-preflight.md','class-skills-checkpoint-2026-10-22.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','static-field-fixtures.json','mage-static-fixtures.json',
            'source-recheck.log','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
        *(ROOT/'tools/reference'/name for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
    result=dict(referenceVersion='3.3.93847',component='Player Static Field input/resources/save acceptance and shared Mage receiver',installed=False,graphicalClientVerified=False,completedClasses=[],
        referencePages=['https://diabloforfans.com/skill/static-field?lang=enUS','https://classic.battle.net/diablo2exp/skills/sorceress-lightning.shtml'],
        verified=dict(nativeMarkers=245,boots=3,actualReloads=2,saves=3,oldSourceCases=16200,oldPreparedDeliveries=2565,oldNegativeResistancePaths=1026,independentRanks=255,
            staticRightQuickActions=6,retainedRightQuickActions=42,postLoadFreshStaticCasts=2,mageStaticLiveSessions=3,fixtureFamilies=22,sharedChecks=6,auditMarkers=4,auditFingerprints=sizes,pythonTests=71,unchangedInstalledArtifacts=106),
        legacySource=dict(commit=tree['sha'],completeFunctions=reviewed,scope='Complete old wrapper/callback/percentage execution with declared enumeration/stat/typed/event services;native preparation comparisons are separate. Current negative-resistance compensation/floor/event order remains unresolved.'),
        currentCandidate=dict(table='0x163a8b0',slot=20,rva='0x43d080',bytesRead=0,callbackSemanticsVerified=False,processInput=False,protectedPagesRead=False),
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['Static player admission refuses dead/internal/nonworld casters before payment. Existing instant shared caster-centred player/Mage receiver,constant9mana,radius-only scaling,fractional preparation,resistance,immunity and designated-facet floors are exercised.',
            'Actual Eschuta/hard mastery/potion controls retain no enhancement of Static damage. The current native negative-resistance amplification is characterized explicitly as a parity gap rather than claimed as current D2 behavior.',
            'Six real inputs,two post-load fresh casts and canceled pending actions retain held wounds,right/quick bindings and exact controlled mana debits. Existing fixture eligibility hook suppresses ambient regeneration only for StaticFieldFixture;gameplay regeneration is unchanged.',
            'Accepted Sorceress/Poison Nova and shared Mage Static proc tests remain,including three natural Mage Static sessions. Only Static verification/remaining metadata changes runtime data;installed artifacts and saves are unchanged.'],
        remaining=['Exact modern negative-resistance precompensation,pierce/Sunder/absorb/MDR and event/floor order remain unverified. Current callback code is protected/unreadable;metadata inspection did not read process memory or establish dispatcher/semantic identity.',
            'Native aura filter/geometry/ordering and special/PvP recipients,full graphical acceptance and actual old-DLL upgrade/unheld concurrent saves remain open. Controlled saved mana excludes ambient regeneration and does not establish an offline resource policy.',
            'Passives,remaining lightning lifecycle and Inferno held-input/payment/release remain unfinished. Shared projectile terrain and ordinary cold-shatter gaps are separate. Effects/audio/tree/hotkey assets remain independently pending;no class certificate or deployment.'])
    output=ROOT/'tools/reference/sorceress-static-field-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Static Field:245native markers/3boots/2reloads,six real casts/two fresh postload casts,retained Mage/Sorceress,four audits/71Python;modern receiver gaps explicit;no class certificate/deployment.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
