#!/usr/bin/env python3
"""Separate Rabies marker/emitter lifecycle and retained packets with actual reloads."""
import check_creeper_stats as retained

if __name__ == '__main__':
    retained.STAGE=retained.ROOT/'.local/class-skills/druid-rabies-lifecycle'
    retained.check(extra_commands=['druid-rabies-packet-check','druid-rabies-lifecycle-check'],
        extra_reload_commands=['druid-rabies-lifecycle-reload-check'],
        extra_before_save_commands=['druid-rabies-lifecycle-snapshot-check'],
        extra_fixtures=['druid-rabies-lifecycle-fixtures.json'],
        extra_probes=['druid-rabies-factory','druid-rabies-spread','druid-rabies-lifecycle'])
    print('Separate Rabies markers/emitters, source clock, migration and retained poison pass3boots/2actual reloads; learned bite/casting not enabled or installed.')
