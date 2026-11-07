#!/usr/bin/env python3
"""Compare shared startup dispatch to complete source in disposable worlds."""
import argparse
import os
from pathlib import Path
from check_iron_golem_birth import ROOT, check
from check_npc_item_skills import configuration as prior


def configuration(retained=False, isolate=False, shared=False):
    c = prior(retained, isolate, shared)
    suffix = '-shared' if shared else '-isolate' if isolate else '-retained' if retained else ''
    c['stage'] = ROOT / ('.local/class-skills/skill-startup' + suffix)
    if not shared:
        c['extra_families'] += ('skill-startup',)
        c['extra_files']['skill-startup-cases.json'] = ROOT / '.local/class-skills/skill-startup-source/cases.json'
    c['extra_inputs'] += (Path(__file__).resolve(), ROOT / 'tools/reference/skill-startup-source-evidence.json')
    return c


if __name__ == '__main__':
    p = argparse.ArgumentParser()
    p.add_argument('--retained', action='store_true')
    p.add_argument('--isolate', action='store_true')
    p.add_argument('--shared', action='store_true')
    a = p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(a.retained, a.isolate, a.shared))
