#!/usr/bin/env python3
"""Retain six continuation scenarios under the approved saved Pierce index."""
import os
from pathlib import Path
from check_iron_golem_birth import ROOT,check
from check_amazon_lifecycle_acceptance import configuration as combined

def configuration():
    original=combined();families=('poison-javelin','lightning-bolt','lightning-fury','exploding-arrow','freezing-arrow','immolation-arrow')
    return dict(stage=ROOT/'.local/class-skills/amazon-pierce-continuation',extra_families=families,
        extra_checks=('pierce-seed-check',),extra_files=original['extra_files'],
        extra_manifest_names=original['extra_manifest_names'],
        extra_wait_results={k:v for k,v in original['extra_wait_results'].items()if any(k in (f+'-check',f+'-reload-check')for f in families)},
        extra_inputs=(*original['extra_inputs'],Path(__file__).resolve()))

if __name__=='__main__':
    os.environ.update(SANCTUARY_PROJECT_ROOT=str(ROOT),SANCTUARY_FIRST_LOOP_ROOT=str(ROOT));check(**configuration())
