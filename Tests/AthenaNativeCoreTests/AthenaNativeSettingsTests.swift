import XCTest
@testable import AthenaNativeCore

final class AthenaNativeSettingsTests: XCTestCase {
    func testUnsupportedVoiceNormalizesToDefault() {
        let settings = AthenaNativeSettings(voice: "not-a-voice")

        XCTAssertEqual(settings.voice, RealtimeVoiceOptions.defaultVoice)
    }

    func testSettingsRoundTripUsesJsonFile() throws {
        let directory = FileManager.default.temporaryDirectory
            .appendingPathComponent("athena-settings-\(UUID().uuidString)", isDirectory: true)
        let file = directory.appendingPathComponent("settings.json")
        let expected = AthenaNativeSettings(
            voice: "cedar",
            musicDirectory: "/tmp/Athena Music",
            hasCompletedOnboarding: true
        )

        try expected.save(to: file)
        let actual = AthenaNativeSettings.load(from: file)

        XCTAssertEqual(actual, expected)
    }
}
