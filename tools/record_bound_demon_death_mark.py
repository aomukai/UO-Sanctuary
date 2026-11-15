#!/usr/bin/env python3
"""Authenticate native bound Death Mark and retained original behavior."""
import argparse
from collections import Counter
import hashlib
import json
import subprocess
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT
STAGE=ROOT/'.local/class-skills/bind-demon-death-mark'
PARENT='91179f54352d753a9b43d9ca08be3faabaf81108'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str) else name
        inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text()) if p.suffix=='.json' else None
    born=pin(STAGE/'birth-inputs.json')
    snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    first=pin(STAGE/'source-snapshot-1.json')['inputs']
    last=pin(STAGE/'source-snapshot-2.json')['inputs']
    production=lambda s:{n:h for n,h in s['sourceInputs'].items() if not n.startswith('tests/')}
    assert production(first)==production(last) and first['projectInputs']==last['projectInputs']
    for n,h in born['sourceInputs'].items():assert e.sha(ROOT/n)==h,n
    parent=pin('tools/reference/consume-aura-native-evidence.json')
    previous=pin(ROOT/'.local/class-skills/consume-auras/birth-inputs.json')
    old={n:h for n,h in previous['sourceInputs'].items() if n.endswith('.cs')}
    now={n:h for n,h in born['sourceInputs'].items() if n.endswith('.cs')}
    new={'src/Sanctuary.Content/DeathMarkBoundWorld.cs','tests/Sanctuary.Integration/BoundDemonDeathMarkChecks.cs'}
    changed={'src/Sanctuary.Content/'+n+'.cs' for n in ('DeathMarkNativeRelease','DeathMarkWorldRelease','DeathMarkWorld','DeathMarkPets')}
    assert set(now)-set(old)==new and not set(old)-set(now) and {n for n in old if old[n]!=now[n]}==changed
    assert born['copiedData']==previous['copiedData']
    for kind,number in (('content',1),('integration',2)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log)
        assert all(s in log.read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):
        assert born['artifacts'][n]==parent['artifacts'][n]==e.sha(STAGE/'native/Distribution'/n)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2})
    for family in ('death-mark-world','death-mark-action','bound-demon-death-mark'):
        expected.update({family+'-check':1,family+'-check-result':1,family+'-reload-check':2,family+'-reload-check-result':2})
    log=e.cohort(STAGE,expected,27);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log')
    assert log.count('Client: 127.0.0.1: Connected.')==9
    assert log.count('Bound Death Mark:mixed saved-order roster/shared SC/once-only mana/cooldown;')==1
    assert log.count('Bound Death Mark:original bound type/serial/weapon/stats/wounds/resources and mixed creation order survive reload;')==2
    for family in ('death-mark-world','death-mark-action','bound-demon-death-mark'):
        saved=pin(STAGE/(family+'-fixtures.json'));assert saved['Generation']==2
    archived=STAGE/'native-1';failed=pin(archived/'birth-inputs.json')
    pin(archived/'birth-native-checks.log');pin(archived/'native-run-1.log')
    assert 'bound-demon-death-mark-reload-check-result: failed' in (archived/'birth-native-checks.log').read_text()
    assert failed['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
    for n,h in failed['sourceInputs'].items():assert e.sha(archived/'source'/n)==h,n
    for kind in ('Content','Integration'):
        assert e.sha(archived/kind.lower()/f'Sanctuary.{kind}.dll')==failed['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(ROOT/n)==hashlib.sha256(subprocess.check_output(['git','show',PARENT+':'+n],cwd=ROOT)).hexdigest();frozen.append(n)
    for n in (*sorted(new|changed),'tests/check_bound_demon_death_mark.py','tools/record_bound_demon_death_mark.py','docs/bind-demon-death-mark-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md','tools/reference/death-mark-action-approval.json'):pin(n)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0]
    assert 'CombatAbility.BindDemon' not in code and 'CombatAbility.Consume' not in code
    protection=e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43)
    result=dict(referenceVersion='3.3.93847',component='Prepared original native bound Death Mark movement/commands/shared controls',
        handlers=226,warlockHandlers=17,completedClasses=[],learnedBindDemonEnabled=False,nativeMarkers=27,markers=dict(expected),actualInputs=0,
        authenticatedSessions=9,boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=2,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,newLiveReferenceReads=0,
        frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],preservation={**protection,'installed':False},
        established=['Mixed Goatman/original Daemon/Tainted roster retains saved creation order;actual bound body uses shared nearby/floor/fit/occupancy and native Attack/Guard orders without fabricated source controller.',
            'Shared SC/unpaid windup/once-only payment and cooldown remain. Stronger recipient mark refuses after completed bound warp,which remains unpaid without cooldown or rollback.',
            'Shared original control managers clear requested stun/freeze/chill while preserving native identity,AI,equipment,wounds,poison and fear. Untagged UO paralysis and unqualified active bound member refuse;other-facet/parked exclusion passes.',
            'Original bound body/type/weapon/stats/resources/wounds and mixed order survive two reloads and repeat player actions. Original three-species control/world and player-action cohorts pass.',
            'Native1 initial scene passed but new reload fixture omitted existing controller Tick after Resume. Exact artifacts/source/logs archived;I2adds that fixture step;Content1unchanged.'],
        limits=['Prepared ground/single-native-tile/16height projection belongs in full Bind composite proposal;current D2R binder footprint not recovered. Flying bound actors and untagged native controls remain unsupported.',
            'Full Bind channel/chance/health/quality/progress/payment/cancellation/late-callback/source modifiers/underlings/sunder/life-buff context and unmapped/named actor qualification remain unfinished.',
            'Four prior complete proposals pending/frozen;no complete Bind proposal or learned Bind admission. No actual socket ability inputs or general actor/RNG/property/PvP/client/globalworld/full-class certification claimed.',
            'No native/client build,deployment,new artwork or installed-save/configuration write. Installed D2R3.4.95032does not replace pinned3.3.93847.'],inputs=inputs)
    out=ROOT/'tools/reference/bound-demon-death-mark-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert out.read_text()==encoded,'Bound Death Mark evidence stale'
    else:out.write_text(encoded)
    print('Bound Death Mark:C1/I2/S2/N2,27markers/9connections/2reloads;original native identity and retained Death Mark behavior verified;full Bind remains gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
