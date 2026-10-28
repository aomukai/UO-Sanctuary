#!/usr/bin/env python3
"""Retain Fury/Shock Wave combat, inputs and pending-series saves after frame correction."""
import check_druid_fury as retained

if __name__=='__main__':
    retained.STAGE=retained.ROOT/'.local/class-skills/creeper-frame-correction'
    assert retained.STAGE.name=='creeper-frame-correction'
    retained.check()
