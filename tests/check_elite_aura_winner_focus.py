#!/usr/bin/env python3
"""Aura pulse winners use the same recipient/floor admission as their level."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT, check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/elite-aura-winner-focus',extra_checks=('elite-aura-winner-check',),extra_inputs=(Path(__file__).resolve(),))
