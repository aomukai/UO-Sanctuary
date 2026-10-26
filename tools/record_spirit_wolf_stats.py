#!/usr/bin/env python3
"""Bind native Spirit Wolf numeric preparation to source cases and real gear checks."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
BASE = ROOT / '.local/class-skills'
STAGE = BASE / 'beasts'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def record(check=False):
    spec = importlib.util.spec_from_file_location('linear_fix_record', ROOT / 'tools/record_linear_formula_fix.py')
    module = importlib.util.module_from_spec(spec); spec.loader.exec_module(module)
    module.inventory(STAGE / 'stats-inputs.json')
    log = (STAGE / 'stats-native-checks.log').read_text()
    assert ' ERR]' not in log and ': failed' not in log
    for command in ['spirit-wolf-stats', 'skill-linear', 'creeper-stats', 'raven-stats', 'raven-flight', 'toward-path', 'valkyrie-creation', 'named-gear']:
        assert log.count(command + '-check: passed') == 1, command
    assert '3825 complete unmastered cold getters and45900 complete do119/base/passive contexts match' in log
    assert log.count('Shutting down') == 1
    source = json.loads((ROOT / 'tools/reference/spirit-wolf-creation-source-evidence.json').read_text())
    assert source['fixture']['cold'] == 7650 and source['fixture']['birth'] == 45906
    assert sha(ROOT / source['fixture']['path']) == source['fixture']['sha256']
    for name, digest in source['generatedSourceHashes'].items():
        assert sha(ROOT / name) == digest, name
    for name, digest in source['sourceHashes'].items():
        assert sha(BASE / 'd2moo-pinned-source' / name) == digest, name
    warnings = {}
    for kind in ['content', 'integration']:
        text = (STAGE / (kind + '-build.log')).read_text()
        assert 'Build succeeded.' in text and '0 Error(s)' in text
        warnings[kind] = int(re.findall(r'(\d+) Warning\(s\)', text)[-1]); assert warnings[kind] == 0
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    present = [x for x in status['skills'] if x['implementation'] == 'handler-present']
    assert len(present) == 166 and sum(x['characterClass'] == 'Druid' for x in present) == 10 and not status['completedClasses']
    paths = [ROOT / p for p in ['src/Sanctuary.Content/DruidSpiritWolfStats.cs', 'src/Sanctuary.Content/ValkyrieCreation.cs',
        'src/Sanctuary.Content/SkillMechanics.cs', 'tests/Sanctuary.Integration/SpiritWolfStatsChecks.cs', 'tests/check_druid_beasts.py',
        'tools/build_class_skills.py', 'tools/probe_spirit_wolf_creation_reference.py', 'tools/record_spirit_wolf_stats.py',
        'tools/reference/spirit-wolf-creation-source-evidence.json', 'data/Sanctuary/Balance/necromancer-summons.json',
        'data/Sanctuary/Balance/summon-level-stats.json']]
    paths += [STAGE / p for p in ['stats-inputs.json', 'stats-native-checks.log', 'stats-run.log', 'content/Sanctuary.Content.dll',
        'integration/Sanctuary.Integration.dll', 'content-build.log', 'integration-build.log']]
    evidence = dict(referenceVersion='3.3.93847', handlerEnabled=False, actorEnabled=False, installed=False,
                    handlers=166, druidHandlers=10, completedClasses=[], goalComplete=False, boots=1, actualReloads=0,
                    fullClassRegression=False, buildWarnings=warnings, sourceAndNativeColdCases=3825, sourceAndNativeBirthCases=45900,
                    sourceAndNativeComparisons=49725, sourceOnlyCreationFailures=6,
                    hashes={str(p.relative_to(ROOT)): sha(p) for p in paths},
                    scope='Prepared numeric do119/cold components only. The broader shared-rank candidate regression and any installation have separate evidence.',
                    established=[
                        'Native unmastered cold endpoints/length and whole-life passive truncation match3825 complete source getter cases across all255ranks, five effective Grizzly ranks and three deliberately ignored owner-mastery contexts. Nonzero applied-mastery source controls are retained separately.',
                        'Native supplied-spawn do119/base/passive components match45900 successful source contexts across three owner levels,255ranks,three difficulties,two game contexts,five paired effective Dire/Grizzly ranks and fractional fixed-point base-life controls. Explicit owner level, base monlvl additions, separate armor/tohit passives, four capped resistance values, captured cold and equipment request are preserved.',
                        'Actual learned Druid and Tarnhelm snapshots check rank1-to2,143-to156life, unchanged pre-passive32FP regeneration,2–6to3–7cold without Grizzly, immutable prior snapshots and fresh effective Grizzly synergy. No pet actor or ordinary attack is inferred from these owner snapshots.',
                        'Effective28/hard20 wolf/Dire/Grizzly inputs match the user1040life and343–359cold observations. Changing supplied hard ranks alone leaves those effective-rank results unchanged. Copied armor-percent516 is distinguished from the tooltip linear bonus320. The source sixth cold-sunder value17 is retained as data only.',
                        'Shared linear/curse corrections and existing prepared Creeper, learned Raven stats/flight/blind/path, toward path,Valkyrie creation and named-item consumers pass the same disposable boot. Builds have zero warnings/errors.'
                    ], limits=[
                        'Source-reviewed130HP/67defense/0initialAR/32regen bases are supplied to the complete creation oracle; full actual modern spawn initialization is not executed by it. Source formula/stat/allocation/world services remain bounded. Birth source contexts pair Dire/Grizzly ranks; getter contexts separately vary Grizzly.',
                        'Current slot6 and nonzero cold sunder are not covered by the older five-slot helper or a live consumer. Raw base stats/passives do not prove complete runtime AR/defense/resistance/attack delivery or stat capture/update timing.',
                        'No learned casting or saved wolf exists yet. Individual target/follow/teleport/AI/animation/cold-hit/regen/health-rescale/lifecycle/input/two-reload acceptance, exact modern fallback polarity/global RNG/geometry/client and final effects/audio/icons remain pending.',
                        'No full-class parity or installation is claimed by these numeric checks; coverage stays166/Druid10. Existing wolves retaining their own targets is separately recorded from the user and must not be replaced by Raven flock focus.'
                    ])
    path = ROOT / 'tools/reference/spirit-wolf-stats-native-evidence.json'; encoded = json.dumps(evidence, indent=2) + '\n'
    if check:
        assert path.read_text() == encoded
    else:
        path.write_text(encoded)
    print('Spirit Wolf prepared numeric backend:49725 source/native comparisons and actual soft-rank/immutable snapshot checks; actor/handler pending.')


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__); parser.add_argument('--check', action='store_true')
    record(parser.parse_args().check)
