#!/usr/bin/env python3
"""Run the complete class/projectile/named-gear gate on the frozen Fury package."""
import os
from pathlib import Path

ROOT=Path(__file__).resolve().parents[1]
STAGE=ROOT/'.local/class-skills/druid-fury'
os.environ.update({
    'SANCTUARY_CLASS_SKILL_PACKAGE':str(STAGE),
    'SANCTUARY_PROJECT_ROOT':str(ROOT),
    'SANCTUARY_FIRST_LOOP_ROOT':str(ROOT),
    'SANCTUARY_FIRST_LOOP_AUDIT':str(STAGE/'full-first-loop-affix-audit.json'),
    'SANCTUARY_NAMED_AUDIT':str(STAGE/'full-named-item-runtime-audit.json'),
    'SANCTUARY_TEST_NAMED_GEAR':'1',
})

if __name__=='__main__':
    # PACKAGE is read at import time,after the complete gate is selected above.
    from check_class_skills import check
    check()
