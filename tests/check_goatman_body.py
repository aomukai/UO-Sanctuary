#!/usr/bin/env python3
"""Saved prepared Goatman body with current suppliers and retained original items."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/goatman-body',
        extra_files={n:ROOT/'.local/class-skills'/n for n in ('goatman-formula-source-probe.json','goatman-creation-source-probe.json','goatman-tooltip-observation.json')},
        extra_families=('goatman-body','goatman-foundation'),
        extra_inputs=(Path(__file__).resolve(),))
