#!/usr/bin/env python3
"""Shared Arctic potion delivery, original cold controls and saved emitters."""
import argparse
import os
from pathlib import Path
from check_iron_golem_birth import ROOT, check
from check_skill_startup import configuration as prior


def configuration(retained=False, isolate=False, shared=False):
    # A small diagnostic cohort isolates this effect from unrelated NPC
    # fixtures. Focused/retained/shared cohorts preserve the prior suites.
    c = dict(extra_families=(),extra_files={},extra_inputs=()) if isolate else prior(retained, isolate, shared)
    suffix = '-shared' if shared else '-isolate' if isolate else '-retained' if retained else ''
    c['stage'] = ROOT/('.local/class-skills/arctic-potion'+suffix)
    if not shared:
        # The existing stream fixture snapshots the complete pool. Create our
        # saved shot first so that its original-cold regression owns that pool
        # snapshot too; neither fixture hides or drops the other's emissions.
        c['extra_families'] += ('arctic-potion','arctic-stream')
        for name in ['arctic-stream-damage','arctic-stream-factory']:
            filename=name+'-source-probe.json'
            c['extra_files'][filename]=ROOT/'.local/class-skills'/filename
        c['extra_live_commands']=(*c.get('extra_live_commands',()),'arctic-stream-live')
        c['checked_live_commands']=(*c.get('checked_live_commands',()),'arctic-stream-live')
        c['extra_pre_save_commands']=(*c.get('extra_pre_save_commands',()),'arctic-stream-expiry-stage-check')
    c['extra_inputs'] += (Path(__file__).resolve(),)
    return c


if __name__ == '__main__':
    p=argparse.ArgumentParser()
    p.add_argument('--retained',action='store_true')
    p.add_argument('--isolate',action='store_true')
    p.add_argument('--shared',action='store_true')
    a=p.parse_args()
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(**configuration(a.retained,a.isolate,a.shared))
