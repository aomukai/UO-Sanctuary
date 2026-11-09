#!/usr/bin/env python3
"""Build-stage the existing native tree plus one scoped, reproducible Ward bridge."""
import argparse,difflib,hashlib,json,shutil,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1];STAGE=ROOT/'.local/class-skills/psychic-ward-cast';PARENT=ROOT/'.local/class-skills/player-melee-return/native';PATCH=ROOT/'patches/ModernUO/0073-physical-ward-hit-context.patch'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def edits():
    result={}
    path='Projects/UOContent/Mobiles/ClassCombatHooks.cs';before=(PARENT/path).read_text();after=before.replace('    public int Physical256 { get; init; }','    public int Physical256 { get; init; }\n    public object WeaponContext { get; init; }')
    after=after.replace('    public static Action<Mobile, Mobile, int, bool> PreparedWeaponDamage { get; set; }','''    public static Action<Mobile, Mobile, int, bool> PreparedWeaponDamage { get; set; }
    public static Action<Mobile, Mobile, int, bool, object> PreparedWeaponDamageContext { get; set; }
    public static void DispatchPreparedWeaponDamage(Mobile source, Mobile target, int physical256, bool ranged, ClassSkillDamage? hit)
    {
        if (hit?.WeaponContext is { } context && PreparedWeaponDamageContext is { } receive)
        {
            receive(source, target, physical256, ranged, context);
        }
        else
        {
            PreparedWeaponDamage?.Invoke(source, target, physical256, ranged);
        }
    }''')
    old='    public static Func<Mobile, Mobile, int, int, int, int, int, int, bool, bool, ClassSkillDamage?> SkillResistedDamage { get; set; }';new='''    public static Func<Mobile, Mobile, int, int, int, int, int, int, bool, bool, bool, ClassSkillDamage?> SkillResistedDamage { get; set; }
    public static Func<Mobile, Mobile, int, int, bool, bool, ClassSkillDamage?> SkillUnresistedDamage { get; set; }''';assert old in after;after=after.replace(old,new);result[path]=(before,after)
    path='Projects/UOContent/Misc/AOS.cs';before=(PARENT/path).read_text();after=before.replace('damage, phys, fire, cold, pois, nrgy, spellDamage, weaponDamage);','damage, phys, fire, cold, pois, nrgy, spellDamage, weaponDamage, archer);')
    needle='ClassCombatHooks.PreparedWeaponDamage?.Invoke(from, m, preparedPhysical, archer);';assert after.count(needle)==2;after=after.replace(needle,'ClassCombatHooks.DispatchPreparedWeaponDamage(from, m, preparedPhysical, archer, skillResistance);')
    needle='            var bcFrom = from as BaseCreature;';assert after.count(needle)==1;after=after.replace(needle,'''            if (ignoreArmor)
            {
                skillResistance = ClassCombatHooks.SkillUnresistedDamage?.Invoke(m, from, damage, phys, weaponDamage, archer);
                if (skillResistance is { } unresisted)
                {
                    totalDamage -= damage - unresisted.Total;
                }
            }

'''+needle)
    needle='''            if (totalDamage <= 0 && fixedTotal256 is not > 0)
            {
                return 0;
            }''';assert after.count(needle)==1;after=after.replace(needle,'''            if (totalDamage <= 0 && fixedTotal256 is not > 0)
            {
                if (weaponDamage && skillResistance?.WeaponContext != null)
                {
                    var physical256 = skillResistance.Value.Physical256;
                    ClassCombatHooks.DispatchPreparedWeaponDamage(from, m, physical256, archer, skillResistance);
                }
                return 0;
            }''');result[path]=(before,after)
    assert all(a!=b for a,b in result.values());return result
def prepare(check=False):
    changes=edits();patch=''.join(''.join(difflib.unified_diff(a.splitlines(True),b.splitlines(True),fromfile='a/'+name,tofile='b/'+name))for name,(a,b)in changes.items())
    if check:
        assert PATCH.read_text()==patch
    else:
        assert not PATCH.exists();PATCH.write_text(patch)
        assert not(STAGE/'native').exists();STAGE.mkdir(exist_ok=True)
        shutil.copytree(PARENT,STAGE/'native',ignore=shutil.ignore_patterns('.git'))
        subprocess.run(['git','init','--quiet',str(STAGE/'native')],check=True)
        subprocess.run(['git','apply','--check',str(PATCH)],cwd=STAGE/'native',check=True)
        subprocess.run(['git','apply',str(PATCH)],cwd=STAGE/'native',check=True)
    for name,(_,expected)in changes.items():assert(STAGE/'native'/name).read_text()==expected,name
    result=dict(parent='player-melee-return/native',patch=str(PATCH.relative_to(ROOT)),patchSha256=sha(PATCH),changedSources={name:dict(before=hashlib.sha256(a.encode()).hexdigest(),after=sha(STAGE/'native'/name))for name,(a,b)in changes.items()},serverEngineModified=False,newSavedFormats=0)
    path=STAGE/'native-source-evidence.json';text=json.dumps(result,indent=2)+'\n'
    if check:assert path.read_text()==text
    else:path.write_text(text)
    print('Ward native bridge staged in separate tree;two content files only,no engine/save changes.')
if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('--check',action='store_true');prepare(p.parse_args().check)
