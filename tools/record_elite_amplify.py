#!/usr/bin/env python3
"""Bind fresh elite AMP metadata,shared arbitration and preserved V0 layouts."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
from review_weapon_event_order import function

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/elite-amplify'
FOCUS=ROOT/'.local/class-skills/elite-amplify-focus'
PARENT='ae80a567fdef88e285d92fa5790a3d0198c8e07f'
sha=inventory.sha


def body(text,start):
    begin=text.index(start);end=text.index('{',begin)+1;depth=1
    while depth:
        depth+=(text[end]=='{')-(text[end]=='}');end+=1
    return text[begin:end]


def record(check=False):
    inventory.STAGE=STAGE
    native=inventory.validate_inventory(STAGE/'birth-inputs.json')
    focus=inventory.validate_inventory(FOCUS/'birth-inputs.json')
    audit=inventory.validate_inventory(STAGE/'audit-inputs.json')
    for folder,name in ((STAGE,'birth-inputs.json'),(FOCUS,'birth-inputs.json'),(STAGE,'audit-inputs.json')):
        snapshot(STAGE/'pre-build-3.json',folder/name)
    assert native['artifacts']==focus['artifacts']==audit['artifacts']
    assert native['copiedData']==focus['copiedData']==audit['copiedData']
    previous=ROOT/'.local/class-skills/lower-resist-elite'
    prior=json.loads((previous/'birth-inputs.json').read_text())
    assert {n for n in set(prior['sourceInputs'])|set(native['sourceInputs'])if n.endswith('.cs')and prior['sourceInputs'].get(n)!=native['sourceInputs'].get(n)}=={
        'src/Sanctuary.Content/'+n+'.cs'for n in ('AttractCasts','EliteAuras','EliteCombat','GrimWardCurseTransport','NecromancerCurseCasts','NecromancerEffects')}|{'tests/Sanctuary.Integration/EliteAmplifyChecks.cs'}
    assert {n for n in set(prior['copiedData'])|set(native['copiedData'])if prior['copiedData'].get(n)!=native['copiedData'].get(n)}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert native['artifacts'][name]==prior['artifacts'][name]==sha(STAGE/'native/Distribution'/name)
    parentraw=subprocess.check_output(['git','show',PARENT+':tools/reference/lower-resist-elite-native-evidence.json'],cwd=ROOT)
    parent=json.loads(parentraw);baseline=previous/'birth-native-checks.log'
    assert sha(baseline)==parent['evidence'][str(baseline.relative_to(ROOT))]
    expected=markers(baseline)
    for family in ('elite-amplify','amplify-damage','decrepify'):
        expected[family+'-check']+=1;expected[family+'-reload-check']+=2
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==454
    focused={'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,
             'npc-resource-callbacks-reload-check':2,'elite-amplify-check':1,'elite-amplify-reload-check':2}
    assert markers(FOCUS/'birth-native-checks.log')==focused
    for folder in (STAGE,FOCUS):
        log=(folder/'birth-native-checks.log').read_text();assert ': failed'not in log and ' ERR]'not in log
        assert log.count('Shutting down')==log.count('Writing world save snapshot done')==3
        assert 'Duration-only Amplify cannot arbitrate a known-rank curse' in log
        saved=json.loads((folder/'elite-amplify-fixtures.json').read_text())
        assert len({saved[n]for n in ('Owner','Target','Source','Orphan','LostSource','Legacy')})==6
        assert(saved['Life'],saved['OrphanLife'],saved['Mana'])==(12799743,12799743,22784)
        for key,source in (('Known','Source'),('Orphaned','LostSource')):
            c=saved[key];assert c['Ability']==82 and c['Rank']==19 and c['Source']==saved[source]and c['PhysicalDelta']==-100
        assert saved['Known']['UntilUtc']==saved['Orphaned']['UntilUtc']==saved['LegacyUntil']
    for kind in ('content','integration'):
        assert all(t in(STAGE/(kind+'-build-3.log')).read_text()for t in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    before=STAGE/'before-fix';old=json.loads((before/'birth-inputs.json').read_text())
    assert 'Fresh elite Amplify blocked learned Decrepify'in(before/'birth-native-checks.log').read_text()
    for name in ('EliteAuras','EliteCombat','NecromancerEffects','NecromancerCurseCasts'):
        assert sha(before/(name+'.cs'))==prior['sourceInputs']['src/Sanctuary.Content/'+name+'.cs']==old['sourceInputs']['src/Sanctuary.Content/'+name+'.cs']
    initial=(before/'EliteAmplifyChecks.cs').read_text();current=(ROOT/'tests/Sanctuary.Integration/EliteAmplifyChecks.cs').read_text()
    boundary='Reset(t);t.SetResistance(ResistanceType.Physical,0);MagePetBuffChecks.Train'
    assert initial.split(boundary)[0]==current.split(boundary)[0] # Original failing admission/rank/penalty assertions retained.
    for kind in ('content','integration'):
        name='Sanctuary.'+kind.title()+'.dll';assert sha(before/kind/name)==old['artifacts']['Assemblies/'+name]
    preguard=STAGE/'pre-compatibility-guard';older=json.loads((preguard/'birth-inputs.json').read_text())
    assert markers(preguard/'birth-native-checks.log')==expected and markers(preguard/'focus-native-checks.log')==focused
    for kind in ('content','integration'):
        name='Sanctuary.'+kind.title()+'.dll';assert sha(preguard/kind/name)==older['artifacts']['Assemblies/'+name]
    # The timestamp binary layout is unchanged;metadata is an optional field
    # in the separate existing Necromancer JSON,not an appended binary record.
    layouts={}
    for filename,signature in (('EliteAuras.cs','public override void Serialize(IGenericWriter writer)'),
                               ('EliteAuras.cs','public override void Deserialize(IGenericReader reader)'),
                               ('NecromancerEffects.cs','public override void Serialize(IGenericWriter writer)')):
        left=body((before/filename).read_text(),signature);right=body((ROOT/'src/Sanctuary.Content'/filename).read_text(),signature)
        assert left==right;layouts[filename+':'+signature]=hashlib.sha256(right.encode()).hexdigest()
    treepath=ROOT/'.local/class-skills/d2moo-source-tree.json';tree=json.loads(treepath.read_text())
    sourcepath=ROOT/'.local/class-skills/d2moo-pinned-source/source/D2Game/src/MONSTER/MonsterUnique.cpp';raw=sourcepath.read_bytes()
    assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(r['sha']for r in tree['tree']if r['path']=='source/D2Game/src/MONSTER/MonsterUnique.cpp')
    routines={n:function(raw.decode(),n)for n in ('MONSTERUNIQUE_CastAmplifyDamage','MONSTERUNIQUE_CurseCallback_ApplyAmplifyDamage')}
    assert 'STAT_LEVEL, 0) / 5 + 1'in routines['MONSTERUNIQUE_CastAmplifyDamage']and 'curse.pUnit = pUnit;'in routines['MONSTERUNIQUE_CurseCallback_ApplyAmplifyDamage']
    for name,count in (('named-item-runtime-audit.json',992),('first-loop-affix-audit.json',21)):
        new=json.loads((STAGE/name).read_text());assert new==json.loads((ROOT/'tools/reference'/name).read_text())and len(new['InputHashes'])==count
        assert new['ContentAssemblySha256']==native['artifacts']['Assemblies/Sanctuary.Content.dll']
        for filename,digest in new['InputHashes'].items():assert sha(ROOT/filename)==digest,filename
        previousaudit=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert{k:v for k,v in new.items()if k not in skip}=={k:v for k,v in previousaudit.items()if k not in skip}
    assert(STAGE/'audit-native-checks.log').read_text().count(': passed')==2
    assert 'Ran 71 tests'in(STAGE/'python-tests.log').read_text()and '\nOK\n'in(STAGE/'python-tests.log').read_text()
    for name,t in (('readiness-check.log','573 definitions accounted for'),('catalogue-check.log','Eight classes, 240 source skills'),
                   ('roadmap-check.log','Class handler coverage, blockers, effects and icons recorded separately.')):assert t in(STAGE/name).read_text()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for name,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/name)==digest,name
    protected=json.loads((ROOT/'.local/class-skills/creeper-frame-installation.json').read_text())['protected']
    for key,relative,count in (('saves','.local/server/Saves',38),('configuration','.local/server/Configuration',20)):
        folder=Path(installed['runtimeRoot'])/relative;assert len(protected[key])==count and{str(p.relative_to(folder)):sha(p)for p in folder.rglob('*')if p.is_file()}==protected[key]
    state=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text());assert not state['completedClasses']and sum(r['implementation']=='handler-present'for r in state['skills'])==191
    files=[Path(__file__).resolve(),ROOT/'tools/build_skill_roadmap.py',treepath,sourcepath,
           *(ROOT/'tests'/n for n in ('check_elite_amplify.py','check_elite_amplify_focus.py')),
           *(ROOT/'docs'/n for n in ('elite-amplify-checkpoint.md','amplify-damage-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md')),
           *(STAGE/n for n in ('pre-build-1.json','pre-build-2.json','pre-build-3.json','birth-inputs.json','audit-inputs.json','birth-native-checks.log','native-run-2.log',
                               'content-build-1.log','integration-build-1.log','content-build-2.log','integration-build-2.log','content-build-3.log','integration-build-3.log',
                               'audit-native-checks.log','audit-run-2.log','python-tests.log','readiness-check.log','catalogue-check.log','roadmap-check.log','elite-amplify-fixtures.json')),
           *(FOCUS/n for n in ('birth-inputs.json','birth-native-checks.log','native-run-3.log','elite-amplify-fixtures.json')),
           *(p for folder in (before,preguard)for p in sorted(folder.rglob('*'))if p.is_file())]
    result=dict(referenceVersion='3.3.93847',parent=PARENT,parentReceiptSha256=hashlib.sha256(parentraw).hexdigest(),component='Fresh native elite AMP metadata and shared curse group',
        handlerEnabled=False,goalComplete=False,completedClasses=[],
        verified=dict(nativeMarkers=454,retainedNativeMarkers=445,focusedMarkers=12,boots=3,reloads=2,saves=3,beforeFixRegressionReproduced=True,
                      initialRegressionBodyRetained=True,knownOrphanAndUnknownOriginals=True,binaryV0LayoutsUnchanged=True,pythonTests=71,namedDecisions=573,
                      auditFingerprints=[992,21],handlers=191,protectedInstalledArtifactsUnchanged=106,protectedSavesUnchanged=38,protectedConfigurationsUnchanged=20),
        sourceReview=dict(commit=tree['sha'],scope='Static complete older proc/receiver review;existing native rank transport,no current execution or full proc/area certificate',
                          routines={n:hashlib.sha256(b.encode()).hexdigest()for n,b in routines.items()}),unchangedLayoutBodies=layouts,tested=native,
        limitations=['Fresh known metadata only;duration-only historical/compatibility records remain unknown and compound replacement refuses without inferred rank.',
                     'Retained null primary/creator cannot form a known native projection;that branch refuses and reports rather than inventing a primary.',
                     'Native proc RNG,target-centered area,recipient/room/event/dynamic-stat/species/PvP/client and complete modern initializer behavior remain separate.',
                     'Known/orphan/legacy inspected originals use explicit held deadlines;actual old-binary upgrades and unheld snapshots remain separate.',
                     'No new handler,class completion,deployment or assets;effects/audio and talent/hotkey icons remain separate;barrier hidedead44/initial graph remains deferred.'],
        evidence={str(p.relative_to(ROOT)):sha(p)for p in files})
    target=ROOT/'tools/reference/elite-amplify-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded
    else:target.write_text(encoded)
    print('Fresh elite AMP:454broader/12focused checks;445parent retained;known/orphan/unknown2reloads;V0binary layouts and protected build/saves preserved.')


if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--check',action='store_true');record(parser.parse_args().check)
