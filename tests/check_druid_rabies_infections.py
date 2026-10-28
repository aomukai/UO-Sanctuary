#!/usr/bin/env python3
"""Prepared Rabies infection/world controller with two actual native reloads."""
import check_creeper_stats as retained

if __name__ == '__main__':
    retained.STAGE=retained.ROOT/'.local/class-skills/druid-rabies-infections'
    retained.check(extra_commands=['druid-rabies-packet-check','druid-rabies-infection-check'],
        extra_reload_commands=['druid-rabies-infection-reload-check'],
        extra_before_save_commands=['druid-rabies-infection-snapshot-check'],
        extra_fixtures=['druid-rabies-infection-fixtures.json'],
        extra_probes=['druid-rabies-factory','druid-rabies-spread'])
    print('Prepared Rabies infection/spread and packet checks pass3boots/2actual reloads; learned bite/casting not enabled or installed.')
