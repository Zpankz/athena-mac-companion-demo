using System.Diagnostics;

namespace AthenaCompanion.Tools;

internal sealed class ScreenCaptureService
{
    public byte[] CapturePrimaryScreenPng()
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("Screen capture is currently implemented through macOS screencapture.");
        }

        var path = Path.Combine(Path.GetTempPath(), $"athena-screen-{Guid.NewGuid():N}.png");
        try
        {
            var startInfo = new ProcessStartInfo("/usr/sbin/screencapture")
            {
                RedirectStandardError = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add("-x");
            startInfo.ArgumentList.Add("-t");
            startInfo.ArgumentList.Add("png");
            startInfo.ArgumentList.Add(path);

            using var process = Process.Start(startInfo) ??
                throw new InvalidOperationException("Unable to start macOS screen capture.");
            var error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0 || !File.Exists(path))
            {
                throw new InvalidOperationException($"macOS screen capture failed. {error}".Trim());
            }

            return File.ReadAllBytes(path);
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // Best effort cleanup of a temporary screenshot.
            }
        }
    }
}
