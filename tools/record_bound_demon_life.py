#!/usr/bin/env python3
"""Authenticate prepared binding with existing native BO/Oak resource state."""
import argparse
from collections import Counter
import hashlib,json,subprocess
import skill_checkpoint_evidence as e
from snapshot_skill_build import check as snapshot
ROOT=e.ROOT;STAGE=ROOT/'.local/class-skills/bind-demon-life'
PARENT='7f67f3f2d10a46959665d58411a458c1a3b74cbb'
def record(check=False):
    inputs={}
    def pin(name):
        p=ROOT/name if isinstance(name,str) else name;inputs[str(p.relative_to(ROOT))]=e.sha(p)
        return json.loads(p.read_text()) if p.suffix=='.json' else None
    born=pin(STAGE/'birth-inputs.json');snapshot(STAGE/'source-snapshot-2.json',STAGE/'birth-inputs.json')
    for n,h in born['sourceInputs'].items():assert e.sha(ROOT/n)==h,n
    parent=pin('tools/reference/bound-demon-aura-native-evidence.json');previous=pin(ROOT/'.local/class-skills/bind-demon-auras/birth-inputs.json')
    old={n:h for n,h in previous['sourceInputs'].items() if n.endswith('.cs')};now={n:h for n,h in born['sourceInputs'].items() if n.endswith('.cs')}
    new={'tests/Sanctuary.Integration/BoundDemonLifeChecks.cs'}
    changed={'src/Sanctuary.Content/'+n+'.cs' for n in ('BattleOrders','MonsterVariantBaselines','BoundDemonHosts')}
    assert set(now)-set(old)==new and not set(old)-set(now) and {n for n in old if old[n]!=now[n]}==changed and born['copiedData']==previous['copiedData']
    for kind,number in (('content',1),('integration',2)):
        log=STAGE/f'{kind}-build-{number}.log';pin(log);assert all(s in log.read_text() for s in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
        assert e.sha(STAGE/kind/f'Sanctuary.{kind.title()}.dll')==born['artifacts'][f'Assemblies/Sanctuary.{kind.title()}.dll']
    for n in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert born['artifacts'][n]==parent['artifacts'][n]==e.sha(STAGE/'native/Distribution'/n)
    expected=Counter({'iron-golem-birth-check':3,'iron-golem-items-check':1,'iron-golem-items-reload-check':2,'npc-resource-callbacks-check':1,'npc-resource-callbacks-reload-check':2,'revive-facet-check':1,'revive-facet-reload-check':2})
    for family in ('bound-demon-host','bound-demon-aura','bound-demon-life'):
        expected.update({family+'-check':1,family+'-check-result':1,family+'-reload-check':2,family+'-reload-check-result':2})
    text=e.cohort(STAGE,expected,30);pin(STAGE/'birth-native-checks.log');pin(STAGE/'native-run-2.log');assert text.count('Client: 127.0.0.1: Connected.')==9
    for phrase in ('Bound life:unknown strength/opaque maximum and conflicting historical Normal baseline refuse;',
        'Bound life:actual BO35 before binding retains creator/supplier/deadline;',
        'Bound life:actual Oak30 and BO35plus Oak30 retain shared leases once above cap;',
        'Bound life:unbuffed12345 remains exact through BO floor/cap boundary and three facets;'):
        assert text.count(phrase)==1,phrase
    assert text.count('Bound life:original active BO/base/native identity and exact wounds/resources survive reload;')==2
    for family in ('bound-demon-host','bound-demon-aura','bound-demon-life'):assert pin(STAGE/(family+'-fixtures.json'))['Generation']==2
    saved=pin(STAGE/'bound-demon-life-fixtures.json');assert saved['State']['Normal']['Life256']==12345*256 and saved['State']['BaseMaximum256']==12345*256 and saved['Orders']['Percent']==35
    assert saved['Maximum']==12345*256+int(12345*256*.35)
    archive=STAGE/'native-1';earlier=pin(archive/'birth-inputs.json');log=archive/'birth-native-checks.log';pin(log);pin(archive/'native-run-1.log')
    assert 'does not have a serialization constructor' in log.read_text() and 'bound-demon-life-check-result: passed' in log.read_text()
    for n,h in earlier['sourceInputs'].items():assert e.sha(archive/'source'/n)==h,n
    for kind in ('Content','Integration'):assert e.sha(archive/kind.lower()/f'Sanctuary.{kind}.dll')==earlier['artifacts'][f'Assemblies/Sanctuary.{kind}.dll']
    early={n:h for n,h in earlier['sourceInputs'].items() if n.endswith('.cs')}
    assert set(early)==set(now) and {n for n in now if now[n]!=early[n]}==new
    assert earlier['artifacts']['Assemblies/Sanctuary.Content.dll']==born['artifacts']['Assemblies/Sanctuary.Content.dll']
    pin(STAGE/'source-snapshot-1.json');pin(STAGE/'source-snapshot-2.json');pin(STAGE/'integration-build-1.log')
    frozen=[]
    for stem,proposal in (('blood-oath-damage','blood-oath-damage'),('engorge-world','engorge-world-rule'),('blood-boil-world','blood-boil-rule'),('consume-world','consume-world-rule')):
        for n in (f'docs/{proposal}-proposal.md',f'tools/reference/{stem}-native-evidence.json'):
            pin(n);assert e.sha(ROOT/n)==hashlib.sha256(subprocess.check_output(['git','show',PARENT+':'+n],cwd=ROOT)).hexdigest();frozen.append(n)
    review=pin('tools/reference/bound-demon-underling-review.json');assert len(review['rows'])==18
    for n,h in review['inputs'].items():assert e.sha(ROOT/n)==h,n
    for n in (*sorted(new|changed),'tests/check_bound_demon_life.py','tools/record_bound_demon_life.py','docs/bind-demon-life-checkpoint.md',
        'docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-reuse-notes.md','docs/skill-effects.md','docs/skill-icons.md'):pin(n)
    code=(ROOT/'src/Sanctuary.Content/WarlockCombat.cs').read_text().split('public static bool Implemented')[1].split(';',1)[0];assert 'CombatAbility.BindDemon' not in code and 'CombatAbility.Consume' not in code
    result=dict(referenceVersion='3.3.93847',component='Prepared native bound preexisting life-buff capture',handlers=226,warlockHandlers=17,completedClasses=[],learnedBindDemonEnabled=False,
        nativeMarkers=30,markers=dict(expected),actualInputs=0,authenticatedSessions=9,boots=3,reloads=2,saves=3,contentBuild=1,integrationBuild=2,sourceSnapshot=2,nativeRun=2,
        nativeRebuilt=False,clientRebuilt=False,newTimers=0,newPersistenceProfiles=[],existingSerializationVersionsChanged=False,newLiveReferenceReads=0,
        actualNativeBodies=True,productionContentUnchangedAfterN1=True,frozenPendingProposalFiles=frozen,artifacts=born['artifacts'],
        preservation={**e.protection('tools/reference/hex-siphon-cast-client-evidence.json',43),'installed':False},
        established=['Native callback supplies raw unbuffed Normal maximum,without removing effects or reversing native capped/floored maxima. Unknown Strength/getter compositions and conflicting saved baseline explicitly refuse.',
            'Actual BO35 prior to binding works with new,cached and old null-life Normal baselines;50000raw/native65000cap becomes20000bound then27000buffed. Live creator/supplier/deadline,native AI/serial,mana/stamina remain unchanged.',
            'Actual Oak30 and BO35+Oak30 retain original leases,apply once above cap at26000/33000,and preserve wounds on independent removal. This directly exercises shared resource application,not enemy eligibility for a player aura/shout.',
            '12345base retains exact bound FP8 maximum despite native floor;Normal/Nightmare/Hell and two saves/reloads preserve identity/base/BO metadata/wounds/resources. Genuine remove/reapply after reload retains wound ratio.',
            'Original host/aura/Revive facet/NPC resource cohorts retain. N1 initial scenes pass but test subclass constructor blocks reload;exact archive retained. I2 replaces fixture with scoped getter control;Content1 unchanged.'],
        limits=['Prepared native base/cap/buff context is not recovered current D2R binder HP ordering. Full Bind channel/chance/quality/progress/payment/cancel/lifecycle/species/sunder/underling composite remains gated.',
            'Underling5/7cap interpretation is an inference from species and older tester notes;producer,ownership and child lifecycle remain unverified. No pack/clone/pool behavior invented.',
            'Four earlier proposals remain pending/frozen;226handlers/Warlock17/no class certified. No general item-resource or full-class certification/new socket ability input claimed.',
            'Reference remains3.3.93847 despite installed3.4.95032;installed server artifacts/saves/config/client patches unchanged;no artwork and missing effects/icons separate.'],inputs=inputs)
    target=ROOT/'tools/reference/bound-demon-life-native-evidence.json';encoded=json.dumps(result,indent=2)+'\n'
    if check:assert target.read_text()==encoded,'Bound life evidence stale'
    else:target.write_text(encoded)
    print('Bound life:C1/I2/S2/N2,30markers/9connections/2reloads;existing BO/Oak base/leases/wounds verified;full Bind remains gated.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
