#!/usr/bin/env python3
"""Bind learned Terror casting/world evidence and its remaining source adapters."""
import argparse
from collections import Counter
import json
from pathlib import Path
import record_grizzly_body as inventory
from record_poison_dagger_contact import markers
from snapshot_skill_build import check as snapshot

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/terror-cast'
sha=inventory.sha

def record(build,run,check=False):
    inventory.STAGE=STAGE
    inputs={n:inventory.validate_inventory(STAGE/(n+'-inputs.json')) for n in ['birth','audit']}
    for n,value in inputs.items():
        snapshot(STAGE/f'pre-build-{build}.json',STAGE/(n+'-inputs.json'))
        assert value['artifacts']==inputs['birth']['artifacts'] and value['copiedData']==inputs['birth']['copiedData']
    parent=ROOT/'tools/reference/blind-world-native-evidence.json'
    prior=json.loads(parent.read_text());ref=prior['inputInventories']['audit'];assert sha(ROOT/ref['path'])==ref['sha256']
    old=json.loads((ROOT/ref['path']).read_text())
    before={p:h for p,h in old['sourceInputs'].items() if p.endswith('.cs')};after={p:h for p,h in inputs['birth']['sourceInputs'].items() if p.endswith('.cs')}
    added=set(after)-set(before);changed={p for p in before if after[p]!=before[p]}
    assert added=={'tests/Sanctuary.Integration/TerrorCastChecks.cs'}
    assert changed=={'src/Sanctuary.Content/SourceTerror.cs','src/Sanctuary.Content/NecromancerCurseCasts.cs','src/Sanctuary.Content/NecromancerEffects.cs'}
    assert set(before)<=set(after) and inputs['birth']['copiedData']==old['copiedData']
    for name in ['ModernUO.dll','Server.dll','Assemblies/UOContent.dll']:
        assert sha(STAGE/'native/Distribution'/name)==inputs['birth']['artifacts'][name]==old['artifacts'][name]
    expected=Counter({'iron-golem-birth-check':3,'class-skills-ai-check':3,'class-skills-ai-result':3,'blind-policy-check':3,
        'howl-result':3,'taunt-result':3,'blind-world-live-result':3})
    for name in ['iron-golem-items','npc-resource-callbacks','terror-cast','dim-vision-cast','blind-world','howl','taunt']:
        expected[name+'-check']=1;expected[name+'-reload-check']=2
    for name in ['terror-cast-right','terror-cast-quick']:
        expected[name+'-check']=expected[name+'-result']=3
    log=(STAGE/'birth-native-checks.log').read_text()
    assert markers(STAGE/'birth-native-checks.log')==expected and sum(expected.values())==54
    assert ': failed' not in log and ' ERR]' not in log and log.count('Shutting down')==log.count('Writing world save snapshot done')==3
    assert 'Learned Terror:3600source rank/creator/difficulty comparisons' in log
    for kind in ['content','integration']:
        text=(STAGE/f'{kind}-build-{build}.log').read_text();assert all(x in text for x in ['Build succeeded.','0 Warning(s)','0 Error(s)'])
    for name,count in [('named-item-runtime-audit.json',852),('first-loop-affix-audit.json',21)]:
        a=json.loads((STAGE/name).read_text());b=json.loads((ROOT/'.local/class-skills/blind-world'/name).read_text())
        assert a==json.loads((ROOT/'tools/reference'/name).read_text())
        assert all(a[k]==v for k,v in b.items() if k not in ['ContentAssemblyPath','ContentAssemblySha256','InputHashes'])
        assert len(a['InputHashes'])==count and a['ContentAssemblySha256']==inputs['birth']['artifacts']['Assemblies/Sanctuary.Content.dll']
        for p,h in a['InputHashes'].items():assert sha(ROOT/p)==h,p
    assert markers(STAGE/'audit-native-checks.log')==Counter({'paladin-thorns-check':1,'paladin-thorns-result':1,'named-gear-check':1,'first-loop-catalogue-check':1})
    audit=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in audit and ' ERR]' not in audit and audit.count('Shutting down')==1
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    assert '573 definitions accounted for' in (STAGE/'named-readiness.log').read_text()
    saved=json.loads((STAGE/'terror-cast-fixtures.json').read_text());curse=saved['Curse'];ai=curse['Terror']
    assert saved['Generation']==2 and curse['Ability']==86 and curse['Rank']==21 and curse['Source']==saved['Owner']
    assert ai['Skill']==86 and ai['LaunchRank']==21 and ai['Started'] and ai['Override']==0 and ai['Focus']==saved['Owner'] and ai['Goal'] is not None
    assert saved['Old']['Terror'] is None and saved['Old']['Rank']==1
    source_path=ROOT/'tools/reference/necromancer-ai-curse-source-evidence.json';source=json.loads(source_path.read_text())
    assert source['contexts']==12000 and source['difficultyDivisors']==dict(Normal=1,Nightmare=2,Hell=4)
    for group in ['inputs','artifacts']:
        for p,h in source[group].items():assert sha(ROOT/p)==h,p
    for key in ['reusedReference','reusedReceiverCpp']:assert sha(ROOT/source[key]['path'])==source[key]['sha256']
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for p,h in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/p)==h,p
    docs=['docs/terror-cast-checkpoint.md','docs/class-skills-resume-checkpoint.md','docs/class-skill-roadmap.md','docs/necromancer-mechanical-audit.md','docs/skill-mechanical-reference.md','docs/skill-effects.md','docs/skill-icons.md']
    files=[parent,source_path,Path(__file__),*[ROOT/p for p in added|changed],*[ROOT/p for p in docs],ROOT/'tests/check_terror_cast.py',ROOT/'tests/check_iron_golem_birth.py',ROOT/'tests/export_runtime_audits.py',ROOT/'tools/reference/named-item-runtime-audit.json',ROOT/'tools/reference/first-loop-affix-audit.json']
    files += [STAGE/p for p in [f'pre-build-{build}.json',f'content-build-{build}.log',f'integration-build-{build}.log',f'run-{run}.log',f'audit-run-{run}.log','birth-inputs.json','audit-inputs.json','birth-native-checks.log','audit-native-checks.log','python-tests.log','named-readiness.log','terror-cast-fixtures.json','dim-vision-cast-fixtures.json','blind-world-fixtures.json','howl.json','taunt.json']]
    result=dict(referenceVersion='3.3.93847',component='Learned Terror shared casting and native fear controller',installed=False,goalComplete=False,completedClasses=[],build=build,run=run,
        verified=dict(nativeMarkers=54,boots=3,actualReloads=2,saves=3,auditMarkers=4,pythonTests=71,namedDefinitions=573,auditFingerprints=[852,21],unchangedInstalledArtifacts=106,
            sourceDo30ReceiverContexts=12000,nativeTerrorScalarArbitrationComparisons=3600,connectedRightQuickCasts=6,autonomousEscapeChecks=6,naturalEightSecondExpiries=6,actualSavedEscapeResumes=2,mana256=1792,handlers=186,necromancerHandlers=30),
        inputInventories={n:dict(path=str((STAGE/(n+'-inputs.json')).relative_to(ROOT)),sha256=sha(STAGE/(n+'-inputs.json'))) for n in inputs},changedSourceHashes={p:after[p] for p in sorted(added|changed)},artifacts={str(p.relative_to(ROOT)):sha(p) for p in sorted(set(files))},
        established=[
            'Learned Terror now uses shared do30/no-stat casting/rank/creator arbitration. Sevenmana,fixed source radius4,base200+25*(rank-1)frames;configured ownerfacet divisor1/2/4precedes curse resistance. Weaker rejects,equal retainscreator/refreshesexpiry and reinitializesAI,higher replaces;Attract remains protected.',
            'The learned initializer captures existing native Combatant before control reset;it never assigns the caster merely because they cast. Tests demonstrate escape away from a different held player and natural nearer-threat acquisition when no target was held. Howl retains its separate explicit caster override and rank1stored-curse contract.',
            'Genuine learned Terror payloads validate and survive save/reload beside legacy bare,Howl and Taunt records. Two reloads resume actual native escape steps;real Wormskull effective21 and bindings survive. Six right/quick casts each pay7mana once,autonomously flee without damage and expire/retirecontrol naturally after8seconds.',
            '54relevant markers cover learnedTerror plus shared DimVision,blind policy/world/natural clocks,Howl,Taunt,classAI and originalitem/resource persistence over3boots/2reloads/3saves. Four shared audits,71Python,573named,clean builds and852/21fingerprints pass;runtime data,stagedkernel and106installed artifacts unchanged.'],
        remaining=[
            'Full skill/class/current-executable parity remains unfinished. Old-source AI initializer/dispatcher bodies are reviewed,not executed;native Combatant stands in for dynamic-path target. Current-executable corroboration and source callback-bearing AI transitions remain unverified. Unsupported source/native AI mappings reject with warning.',
            'Existing SourceTerror native target scan/LOS/ties,coordinate projection,path/blocked fallback,mode/cadence/initial event scheduling and RNG are explicit world adapters. This learned receiver does not certify those old Howl-controller boundaries or native movement-step cooldown restoration. No invented Howl one-frame idle is added to learned initialization.',
            'Legacy bare Terror saves retain the earlier cast-origin fallback. Enemy-owned summons,bard/PvP/special alignment,uninterruptible state and unmapped native encounter AI remain unsupported or uncertain. No full extended521cohort acceptance is claimed.',
            'No new handler or assets;186handlers/Necromancer30 and no complete class. Terror curseterror/cursecast effects/audio and talent/hotkey icons remain separate identified placeholders. Approved pet upgrades and safe-area parking remain queued.'])
    dest=ROOT/'tools/reference/terror-cast-native-evidence.json';render=json.dumps(result,indent=2)+'\n'
    if check:assert dest.read_text()==render
    else:dest.write_text(render)
    print('Learned Terror:54relevant markers,6real casts/autonomous escapes/expiries,2actual reload resumes;source/world gaps remain explicit.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--build',type=int,required=True);p.add_argument('--run',type=int,required=True);p.add_argument('--check',action='store_true');a=p.parse_args();record(a.build,a.run,a.check)
