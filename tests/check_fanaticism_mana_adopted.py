#!/usr/bin/env python3
"""Approved default Fanaticism with actual zero-mana right/quick inputs."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT, check
from check_fanaticism_mana import configuration as prior
from check_assassin_planned_acceptance import paced_clients

STAGE = ROOT / '.local/class-skills/fanaticism-mana-adopted'


def configuration():
    c = prior()
    c['stage'] = STAGE
    c['extra_inputs'] += (Path(__file__).resolve(), ROOT / 'tools/reference/fanaticism-mana-approval.json')
    return c


if __name__ == '__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    with paced_clients():
        check(**configuration())
