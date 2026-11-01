#!/usr/bin/env python3
"""Compose the legacy barrier death modifier with the existing death-entry proof.

ModeChange is a declared entry/dispatch adapter, not the full engine scheduler.
Pool frees are quarantined in positive controls; actual-delete negative controls
expose the unchanged legacy iterator's lifetime defect.
"""
import argparse
import hashlib
import json
import re
import subprocess
from pathlib import Path

from probe_bone_barrier_death import function

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'bone-barrier-modifier-source'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def build(check=False):
    STAGE.mkdir(parents=True, exist_ok=True)
    tree = json.loads((BASE / 'd2moo-source-tree.json').read_text())
    assert tree['sha'] == '0c0bbd1e6bef9181a3da1c046523af622a925416'
    inputs = {}

    def source(relative):
        path = BASE / 'd2moo-pinned-source' / relative
        raw = path.read_bytes()
        blob = hashlib.sha1(b'blob ' + str(len(raw)).encode() + b'\0' + raw).hexdigest()
        assert blob == next(x['sha'] for x in tree['tree'] if x['path'] == relative)
        inputs[str(path.relative_to(ROOT))] = sha(path)
        return raw.decode()

    general = source('source/D2Game/src/AI/AiGeneral.cpp')
    unique = source('source/D2Game/src/MONSTER/MonsterUnique.cpp')
    mode = source('source/D2Game/src/MONSTER/MonsterMode.cpp')
    nec = source('source/D2Game/src/SKILLS/SkillNec.cpp')
    maker = source('source/D2Game/src/MISSILES/MissMode.cpp')
    skills = source('source/D2Game/src/SKILLS/Skills.cpp')
    # Pin the complete callers as reviewed evidence, separate from compiled code.
    reviewed = {name: function(text, name) for text, name in [
        (mode, 'D2GAME_ModeChange_6FC65220'),
        (nec, 'SKILLS_SrvDo060_BoneWall'),
        (nec, 'SKILLS_SrvDo062_BonePrison'),
        (maker, 'MISSMODE_SrvDo13_BoneWallMaker'),
        (skills, 'D2GAME_SummonPet_6FD14430'),
    ]}
    order = reviewed['D2GAME_ModeChange_6FC65220']
    assert order.index('unk0x00(pGame, pModeChange)') < order.index('sub_6FC6E860(pGame, pUnit)')
    assert 'pUnit->dwUnitId, pUnit->dwUnitType, 0, 1' in reviewed['SKILLS_SrvDo060_BoneWall']
    assert 'pOwner->dwUnitId, pOwner->dwUnitType, 0, 0' in reviewed['SKILLS_SrvDo062_BonePrison']
    assert 'pMonster->dwUnitId, 1, 0, 0' in reviewed['MISSMODE_SrvDo13_BoneWallMaker']
    parent_path = ROOT / 'tools/reference/sentry-death-entry-source-evidence.json'
    parent = json.loads(parent_path.read_text())
    cpp_path = BASE / 'sentry-death-entry-source-probe.cpp'
    cpp = cpp_path.read_text().split('int main()', 1)[0]
    for name, digest in parent['compiledRoutineHashes'].items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == digest, name
    inputs[str(parent_path.relative_to(ROOT))] = sha(parent_path)
    inputs[str(cpp_path.relative_to(ROOT))] = sha(cpp_path)
    cpp = cpp.replace('#include <cstdlib>', '#include <cstdlib>\n#include <set>\n#include <vector>\n#include <cstring>')
    cpp = cpp.replace('struct D2AiControlStrc;struct D2ActiveRoomStrc;',
        'struct D2AiControlStrc;struct D2MonsterDataStrc {D2AiControlStrc* pAiControl=nullptr;uint8_t nMonUmod[9]={};int nTypeFlag=0;};struct D2ActiveRoomStrc;')
    cpp = cpp.replace('D2AiControlStrc* ai=nullptr;', 'D2MonsterDataStrc* pMonsterData=nullptr;D2AiControlStrc* ai=nullptr;')
    cpp = cpp.replace('int unk0x14[1]={0};', 'int unk0x14[1]={0};int nMode=0;')
    allocation = function(general, 'AIGENERAL_AllocMinionList')
    old = 'void AIGENERAL_AllocMinionList(D2GameStrc*,D2UnitStrc*,D2UnitStrc*){assert(false);}'
    assert cpp.count(old) == 1
    cpp = cpp.replace(old, allocation)
    added = {name: function(text, name) for text, name in [
        (general, 'AIGENERAL_FreeMinionList'),
        (general, 'AIGENERAL_ExecuteCallbackOnMinions'),
        (unique, 'MONSTERUNIQUE_GetUMods'),
        (unique, 'MONSTERUNIQUE_MinionCallback_KillMinion'),
        (unique, 'MONSTERUNIQUE_KillMinions'),
        (unique, 'sub_6FC6E780'),
        (unique, 'sub_6FC6E860'),
    ]}
    dispatcher = added['sub_6FC6E780']
    table = dispatcher[dispatcher.index('constexpr D2UnkUModStrc'):dispatcher.index('int32_t bUnique')]
    inactive = sorted(set(re.findall(r'\b(?:MONSTERUNIQUE_\w+|sub_[0-9A-F]+)\b', table)) - {'MONSTERUNIQUE_KillMinions'})
    cpp += r'''
constexpr int MONTYPEFLAG_UNIQUE=1;
using MonUModFunc=void(*)(D2GameStrc*,D2UnitStrc*,int32_t,int32_t);
struct D2UnkUModStrc {MonUModFunc unk0x00[6];};
bool immediateFree=false;std::vector<D2MinionListStrc*> quarantined;
void PoolFree(D2MinionListStrc*p){if(immediateFree)delete p;else quarantined.push_back(p);}
#define D2_FREE_POOL(pool,ptr) PoolFree(ptr)
void MONSTERMODE_GetModeChangeInfo(D2UnitStrc*u,int n,D2ModeChangeStrc*m){m->pUnit=u;m->nMode=n;}
int D2GAME_ModeChange_6FC65220(D2GameStrc*,D2ModeChangeStrc*,int);
'''
    for name in inactive:
        cpp += f'void {name}(D2GameStrc*,D2UnitStrc*,int32_t,int32_t){{assert(false);}}\n'
    cpp += '\n'.join(added.values())
    cpp += r'''
int entryCount=0;
// Explicit adapter: complete regular entry then complete index-1 dispatcher.
// Path/stat/animation services of the complete ModeChange caller are not run.
int D2GAME_ModeChange_6FC65220(D2GameStrc*g,D2ModeChangeStrc*m,int a){
 assert(a==1&&m->nMode==0);assert(++entryCount<30);
 int result=sub_6FC63B30(g,m);sub_6FC6E860(g,m->pUnit);return result;
}
std::vector<int> List(D2UnitStrc*u){std::vector<int> v;for(auto*p=u->ai->pMinionList;p;p=p->pNext)v.push_back(p->dwMinionGUID);return v;}
int main(int argc,char**argv){
 bool nullOwner=argc>1&&strcmp(argv[1],"null-owner")==0;
 immediateFree=argc>1&&strcmp(argv[1],"immediate-free")==0;
 bool first=true;printf("[");
 for(int kind:{78,88})for(int flags:{2,3})for(int childFlags:{0,1,2,3})for(int count:{1,2,3,4})for(int victim=0;victim<=count;victim++){
  // Root flag2/player-owner path is a separately reproduced legacy fault.
  if(argc==1&&victim==0&&flags==2)continue;
  if(nullOwner&&!(kind==78&&flags==2&&childFlags==0&&count==1&&victim==0))continue;
  if(immediateFree&&!(kind==78&&flags==2&&childFlags==0&&count==2&&victim==1))continue;
  game={};owner={};room={};region={};monster={};model={};entryCount=0;
  D2MonsterRegionStrc*regions[]={&region};game.pMonReg=regions;
  owner.dwUnitType=0;owner.dwUnitId=1;game.pUnitList[0][1]=&owner;
  D2UnitStrc units[5];D2MonsterDataStrc data[5];D2PathStrc paths[5];D2StatListStrc stats[5];
  for(int i=0;i<=count;i++){
   auto&u=units[i];u.dwUnitId=17+i;u.dwClassId=MONSTER_BONEWALL;u.room=&room;u.pDynamicPath=&paths[i];u.pMonsterData=&data[i];
   u.ai=AIGENERAL_AllocAiControl(&game);data[i].pAiControl=u.ai;data[i].nMonUmod[0]=15;
   u.ai->nAiFlags=i?childFlags:flags;
   AIGENERAL_SetOwnerData(&game,&u,i?17:1,i?1:0,0,0);
   u.dwFlags=UNITFLAG_NOTC|UNITFLAG_TARGETABLE;u.dwFlagEx=UNITFLAGEX_STOREOWNERINFO;
   u.stats[STAT_HITPOINTS]=257+i;u.stats[STAT_MAXHP]=25600;
   u.pStatListEx=&stats[i];stats[i].values=u.stats;stats[i].dwFlags=STATLIST_EXTENDED;
   game.pUnitList[1][17+i]=&u;
   if(i)AIGENERAL_AllocMinionList(&game,&units[0],&u);
  }
  hoverFrees=refreshes=eventDeletes=lootAttempts=deathEvents=freed=0;arrayPresent=true;boss=false;
  D2ModeChangeStrc change;MONSTERMODE_GetModeChangeInfo(&units[victim],0,&change);
  assert(D2GAME_ModeChange_6FC65220(&game,&change,1)==1);
  assert(entryCount==count+1&&eventDeletes==count+1&&lootAttempts==0);
  printf("%s{\"input\":[%d,%d,%d,%d,%d],\"entries\":%d,\"units\":[",first?"":",",kind,flags,childFlags,count,victim,entryCount);first=false;
  std::set<D2MinionListStrc*> nodes(quarantined.begin(),quarantined.end());
  for(int i=0;i<=count;i++){
   auto&u=units[i];assert(u.dwAnimMode==0&&u.stats[30]==1&&u.stats[STAT_HITPOINTS]==257+i);
   assert(SUNIT_GetServerUnit(&game,1,17+i)==&u);
   sub_6FC641D0(&game,&u);assert(u.dwAnimMode==12);
   printf("%s[%d,%d,%d,%d,%d]",i?",":"",u.dwAnimMode,u.ai->dwOwnerTypeEx,u.ai->dwOwnerGUIDEx,u.ai->nAiFlags,(int)List(&u).size());
   for(auto*p=u.ai->pMinionList;p;p=p->pNext)nodes.insert(p);
  }
  printf("]}");assert(deathEvents==count+1);
  for(auto*p:nodes)delete p;quarantined.clear();for(int i=0;i<=count;i++)delete units[i].ai;
 }
 printf("]\n");
}
'''
    compiled = dict(parent['compiledRoutineHashes'])
    compiled.update({name: hashlib.sha256(body.encode()).hexdigest() for name, body in added.items()})
    compiled['AIGENERAL_AllocMinionList'] = hashlib.sha256(allocation.encode()).hexdigest()
    for name, digest in compiled.items():
        assert hashlib.sha256(function(cpp, name).encode()).hexdigest() == digest, name
    path = STAGE / 'probe.cpp'
    path.write_text(cpp)
    with (STAGE / 'compiler.log').open('w') as out:
        subprocess.run(['g++', '-std=c++20', '-O1', '-g', '-fsanitize=address,undefined',
                        '-fno-sanitize-recover=all', str(path), '-o', str(STAGE / 'probe')],
                       stdout=out, stderr=subprocess.STDOUT, check=True)
    run = subprocess.run([str(STAGE / 'probe')], capture_output=True)
    (STAGE / 'sanitizer.log').write_bytes(run.stderr)
    assert run.returncode == 0 and not run.stderr, run.stderr.decode()[-4000:]
    cases = json.loads(run.stdout)
    assert len(cases) == 192
    for row in cases:
        kind, flags, child_flags, count, victim = row['input']
        assert row['entries'] == count + 1
        assert len(row['units']) == count + 1
        assert all(unit[:3] == [12, 1, -1] for unit in row['units']), row
    negative_logs = []
    for control, expected in [('null-owner', b"member access within null pointer of type 'struct D2AiControlStrc'"),
                              ('immediate-free', b'heap-use-after-free')]:
        negative = subprocess.run([str(STAGE / 'probe'), control], capture_output=True)
        log = STAGE / (control + '-sanitizer.log')
        normalized = re.sub(rb'==\d+==', b'==PID==', negative.stderr)
        normalized = re.sub(rb'0x[0-9a-f]+', b'0xADDR', normalized)
        log.write_bytes(normalized)
        assert negative.returncode != 0 and expected in negative.stderr, negative.stderr.decode()[-3000:]
        negative_logs.append(log)
    output = STAGE / 'cases.json'
    encoded = json.dumps(cases, separators=(',', ':')) + '\n'
    if check:
        assert output.read_text() == encoded
    else:
        output.write_text(encoded)
    installed = json.loads((ROOT / 'tools/reference/creeper-frame-installation-evidence.json').read_text())
    assert len(installed['installedArtifacts']) == 106
    for name, digest in installed['installedArtifacts'].items():
        assert sha(Path(installed['runtimeRoot']) / name) == digest, name
    native_path = ROOT / 'tools/reference/parking-boundaries-native-evidence.json'
    native = json.loads(native_path.read_text())
    assert native['verified']['nativeMarkers'] == 406
    frozen = native['inputInventories']['birth']
    assert sha(ROOT / frozen['path']) == frozen['sha256']
    from snapshot_skill_build import check as snapshot
    snapshot(BASE / 'parking-boundaries/pre-build-4.json', ROOT / frozen['path'])
    inputs[frozen['path']] = frozen['sha256']
    inputs[str(native_path.relative_to(ROOT))] = sha(native_path)
    inputs[str(Path(__file__).relative_to(ROOT))] = sha(Path(__file__))
    result = dict(referenceVersion='3.3.93847', legacyCommit=tree['sha'], runtimeChanged=False,
        currentExecutableParity=False, lastNativeEvidence=str(native_path.relative_to(ROOT)),
        unchangedInstalledArtifacts=106, inputs=inputs, compiledRoutineHashes=compiled,
        reviewedRoutineHashes={name: hashlib.sha256(body.encode()).hexdigest() for name, body in reviewed.items()},
        verified=dict(controlledGraphContexts=len(cases), negativeSanitizerControls=2,
                      fullModifierTableRows=43, modifierSlots=9),
        artifacts={str(p.relative_to(ROOT)): sha(p) for p in [path, STAGE / 'compiler.log',
                   STAGE / 'sanitizer.log', output, *negative_logs]},
        established=[
            'Complete regular death entry/cleanup and AI owner graph bodies are reused unchanged. Complete 43-row/four-argument UMod dispatcher, getter, index-1 wrapper, KillMinions, callback, list removal and traversal execute unchanged.',
            '192 controlled Wall/Prison contexts enter every registered segment into death exactly once, with dead collision requested once each, then retain every GUID and supplied fractional HP in mode12. Positive pool frees quarantine nodes until each case ends; this does not prove actual D2 pool lifetime semantics.',
            'Complete reviewed skill/common-summon/maker bodies set root AI owner to caster and child AI owner to root, with UMod15 on every segment. ModeChange dispatches index1 after regular death entry/graph updates. Low-level initial flags are still explicit inputs.',
            'Two negative sanitizer controls reproduce unchanged legacy null-owner-AI traversal on flag2 root death and iterator heap-use-after-free with immediate physical list-node free. Neither defect is imported into Sanctuary.'],
        remaining=[
            'Full ModeChange/stat/path/animation/event scheduling and modifier assignment are declared boundaries; the entry/dispatch adapter is not their implementation. Initial low-level spawn flags and modern executable parity remain uncertain.',
            'Actual allocator guarantees or corrected modern traversal/root-owner resolution are needed before treating the legacy graph as a safe current-engine specification. Missing/stale child GUIDs, owner disappearance, overlapping casts and collision geometry need native integration.',
            'Native immediate barrier deletion/root cleanup remains provisional; death phases, 15-frame metadata scheduling and final GUID removal remain separate acceptance work. No class completion, handler increase, runtime or installed-save change is claimed.'])
    receipt = ROOT / 'tools/reference/bone-barrier-modifier-source-evidence.json'
    encoded = json.dumps(result, indent=2) + '\n'
    if check:
        assert receipt.read_text() == encoded
    else:
        receipt.write_text(encoded)
    print('Barrier UMod15:192 controlled graphs;2 negative sanitizer controls;runtime unchanged.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser()
    parser.add_argument('--check', action='store_true')
    build(parser.parse_args().check)
