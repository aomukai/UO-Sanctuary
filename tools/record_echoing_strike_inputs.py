#!/usr/bin/env python3
"""Authenticate raw Echoing source inputs without claiming final hit/flight rules."""
import argparse,hashlib,json,subprocess
from collections import Counter
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/echoing-strike-inputs';PARENT='dc56b05d9313d7690782ef82cf310e9399689212'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str)else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text())if p.suffix=='.json'else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-5.json',STAGE/'birth-inputs.json');pin(STAGE/'source-snapshot-5.json')
    for n,h in born['sourceInputs'].items():assert e.sha(ROOT/n)==h,n
    previous=pin(ROOT/'.local/class-skills/eldritch-blast-cast/birth-inputs.json');parent=pin('tools/reference/eldritch-blast-cast-native-evidence.json')
    old={n:h for n,h in previous['sourceInputs'].items()if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items()if n.endswith('.cs')}
    added={'src/Sanctuary.Content/EchoingStrikeInputs.cs','tests/Sanctuary.Integration/EchoingStrikeInputChecks.cs'}
    assert set(now)-set(old)==added and not set(old)-set(now)and all(now[n]==h for n,h in old.items())
    assert set(born['copiedData'])-set(previous['copiedData'])=={'Data/Sanctuary/Balance/echoing-strike-profile.json'}
    assert all(born['copiedData'][n]==h for n,h in previous['copiedData'].items())
    for kind,number in (('content',2),('integration',5)):
        path=STAGE/f'{kind}-build-{number}.log';pin(path);assert all(s in path.read_text()for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]==e.sha(STAGE/'native/Distribution'/n)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,
        'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'echoing-strike-inputs-check':1,'echoing-strike-inputs-reload-check':2})
    text=e.cohort(STAGE,expected,12);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-4.log');assert 'Client: 127.0.0.1: Connected.'not in text
    assert text.count('Echoing inputs:actual StoneCrusher/Ironward base+ED+flat ledger')==1
    assert text.count('Echoing inputs:original Warlock/Witch unique weapons,helm,Hex-derived immutable input values')==2
    saved=pin(STAGE/'echoing-strike-inputs-fixtures.json');assert saved['Generation']==2 and saved['UnavailableUnique']==['Effect: skill']
    assert [r['Female']for r in saved['Rows']]==[False,True]
    for r in saved['Rows']:
        c=r['Input'];w=c['WeaponInputs'];poison=w['Elements']
        assert c['Skill']['Rank']==8 and c['Skill']['HardMirroredBlades']==5 and c['Skill']['HardBladeWarp']==3 and c['Skill']['TotalAttacks']==2
        assert w['NormalDamage256']>0 and r['Life']%256==255 and r['Mana']%256==239 and len(r['PoisonItems'])==2
        assert [poison[n]for n in ('PoisonMin','PoisonMax','PoisonFrames','PoisonCount','LifeDrain')]==[193,193,175,2,5]
    profile=pin('data/Sanctuary/Balance/echoing-strike-profile.json');source=pin('tools/reference/d2r-named-equipment/json/missiles.json')
    manifest=pin('tools/reference/d2r-named-equipment/sources.json');digest=e.sha(ROOT/'tools/reference/d2r-named-equipment/json/missiles.json')
    assert profile['referenceVersion']=='3.3.93847'and profile['missileSourceSha256']==digest==manifest['files']['json/missiles.json']['sha256']
    assert profile['missiles']==[dict(key=k,fields=source[k])for k in ('echoingstrike','echoingstrike_detonate')]
    review=pin('tools/reference/echoing-strike-inputs-source-review.json');capture=pin(review['currentDo166']['capture'])
    assert e.sha(ROOT/review['currentDo166']['capture'])==review['currentDo166']['captureSha256']
    fragments=capture['records'][0]['fragments'];assert len(fragments)==6 and all(not f['captured']and f['permissions']=='---s'for f in fragments)
    total=sum(int(f['endRva'],16)-int(f['rva'],16)for f in fragments);assert total==757
    for n,h in capture['artifacts'].items():assert e.sha(ROOT/n)==h,n
    history=[]
    for run in (1,2,3):
        archive=STAGE/f'native-{run}';earlier=pin(archive/'birth-inputs.json');pin(archive/'birth-native-checks.log');pin(archive/f'native-run-{run}.log')
        assert ': failed'in(archive/'birth-native-checks.log').read_text()
        for n,h in earlier['sourceInputs'].items():assert e.sha(archive/'source'/n)==h,n
        for kind in ('Content','Integration'):
            assert e.sha(archive/kind.lower()/f'Sanctuary.{kind}.dll')==earlier['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
        history.append(str(archive.relative_to(ROOT)))
    earlier=e.load(STAGE/'native-3/birth-inputs.json')
    assert {n for n,h in earlier['sourceInputs'].items()if h!=born['sourceInputs'][n]}=={'src/Sanctuary.Content/EchoingStrikeInputs.cs'}
    for name in ('content-build-1.log','integration-build-1.log','integration-build-2.log','integration-build-3.log','integration-build-4.log',
        'source-snapshot-1.json','source-snapshot-2.json','source-snapshot-3.json','source-snapshot-4.json'):pin(STAGE/name)
    client=pin('tools/reference/eldritch-blast-cast-client-evidence.json');cm=pin(ROOT/client['inputManifest']['path'])
    assert e.sha(ROOT/client['inputManifest']['path'])==client['inputManifest']['sha256']
    for kind in ('patchHashes','sharedInputs'):
        for n,h in cm[kind].items():assert e.sha(ROOT/n)==h,n
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(ROOT/n)==hashlib.sha256(subprocess.check_output(['git','show',PARENT+':'+n],cwd=ROOT)).hexdigest();frozen.append(n)
    for n in (*sorted(added),'tests/check_echoing_strike_inputs.py','tools/record_echoing_strike_inputs.py','docs/echoing-strike-inputs-checkpoint.md',
        'docs/class-skills-resume-checkpoint.md','docs/skill-mechanical-reference.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md','docs/class-skill-roadmap.md'):pin(n)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0];assert 'CombatAbility.EchoingStrike'not in code
    result=dict(referenceVersion='3.3.93847',component='Echoing immutable original weapon/skill/element/poison/critical/slow source inputs',handlers=228,warlockHandlers=19,completedClasses=[],learnedEnabled=False,finalPacketImplemented=False,
        contentBuild=2,integrationBuild=5,sourceSnapshot=5,nativeRun=4,nativeMarkers=12,markers=dict(expected),boots=3,reloads=2,saves=3,actualInputs=0,authenticatedSessions=0,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,newLiveReferenceReads=0,currentDo166=dict(capturedBodyBytes=0,totalBodyBytes=total,protectedFragments=6),
        retainedFailedRuns=history,frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],
        preservation={**e.protection('tools/reference/eldritch-blast-cast-client-evidence.json',44),'installedClientPatches':43,'isolatedClientPatches':44,'installed':False},
        verified=['Existing shared melee/weapon/element/poison/supplement/skill/chance/slow collectors compose immutable scalar source inputs. Shared weapon/stat behavior is unchanged;new profile owns exact706/720rows with original source hash. No final arithmetic or emission consumer is selected.',
            'Actual available Stone Crusher/Astreon plus Tarnhelm/Bane/Snakecord/Venom Grip verify primary weapon bases50-61/37-43,unique ED and separate Damage+ledger,blunt50undead,item+Hexmagic,193rawpoisonrate/175frames/twosources/87averagedframes and5life drain. No source116/count scaling prematurely changes metadata.',
            'Soft +skills versus hard Mirrored5threshold,post-capture gear/training/Hex changes,repeatable capture with unchanged life/mana/durability,unarmed refusal and learned binding refusal pass. Saved original Warlock/Witch weapons/helm/poison items/ranks/Hex-derived values/FP256 resources and durability retain through2actual reloads.',
            'N1fixture assumed unavailable Redeemer,N2Caduceus dexterity missing;I2/I3/I4correct source setup and add nonzero two-source poison. N3reached actual reload and exposed the unnecessary map check;C2removes only that read-only world requirement,matching existing collectors. Exact failed sources/DLL/logs retained. Final cohort clean.',
            'No old176alias/supplier compiler matrix,new kernel/client build,timer/profile/version/install/liveD2Rread/artwork. Client/shared sources and four pending proposal/evidence files unchanged;installed workingbuild/config/saves protected.'],
        limits=['Echoing remains unavailable. Current do166body0/757captured;do44/hit34/damage16,source116/128/echo-scalar/ED/poison/critical/durability/leech/Hex/item-event order,return/fan/lease/lifecycle/input remain unresolved consumers.',
            'Research-reported Damage+exclusion/automatic hits/no hit delay is not pinned-current callback equivalence. Ledger doesnot subtract Damage+or alter generic weapon behavior;raw ToHit/NextHit remain distinct from final policy. Input capture time is caller-supplied,not proof of current D2R startup ordering.',
            'Read-only input capture can inspect original offline/internalized characters;this doesnot authorize offline emission. No hit/damage/block/return/socket/graphical or full-class certification is claimed. Effects/audio/icons remain separately needed;four earlier proposals pending.'],inputs=inputs)
    target=ROOT/'tools/reference/echoing-strike-inputs-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded,'Echoing input evidence stale'
    else:target.write_text(encoded)
    print('Echoing inputs:C2/I5/S5/N4,12markers/two actual reloads;real Damage+ledger/two-source poison;228handlers,final packet/flight gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
