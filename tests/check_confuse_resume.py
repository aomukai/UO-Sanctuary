#!/usr/bin/env python3
"""Actual post-load Confuse targeting/combat with the retained Necromancer cohort."""
import sys
from pathlib import Path
from check_blood_life_tap import configuration
from check_iron_golem_birth import ROOT, check

if __name__ == '__main__':
    focused = '--focused' in sys.argv
    base = {} if focused else configuration('check_parking_boundaries.py')
    base['stage'] = ROOT / ('.local/class-skills/confuse-resume-targeted' if focused else '.local/class-skills/confuse-resume')
    base['extra_families'] = ('confuse-resume', *base.get('extra_families', ()))
    base['extra_connected_checks'] = ('confuse-resume-reload-check', *base.get('extra_connected_checks', ()))
    base.setdefault('extra_wait_results', {})['confuse-resume-reload-check'] = 'confuse-resume-reload-result'
    base['extra_inputs'] = (*base.get('extra_inputs', ()), Path(__file__).resolve(), ROOT / 'tests/check_parking_boundaries.py')
    check(**base)
