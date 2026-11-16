#!/usr/bin/env python3
"""Warlock foundations, three demons, Death Mark and Bind: existing native controls."""
import argparse
import hashlib
import json
import os
from pathlib import Path

from check_iron_golem_birth import ROOT, check
from check_assassin_planned_acceptance import paced_clients
from check_bind_demon_learned import wires as bind_wires

STAGE = ROOT / '.local/class-skills/warlock-planned-acceptance'
ROUTES = ('Demonic Mastery', 'Summon Goatman', 'Summon Tainted',
          'Summon Defiler', 'Death Mark', 'Bind Demon', 'Levitation Mastery')
FROZEN = ('Blood Oath', 'Blood Boil', 'Engorge', 'Consume')
FAMILIES = ('goatman-cast', 'goatman-native-host', 'goatman-body',
    'tainted-ground-movement', 'tainted-native-decision', 'tainted-body', 'tainted-cast',
    'defiler-link-numbers', 'defiler-body', 'defiler-health-link',
    'defiler-flying-motion', 'defiler-flying-world', 'defiler-link-casting',
    'defiler-cast', 'death-mark-action', 'demonic-mastery', 'warlock-cast-clock',
    'levitation', 'bind-demon-host-audit', 'bind-demon-input', 'bind-demon-learned')
CONNECTED = ('goatman-cast', 'goatman-native-host', 'tainted-native-decision',
    'tainted-cast', 'defiler-health-link', 'defiler-flying-world',
    'defiler-link-casting', 'defiler-cast', 'death-mark-action', 'demonic-mastery',
    'bind-demon-host-audit', 'bind-demon-input', 'bind-demon-learned')
INPUTS = ('goatman-cast-right', 'goatman-cast-quick',
    'tainted-cast-right', 'tainted-cast-quick', 'defiler-cast-right', 'defiler-cast-quick',
    'death-mark-input-left', 'death-mark-input-right', 'death-mark-input-quick')


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def configuration():
    clock_path = ROOT / 'tools/reference/warlock-cast-clock-data-evidence.json'
    clock = json.loads(clock_path.read_text())['sourceFixture']
    source = ROOT / clock['path']
    assert sha(source) == clock['sha256'], 'Pinned shared SC control changed'
    files = {
        'defiler-link-source-cases.json': ROOT / '.local/class-skills/defiler-link-source/formula.json',
        'defiler-link-cast-source-cases.json': ROOT / '.local/class-skills/defiler-link-cast-source/cases.json',
        'defiler-flying-motion-source-cases.json': ROOT / '.local/class-skills/defiler-flying-source/cases.json',
        'warlock-shared-sc-cases.json': source,
    }
    names = tuple(family + suffix for family in CONNECTED
                  for suffix in ('-check', '-reload-check'))
    waits = {name: name + '-result' for name in names}
    waits['levitation-validation-check'] = 'levitation-validation-result'
    inputs = [Path(__file__).resolve(),
        ROOT / 'tests/check_assassin_planned_acceptance.py',
        ROOT / 'tests/check_bind_demon_learned.py',
        ROOT / 'tests/check_death_mark_learned.py',
        ROOT / 'tests/check_demonic_mastery.py', ROOT / 'tests/check_warlock_cast_clock.py',
        ROOT / 'tests/check_levitation_mastery.py', clock_path]
    inputs += [ROOT / 'tools/reference' / name for name in (
        'demonic-mastery-source-review.json', 'bind-demon-world-rule-approval.json',
        'death-mark-native-world-approval.json', 'death-mark-recipient-approval.json',
        'death-mark-action-approval.json', 'goatman-native-host-approval.json',
        'item-content-scope-decision.json', 'summon-snapshot-decision.json')]
    for path in (*files.values(), *inputs):
        assert path.is_file(), path
    status = json.loads((ROOT / 'data/Sanctuary/Balance/class-skill-status.json').read_text())
    talents = [row for row in status['skills'] if row['characterClass'] == 'Warlock']
    assert len(talents) == 30
    assert all(next(row for row in talents if row['name'] == name)['implementation']
        == 'handler-present' for name in ROUTES)
    assert all(next(row for row in talents if row['name'] == name)['implementation']
        == 'pending' for name in FROZEN)
    assert status['contentScope']['charms'] is False and status['contentScope']['runewords'] is False
    return dict(stage=STAGE, extra_files=files, extra_families=FAMILIES,
        extra_checks=('levitation-validation-check',),
        extra_connected_checks=names, extra_wait_results=waits,
        extra_connection_settle=.15, extra_target_input_commands=INPUTS,
        extra_wire_callbacks=(bind_wires,),
        save_command='defiler-link-casting-save', save_result='defiler-link-casting-save-result',
        connected_save=True, extra_inputs=tuple(inputs))


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--plan', action='store_true')
    args = parser.parse_args()
    config = configuration()
    if args.plan:
        print(json.dumps(dict(routes=ROUTES, frozenPending=FROZEN,
            fixtureFamilies=len(FAMILIES), sharedChecks=1,
            currentInputCommands=len(INPUTS), bindWireSequencesPerBoot=2,
            cachedReferenceFiles=len(config['extra_files']),
            save='Existing connected Defiler link save; three boots/two reloads'), indent=2))
    else:
        os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
        with paced_clients():
            check(**config)
