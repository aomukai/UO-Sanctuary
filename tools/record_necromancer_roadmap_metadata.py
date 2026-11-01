#!/usr/bin/env python3
"""Bind annotation-only roadmap changes to the frozen full Necromancer run."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import subprocess

from record_poison_dagger_contact import markers
from snapshot_skill_build import current

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/necromancer-roadmap-metadata'
FULL=ROOT/'.local/class-skills/necromancer-acceptance'
STATUS='data/Sanctuary/Balance/class-skill-status.json'
ROADMAP_NOTE=('**Metadata reconciliation (2026-11-01 JST).** All 30 Necromancer talents now carry '
    '`partial-native-coverage`: ten stale labels are corrected and outdated remaining-work notes are refreshed. '
    'No completion flag, handler, source ID or gameplay value changes. The generator updates marked tables '
    'while preserving authored history and the separate effects/icons ledgers. The 845-marker full acceptance '
    'snapshot remains frozen at `6dc7e60`; current annotation-only provenance is in '
    '[the metadata checkpoint](necromancer-roadmap-metadata-checkpoint.md). Individual parity gaps remain explicit. '
    'Next: Sorceress baseline and unaffected mechanics. Earlier entries describe historical checkpoints.')


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def load(path):return json.loads(path.read_text())


def record(check=False):
    baseline=load(STAGE/'baseline.json')
    for name,digest in baseline['files'].items():assert sha(STAGE/'before'/name)==digest,name
    full_path=ROOT/'tools/reference/necromancer-acceptance-native-evidence.json'
    assert sha(full_path)==baseline['files'][str(full_path.relative_to(ROOT))]
    assert full_path.read_bytes()==subprocess.check_output(['git','show',baseline['fullCommit']+':'+str(full_path.relative_to(ROOT))],cwd=ROOT)
    full=load(full_path)
    assert full['verified']['nativeMarkers']==845 and full['verified']['actualReloads']==2
    log=FULL/'birth-native-checks.log';text=log.read_text()
    assert sha(log)==full['artifacts'][str(log.relative_to(ROOT))]
    assert sum(markers(log).values())==845 and ' ERR]' not in text and ': failed' not in text
    assert text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    for kind,row in full['inputInventories'].items():assert sha(ROOT/row['path'])==row['sha256'],kind
    frozen=load(FULL/'pre-build-10.json')['inputs'];now=current()
    assert frozen['sourceInputs']==now['sourceInputs'] and frozen['projectInputs']==now['projectInputs']
    assert frozen['copiedData'].keys()==now['copiedData'].keys()
    differences=[name for name in frozen['copiedData'] if frozen['copiedData'][name]!=now['copiedData'][name]]
    assert differences==['Data/Sanctuary/Balance/class-skill-status.json'],differences
    birth=load(FULL/'birth-inputs.json');audit=load(STAGE/'audit-inputs.json')
    assert audit['copiedData']==now['copiedData'] and audit['artifacts']==birth['artifacts']
    assert {p:h for p,h in audit['sourceInputs'].items() if p.endswith('.cs')}==now['sourceInputs']
    for name,digest in birth['artifacts'].items():
        if name.startswith('Assemblies/Sanctuary.'):
            kind='integration' if name.endswith('Sanctuary.Integration.dll') else 'content'
            path=STAGE/kind/Path(name).name
        else:path=STAGE/'native/Distribution'/name
        assert sha(path)==digest,name
    before=load(STAGE/'before'/STATUS);after=load(ROOT/STATUS)
    plan=load(ROOT/'tools/reference/necromancer-roadmap-metadata-plan.json')
    assert sha(ROOT/STATUS)==plan['proposedStatusSha256']
    assert sha(STAGE/'before'/STATUS)==plan['expectedStatusSha256']
    assert sha(ROOT/'tools/reference/necromancer-roadmap-metadata.patch')==plan['patchSha256']
    assert {k:v for k,v in before.items() if k!='skills'}=={k:v for k,v in after.items() if k!='skills'}
    changes={};labels=0
    for a,b in zip(before['skills'],after['skills'],strict=True):
        changed=sorted(k for k in a if a[k]!=b[k])
        if changed:
            assert a['characterClass']=='Necromancer' and set(changed)<={'verification','remaining'}
            changes[a['talentId']]=changed;labels+=a['verification']!=b['verification']
    assert changes==plan['reviewedFieldChanges'] and labels==10
    assert len(after['skills'])==240 and not after['completedClasses'] and not any(r['complete'] for r in after['skills'])
    necro=[r for r in after['skills'] if r['characterClass']=='Necromancer']
    assert len(necro)==30 and all(r['verification']=='partial-native-coverage' for r in necro)
    assert sum(r['implementation']=='handler-present' for r in after['skills'])==186
    table=r'(?m)^\|[^\n]*\|\n(?:\|[^\n]*\|(?:\n|$))+'
    region=r'(?m)^<!-- (?:BEGIN|END) GENERATED: [^\n]+ -->\n'
    for name,count in (('class-skill-roadmap',9),('skill-effects',1),('skill-icons',1)):
        path=Path('docs')/(name+'.md');old=(STAGE/'before'/path).read_text();new=(ROOT/path).read_text()
        assert new.count('<!-- BEGIN GENERATED: ')==new.count('<!-- END GENERATED: ')==count
        if name=='class-skill-roadmap':
            assert new.count(ROADMAP_NOTE)==1;new=new.replace(ROADMAP_NOTE+'\n\n','',1)
        assert re.sub(table,'<TABLE>\n',old)==re.sub(table,'<TABLE>\n',re.sub(region,'',new)),name
    strip=lambda d:{k:v for k,v in d.items() if k not in ('ContentAssemblyPath','ContentAssemblySha256','InputHashes')}
    for name,size in (('named-item-runtime-audit.json',887),('first-loop-affix-audit.json',21)):
        result=load(STAGE/name);assert result==load(ROOT/'tools/reference'/name)
        assert len(result['InputHashes'])==size and strip(result)==strip(load(STAGE/'before/tools/reference'/name))
        assert result['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in result['InputHashes'].items():assert sha(ROOT/path)==digest,path
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    assert ' ERR]' not in (STAGE/'audit-native-checks.log').read_text() and ': failed' not in (STAGE/'audit-native-checks.log').read_text()
    assert 'exported from the selected build' in (STAGE/'audit-run-1.log').read_text()
    assert 'Ran 71 tests' in (STAGE/'python-tests.log').read_text() and '\nOK\n' in (STAGE/'python-tests.log').read_text()
    assert '573 definitions accounted for' in (STAGE/'named-readiness.log').read_text()
    assert 'Eight classes, 240 source skills' in (STAGE/'catalogue-check.log').read_text()
    assert 'recorded separately' in (STAGE/'roadmap-check.log').read_text()
    installed=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json')
    assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==digest
    files=[p for p in STAGE.rglob('*') if p.is_file() and not any(part in ('content','integration','native','runtime') for part in p.relative_to(STAGE).parts)]
    files += [ROOT/n for n in (*baseline['files'], 'tools/reference/necromancer-roadmap-metadata-plan.json','tools/reference/necromancer-roadmap-metadata.patch',
        'tools/record_necromancer_roadmap_metadata.py','docs/necromancer-roadmap-metadata-checkpoint.md','docs/class-skills-resume-checkpoint.md')]
    result=dict(referenceVersion='3.3.93847',component='Necromancer roadmap annotation reconciliation',proposalApplied=True,gameplayChanged=False,installed=False,
        completedClasses=[],frozenFullCommit=baseline['fullCommit'],frozenFullReceiptSha256=sha(full_path),
        verified=dict(retainedNativeMarkers=845,changedMetadataFiles=1,changedRows=len(changes),correctedVerificationLabels=10,
            necromancerPartialLabels=30,handlers=186,skills=240,preservedAuthoredDocuments=3,generatedRegions=11,
            freshAuditMarkers=4,auditFingerprints=[887,21],pythonTests=71,namedDefinitions=573,unchangedInstalledArtifacts=106),
        reviewedFieldChanges=changes,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=['All C#/project inputs and all five tested assemblies are identical to the frozen full845-marker run. The only copied-data change is the annotation-only class-skill-status file;all implementation/source IDs and other classes are preserved.',
            'Ten stale labels become partial-native-coverage;remaining notes reflect existing receipts. All completion flags remain false. Marked table generation retains existing authored prose,with one explicitly recorded new roadmap checkpoint paragraph.',
            'Fresh selected-build audits preserve decisions with current metadata fingerprints. The strict frozen full recorder remains tied to its original annotation bytes;this receipt verifies the current annotation-only bridge rather than weakening that recorder.'],
        remaining=['No new talent is enabled or certified complete. Individual barrier/current-engine/native/control/credit/PvP/Sunder/client and asset gaps remain.',
            'Continue the Sorceress baseline using existing shared components;keep Inferno modern input/payment cadence individually deferred.'])
    output=ROOT/'tools/reference/necromancer-roadmap-metadata-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert output.read_text()==encoded
    else:output.write_text(encoded)
    print('Roadmap annotations:10labels corrected,30Necromancer partial scopes;frozen845run retained,current audits/71Python pass;no gameplay or completion change.')


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--check',action='store_true')
    record(parser.parse_args().check)
