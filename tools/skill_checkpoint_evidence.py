#!/usr/bin/env python3
"""Shared strict evidence checks for incremental class-skill checkpoints."""
from collections import Counter
import hashlib
import json
from pathlib import Path
import subprocess
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]


def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def load(path):return json.loads(path.read_text())


def inventory(stage,wide,prebuild):
    paths=(stage/'birth-inputs.json',wide/'birth-inputs.json',stage/'audit-inputs.json')
    born,other,audit=map(load,paths)
    assert born['artifacts']==other['artifacts']==audit['artifacts']
    assert born['copiedData']==other['copiedData']==audit['copiedData']
    for path in paths:
        snapshot(prebuild,path)
        for source,digest in load(path)['sourceInputs'].items():assert sha(ROOT/source)==digest,source
    for kind in ('Content','Integration'):
        assert sha(stage/kind.lower()/f'Sanctuary.{kind}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    return born


def cohort(stage,expected,count):
    path=stage/'birth-native-checks.log';log=path.read_text()
    assert markers(path)==expected and sum(markers(path).values())==count
    assert ': failed' not in log and ' ERR]' not in log
    assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    return log


def audits(stage,parent,artifacts):
    counts={}
    for name in ('named-item-runtime-audit.json','first-loop-affix-audit.json'):
        current=load(stage/name);assert current==load(ROOT/'tools/reference'/name)
        baseline=json.loads(subprocess.check_output(['git','show',parent+':tools/reference/'+name],cwd=ROOT))
        omit={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in omit}=={k:v for k,v in baseline.items() if k not in omit}
        assert current['ContentAssemblySha256']==artifacts['Assemblies/Sanctuary.Content.dll']
        for source,digest in current['InputHashes'].items():assert sha(ROOT/source)==digest,source
        counts[name]=len(current['InputHashes'])
    assert len(load(stage/'named-item-runtime-audit.json')['Definitions'])==573
    assert markers(stage/'audit-native-checks.log')==Counter({'named-gear-check':1,'first-loop-catalogue-check':1})
    return counts


def protection():
    installation=load(ROOT/'tools/reference/creeper-frame-installation-evidence.json')
    assert len(installation['installedArtifacts'])==106
    for path,digest in installation['installedArtifacts'].items():assert sha(Path(installation['runtimeRoot'])/path)==digest,path
    protected=load(ROOT/'.local/class-skills/creeper-frame-installation.json')['protected']
    for kind,relative,count in (('saves','Saves',38),('configuration','Configuration',20)):
        folder=Path(installation['runtimeRoot'])/'.local/server'/relative;assert len(protected[kind])==count
        assert {str(p.relative_to(folder)):sha(p) for p in folder.rglob('*') if p.is_file()}==protected[kind]
    client=load(ROOT/'tools/reference/poison-corpse-aim-client-evidence.json');assert client['patches']==40
    for kind in ('patchHashes','sharedInputs'):
        for path,digest in client[kind].items():assert sha(ROOT/path)==digest,path
    return dict(installedArtifacts=106,saves=38,configuration=20,clientPatches=40)


def verification(stage):
    commands=load(stage/'verification-commands.json');assert len(commands)>=13 and all(c['exitCode']==0 for c in commands)
    text=(stage/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text


def stage_files(*stages):
    return [p for stage in stages for p in stage.iterdir() if p.is_file() and p.suffix in ('.json','.log') and p.name!='execution-state.json']


def write(name,receipt,files,check):
    receipt['files']={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))}
    path=ROOT/'tools/reference'/name;encoded=json.dumps(receipt,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Checkpoint receipt stale: '+name
    else:path.write_text(encoded)
