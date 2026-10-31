#!/usr/bin/env python3
"""Bind bounded Attract acceptance to the exact tested build and reused source review."""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/attract-cast'
sha=inventory.sha

def source_callers():
    rel='source/D2Game/src/SKILLS/Skills.cpp'
    tree=json.loads((ROOT/'.local/class-skills/d2moo-source-tree.json').read_text())
    assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416'
    p=ROOT/'.local/class-skills/d2moo-pinned-source'/rel;raw=p.read_bytes()
    blob=next(r['sha'] for r in tree['tree'] if r['path']==rel)
    assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==blob
    text=raw.decode();bodies={}
    for name in ['SKILLS_SrvSt18_Attract','sub_6FD10360']:
        m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m
        i=text.index('{',m.start())+1;depth=1
        while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
        bodies[name]=text[m.start():i]
    assert re.search(r'\{\s*return 1;\s*\}',bodies['SKILLS_SrvSt18_Attract'])
    assert 'SUNIT_GetTargetUnit(pGame, pUnit)' in bodies['sub_6FD10360']
    assert 'nX = CLIENTS_GetUnitX(pTarget)' in bodies['sub_6FD10360'] and 'nY = CLIENTS_GetUnitY(pTarget)' in bodies['sub_6FD10360']
    return dict(path=str(p.relative_to(ROOT)),sha256=sha(p),reviewedNotExecuted={n:hashlib.sha256(b.encode()).hexdigest() for n,b in bodies.items()})

