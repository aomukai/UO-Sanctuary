// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;
using Server;
using Server.Logging;

namespace Sanctuary;

public static class LocalServerControl
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(LocalServerControl));
    private static readonly string _instance = Guid.NewGuid().ToString("N");
    private static string _directory;
    private static string _request;
    private static DateTime _deadline;
    private static long? _beforeSave;

    public static void Initialize()
    {
        if (!OperatingSystem.IsLinux() || ServerConfiguration.Listeners.Count == 0
            || !ServerConfiguration.Listeners.All(listener => IPAddress.IsLoopback(listener.Address)))
            return;

        _directory = Path.Join(Core.BaseDirectory, ".launcher-control");
        try
        {
            Directory.CreateDirectory(_directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            Publish("ready");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(error, "Could not initialize local launcher control");
            return;
        }

        Timer.StartTimer(TimeSpan.FromMilliseconds(250), TimeSpan.FromMilliseconds(250), Poll);
    }

    private static void Publish(string state)
    {
        string path = Path.Join(_directory, "status.json");
        string pending = Path.Join(_directory, $"status-{_instance}.pending");
        File.WriteAllText(pending, JsonSerializer.Serialize(new { pid = Environment.ProcessId, instance = _instance, request = _request, state }));
        File.Move(pending, path, overwrite: true);
    }

    private static void Poll()
    {
        try
        {
            if (_request == null)
            {
                string path = Path.Join(_directory, "shutdown.request");
                if (!File.Exists(path))
                    return;

                string[] command = File.ReadAllLines(path);
                File.Delete(path);
                if (command.Length != 2 || command[0] != _instance || !Guid.TryParseExact(command[1], "N", out _))
                    return;

                _request = command[1];
                _deadline = DateTime.UtcNow.AddSeconds(110);
            }

            if (_beforeSave is long before && World.CompletedSaves != before)
            {
                Publish("saved");
                Core.Kill();
                return;
            }

            if (DateTime.UtcNow >= _deadline || (_beforeSave.HasValue && World.WorldState == WorldState.Running))
            {
                Publish("failed");
                _request = null;
                _beforeSave = null;
                return;
            }

            if (!_beforeSave.HasValue && World.WorldState == WorldState.Running)
            {
                _beforeSave = World.CompletedSaves;
                World.Save();
            }
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            _logger.Warning(error, "Local launcher control could not read or publish its request");
            _request = null;
            _beforeSave = null;
        }
    }
}
