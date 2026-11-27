"""Launch the installed local game and supervise its save and shutdown."""

import fcntl
import ipaddress
import json
import os
from pathlib import Path
import pty
from queue import Empty, SimpleQueue
import re
import select
import socket
import subprocess
import termios
from tempfile import NamedTemporaryFile
from threading import Event, Thread
import time
from uuid import uuid4


STATUS_COMMAND = "sanctuary-status"
STATUS_REPLY = "Britain baseline status:"
SAVE_DONE = "Writing world save snapshot done"
SAVE_FAILURES = ("Preparing to save world failed", "Saving world failed", "Writing world save snapshot failed")
SERVER_RESTART_EXIT_CODE = 75
ANSI = re.compile(r"\x1b\[[0-?]*[ -/]*[@-~]")


LANGUAGES = {"English": "ENU", "Deutsch": "DEU", "日本語": "JPN"}


def selected_language(settings_path):
    try:
        value = json.loads(settings_path.read_text()).get("lang", "")
    except (FileNotFoundError, ValueError, AttributeError):
        return "English"
    return next((label for label, code in LANGUAGES.items() if code == str(value).upper()), "English")


def save_language(settings_path, label):
    code = LANGUAGES.get(label)
    if code is None:
        raise ValueError("Choose English, Deutsch, or 日本語.")
    settings = json.loads(settings_path.read_text())
    if not isinstance(settings, dict):
        raise ValueError("ClassicUO settings must be a JSON object.")
    if settings.get("lang") == code:
        return
    settings["lang"] = code
    with NamedTemporaryFile("w", dir=settings_path.parent, encoding="utf-8", delete=False) as output:
        pending = output.name
        try:
            json.dump(settings, output, indent=2, ensure_ascii=False)
            output.write("\n")
            output.flush()
            os.replace(pending, settings_path)
        finally:
            if os.path.exists(pending):
                os.unlink(pending)


def local_addresses(settings_path):
    settings = json.loads(settings_path.read_text())
    try:
        port = int(settings["port"])
        host = settings["ip"]
        addresses = ["127.0.0.1", "::1"] if host.lower() == "localhost" else [str(ipaddress.ip_address(host))]
        if not 1 <= port <= 65535 or any(not ipaddress.ip_address(address).is_loopback for address in addresses):
            raise ValueError
    except (AttributeError, KeyError, TypeError, ValueError) as error:
        raise ValueError("Set the ClassicUO profile to a local address and port before playing.") from error
    return [(address, port) for address in addresses]


def socket_open(address):
    try:
        with socket.create_connection(address, timeout=0.2):
            return True
    except OSError:
        return False


def request_client_quit(pid):
    from Xlib import X, Xatom, display, error, protocol

    try:
        connection = display.Display()
    except error.DisplayError as failure:
        raise RuntimeError("Close ClassicUO, then try Save & Shut Down again.") from failure
    try:
        pid_atom = connection.intern_atom("_NET_WM_PID")
        protocols = connection.intern_atom("WM_PROTOCOLS")
        delete = connection.intern_atom("WM_DELETE_WINDOW")
        windows = connection.screen().root.query_tree().children
        while windows:
            window = windows.pop()
            try:
                owner = window.get_full_property(pid_atom, Xatom.CARDINAL)
                if owner is not None and list(owner.value) == [pid] and delete in window.get_wm_protocols():
                    event = protocol.event.ClientMessage(window=window, client_type=protocols,
                                                         data=(32, [delete, X.CurrentTime, 0, 0, 0]))
                    window.send_event(event)
                    connection.sync()
                    return True
                windows.extend(window.query_tree().children)
            except error.BadWindow:
                continue
        return False
    finally:
        connection.close()


