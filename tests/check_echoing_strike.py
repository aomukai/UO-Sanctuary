#!/usr/bin/env python3
"""Pinned calc aliases, Echoing supplier/saves and shared Mirrored regression."""
import os
from check_iron_golem_birth import ROOT,check
if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/echoing-strike',extra_families=('echoing-strike','mirrored-blades'),
          extra_files={'echoing-calc-aliases.json':ROOT/'.local/class-skills/echoing-strike/calc-aliases.json'},
          extra_inputs=(ROOT/'tests/check_echoing_strike.py',))
