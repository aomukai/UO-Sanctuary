#!/usr/bin/env python3
"""Bound the next demon path work to owned species data and retained source."""
import argparse,hashlib,json,re
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
def record(check=False):
    inputs={}
    def read(name):
        p=ROOT/name;raw=p.read_bytes();inputs[name]=hashlib.sha256(raw).hexdigest();return json.loads(raw)if p.suffix=='.json'else raw.decode()
    monsters=read('tools/reference/d2r-named-equipment/json/monstats.json');secondary=read('tools/reference/d2r-named-equipment/json/monstats2.json')
    base='.local/class-skills/d2moo-pinned-source/source/D2Common/'
    source=read(base+'src/Path/Path.cpp');collision=read(base+'include/D2Collision.h');header=read(base+'include/Path/Path.h')
    allocator=source.split('void __stdcall PATH_AllocDynamicPath(',1)[1].split('//D2Common.0x6FDA9720',1)[0]
    assert 'D2_CALLOC_STRC_POOL' in allocator and 'PATH_ToFP16Center(nX)' in allocator and 'PATH_ToFP16Center(nY)'in allocator
    assert 'PATH_SetType(pDynamicPath, PATHTYPE_TOWARD)'in allocator and 'dwMonStatsFlags & gdwBitMasks[MONSTATSFLAGINDEX_FLYING]'in allocator
    assert 'dwMonStatsFlags & gdwBitMasks[MONSTATSFLAGINDEX_OPENDOORS]'in allocator and 'if (bSetFlag)'in allocator
    constants={name:int(value,16)for name,value in re.findall(r'\b(COLLIDE_\w+)\s*=\s*(0x[0-9A-Fa-f]+)',collision)}
    ground=constants['COLLIDE_WALL']|constants['COLLIDE_OBJECT']|constants['COLLIDE_NO_PATH']|constants['COLLIDE_PET']
    flying=constants['COLLIDE_MISSILE_BARRIER']|constants['COLLIDE_DOOR']|constants['COLLIDE_NO_PATH']
    assert ground==0x3401 and flying==0x1804 and constants['COLLIDE_MONSTER']==0x100
    assert '(value << 16) + (1 << 15)'in header
    rows=[]
    for name in ['wargoatman','warbighead','warputriddefiler']:
        m=next(r for r in monsters.values()if r.get('Id')==name);s=next(r for r in secondary.values()if r.get('Id')==m['MonStatsEx'])
        assert m['opendoors']==1
        rows.append(dict(sourceClass=m['*hcIdx'],name=name,secondary=m['MonStatsEx'],sizeX=s['SizeX'],sizeY=s['SizeY'],flying=bool(m.get('flying',0)),opensDoors=True,
            legacyAllocatorMoveMask=hex(flying if m.get('flying',0)else ground),maskParityWithCurrentConstructor=False))
    assert [(r['name'],r['sizeX'],r['sizeY'],r['flying'])for r in rows]==[('wargoatman',2,2,False),('warbighead',3,3,False),('warputriddefiler',2,2,True)]
    current=read('src/Sanctuary.Content/GenericPetPathCommit.cs');assert 'p.Word30=0;p.Word34=0;p.Raw96=s.Reset96;'in current
    commit=read('tools/reference/generic-pet-path-commit-source-evidence.json');assert commit['referenceVersion']=='3.3.93847'and commit['reset96']==0
    placement=read('src/Sanctuary.Content/GenericPetMonsterPlacement.cs');assert 'unit.Word180,0x1c09,0x1c09'in placement
    read('src/Sanctuary.Content/GenericPetNativePlacement.cs');read('src/Sanctuary.Content/GenericPetBirthStartup.cs');read('tools/reference/pet-birth-startup-native-evidence.json')
    read('tools/reference/sigil-death-reference-gap.json');read('tools/reference/slow-missiles-preflight.json')
    read('tools/record_pet_birth_path_preflight.py')
    report=dict(referenceVersion='3.3.93847',component='demon birth-path source preflight',runtimeBehaviorChanged=False,handlers=220,warlockHandlers=11,completedClasses=[],newReferenceClientReads=0,newBuilds=0,species=rows,
        established=['Owned current monstats/monstats2 distinguish Defiler flying and Tainted3x3 versus Goatman/Defiler2x2 source dimensions. Appearance mappings do not supply these movement properties.',
        'Retained older common path allocator centers source cells at FP16+0x8000 and branches flying before open-door ground movement. Older source masks are0x1804/0x3401;these are not established current-constructor outputs.',
        'Current qualified reset clears0x20 and both path counters and sets Raw96to0. That is reset behavior,not proof of all fresh path constructor values.',
        'Current qualified monster placement uses its own0x1c09 nearby-search masks. Do not overwrite these with a legacy path movement mask;the operations are distinct.'],
        unresolved=['Current common path constructor/caller mapping,including optional bSetFlag,field layout,source unit size versus collision-pattern semantics and initial native path type/flags remain unqualified.',
        'Existing native body/path acceptance validates identity/position/mask bounds,but does not certify species-derived birth footprints or flying terrain behavior. Fixture Size5c1 and mask0x1c09 are not production defaults.',
        'Sigil:Death still needs event33/34 identity/bookkeeping and do153 explosion rules;shared Sigil launch alone is insufficient.',
        'Slow Missiles still needs native ordinary ranged launch/travel/impact and relevant spell/breath/child scopes. Native BaseRanged.OnSwing resolves CheckHit/OnHit immediately after OnFired.'],
        next='Qualify the shared constructor or prepare an explicit complete native world adaptation from these distinct species/collision requirements;reuse existing path commit/placement/AI policies. Do not enable learned summons or Slow Missiles from cast-only evidence.',inputs=inputs)
    path=ROOT/'tools/reference/pet-birth-path-preflight.json';encoded=json.dumps(report,indent=2)+'\n'
    if check:assert path.read_text()==encoded,'Birth-path preflight stale'
    else:path.write_text(encoded)
    print('Birth-path preflight:current species dimensions/flying distinction pinned;legacy masks separated from current nearby search/reset;constructor/producers remain explicit gaps;no runtime changes.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');record(p.parse_args().check)