class LocalGame:
    def __init__(self, local, prepare_launch, command_environment):
        self.logs = local / "logs"
        self.server_folder = (local / "server").resolve()
        self.control = self.server_folder / ".launcher-control"
        self.prepare_launch = prepare_launch
        self.command_environment = command_environment
        self.server = None
        self.client = None
        self.console = None
        self.output = None
        self.snapshot_in_progress = False
        self.shutdown_requested = False

    def spawn(self, spec, name, stdin):
        argv, folder, environment = spec
        self.logs.mkdir(parents=True, exist_ok=True)
        log = self.log_path(name)
        if log.is_file():
            log.replace(log.with_suffix(".previous.log"))
        with log.open("wb") as output:
            return subprocess.Popen(argv, cwd=folder, env=self.command_environment(environment),
                                    stdin=stdin, stdout=output, stderr=subprocess.STDOUT,
                                    start_new_session=True)

    def log_path(self, name):
        return self.logs / f"launcher-{name}.log"

    def control_status(self):
        try:
            status = json.loads((self.control / "status.json").read_text())
            if isinstance(status, dict) and type(status.get("pid")) is int and status["pid"] > 0 \
                    and isinstance(status.get("instance"), str) and re.fullmatch("[0-9a-f]{32}", status["instance"]):
                return status
        except (OSError, ValueError):
            pass
        return None

    def running_server(self):
        status = self.control_status()
        if status is not None:
            process = Path("/proc") / str(status["pid"])
            try:
                arguments = (process / "cmdline").read_bytes().split(b"\0")
                if (process / "cwd").resolve(strict=True) == self.server_folder and any(
                    os.path.basename(arg) in (b"ModernUO", b"ModernUO.dll") for arg in arguments[:3]
                ):
                    return status
            except OSError:
                pass
        return None

    def server_running(self):
        return (self.server is not None and self.server.poll() is None) or self.running_server() is not None

    def play(self, progress, closing):
        server_spec = None if self.server_running() else self.prepare_launch("server")
        client_argv, client_folder, client_env = self.prepare_launch("client")
        client = client_argv, client_folder, {**client_env, "SDL_VIDEODRIVER": "x11"}
        addresses = local_addresses(client_folder / "settings.json")
        if server_spec is None:
            if not any(map(socket_open, addresses)):
                raise RuntimeError("The local server is still starting or shutting down.")
            if not closing.is_set():
                progress("Opening ClassicUO…")
                self.client = self.spawn(client, "client", subprocess.DEVNULL)
            return
        if any(map(socket_open, addresses)):
            raise ValueError("The local game port is already in use. Close the other server and try again.")

        server_argv, server_folder, server_env = server_spec
        self.console, terminal = pty.openpty()
        try:
            flags = termios.tcgetattr(terminal)
            flags[3] &= ~(termios.ECHO | termios.ECHONL)
            termios.tcsetattr(terminal, termios.TCSANOW, flags)
            server = server_argv, server_folder, {**server_env, "MODERNUO_RESTART_EXIT_CODE": str(SERVER_RESTART_EXIT_CODE)}
            self.server = self.spawn(server, "server", terminal)
        except (OSError, termios.error):
            self.release_server()
            raise
        finally:
            os.close(terminal)

        try:
            self.output = self.log_path("server").open("r", encoding="utf-8", errors="replace")
            listeners = {f"Listening: [{host}]:{port}" if ":" in host else f"Listening: {host}:{port}"
                         for host, port in addresses}
            self.wait_for(listeners.__contains__, "the local server to listen", timeout=120, starting=True)
            self.sync_console()
            if not any(map(socket_open, addresses)):
                raise RuntimeError("ModernUO reported ready, but its local port is unavailable.")
        except (OSError, RuntimeError):
            self.stop_startup()
            raise

        if not closing.is_set():
            progress("Opening ClassicUO…")
            # Tk and the client use the same X11 display, including on XWayland.
            self.client = self.spawn(client, "client", subprocess.DEVNULL)

    def stop_startup(self):
        # A startup failure can leave the server blocked on an interactive setup prompt.
        if self.server.poll() is None:
            self.server.terminate()
            try:
                self.server.wait(timeout=10)
            except subprocess.TimeoutExpired as error:
                raise RuntimeError("ModernUO is still starting. Try Save & Shut Down again.") from error
        self.release_server()

    def stop(self, progress):
        if self.shutdown_requested:
            return self.finish_shutdown(progress)

        client_status = None
        if self.client is not None:
            if self.client.poll() is None:
                progress("Closing ClassicUO…")
                deadline = time.monotonic() + 20
                while self.client.poll() is None and not request_client_quit(self.client.pid):
                    if time.monotonic() >= deadline:
                        raise RuntimeError("Close ClassicUO, then try Save & Shut Down again.")
                    time.sleep(0.15)
                try:
                    self.client.wait(timeout=20)
                except subprocess.TimeoutExpired as error:
                    raise RuntimeError("Close ClassicUO, then try Save & Shut Down again.") from error
            client_status = self.client.returncode
            self.client = None

        server_status = self.server.poll() if self.server is not None else None
        if server_status is not None:
            self.release_server()
        if self.server is None:
            if self.running_server() is not None:
                return self.stop_running_server(progress)
            if server_status == SERVER_RESTART_EXIT_CODE:
                return "ModernUO stopped. Select Play to load the last completed world save."
            raise RuntimeError("There is no running local server to shut down.")

        progress("Saving the world…")
        self.sync_console()
        if self.snapshot_in_progress:
            self.wait_for_save()
            self.sync_console()
        self.send("save")
        self.wait_for(lambda message: message == "Saving world", "the final save to start", saving=True)
        self.wait_for_save()

        self.send("shutdown")
        self.shutdown_requested = True
        result = self.finish_shutdown(progress)
        if client_status:
            return f"{result} ClassicUO exited with code {client_status}."
        return result

    def stop_running_server(self, progress):
        server = self.running_server()
        if server is None:
            raise RuntimeError("There is no running local server to shut down.")

        request = uuid4().hex
        pid = os.pidfd_open(server["pid"])
        try:
            exit_poll = select.poll()
            exit_poll.register(pid, select.POLLIN)
            if exit_poll.poll(0):
                raise RuntimeError("ModernUO has already stopped.")

            progress("Saving the world…")
            with NamedTemporaryFile("w", dir=self.control, encoding="utf-8", delete=False) as output:
                pending = output.name
                output.write(f"{server['instance']}\n{request}\n")
            try:
                os.replace(pending, self.control / "shutdown.request")
            finally:
                if os.path.exists(pending):
                    os.unlink(pending)

            saved = False
            deadline = time.monotonic() + 120
            while time.monotonic() < deadline:
                stopped = bool(exit_poll.poll(100))
                status = self.control_status()
                if status is not None and status["instance"] == server["instance"] and status.get("request") == request:
                    if status.get("state") == "failed":
                        raise RuntimeError("The world save failed. ModernUO is still running.")
                    if status.get("state") == "saved" and not saved:
                        saved = True
                        progress("Shutting down ModernUO…")
                if stopped:
                    if saved:
                        return "World saved."
                    raise RuntimeError("ModernUO exited before acknowledging the save.")
            raise RuntimeError("The world was saved. ModernUO is still shutting down." if saved
                               else "Timed out waiting for the local server to save the world.")
        finally:
            os.close(pid)

    def finish_shutdown(self, progress):
        progress("Shutting down ModernUO…")
        try:
            status = self.server.wait(timeout=30)
        except subprocess.TimeoutExpired as error:
            raise RuntimeError("The world was saved. ModernUO is still shutting down.") from error
        self.release_server()
        if status:
            raise RuntimeError(f"The world was saved. ModernUO then exited with code {status}.")
        return "World saved."

    def release_server(self):
        if self.console is not None:
            os.close(self.console)
        if self.output is not None:
            self.output.close()
        self.console = self.output = self.server = None
        self.snapshot_in_progress = False
        self.shutdown_requested = False

    def send(self, command):
        os.write(self.console, f"{command}\n".encode())

    def sync_console(self):
        self.send(STATUS_COMMAND)
        self.wait_for(lambda message: message.startswith(STATUS_REPLY), "ModernUO’s console")

    def wait_for_save(self):
        self.wait_for(lambda message: message.startswith(SAVE_DONE), "the save to reach disk", timeout=120, saving=True)

    def wait_for(self, matches, description, timeout=30, starting=False, saving=False):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            position = self.output.tell()
            line = self.output.readline()
            if not line.endswith("\n"):
                self.output.seek(position)
                if self.server.poll() is not None:
                    raise RuntimeError(f"ModernUO exited while waiting for {description}.")
                time.sleep(0.08)
                continue

            message = ANSI.sub("", line).split(" <s:", 1)[0].split("] ", 1)[-1].strip()
            if starting and ("create the owner account now?" in message or
                             "Pre-bake the pathfinding cache" in message or message == "No world save data found."):
                raise RuntimeError("ModernUO needs console setup. Run ./dev server --installed in a terminal first.")
            if message == "Saving world":
                self.snapshot_in_progress = True
            elif message.startswith((SAVE_DONE, *SAVE_FAILURES)):
                self.snapshot_in_progress = False
                if saving and message.startswith(SAVE_FAILURES):
                    raise RuntimeError("The world save failed. ModernUO is still running.")
            if matches(message):
                return
        raise RuntimeError(f"Timed out waiting for {description}.")


