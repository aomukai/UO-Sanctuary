#!/usr/bin/env python3
"""Pin reviewed blade AI-to-wall-follow path flow; not a compiled movement proof."""
import argparse,hashlib,json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills'
def build(check=False):
 tree=json.loads((STAGE/'d2moo-source-tree.json').read_text());assert tree['sha']=='0c0bbd1e6bef9181a3da1c046523af622a925416';hashes={};blocks={}
 def source(rel):
  raw=(STAGE/'d2moo-pinned-source'/rel).read_bytes();assert hashlib.sha1(b'blob '+str(len(raw)).encode()+b'\0'+raw).hexdigest()==next(x['sha'] for x in tree['tree'] if x['path']==rel);hashes[rel]=hashlib.sha256(raw).hexdigest();return raw.decode()
 def routine(text,name):
  m=re.search(r'^.*\b'+name+r'\([^;\n]*\)\s*\n\{',text,re.M);assert m,name;i=text.index('{',m.start())+1;depth=1
  while depth:depth+=(text[i]=='{')-(text[i]=='}');i+=1
  b=text[m.start():i];blocks[name]=hashlib.sha256(b.encode()).hexdigest();return b
 think=source('source/D2Game/src/AI/AiThink.cpp');tactics=source('source/D2Game/src/AI/AiTactics.cpp');mode=source('source/D2Game/src/MONSTER/MonsterMode.cpp');header=source('source/D2Common/include/Path/Path.h');dispatch=source('source/D2Common/src/Path/Path.cpp');source('source/D2Common/src/Path/PathWF.cpp');source('source/D2Common/include/Path/PathWF.h');collision=source('source/D2Common/src/D2Collision.cpp');units=source('source/D2Common/src/Units/Units.cpp');monster=source('source/D2Game/src/MONSTER/Monster.cpp')
 blade=routine(think,'AITHINK_Fn102_BladeCreeper');velocity=routine(tactics,'AITACTICS_SetVelocity');walk=routine(tactics,'AITACTICS_WalkToTargetCoordinatesDeleteAiEvent');move=routine(tactics,'AITACTICS_MoveToTarget');setter=routine(mode,'D2GAME_MONSTERMODE_SetVelocityParams_6FC62FC0');change=routine(mode,'D2GAME_ModeChange_6FC65220');path=routine(mode,'sub_6FC65680')
 assert 'AITACTICS_SetVelocity(pUnit, 15, 0, 20);' in blade
 assert 'D2GAME_MONSTERMODE_SetVelocityParams_6FC62FC0(pUnit->pMonsterData->pAiParam, a2, nVel, a4);' in velocity
 assert 'return AITACTICS_MoveToTarget(pGame, pUnit, nullptr, MONMODE_WALK, nX, nY, 1u, 4);' in walk
 assert 'D2GAME_ModeChange_6FC65220(pGame, &modeChange, 1)' in move and 'D2GAME_EVENTS_Delete_6FC34840' in move
 assert re.search(r'if \(nVel\)\s*\{\s*pAiParam->nVelocity = nVel;',setter)
 assert 'pAiParam->unk0x18 = a2;' in setter and 'pAiParam->unk0x20 = std::min(a4, 77ui8);' in setter
 assert 'nPathType = pAiParam->unk0x18;' in change and 'sub_6FC65680(pUnit, nPathType, pAiParam, dwNewDist)' in change
 assert 'PATH_SetType(pUnit->pDynamicPath, nPathType);' in path and 'D2Common_10142(pUnit->pDynamicPath, pUnit, 0);' in path
 allocation=routine(dispatch,'PATH_AllocDynamicPath');mapping=routine(dispatch,'D2Common_11281_CollisionPatternFromSize');distance=routine(dispatch,'PATH_SetNewDistance')
 collision_check=routine(collision,'COLLISION_CheckAnyCollisionWithPattern');footprint=routine(collision,'COLLISION_SetMaskWithPattern')
 monster_stats=routine(monster,'MONSTER_InitializeStatsAndSkills');base_velocity=routine(units,'UNITS_GetBaseVelocity');walk_velocity=routine(units,'UNITS_UpdateRunWalkAnimRateAndVelocity')
 assert 'STATLIST_SetUnitStat(pUnit, STAT_VELOCITYPERCENT, 75, 0);' in monster_stats
 assert 'pDynamicPath->nDistMax = nNewDistance;' in distance and 'pDynamicPath->nDist = nNewDistance;' in distance
 assert 'return DATATBLS_GetMonStatsTxtRecord(nClassId)->nVelocity << 8;' in base_velocity
 assert 'PATH_SetVelocity(pUnit->pDynamicPath, nVelocityPercent * nVelocity / 100' in walk_velocity
 assert 'case COLLISION_PATTERN_NONE:' in collision_check and 'return COLLISION_CheckMask(pRoom, nX, nY, nMask);' in collision_check
 assert 'case COLLISION_PATTERN_NONE:' not in footprint
 assert re.search(r'PATHTYPE_WALL_FOLLOW\s*=\s*15',header) and '/*[15]*/,PATH_ComputePathOrSlideAlongObstacles' in dispatch
 e=dict(referenceVersion='3.3.93847',talentId='assassin.bladesentinel',handlerEnabled=False,legacyCommit=tree['sha'],sourceHashes=hashes,reviewedRoutineHashes=blocks,compiled=False,established=[
 'Blade AI calls SetVelocity(15,0,20). The first argument becomes AI path-type override15, not a15percent velocity bonus. Zero velocity argument leaves velocity override untouched; distance override is20, capped77 by its setter.',
 'WalkToTargetCoordinatesDeleteAiEvent uses MONMODE_WALK and movement flags4. MoveToTarget sets one step and invokes mode change; failure deletes the old AI event before returning0 to BladeCreeper reversal/owner fallback logic.',
 'ModeChange consumes path-type override, passes it through sub_6FC65680, which sets new distance/path type and runs path calculation. Path enum15 is WALL_FOLLOW and dispatch index15 maps to PATH_ComputePathOrSlideAlongObstacles.',
 'Thus endpoint movement requires source wall-follow behavior, not a straight ballistic return or a substituted general pet chase. Exact spatial/velocity/event projection still requires the full relevant path and mode consumers.'
 ],sourceKernelEvidence='tools/reference/wall-follow-engine-evidence.json',additionalReviewedFacts=['Current blade SizeX0 selects collision patternNONE, which still checks the masked center cell. Its footprint setter has no NONE case, so it adds no blocking footprint. Full pattern checker/setter are now included in the separate13200-case proof.','The generic monster spawn initializer sets velocitypercent75. Base monster velocity is monstatsVelocity<<8; the run/walk updater applies the derived percentage with minimum25. Exact selector/mode/stat/clock chain remains to be executed before choosing native speed.','PATH_SetNewDistance updates both nDistMax and nDist, capped at77. Thus the Blade AI distance20 enters the wall-follow budget; it is distinct from the separate PATH_SetDistance/nDistance field.','ISATT gates CANBEATTACKED in the reviewed monster initializer. Current blade row lacks isAtt; do not conflate this with the separate TARGETABLE flag assigned by MONSTER_Initialize. Full spawn flags and native nonattackability remain acceptance work.'],supplementarySourceCaveats=['D2Common_10142 general path initializer is explicitly marked BROKEN in the reconstruction. The separately compiled/ported PathWF kernel does not certify that entire initializer or current binary path flow.','PathWF documents an empty-path INT_MAX fix relative to original memory behavior. Native follows the reconstruction within recorded bounds and refuses undefined sync-index/overflow edges; modern acceptance remains open.'],remaining=['Full PathWF kernel is compiled/ported and compared separately; connect the actual source room/pattern/mask world projection.','Map source mode velocity/base12 and modifiers, path advancement, AI completion/failure event scheduling and lifetime checks to native time/half-tile coordinates.','Keep exact modern terrain/movement acceptance explicit; this is an immutable source-flow review, not a full compiled path or native motion certificate.'])
 p=ROOT/'tools/reference/blade-sentinel-movement-review.json';render=json.dumps(e,indent=2)+'\n'
 if check:assert p.read_text()==render
 else:p.write_text(render)
 print('Blade Sentinel movement flow pinned: path15 WALL_FOLLOW, distance20; velocity argument0 is not a new speed override. Full path/native motion remains next.')
if __name__=='__main__':
 p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');build(p.parse_args().check)
