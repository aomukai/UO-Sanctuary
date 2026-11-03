#!/usr/bin/env python3
"""Tainted captured source suppliers and real owner/gear persistence."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/tainted-foundation-focus',
        extra_files={'tainted-formula-source-cases.json':ROOT/'.local/class-skills/tainted-foundation-source/formula.json',
                     'tainted-fireball-source-cases.json':ROOT/'.local/class-skills/tainted-foundation-source/fireball.json',
                     'goatman-creation-source-probe.json':ROOT/'.local/class-skills/goatman-creation-source-probe.json'},
        extra_families=('tainted-foundation',),extra_inputs=(Path(__file__).resolve(),))