def record(build,run,audit_run,check=False):
    inventory.STAGE=STAGE
    inputs={n:inventory.validate_inventory(STAGE/(n+'-inputs.json')) for n in ['birth','audit']}
    for n,v in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(n+'-inputs.json'))
        assert v['artifacts']==inputs['birth']['artifacts'] and v['copiedData']==inputs['birth']['copiedData']
    parent=ROOT/'tools/reference/confuse-cast-native-evidence.json';prior=json.loads(parent.read_text());ref=prior['inputInventories']['audit']
    assert sha(ROOT/ref['path'])==ref['sha256'];old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    added=set(after)-set(before);changed={p for p in before if after[p]!=before[p]}
    assert added=={'src/Sanctuary.Content/AttractCasts.cs','tests/Sanctuary.Integration/AttractCastChecks.cs'}
    assert changed=={'src/Sanctuary.Content/NecromancerCombat.cs','src/Sanctuary.Content/NecromancerEffects.cs','src/Sanctuary.Content/SourceMind.cs','src/Sanctuary.Content/SourceTerror.cs','src/Sanctuary.Content/SourceBlind.cs','tests/Sanctuary.Integration/ConfuseCastChecks.cs','tests/Sanctuary.Integration/FixtureSpace.cs'}
    assert set(before)<=set(after) and inputs['birth']['copiedData']==old['copiedData']
    for n in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:assert sha(STAGE/'native/Distribution'/n)==inputs['birth']['artifacts'][n]==old['artifacts'][n]
    expected=Counter({'iron-golem-birth-check':3,'class-skills-ai-check':3,'class-skills-ai-result':3,'blind-policy-check':3,'skill-linear-check':3,'howl-result':3,'taunt-result':3,'blind-world-live-result':3,'mind-blast-result':1,'mind-blast-reload-result':2,'attract-cast-reload-result':2})
    for n in ['iron-golem-items','npc-resource-callbacks','attract-cast','confuse-cast','terror-cast','dim-vision-cast','blind-world','howl','taunt','mind-blast']:expected[n+'-check']=1;expected[n+'-reload-check']=2
    for n in ['attract-cast-right','attract-cast-quick','confuse-cast-right']:expected[n+'-check']=expected[n+'-result']=3
    text=(STAGE/'birth-native-checks.log').read_text();assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==77
    assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==text.count('Writing world save snapshot done')==3
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    fingerprints=[]
    for n in ['named-item-runtime-audit.json','first-loop-affix-audit.json']:
        a=json.loads((STAGE/n).read_text());b=json.loads((ROOT/'.local/class-skills/confuse-cast'/n).read_text())
        assert a==json.loads((ROOT/'tools/reference'/n).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
        fingerprints.append(len(a['InputHashes']))
    assert fingerprints==[854,21]
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count('Shutting down')==1
    assert 'pass across3boots/2reloads' in (STAGE/f'run-{run}.log').read_text()
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    assert '573 definitions accounted for' in (STAGE/'named-readiness.log').read_text()
    s=json.loads((STAGE/'attract-cast-fixtures.json').read_text())
    assert s['Generation']==2 and s['Mind']['Mode']==2 and s['Mind']['Focus']==s['Selected'] and s['Mind']['Draws']==7
    assert s['Curse']['Source']==s['Owner'] and s['Mind']['UntilUtc']==s['Curse']['UntilUtc'] and s['Old']['Rank']==1
    review_path=ROOT/'tools/reference/confuse-source-review.json';review=json.loads(review_path.read_text());assert review['referenceVersion']=='3.3.93847' and len(review['reviewedNotExecuted'])==10
    for p,h in review['inputs'].items():assert sha(ROOT/p)==h,p
    callers=source_callers()
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    docs=['docs/attract-cast-checkpoint.md','docs/class-skill-roadmap.md','docs/class-skills-resume-checkpoint.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md']
    files=[parent,review_path,Path(__file__),ROOT/callers['path'],*[ROOT/p for p in added|changed],*[ROOT/p for p in docs],ROOT/'tools/review_confuse_source.py',ROOT/'tests/check_attract_cast.py',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tests/export_runtime_audits.py',ROOT/'tools/reference/named-item-runtime-audit.json',ROOT/'tools/reference/first-loop-affix-audit.json']
    files += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{audit_run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','named-readiness.log','attract-cast-fixtures.json','confuse-cast-fixtures.json','terror-cast-fixtures.json','dim-vision-cast-fixtures.json','blind-world-fixtures.json','howl.json','taunt.json','mind-blast-fixtures.json']]
    result=dict(referenceVersion='3.3.93847',component='Attract learned casting, protected neutral target and independent neighbor overrides',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,auditRun=audit_run,
        verified=dict(nativeMarkers=77,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=fingerprints,unchangedInstalledArtifacts=106,connectedAttractCasts=6,connectedConfuseRegressionCasts=3,autonomousAttractCombatChecks=8,naturalTwelveSecondExpiries=6,mana256=4352,reusedReviewedSourceBodies=10,additionalReviewedSourceBodies=2,compiledNewSourceBodies=0,handlers=186,necromancerHandlers=30),
        inputInventories={n:dict(path=str((STAGE/(n+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(n+'-inputs.json'))) for n in inputs},changedSourceHashes={p:after[p] for p in sorted(added|changed)},additionalSourceReview=callers,artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'Learned Attract costs 17 mana at release, radius 9 source units and 300+90*(rank-1) frames, using owner-facet 1/2/4 division before selected-target curse resistance. The protected selected target becomes Neutral and remains attackable without ownership.',
            'One cast-centered scan gives eligible Evil neighbors explicit mode-2 target GUIDs and independent AIRESET deadlines while retaining their curses. Later arrivals get no override. Explicit targeting consumes no RNG or acquisition scan clock; latest casts overwrite the slot while retaining the actor stream. Target death clears it; selected curse removal does not.',
            'Existing SourceMind persistence and allegiance, curse/radius/duration services and blind/fear target resolvers are reused. First fear escape clears the generic override. Connected input authentication is shared with the retained Confuse fixture. Six real right/quick casts damage the selected NPC autonomously and expire after 12 seconds; both reloads resume actual explicit-target NPC damage with unchanged saved seed, draws and deadline.',
            '77 relevant native markers, four audits, 71 Python tests, 573 unchanged named decisions, 854/21 fingerprints and clean builds pass. Runtime data, staged kernel and 106 installed artifacts are unchanged. Existing source review and two additional complete caller bodies are statically checked; no new source-body execution or complete class claim.'],
        remaining=[
            'Legacy late factory failure may orphan Neutral alignment after neighbor mutation. This unsupported branch is explicitly logged/rejected before mutation; current failure-path payment timing is uncertain. Monster-owned minion detach and source target-node/event linkage lack native adapters.',
            'Native room/region/geometry/ordering, complete source-world/current-executable AI, conversion coexistence, damage/experience credit and global historical RNG remain unsupported or uncertain. Legacy bare saves cannot reconstruct missing historical neighbor overrides. No full extended cohort or full class completion is claimed.',
            'Attract curseattract/cursecast effects/audio and talent/hotkey icons remain separate identified placeholders. Pet upgrades and actual-safe-area parking remain queued.'])
    dest=ROOT/'tools/reference/attract-cast-native-evidence.json';render=json.dumps(result,indent=2)+'\n'
    if check:assert dest.read_text()==render
    else:dest.write_text(render)
    print('Attract: 77 relevant checks, six real casts, two autonomous combat reloads; source/world limits explicit.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--audit-run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.audit_run,a.check)