def run_launcher(local, prepare_launch, command_environment):
    try:
        import tkinter as tk
        from tkinter import ttk
    except ImportError as error:
        raise ValueError("Install your Linux distribution’s Tkinter package (python3-tk on Ubuntu).") from error
    try:
        from Xlib import display as _display
    except ImportError as error:
        raise ValueError("Install Python Xlib for clean client shutdown (python3-xlib on Ubuntu).") from error

    local.mkdir(parents=True, exist_ok=True)
    with (local / "launcher.lock").open("a") as lock:
        try:
            fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        except BlockingIOError as error:
            raise ValueError("The Sanctuary launcher is already open.") from error

        try:
            window = tk.Tk()
        except tk.TclError as error:
            raise ValueError(f"Could not open the launcher window: {error}") from error
        window.title("Sanctuary")
        window.resizable(False, False)
        panel = ttk.Frame(window, padding=20)
        panel.grid()
        status = tk.StringVar(value="Ready to play.")
        ttk.Label(panel, textvariable=status, width=42, wraplength=310, justify="center").grid(pady=(0, 12))
        settings_path = local / "client" / "settings.json"
        language = tk.StringVar(value=selected_language(settings_path))
        language_row = ttk.Frame(panel)
        language_row.grid(pady=(0, 12))

        def change_language():
            try:
                save_language(settings_path, language.get())
            except (OSError, ValueError) as error:
                language.set(selected_language(settings_path))
                status.set(f"Could not save the game language. {error}")

        ttk.Label(language_row, text="Game language").grid(row=0, columnspan=len(LANGUAGES), pady=(0, 4))
        language_choices = []
        for column, label in enumerate(LANGUAGES):
            choice = ttk.Radiobutton(language_row, text=label, value=label,
                                     variable=language, command=change_language)
            choice.grid(row=1, column=column, padx=5)
            language_choices.append(choice)

        game = LocalGame(local, prepare_launch, command_environment)
        messages = SimpleQueue()
        busy = False
        closing = Event()

        def run_task(action, text):
            nonlocal busy
            busy = True
            play_button.state(["disabled"])
            stop_button.state(["disabled"])
            for choice in language_choices:
                choice.state(["disabled"])
            status.set(text)

            def worker():
                try:
                    result = action(lambda message: messages.put(("progress", message)))
                except (OSError, RuntimeError, ValueError, subprocess.SubprocessError) as error:
                    messages.put(("error", f"{error}\nLogs: .local/logs/launcher-*.log"))
                else:
                    messages.put(("done", result or "Playing locally."))

            Thread(target=worker, daemon=True).start()

        def play():
            choice = language.get()

            def start(progress):
                save_language(settings_path, choice)
                return game.play(progress, closing)

            run_task(start, "Opening the local game…")

        def stop():
            run_task(game.stop, "Saving and shutting down…")

        def close_window():
            closing.set()
            if not busy:
                game.release_server()
                window.destroy()

        def poll():
            nonlocal busy
            while True:
                try:
                    kind, text = messages.get_nowait()
                except Empty:
                    break
                status.set(text)
                if kind == "progress":
                    continue
                busy = False

            if not busy:
                if closing.is_set():
                    close_window()
                    return
                server_exited = game.server.poll() if game.server is not None else None
                if server_exited is not None:
                    game.release_server()
                    status.set("ModernUO stopped. Select Play to load the last completed world save.")
                if game.client is not None and game.client.poll() is not None:
                    game.client = None
                    if game.server_running():
                        status.set("ClassicUO closed. The local server is still running.")
                play_button.state(["!disabled" if game.client is None else "disabled"])
                stop_button.state(["!disabled" if game.server_running() else "disabled"])
                for choice in language_choices:
                    choice.state(["!disabled" if game.client is None else "disabled"])
            window.after(200, poll)

        play_button = ttk.Button(panel, text="Play", command=play, width=22)
        play_button.grid(pady=(0, 5))
        stop_button = ttk.Button(panel, text="Save & Shut Down", command=stop, width=22)
        stop_button.grid()
        stop_button.state(["!disabled" if game.server_running() else "disabled"])
        if game.server_running():
            status.set("Local server is running.")
        play_button.focus_set()
        window.protocol("WM_DELETE_WINDOW", close_window)
        window.after(100, poll)
        window.mainloop()
