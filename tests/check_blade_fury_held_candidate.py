#!/usr/bin/env python3
"""Review-only Blade Fury input policy; no production handler changes."""
import argparse
import os
from pathlib import Path
from check_iron_golem_birth import ROOT, check
from check_blade_fury_reentry import configuration as previous


def configuration(retained=False):
    if retained:
        c = previous()
        c['stage'] = ROOT / '.local/class-skills/blade-fury-held-candidate-retained'
        c['extra_families'] += ('blade-fury-held-candidate',)
        c['extra_connected_checks'] += ('blade-fury-held-candidate-check', 'blade-fury-held-candidate-reload-check')
        c['extra_inputs'] += (Path(__file__).resolve(),)
        c['extra_connection_settle'] = 0.3
        return c
    return dict(stage=ROOT / '.local/class-skills/blade-fury-held-candidate',
                extra_families=('blade-fury-held-candidate',),
                extra_connected_checks=('blade-fury-held-candidate-check', 'blade-fury-held-candidate-reload-check'),
                extra_inputs=(Path(__file__).resolve(),), extra_connection_settle=0.3)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--retained', action='store_true')
    args = parser.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(args.retained))
