#!/usr/bin/env python3
"""Authenticate captured bound modifier consumers and retained native behavior."""
import argparse
from collections import Counter
import hashlib
import json
import subprocess
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
from review_bound_demon_affixes import record as source_review
ROOT=e.ROOT
STAGE=ROOT/'.local/class-skills/bind-demon-affixes'
PARENT='f6fd64effb3003c5bd38f29a027b9aabacceea0c'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str) else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text()) if p.suffix=='.json' else None
    source_review(True);review=pin('tools/reference/bound-demon-affix-source-review.json')
    assert [x['sourceId'] for x in review['modifiers']]==[5,6,27,30]
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    production=lambda s:{n:h for n,h in s['sourceInputs'].items() if not n.startswith('tests/')}
    snapshots=[pin(STAGE/f'source-snapshot-{n}.json')['inputs'] for n in (1,2,3)]
    assert all(production(x)==production(snapshots[0]) and x['projectInputs']==snapshots[0]['projectInputs'] for x in snapshots)
    for n,h in born['sourceInputs'].items():assert e.sha(ROOT/n)==h,n
    parent=pin('tools/reference/bound-demon-death-mark-native-evidence.json');previous=pin(ROOT/'.local/class-skills/bind-demon-death-mark/birth-inputs.json')
    old={n:h for n,h in previous['sourceInputs'].items() if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items() if n.endswith('.cs')}
    new={'src/Sanctuary.Content/BoundDemonUpgrades.cs','tests/Sanctuary.Integration/BoundDemonAffixChecks.cs'}
    changed={'src/Sanctuary.Content/'+n+'.cs' for n in ('BoundDemonHosts','ClassStats','NpcSpectralHit','SkillCombat')}
    assert set(now)-set(old)==new and not set(old)-set(now) and {n for n in old if old[n]!=now[n]}==changed
    assert born['copiedData']==previous['copiedData']
    for kind,number in (('content',1),('integration',3)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log);assert all(s in log.read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]==e.sha(STAGE/'native/Distribution'/n)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
        'iron-golem-spectral-check':1,'iron-golem-spectral-reload-check':2})
    for family in ('bound-demon-typed','bound-demon-death-mark','bound-demon-affix'):
        expected.update({family+'-check':1,family+'-check-result':1,family+'-reload-check':2,family+'-reload-check-result':2})
    log=e.cohort(STAGE,expected,30);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-3.log');assert log.count('Client: 127.0.0.1: Connected.')==9
    assert log.count('Bound affixes:all five deterministic Spectral channels/max raw basis,post-direct surviving cold/credited poison,real Dagger OnHit,ranged and armor-ignore paths pass;')==1
    assert log.count('Bound affixes:effective rank5Strong/rank10movement-only Fast/rank15Spectral snapshot thresholds,')==1
    assert log.count('Bound affixes:original rank15birth/modifiers/type/weapon/wounds/resources and native40resistances survive reload without accumulation;')==2
    for family in ('bound-demon-typed','bound-demon-death-mark','bound-demon-affix'):
        assert pin(STAGE/(family+'-fixtures.json'))['Generation']==2
    saved=pin(STAGE/'bound-demon-affix-fixtures.json');assert saved['State']['Birth']['Rank']==15 and saved['State']['Birth']['Modifier15']==27 and saved['State']['Birth']['Modifier20']==0
    pin(STAGE/'iron-golem-spectral-fixtures.json')
    for number in (1,2):
        archive=STAGE/f'native-{number}';manifest=pin(archive/'birth-inputs.json');pin(archive/'birth-native-checks.log');pin(archive/f'native-run-{number}.log')
        for n,h in manifest['sourceInputs'].items():assert e.sha(archive/'source'/n)==h,n
        for kind in ('Content','Integration'):
            assert e.sha(archive/kind.lower()/f'Sanctuary.{kind}.dll')==manifest['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
        assert manifest['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
        if number==1:assert 'Spectral poison lacks survivor clock' in (archive/'birth-native-checks.log').read_text()
        else:e.cohort(archive,expected,30)
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(ROOT/n)==hashlib.sha256(subprocess.check_output(['git','show',PARENT+':'+n],cwd=ROOT)).hexdigest();frozen.append(n)
    for n in (*sorted(new|changed),'tests/check_bound_demon_affixes.py','tools/review_bound_demon_affixes.py','tools/record_bound_demon_affixes.py','docs/bind-demon-affix-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(n)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0]
    assert 'CombatAbility.BindDemon' not in code and 'CombatAbility.Consume' not in code
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Prepared captured native bound Strong/Fast/Spectral affixes',handlers=226,warlockHandlers=17,completedClasses=[],
        learnedBindDemonEnabled=False,nativeMarkers=30,markers=dict(expected),actualInputs=0,authenticatedSessions=9,boots=3,reloads=2,saves=3,
        contentBuild=1,integrationBuild=3,sourceSnapshot=3,nativeRun=3,nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,newLiveReferenceReads=0,
        capturedThresholds={'5':'ExtraStrong','10':'ExtraFast','15':'SpectralHit'},aura30Enabled=False,currentD2RModifierCallbackParity=False,
        realNativeWeaponHit=True,nativeRangedAndArmorIgnore=True,actualColdAndPoisonSurvivorDelivery=True,scopedZeroRefusalVerified=True,
        frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['Pinned monumod identities and skill threshold expressions qualified. Existing direct native affix contracts reused;official Patch3.2bound-aura replacement crosscheck recorded without repinning.',
            'Captured effective5/10/15thresholds survive gear/training changes. Direct Strong joins AR/ED pools;movement-only Fast joins native movement and preserves captured attack speed;three facets retain wounds/birth.',
            'Spectral shares independent raw-basis/two-draw producer,typed direct damage,and survivor chill/credited aggregate poison. Actual Dagger.OnHit,all five channels,native ranged/armor-ignore/typed packets pass;spells unchanged.',
            'Ordered native resistance adds after baseline reset without save/facet accumulation. Original AI/type/weapon/resources/wounds and birth survive two reloads with resumed native damage.',
            'Unscoped native weapon contexts and entirely absorbed poison refuse without HP/status mutation. Next normal attack proves transient scope cleanup. Iron Spectral,typed Imp and bound Death Mark retain.',
            'N1fixture expected newer poison clock rather than existing aggregate state. N2fixes that assertion/passes30;N3adds explicit refusal/scope cleanup. Exact earlier sources/DLLs/logs archived;Content1unchanged.'],
        limits=['Aura30selection/rank/RNG/native delivery remains unfinished. Fully absorbed cold/poison needs a qualified native post-hit bridge;unscoped manual native weapon AOS is unsupported.',
            'Existing-native-affix projection is not recovered current D2R modifier/stat-list/RNG parity. Full Bind channel/chance/payment/underlings/sunder/life-buff and native/named actor mappings remain unfinished.',
            'Four earlier proposals pending/frozen,no complete Bind proposal or learned admission. No actual socket ability inputs or general property/PvP/client/globalworld/class certification claimed.',
            'No native/client build/deploy/artwork or installed-save/configuration write. Installed D2R3.4.95032does not replace reference3.3.93847.'],inputs=inputs)
    target=ROOT/'tools/reference/bound-demon-affix-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded,'Bound affix evidence stale'
    else:target.write_text(encoded)
    print('Bound affixes:C1/I3/S3/N3,30markers/9connections/2reloads;Strong/Fast/Spectral native consumers verified;Aura/full Bind remain unfinished.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
