#!/usr/bin/env python3
"""Barbarian mouse/save policy with the three existing class binding regressions."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT, check

def configuration():
    classes=('barbarian','necromancer','sorceress','paladin')
    return dict(stage=ROOT/'.local/class-skills/barbarian-bindings',
        extra_families=tuple(name+'-binding' for name in classes),
        extra_files={name+'-binding-reference.json':ROOT/'tools/reference'/ (name+'-binding-reference.json') for name in classes},
        extra_inputs=(Path(__file__).resolve(),))

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration())
