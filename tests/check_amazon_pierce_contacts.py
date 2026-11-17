#!/usr/bin/env python3
"""Existing seven item-contact matrices with deterministic approved Pierce setup."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_amazon_lifecycle_acceptance import configuration as prior

def configuration():
    c=prior()
    return dict(stage=ROOT/'.local/class-skills/amazon-pierce-contacts',
        extra_families=tuple(n+'-contact'for n in ('plague-javelin','poison-javelin','lightning-bolt','lightning-fury','strafe','magic-arrow','multiple-shot'))+('pierce-action',),
        extra_checks=('pierce-seed-check',),extra_files=c['extra_files'],
        extra_inputs=(*c['extra_inputs'],Path(__file__).resolve()))

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration())
