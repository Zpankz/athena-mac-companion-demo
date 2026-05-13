namespace AthenaCompanion.Security;

internal enum OpenAiKeySource
{
    None,
    WindowsCredentialManager,
    MacOSKeychain,
    EnvironmentVariable
}
