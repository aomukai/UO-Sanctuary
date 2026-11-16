#!/usr/bin/env python3
"""Narrow original-item Fire Ball producer and actual saved NPC travel."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT, check

STAGE = ROOT / '.local/class-skills/iron-golem-fireball'


def configuration():
    return dict(stage=STAGE, extra_families=('iron-golem-fireball',),
                extra_inputs=(Path(__file__).resolve(),))


if __name__ == '__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration())
