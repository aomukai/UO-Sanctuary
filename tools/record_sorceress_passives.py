#!/usr/bin/env python3
"""Bind four passive contracts and published modern broken-immunity correction."""
import argparse
from collections import Counter
import json
from pathlib import Path
import subprocess
import sys

import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/sorceress-passives'
BUILD=5
sha=inventory.sha


def historical(path):return subprocess.check_output(['git','show','bfeb032:'+path],cwd=ROOT)


def record(check=False):
    inventory.STAGE=STAGE;tested=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/f'pre-build-{BUILD}.json',STAGE/'birth-inputs.json')
    counts=markers(STAGE/'birth-native-checks.log');log=(STAGE/'birth-native-checks.log').read_text()
    assert sum(counts.values())==250 and ' ERR]' not in log and ': failed' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'pass across3boots/2reloads' in (STAGE/f'full-run-{BUILD}.log').read_text()
    sys.path.insert(0,str(ROOT/'tests'))
    from check_sorceress_passives import configuration
    config=configuration(True);assert len(config['extra_families'])==23 and len(config['extra_checks'])==6
    for name in config['extra_families']:assert counts[name+'-check']==1 and counts[name+'-reload-check']==2
    for name in config['extra_checks']:assert counts[name]==3
    for spell in ('fire-bolt','ice-bolt','ice-blast','glacial-spike','fire-ball','nova','frost-nova','static-field'):
        assert counts[spell+'-reload-result']==2
        for mode in ('right','quick'):
            assert counts[spell+'-'+mode+'-check']==counts[spell+'-'+mode+'-result']==3
            assert log.count(' '+spell+'-'+mode+': windup')==3
    for name in ('mage-glacial-live','glacial-player-live','mage-static-live'):assert counts[name+'-result']==3
    assert counts['sorceress-passives-reload-result']==2
    assert 'Sorceress passives:1020independent rank values' in log
    saved=json.loads((STAGE/'sorceress-passives-fixtures.json').read_text())
    assert saved['Generation']==2 and saved['NativeColdBase']==110 and saved['ColdResistance']<98 and saved['Life']<12800000
    assert len(saved['Ranks'])==len(saved['Values'])==4 and all(x>20 for x in saved['Ranks'])
    rules=ROOT/'tools/reference/sorceress-passive-rules.json';reference=json.loads(rules.read_text())
    assert reference['referenceVersion']=='3.3.93847' and 'not a compiled modern executable' in reference['referenceKind']
    for path,digest in reference['pinnedInputs'].items():assert sha(ROOT/path)==digest,path
    expected={37:(30,12),61:(30,7),63:(50,12),65:(20,5)}
    assert {x['sourceId'] for x in reference['passives']}==expected.keys()
    for row in reference['passives']:
        base,increment=expected[row['sourceId']];assert row['rankValues']==[base+increment*(rank-1) for rank in range(1,256)]
    full=ROOT/'tools/reference/d2r-named-equipment/json/skills.json'
    data=ROOT/'data/Sanctuary/Balance/necromancer-summons.json';assert data.read_bytes()==historical(str(data.relative_to(ROOT)))
    status=ROOT/'data/Sanctuary/Balance/class-skill-status.json';now=json.loads(status.read_text());before=json.loads(historical(str(status.relative_to(ROOT))))
    for talent in ('sorceress.warmth','sorceress.firemastery','sorceress.lightningmastery','sorceress.coldmastery'):
        updated=next(x for x in now['skills'] if x['talentId']==talent);old=next(x for x in before['skills'] if x['talentId']==talent)
        assert old['verification']==updated['verification']=='partial-native-coverage'
        old.update({k:updated[k] for k in ('verification','remaining')})
    assert before==now and not now['completedClasses']
    parent=ROOT/'tools/reference/sorceress-static-field-native-evidence.json';assert parent.read_bytes()==historical(str(parent.relative_to(ROOT)))
    prior=json.loads((ROOT/'.local/class-skills/sorceress-static-field/birth-inputs.json').read_text())
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
    assert 'Four passives:1020 independent pinned rank values' in (STAGE/'source-recheck.log').read_text()
    assert 'Traceback' not in (STAGE/'source-recheck.log').read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    files=[Path(__file__).resolve(),ROOT/'tests/check_sorceress_passives.py',ROOT/'tools/check_sorceress_passive_reference.py',ROOT/'tools/build_skill_roadmap.py',
        full,data,parent,rules,
        *(ROOT/'docs'/name for name in ('sorceress-passives-checkpoint.md','sorceress-passives-preflight.md','sorceress-lightning-flight-preflight.md','class-skills-checkpoint-2026-10-22.md','class-skills-resume-checkpoint.md','class-skill-roadmap.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in (f'pre-build-{BUILD}.json','birth-inputs.json','birth-native-checks.log',f'full-run-{BUILD}.log','sorceress-passives-fixtures.json','static-field-fixtures.json',
            'source-recheck.log','audit-inputs.json','audit-native-checks.log',f'audit-run-{BUILD}.log','python-tests.log',f'checks-{BUILD}.log',f'content-build-{BUILD}.log',f'integration-build-{BUILD}.log')),
        *(ROOT/'tools/reference'/name for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'))]
    result=dict(referenceVersion='3.3.93847',component='Warmth/Fire/Lightning/Cold Mastery and modern broken-immunity correction',installed=False,graphicalClientVerified=False,completedClasses=[],
        references=reference['references'],referenceKind=reference['referenceKind'],
        verified=dict(nativeMarkers=250,boots=3,actualReloads=2,saves=3,passiveRankValues=1020,coldCurseConvictionContexts=180,retainedRealInputActions=48,
            postLoadNativeDamageControls=2,fixtureFamilies=23,sharedChecks=6,auditMarkers=4,auditFingerprints=sizes,pythonTests=71,unchangedInstalledArtifacts=106),
        markers=dict(sorted(counts.items())),artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['Ordinary monster immunity broken by supported Lower Resist/Conviction now receives one-fifth skill Cold Mastery. Unbroken immunity remains unbroken;item pierce is separate and retains its full supported reduction. Known legacy Conviction offsets are excluded from the pre-break resistance pool.',
            '1020independent pinned curves,real absent/learned/hard/soft gear behavior,captured once-only Fire/Lightning enhancement and live Cold Mastery receiver controls pass. Actual Eschuta/Nightwing/Magefist and crafted Verite material exercise shared services.',
            'Approved T09native baseline/recovery percentage/material stacking remains unchanged. Respec/class changes and two reloads retain passive ranks/values,equipped original items,controlled mana,wounds and captured curse.',
            'Postload damage uses an explicit synthetic base-resistance input:GraveyardUndead normally reapplies its configured profile on load. This is not arbitrary NPC resistance-override persistence evidence. Final assets remain separate and no active passive binding is added.'],
        remaining=['Sunder state integration,unmapped external/native resistance modifiers,hire/PvP/special recipients and complete modern executable/event ordering remain open. Published rule/data evidence is not complete code execution.',
            'T09absolute regeneration/native first-tick differences remain approved and documented;the save fixture excludes ambient mana recovery. Actual old-DLL upgrade/unheld concurrent saves and graphical acceptance remain unverified.',
            'Remaining lightning lifecycle,Inferno held-input/payment/release,shared projectile terrain and ordinary cold-shatter gaps are separate. Effects/status feedback and passive tree icons remain pending;no class certificate/deployment.'])
    output=ROOT/'tools/reference/sorceress-passives-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Sorceress passives:250native markers/3boots/2reloads,1020rank/180immunity contexts,retained48real inputs,four audits/71Python;T09preserved;no class certificate/deployment.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
