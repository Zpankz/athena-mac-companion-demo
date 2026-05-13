import Foundation

public struct AthenaNativeSettings: Codable, Equatable, Sendable {
    public var voice: String
    public var musicDirectory: String
    public var hasCompletedOnboarding: Bool

    public init(
        voice: String = RealtimeVoiceOptions.defaultVoice,
        musicDirectory: String = MusicDirectoryDefaults.defaultDirectory.path,
        hasCompletedOnboarding: Bool = false
    ) {
        self.voice = voice
        self.musicDirectory = musicDirectory
        self.hasCompletedOnboarding = hasCompletedOnboarding
        normalize()
    }

    public static func load(from url: URL = SettingsPaths.settingsFile) -> Self {
        guard let data = try? Data(contentsOf: url),
              var settings = try? JSONDecoder().decode(Self.self, from: data)
        else {
            return Self()
        }

        settings.normalize()
        return settings
    }

    public func save(to url: URL = SettingsPaths.settingsFile) throws {
        var normalized = self
        normalized.normalize()
        try FileManager.default.createDirectory(
            at: url.deletingLastPathComponent(),
            withIntermediateDirectories: true
        )
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        try encoder.encode(normalized).write(to: url, options: [.atomic])
    }

    public mutating func normalize() {
        if !RealtimeVoiceOptions.supportedVoices.contains(voice) {
            voice = RealtimeVoiceOptions.defaultVoice
        }

        if musicDirectory.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
            musicDirectory = MusicDirectoryDefaults.defaultDirectory.path
        }
    }
}

public enum SettingsPaths {
    public static let applicationSupportDirectory: URL = {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask).first
            ?? FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Library/Application Support", isDirectory: true)
        return base.appendingPathComponent("AthenaCompanion", isDirectory: true)
    }()

    public static let settingsFile = applicationSupportDirectory.appendingPathComponent("settings.json")
}

public enum MusicDirectoryDefaults {
    public static let defaultDirectory: URL = {
        let music = FileManager.default.urls(for: .musicDirectory, in: .userDomainMask).first
            ?? FileManager.default.homeDirectoryForCurrentUser.appendingPathComponent("Music", isDirectory: true)
        return music.appendingPathComponent("Athena Companion", isDirectory: true)
    }()
}

public enum RealtimeVoiceOptions {
    public static let defaultVoice = "marin"
    public static let supportedVoices = [
        "marin",
        "cedar",
        "coral",
        "shimmer",
        "verse",
        "sage",
        "alloy",
        "ash",
        "ballad",
        "echo"
    ]
}
