#!/usr/bin/env python3
"""Current combined proof for four pending talents; no learned gate is opened."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT, check
from check_assassin_planned_acceptance import paced_clients

STAGE = ROOT / '.local/class-skills/warlock-remaining-review'
FAMILIES = ('blood-oath-damage', 'engorge-world', 'blood-boil-world',
            'consume-suppliers', 'consume-world', 'bound-demon-typed',
            'bound-demon-recipient', 'bound-demon-engorge', 'bound-demon-oath',
            'bound-demon-consume', 'consume-aura')


def configuration():
    commands = tuple(name for family in FAMILIES for name in (family + '-check', family + '-reload-check'))
    return dict(stage=STAGE, extra_families=FAMILIES,
        extra_connected_checks=commands, extra_wait_results={n: n + '-result' for n in commands},
        extra_connection_settle=.15,
        extra_inputs=(Path(__file__).resolve(), ROOT / 'tests/check_assassin_planned_acceptance.py',
            ROOT / 'tools/reference/blood-oath-source-review.json',
            ROOT / 'tools/reference/engorge-source-review.json',
            ROOT / 'tools/reference/blood-boil-source-review.json',
            ROOT / 'tools/reference/d2r-consume/sources.json',
            ROOT / 'tools/reference/bind-demon-monsters-source.json'))


if __name__ == '__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():
        check(**configuration())
