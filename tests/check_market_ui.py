"""Actual ClassicUO shop buy/sell, displayed-item delivery and redacted tooltips."""
import json
import os
from pathlib import Path
import subprocess
import time
from PIL import ImageGrab


def exercise(window, click, capture):
    logfile = Path(os.environ['SANCTUARY_TEST_LOG'])
    def command(name):
        offset = logfile.stat().st_size
        os.write(int(os.environ['SANCTUARY_TEST_CONSOLE_FD']), ('market-ui-' + name + '\n').encode())
        end = time.monotonic() + 6
        while time.monotonic() < end:
            text = logfile.read_text(errors='replace')[offset:]
            if f'market-ui-{name}: failed' in text:
                raise AssertionError(text[-2000:])
            if f'market-ui-{name}: passed' in text:
                return json.loads(Path(os.environ['SANCTUARY_TEST_UI_STATE']).read_text())
            time.sleep(.05)
        raise AssertionError('Missing market-ui-' + name)

    click(538, 101)
    command('setup')
    time.sleep(.8)
    geometry = subprocess.check_output(['xdotool', 'getwindowgeometry', '--shell', window], text=True)
    bounds = dict(line.split('=', 1) for line in geometry.splitlines())
    left = max(0, (int(bounds['WIDTH']) - 596) // 2)
    top = max(0, (int(bounds['HEIGHT']) - 404) // 2)
    buy_tab = (left + 65, top + 78)
    sell_tab = (left + 155, top + 78)
    first_item = (left + 106, top + 133)
    confirm = (left + 395, top + 368)
    vendor = (int(bounds['WIDTH']) // 2 + 44, int(bounds['HEIGHT']) // 2 - 25)

    def point(at, *args):
        subprocess.run(['xdotool', 'mousemove', '--window', window, *map(str, at), *args], check=True)

    def tab_colors():
        point((left + 285, top + 65))
        time.sleep(.15)
        frame = ImageGrab.grab(xdisplay=os.environ['DISPLAY'])
        x, y = int(bounds['X']) + left, int(bounds['Y']) + top
        return frame.getpixel((x + 33, y + 72)), frame.getpixel((x + 124, y + 72))

    capture('t11-shop-merchant.png')
    click(*vendor)
    time.sleep(.6)
    capture('t11-shop-buy.png')
    buy_colors = tab_colors()
    assert buy_colors[0] != buy_colors[1], 'Buy tab did not render as selected'
    click(*sell_tab)
    time.sleep(.45)
    capture('t11-shop-empty-sell.png')
    assert tab_colors() == buy_colors[::-1], 'Sell tab closed or failed to show an empty list'
    click(*buy_tab)
    time.sleep(.45)
    assert tab_colors() == buy_colors, 'Could not return to the Buy tab'
    point(first_item)
    time.sleep(1.5)
    capture('t11-shop-buy-tooltip.png')
    point(first_item, 'click', '--repeat', '2', '--delay', '80', '1')
    time.sleep(.4)
    capture('t11-shop-buy-selected.png')
    click(*confirm)
    time.sleep(.6)
    command('bought')
    command('sell')
    click(*vendor)
    time.sleep(.6)
    click(*sell_tab)
    time.sleep(.45)
    capture('t11-shop-sell.png')
    point(first_item)
    time.sleep(1.5)
    capture('t11-shop-unidentified-tooltip.png')
    point(first_item, 'click', '--repeat', '2', '--delay', '80', '1')
    time.sleep(.4)
    capture('t11-shop-sell-selected.png')
    click(*confirm)
    time.sleep(.6)
    command('sold')
    command('final')
    print('Actual shop client: Buy and Sell tabs, empty Sell list, exact purchase and sale prices, and no buyback passed.', flush=True)
