#!/usr/bin/env python3
"""Approved default Blood Oath with original/bound saves and fresh learned controls."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT, check
from check_assassin_planned_acceptance import paced_clients

STAGE = ROOT / '.local/class-skills/blood-oath-learned'


def configuration():
    families = ('blood-oath-damage', 'bound-demon-oath')
    commands = tuple(n for family in families for n in (family + '-check', family + '-reload-check'))
    return dict(stage=STAGE, extra_families=families,
        extra_checks=('blood-oath-learned-check',),
        extra_connected_checks=(*commands, 'blood-oath-learned-check'),
        extra_wait_results={**{n: n + '-result' for n in commands}, 'blood-oath-learned-check': 'blood-oath-learned-result'},
        extra_connection_settle=.15,
        extra_inputs=(Path(__file__).resolve(), ROOT / 'tests/check_assassin_planned_acceptance.py',
            ROOT / 'tools/reference/blood-oath-source-review.json', ROOT / 'tools/reference/bind-demon-monsters-source.json',
            ROOT / 'tools/reference/warlock-final-talents-approval.json'))


if __name__ == '__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():
        check(**configuration())
