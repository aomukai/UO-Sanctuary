#!/usr/bin/env python3
"""Authenticate bound zero-direct Spectral completion and retained event paths."""
import argparse
from collections import Counter
import hashlib,json,subprocess
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/bind-demon-spectral-zero'
PARENT='90030d4f700aaf92aac43c00fc089568bf94db7d'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str) else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text()) if p.suffix=='.json' else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-3.json',STAGE/'birth-inputs.json')
    production=lambda s:{n:h for n,h in s['sourceInputs'].items() if not n.startswith('tests/')}
    snapshots=[pin(STAGE/f'source-snapshot-{n}.json')['inputs'] for n in (1,2,3)]
    assert all(production(x)==production(snapshots[0]) and x['projectInputs']==snapshots[0]['projectInputs'] for x in snapshots)
    for n,h in born['sourceInputs'].items():assert e.sha(ROOT/n)==h,n
    parent=pin('tools/reference/bound-demon-affix-native-evidence.json');previous=pin(ROOT/'.local/class-skills/bind-demon-affixes/birth-inputs.json')
    old={n:h for n,h in previous['sourceInputs'].items() if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items() if n.endswith('.cs')}
    changed={'src/Sanctuary.Content/'+n+'.cs' for n in ('BoundDemonUpgrades','SkillCombat','NecromancerEffects')}|{'tests/Sanctuary.Integration/BoundDemonAffixChecks.cs'}
    assert set(now)==set(old) and {n for n in old if old[n]!=now[n]}==changed and born['copiedData']==previous['copiedData']
    for kind,number in (('content',1),('integration',3)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log);assert all(s in log.read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]==e.sha(STAGE/'native/Distribution'/n)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,
        'iron-golem-spectral-check':1,'iron-golem-spectral-reload-check':2,'psychic-ward-proposal-check':1,'psychic-ward-proposal-reload-check':2})
    for family in ('bound-demon-typed','bound-demon-affix'):
        expected.update({family+'-check':1,family+'-check-result':1,family+'-reload-check':2,family+'-reload-check-result':2})
    text=e.cohort(STAGE,expected,27);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-3.log');assert text.count('Client: 127.0.0.1: Connected.')==6
    assert text.count('Bound Spectral zero:physical-immune poison keeps1600ms aggregate/owner credit without HP or native post-hit;real Dagger zero-amount cold/ranged/immunity/blessed-poison guards pass;nested same-attempt positive packet keeps its own capture and later attacks do not replay zero work')==1
    for family in ('bound-demon-typed','bound-demon-affix','psychic-ward-proposal'):assert pin(STAGE/(family+'-fixtures.json'))['Generation']==2
    pin(STAGE/'iron-golem-spectral-fixtures.json')
    for number,why in ((1,'Zero cold contact bypassed cold immunity'),(2,'Invulnerable early refusal allocated Spectral work')):
        archive=STAGE/f'native-{number}';manifest=pin(archive/'birth-inputs.json');pin(archive/'birth-native-checks.log');pin(archive/f'native-run-{number}.log')
        assert why in (archive/'birth-native-checks.log').read_text()
        for n,h in manifest['sourceInputs'].items():assert e.sha(archive/'source'/n)==h,n
        for kind in ('Content','Integration'):assert e.sha(archive/kind.lower()/f'Sanctuary.{kind}.dll')==manifest['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
        assert manifest['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(ROOT/n)==hashlib.sha256(subprocess.check_output(['git','show',PARENT+':'+n],cwd=ROOT)).hexdigest();frozen.append(n)
    for n in (*sorted(changed),'tests/check_bound_spectral_zero.py','tools/record_bound_spectral_zero.py','docs/bind-demon-spectral-zero-checkpoint.md','docs/bind-demon-affix-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/bound-demon-affix-source-review.json'):pin(n)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0];assert 'CombatAbility.BindDemon' not in code and 'CombatAbility.Consume' not in code
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Prepared zero-direct native bound Spectral survivor completion',handlers=226,warlockHandlers=17,completedClasses=[],learnedBindDemonEnabled=False,
        nativeMarkers=27,markers=dict(expected),actualInputs=0,authenticatedSessions=6,boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=3,sourceSnapshot=3,nativeRun=3,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,newLiveReferenceReads=0,
        zeroDirectPoisonAndColdSupported=True,syntheticDirectDamage=False,syntheticNativePostHit=False,nestedSameAttemptCaptureVerified=True,unscopedManualWeaponAosSupported=False,
        frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['Existing AOS zero-return prepared callback completes exact transient capture after ordinary/Ward reactions. Positive direct packets keep native post-hit. No engine/API/profile/timer change.',
            'Physical-immune zero-direct hit retains1600ms aggregate poison/rate/playercredit and no HP/direct event/native post-hit. Real Dagger zero-amount cold and ranged zero statuses work;poison/cold immunity and blessed-poison guard remain.',
            'Nested same-attempt positive packet consumes its own capture;outer zero status completes once. Later attack and stale prepared callback cannot replay zero work.',
            'Iron Spectral/Ward/typed Imp and existing bound-affix positive/facet/save controls retain through two reloads. New zero scenes are not a claim of newly saved zero-status fixtures;shared Iron status persistence remains in its own cohort.',
            'N1fixture cleanup left legacy chill;N2assumed raw AOS rejected Blessed before capture. Exact archives retained;I3tests actual boundaries. Content1unchanged throughout.'],
        limits=['Unscoped manual native weapon AOS still unsupported. Full Bind/Aura30/underlings/sunder/life-buff/species/channel/chance/payment remain unfinished;four earlier proposals pending/frozen.',
            'Existing Sanctuary affix/event reuse is not recovered current D2R modifier/stat-list/RNG parity. No actual socket ability input or full class/property/PvP/client/globalworld certification claimed.',
            'Installed D2R3.4.95032does not replace3.3.93847. No native/client build,deployment,artwork or installed-save/configuration write.'],inputs=inputs)
    target=ROOT/'tools/reference/bound-demon-spectral-zero-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded,'Bound Spectral zero evidence stale'
    else:target.write_text(encoded)
    print('Bound Spectral zero:C1/I3/S3/N3,27markers/6connections/2reloads;zero-direct survivor delivery and nested capture verified;full Bind remains unfinished.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
