#!/usr/bin/env python3
"""Bind approved cold-only wolf unlocks and retained NPC wound coverage."""
import argparse,json,subprocess
from pathlib import Path
import record_grizzly_body as inventory
from snapshot_skill_build import check as snapshot
ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/spirit-wolf-upgrades'
PARENT='da1c435bf2557b3647ced83892dbf1111b70f2f2'
sha=inventory.sha

def record(check=False):
    inventory.STAGE=STAGE;native=inventory.validate_inventory(STAGE/'birth-inputs.json')
    snapshot(STAGE/'pre-build-4.json',STAGE/'birth-inputs.json')
    log=(STAGE/'birth-native-checks.log').read_text()
    assert ': failed' not in log and ' ERR]' not in log and log.count(': passed')==27
    assert log.count('Writing world save snapshot done')==log.count('Shutting down')==3
    for name in ('spirit-wolf-hit','spirit-wolf-rate','spirit-wolf-upgrades-live'):
        assert log.count(name+'-check: passed')==3
    assert log.count('spirit-wolf-upgrades-live-result: passed')==3
    for name in ('spirit-wolf-upgrades','npc-open-wounds'):
        assert log.count(name+'-check: passed')==1 and log.count(name+'-reload-check: passed')==2
    saved=json.loads((STAGE/'spirit-wolf-upgrades-fixtures.json').read_text())
    assert saved['Owner']>0 and saved['Wolf']>0 and saved['Target']>0 and saved['Rate']>0 and saved['CreditOwner']==saved['Owner'] and saved['Life']>0 and saved['Seed']>0
    audit=json.loads((STAGE/'audit-inputs.json').read_text())
    assert native['artifacts']==audit['artifacts'] and native['copiedData']==audit['copiedData']
    assert {p:h for p,h in native['sourceInputs'].items() if p.endswith('.cs')}==audit['sourceInputs']
    text=(STAGE/'audit-native-checks.log').read_text();assert ': failed' not in text and ' ERR]' not in text and text.count(': passed')==2
    for name,count in [('named-item-runtime-audit.json',917),('first-loop-affix-audit.json',21)]:
        current=json.loads((STAGE/name).read_text());assert current==json.loads((ROOT/'tools/reference'/name).read_text())
        assert len(current['InputHashes'])==count and current['ContentAssemblySha256']==sha(STAGE/'content/Sanctuary.Content.dll')
        for path,digest in current['InputHashes'].items():assert sha(ROOT/path)==digest,path
        old=json.loads(subprocess.check_output(['git','show',PARENT+':tools/reference/'+name],cwd=ROOT));skip={'InputHashes','ContentAssemblySha256','ContentAssemblyPath'}
        assert {k:v for k,v in current.items() if k not in skip}=={k:v for k,v in old.items() if k not in skip}
    for kind in ('content','integration'):
        text=(STAGE/(kind+'-build-4.log')).read_text();assert all(word in text for word in ('Build succeeded.','0 Warning(s)','0 Error(s)'))
    text=(STAGE/'python-tests.log').read_text();assert 'Ran 71 tests' in text and '\nOK\n' in text
    text=(STAGE/'catalogue-checks.log').read_text()
    for word in ('573 definitions accounted for','Eight classes, 240 source skills','Class handler coverage, blockers, effects and icons recorded separately.'):assert word in text
    status=json.loads((ROOT/'data/Sanctuary/Balance/class-skill-status.json').read_text())
    assert not status['completedClasses'] and sum(s['implementation']=='handler-present' for s in status['skills'])==191
    old=json.loads(subprocess.check_output(['git','show',PARENT+':data/Sanctuary/Balance/class-skill-status.json'],cwd=ROOT))
    assert [a['talentId'] for a,b in zip(old['skills'],status['skills']) if a!=b]==['druid.summonspiritwolf']
    previous=json.loads((ROOT/'.local/class-skills/player-attack-slow/birth-inputs.json').read_text())
    for name in ('ModernUO.dll','Server.dll','Assemblies/UOContent.dll'):assert native['artifacts'][name]==previous['artifacts'][name]
    assert {p for p,h in native['copiedData'].items() if previous['copiedData'].get(p)!=h}=={'Data/Sanctuary/Balance/class-skill-status.json'}
    assert {p for p,h in native['sourceInputs'].items() if p.startswith(('src/','shared/')) and previous['sourceInputs'].get(p)!=h}=={
        'src/Sanctuary.Content/'+name+'.cs' for name in ('DruidSpiritWolfBody','DruidSpiritWolfHit','DruidSpiritWolfWorld','NpcCombatEvents','NpcHitSkills','NpcOpenWounds','SpiritWolfUpgrades')}
    installed=json.loads((ROOT/'tools/reference/creeper-frame-installation-evidence.json').read_text());assert len(installed['installedArtifacts'])==106
    for path,digest in installed['installedArtifacts'].items():assert sha(Path(installed['runtimeRoot'])/path)==digest,path
    files=[Path(__file__).resolve(),ROOT/'tests/check_spirit_wolf_upgrades.py',ROOT/'tools/build_skill_roadmap.py',
        *(ROOT/'docs'/name for name in ('spirit-wolf-upgrades-checkpoint.md','class-skill-roadmap.md','class-skills-resume-checkpoint.md','class-skill-reuse-notes.md','pet-adjustment-review.md','skill-effects.md','skill-icons.md')),
        *(STAGE/name for name in ('pre-build-4.json','birth-inputs.json','birth-native-checks.log','native-run-3.log','content-build-4.log','integration-build-4.log','audit-inputs.json','audit-native-checks.log','audit-run-1.log','python-tests.log','catalogue-checks.log','spirit-wolf-upgrades-fixtures.json','npc-open-wounds-fixtures.json'))]
    for name in ('diagnostic-training-helper-1','diagnostic-raw-refresh-2'):
        diagnostic=STAGE/name
        assert sha(diagnostic/'content/Sanctuary.Content.dll')==sha(STAGE/'content/Sanctuary.Content.dll')
        files.extend(p for p in diagnostic.rglob('*') if p.is_file())
    data=dict(referenceVersion='3.3.93847',parent=PARENT,status='Approved Spirit Wolf hard10wounds/hard20breakable cold immunity verified; no new handler/class certificate.',
        verified=dict(nativeMarkers=27,boots=3,reloads=2,saves=3,liveLearnedCastScenes=3,retainedWolfSourcePacketCasesPerBoot=384,retainedWolfSourceRateCasesPerBoot=576,retainedWoundRegenerationCasesPerBoot=2160,auditMarkers=2,pythonTests=71,handlerCount=191,unchangedInstalledArtifacts=106),
        limits=['Sanctuary-approved additions override the D2R baseline; no invented physical damage or Blood Life Tap is added to wolves.',
            'Saved prepared wolf/wound inspection clocks are held explicitly; no uninterrupted eight-second offline wound lifetime is claimed.',
            'Additional typed-only NPC damage-event registrations refuse before attack RNG; broader NPC item/current-engine/geometry/PvP/client acceptance and final assets remain separate.',
            'No fresh full1180class run is claimed for this change; dedicated wolf, shared Blood/item wound, live learned-cast and original save/reload scopes are recorded.'],
        tested=native,evidence={str(p.relative_to(ROOT)):sha(p) for p in files})
    out=ROOT/'tools/reference/spirit-wolf-upgrades-native-evidence.json';encoded=json.dumps(data,indent=2)+'\n'
    if check:assert out.read_text()==encoded
    else:out.write_text(encoded)
    print('Spirit Wolf upgrades:27native markers/3boots/2reloads,3paid learned scenes,shared Blood/item wounds retained;71Python;191handlers;106installed unchanged.')

if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
