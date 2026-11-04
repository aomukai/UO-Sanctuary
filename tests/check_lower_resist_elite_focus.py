#!/usr/bin/env python3
"""Original Lower Resist/Conversion recipient with actual elite resistance mods."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT, check
if __name__ == '__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT/'.local/class-skills/lower-resist-elite-focus', extra_families=('lower-resist-elite',), extra_inputs=(Path(__file__).resolve(),))
