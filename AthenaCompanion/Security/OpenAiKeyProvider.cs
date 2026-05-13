using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AthenaCompanion.Security;

internal sealed class OpenAiKeyProvider
{
    private const string CredentialTarget = "AthenaCompanion.OpenAI.ApiKey";
    private const string CredentialUserName = "OpenAI API Key";

    public OpenAiKeyLookupResult TryGetApiKey()
    {
        var environmentKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (!string.IsNullOrWhiteSpace(environmentKey))
        {
            return new OpenAiKeyLookupResult(environmentKey, OpenAiKeySource.EnvironmentVariable);
        }

        var savedKey = RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
            ? MacOSKeychain.TryRead(CredentialTarget)
            : WindowsCredentialManager.TryRead(CredentialTarget);
        if (!string.IsNullOrWhiteSpace(savedKey))
        {
            return new OpenAiKeyLookupResult(savedKey, RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? OpenAiKeySource.MacOSKeychain
                : OpenAiKeySource.WindowsCredentialManager);
        }

        return new OpenAiKeyLookupResult(null, OpenAiKeySource.None);
    }

    public bool HasSavedCredential() => RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
        ? !string.IsNullOrWhiteSpace(MacOSKeychain.TryRead(CredentialTarget))
        : !string.IsNullOrWhiteSpace(WindowsCredentialManager.TryRead(CredentialTarget));

    public void SaveApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("API key cannot be empty.", nameof(apiKey));
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            MacOSKeychain.Write(CredentialTarget, CredentialUserName, apiKey.Trim());
        }
        else
        {
            WindowsCredentialManager.Write(CredentialTarget, CredentialUserName, apiKey.Trim());
        }
    }

    public void DeleteSavedApiKey()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            MacOSKeychain.Delete(CredentialTarget);
        }
        else
        {
            WindowsCredentialManager.Delete(CredentialTarget);
        }
    }
}

internal sealed record OpenAiKeyLookupResult(string? ApiKey, OpenAiKeySource Source);

internal static class MacOSKeychain
{
    public static string? TryRead(string service)
    {
        var result = RunSecurity("find-generic-password", "-s", service, "-w");
        return result.ExitCode == 0 && !string.IsNullOrWhiteSpace(result.Output)
            ? result.Output.Trim()
            : null;
    }

    public static void Write(string service, string account, string secret)
    {
        var result = RunSecurity("add-generic-password", "-U", "-s", service, "-a", account, "-w", secret);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Unable to save OpenAI API key in macOS Keychain: {result.Error}");
        }
    }

    public static void Delete(string service)
    {
        var result = RunSecurity("delete-generic-password", "-s", service);
        if (result.ExitCode != 0 && !result.Error.Contains("could not be found", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Unable to delete OpenAI API key from macOS Keychain: {result.Error}");
        }
    }

    private static CommandResult RunSecurity(params string[] args)
    {
        var startInfo = new ProcessStartInfo("/usr/bin/security")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        foreach (var arg in args)
        {
            startInfo.ArgumentList.Add(arg);
        }

        using var process = Process.Start(startInfo) ??
            throw new InvalidOperationException("Unable to start macOS security command.");
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new CommandResult(process.ExitCode, output, error);
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}

internal static class WindowsCredentialManager
{
    private const uint CredTypeGeneric = 1;
    private const uint CredPersistLocalMachine = 2;

    public static string? TryRead(string target)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ||
            !CredRead(target, CredTypeGeneric, 0, out var credentialPointer))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<Credential>(credentialPointer);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
            {
                return null;
            }

            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            return Encoding.Unicode.GetString(bytes).TrimEnd('\0');
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public static void Write(string target, string userName, string secret)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            throw new PlatformNotSupportedException("Windows Credential Manager is only available on Windows.");
        }

        var secretBytes = Encoding.Unicode.GetBytes(secret);
        var blob = Marshal.AllocCoTaskMem(secretBytes.Length);

        try
        {
            Marshal.Copy(secretBytes, 0, blob, secretBytes.Length);

            var credential = new Credential
            {
                Type = CredTypeGeneric,
                TargetName = target,
                CredentialBlobSize = (uint)secretBytes.Length,
                CredentialBlob = blob,
                Persist = CredPersistLocalMachine,
                UserName = userName
            };

            if (!CredWrite(ref credential, 0))
            {
                throw new InvalidOperationException($"Unable to save OpenAI API key. Win32 error: {Marshal.GetLastWin32Error()}");
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(blob);
        }
    }

    public static void Delete(string target)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        if (!CredDelete(target, CredTypeGeneric, 0))
        {
            var error = Marshal.GetLastWin32Error();
            const int ErrorNotFound = 1168;
            if (error != ErrorNotFound)
            {
                throw new InvalidOperationException($"Unable to delete OpenAI API key. Win32 error: {error}");
            }
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct Credential
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)]
        public string UserName;
    }

    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, uint type, int reservedFlag, out IntPtr credentialPointer);

    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref Credential credential, uint flags);

    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, uint type, int flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr buffer);
}
