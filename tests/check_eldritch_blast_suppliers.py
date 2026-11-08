#!/usr/bin/env python3
"""Focused supplier/real gear/reload checks; no duplicate old compiler matrices."""
import os
from check_iron_golem_birth import ROOT, check

if __name__ == '__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT), SANCTUARY_FIRST_LOOP_ROOT=str(ROOT))
    check(stage=ROOT / '.local/class-skills/eldritch-blast-suppliers',
          extra_families=('eldritch-blast-suppliers',),
          extra_inputs=(ROOT / 'tests/check_eldritch_blast_suppliers.py',))
