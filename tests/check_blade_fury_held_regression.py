#!/usr/bin/env python3
"""Retain the full original Fury/sentry component cohort on the held build."""
import os
from pathlib import Path
from check_blade_fury_held_candidate import configuration as prior
from check_iron_golem_birth import ROOT, check

if __name__ == '__main__':
    c = prior(retained=True)
    c['stage'] = ROOT / '.local/class-skills/blade-fury-held-input-fury-retained'
    c['extra_inputs'] += (Path(__file__).resolve(),)
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**c)
