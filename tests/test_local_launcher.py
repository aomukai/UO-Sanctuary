import io
import json
from pathlib import Path
import subprocess
from tempfile import TemporaryDirectory
import unittest
from unittest.mock import Mock, call, patch

from tools.local_launcher import LocalGame, save_language, selected_language


STATUS = "Britain baseline status: doors=8"
SAVE = "Saving world"
ON_DISK = "Writing world save snapshot done (0.02 seconds)"


class LocalLauncherTests(unittest.TestCase):
    def test_language_round_trip_preserves_other_settings(self):
        with TemporaryDirectory() as directory:
            settings = Path(directory) / "settings.json"
            settings.write_text('{"port": 2593, "sanctuary_speech_volume": 42}')
            self.assertEqual(selected_language(settings), "English")
            save_language(settings, "日本語")
            self.assertEqual(selected_language(settings), "日本語")
            self.assertEqual(json.loads(settings.read_text()), {
                "port": 2593, "sanctuary_speech_volume": 42, "lang": "JPN",
            })
            save_language(settings, "Deutsch")
            self.assertEqual(selected_language(settings), "Deutsch")

    def test_invalid_language_preserves_file(self):
        with TemporaryDirectory() as directory:
            settings = Path(directory) / "settings.json"
            content = '{"lang": "ENU"}'
            settings.write_text(content)
            with self.assertRaises(ValueError):
                save_language(settings, "invalid")
            self.assertEqual(settings.read_text(), content)

    def test_play_uses_the_staging_server_launch(self):
        prepare = Mock(return_value=([], Path(".local/client"), {}))
        game = LocalGame(Path(".local"), prepare, Mock())
        with patch("tools.local_launcher.local_addresses", return_value=[("127.0.0.1", 2593)]), \
                patch("tools.local_launcher.socket_open", return_value=True):
            with self.assertRaisesRegex(ValueError, "local game port is already in use"):
                game.play(Mock(), Mock())
        self.assertEqual(prepare.call_args_list, [call("server"), call("client")])

    def test_keeps_previous_launch_log(self):
        with TemporaryDirectory() as directory:
            folder = Path(directory)
            game = LocalGame(folder, Mock(), Mock())
            game.logs.mkdir()
            log = game.log_path("server")
            log.write_bytes(b"previous crash details\n")

            with patch("tools.local_launcher.subprocess.Popen") as start:
                start.side_effect = lambda *args, stdout, **kwargs: stdout.write(b"next launch\n")
                game.spawn((["ModernUO"], folder, {}), "server", subprocess.DEVNULL)

            self.assertEqual(log.read_bytes(), b"next launch\n")
            self.assertEqual(log.with_suffix(".previous.log").read_bytes(), b"previous crash details\n")

    def game_at_console(self, messages):
        game = LocalGame(Path(".local"), Mock(), Mock())
        game.ready = True
        game.server = Mock()
        game.server.poll.return_value = None
        game.server.wait.return_value = 0
        transcript = "".join(f"[12:00:00 INF] {message} <s:Server.World>\n" for message in messages)
        game.output = io.StringIO(transcript)
        game.send = Mock()
        return game, transcript

    def test_waits_for_fresh_snapshot_before_shutdown(self):
        game, transcript = self.game_at_console([SAVE, STATUS, ON_DISK, STATUS, SAVE, ON_DISK])
        position_at_shutdown = []

        def record(command):
            if command == "shutdown":
                position_at_shutdown.append(game.output.tell())

        game.send.side_effect = record
        self.assertEqual(game.stop(Mock()), "World saved.")
        self.assertEqual(position_at_shutdown, [len(transcript)])
        self.assertEqual(game.send.call_args_list, [
            call("sanctuary-status"), call("sanctuary-status"), call("save"), call("shutdown"),
        ])

    def test_failed_save_keeps_server_running(self):
        game, _ = self.game_at_console([STATUS, SAVE, "Writing world save snapshot failed"])
        server = game.server
        try:
            with self.assertRaisesRegex(RuntimeError, "world save failed"):
                game.stop(Mock())
            self.assertEqual(game.send.call_args_list, [call("sanctuary-status"), call("save")])
            server.wait.assert_not_called()
            self.assertIs(game.server, server)
        finally:
            game.release_server()

    def test_waiting_for_slow_shutdown_does_not_request_another_save(self):
        game, _ = self.game_at_console([STATUS, SAVE, ON_DISK])
        game.server.wait.side_effect = [subprocess.TimeoutExpired("ModernUO", 30), 0]
        with self.assertRaisesRegex(RuntimeError, "still shutting down"):
            game.stop(Mock())
        self.assertEqual(game.stop(Mock()), "World saved.")
        self.assertEqual(game.send.call_args_list, [call("sanctuary-status"), call("save"), call("shutdown")])
