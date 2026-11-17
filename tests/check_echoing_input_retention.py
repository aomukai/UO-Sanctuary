#!/usr/bin/env python3
"""Original Warlock/Witch Echoing inputs with retained Hex inspection and two reloads."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
STAGE=ROOT/'.local/class-skills/echoing-input-retention'
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=STAGE,extra_families=('echoing-strike-inputs',),extra_inputs=(Path(__file__).resolve(),))
