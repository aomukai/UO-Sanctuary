#!/usr/bin/env python3
"""Preserve native armor-ignore channels in an isolated game-content bridge."""
import argparse
import difflib
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import tarfile

ROOT = Path(__file__).resolve().parents[1]
PARENT = ROOT / '.local/class-skills/bind-demon-typed/native'
STAGE = ROOT / '.local/class-skills/unresisted-shields'
ARCHIVE = Path('/media/aomukai/FILES/Sanctuary-checkpoint-archives/revive-respec/unresisted-shields/native')
BUILD = Path('/dev/shm/sanctuary-unresisted-native-revive-respec')
PATCH = ROOT / 'patches/ModernUO/0075-typed-unresisted-shield-packet.patch'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def changes():
    result = {}
    name = 'Projects/UOContent/Mobiles/ClassCombatHooks.cs'
    before = (PARENT / name).read_text()
    needle = 'public static class ClassCombatHooks'
    assert before.count(needle) == 1
    after = before.replace(needle, '''// Native fields after the existing chaos allocation; no drain inference.
public readonly record struct ClassUnresistedPacket(int Damage, int Physical, int Fire,
    int Cold, int Poison, int Lightning, int Direct, bool Weapon, bool Ranged);
public static class ClassCombatHooks''')
    needle = '    public static Func<Mobile, Mobile, int, int, bool, bool, ClassSkillDamage?> SkillUnresistedDamage { get; set; }'
    assert after.count(needle) == 1
    after = after.replace(needle, needle + '\n    public static Func<Mobile, Mobile, ClassUnresistedPacket, ClassSkillDamage?> SkillUnresistedTypedDamage { get; set; }')
    result[name] = (before, after)
    name = 'Projects/UOContent/Misc/AOS.cs'
    before = (PARENT / name).read_text()
    needle = '                skillResistance = ClassCombatHooks.SkillUnresistedDamage?.Invoke(m, from, damage, phys, weaponDamage, archer);'
    assert before.count(needle) == 1
    after = before.replace(needle, '''                skillResistance = ClassCombatHooks.SkillUnresistedTypedDamage?.Invoke(m, from,
                    new ClassUnresistedPacket(damage, phys, fire, cold, pois, nrgy, direct, weaponDamage, archer))
                    ?? ClassCombatHooks.SkillUnresistedDamage?.Invoke(m, from, damage, phys, weaponDamage, archer);''')
    result[name] = (before, after)
    return result


def prepare(check=False):
    modified = changes()
    patch = ''.join(''.join(difflib.unified_diff(a.splitlines(True), b.splitlines(True),
        fromfile='a/' + name, tofile='b/' + name)) for name, (a, b) in modified.items())
    if check:
        assert PATCH.read_text() == patch
    else:
        assert not PATCH.exists() and not (STAGE / 'native').exists() and not ARCHIVE.exists() and not BUILD.exists()
        STAGE.mkdir(parents=True, exist_ok=True)
        ARCHIVE.mkdir(parents=True)
        shutil.copytree(PARENT, BUILD, ignore=shutil.ignore_patterns('.git', 'Distribution'))
        shutil.copytree(PARENT / 'Distribution', ARCHIVE / 'Distribution')
        (BUILD / 'Distribution').symlink_to(ARCHIVE / 'Distribution', target_is_directory=True)
        (STAGE / 'native').mkdir()
        (STAGE / 'native/Distribution').symlink_to(ARCHIVE / 'Distribution', target_is_directory=True)
        (STAGE / 'native/Projects').symlink_to(BUILD / 'Projects', target_is_directory=True)
        subprocess.run(['git', 'init', '--quiet', str(BUILD)], check=True)
        PATCH.write_text(patch)
        subprocess.run(['git', 'apply', '--check', str(PATCH)], cwd=BUILD, check=True)
        subprocess.run(['git', 'apply', str(PATCH)], cwd=BUILD, check=True)
        for name in modified:
            target = ARCHIVE / 'modified-sources' / name
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(BUILD / name, target)
        with tarfile.open(ARCHIVE / 'native-source-1.tar.gz', 'w:gz') as archive:
            for path in sorted(BUILD.rglob('*')):
                relative = path.relative_to(BUILD)
                if any(part in ('.git', 'bin', 'obj', 'Distribution') for part in relative.parts):
                    continue
                if path.is_file():
                    archive.add(path, arcname=str(relative), recursive=False)
    for name, (_, expected) in modified.items():
        actual = STAGE / 'native' / name
        if not actual.is_file():
            actual = ARCHIVE / 'modified-sources' / name
        assert actual.read_text() == expected, name
    result = dict(parent=str(PARENT.relative_to(ROOT)), patch=str(PATCH.relative_to(ROOT)),
        patchSha256=sha(PATCH), changedSources={name: dict(
            before=hashlib.sha256(a.encode()).hexdigest(), after=sha(ARCHIVE / 'modified-sources' / name))
            for name, (a, _) in modified.items()},
        sourceArchive=str(ARCHIVE / 'native-source-1.tar.gz'),
        sourceArchiveSha256=sha(ARCHIVE / 'native-source-1.tar.gz'),
        buildRoot=str(BUILD), buildRootVolatile=True, binariesAndSourceArchivePersisted=True,
        parentArtifacts={name: sha(PARENT / 'Distribution' / name)
            for name in ('ModernUO.dll', 'Server.dll', 'Assemblies/UOContent.dll')},
        serverEngineModified=False, additiveGameContentApi=True, newSavedFormats=0,
        rule='Supply actual normalized native armor-ignore components to an optional typed consumer, retaining the existing physical-only fallback when it returns null. No resistance/absorption/event/order calculation added to native code.')
    path = STAGE / 'native-source-evidence.json'
    text = json.dumps(result, indent=2) + '\n'
    if check:
        assert path.read_text() == text
    else:
        path.write_text(text)
    print('Typed armor-ignore bridge:two isolated game-content files;engine/saves unchanged.')


if __name__ == '__main__':
    raise SystemExit('Historical native-build producer retired. Patch 0075 is preserved in Git; '
                     'use ./dev build server for a current build. See docs/class-skill-history-retirement.md.')
