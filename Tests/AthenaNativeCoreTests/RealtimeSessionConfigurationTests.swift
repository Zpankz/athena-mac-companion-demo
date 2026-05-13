import XCTest
@testable import AthenaNativeCore

final class RealtimeSessionConfigurationTests: XCTestCase {
    func testDefaultSessionUpdateUsesRealtime2AdvancedAudioShape() throws {
        let configuration = RealtimeSessionConfiguration.athenaDefault(
            instructions: "test",
            voice: "marin"
        )

        let data = try JSONEncoder().encode(RealtimeSessionUpdateEvent(session: configuration))
        let root = try XCTUnwrap(JSONSerialization.jsonObject(with: data) as? [String: Any])
        let session = try XCTUnwrap(root["session"] as? [String: Any])
        let reasoning = try XCTUnwrap(session["reasoning"] as? [String: Any])
        let audio = try XCTUnwrap(session["audio"] as? [String: Any])
        let input = try XCTUnwrap(audio["input"] as? [String: Any])
        let output = try XCTUnwrap(audio["output"] as? [String: Any])
        let inputFormat = try XCTUnwrap(input["format"] as? [String: Any])
        let outputFormat = try XCTUnwrap(output["format"] as? [String: Any])
        let turnDetection = try XCTUnwrap(input["turn_detection"] as? [String: Any])
        let noiseReduction = try XCTUnwrap(input["noise_reduction"] as? [String: Any])
        let transcription = try XCTUnwrap(input["transcription"] as? [String: Any])

        XCTAssertEqual(root["type"] as? String, "session.update")
        XCTAssertEqual(session["type"] as? String, "realtime")
        XCTAssertEqual(session["model"] as? String, "gpt-realtime-2")
        XCTAssertEqual(session["output_modalities"] as? [String], ["audio"])
        XCTAssertEqual(reasoning["effort"] as? String, "low")
        XCTAssertEqual(inputFormat["type"] as? String, "audio/pcm")
        XCTAssertEqual(inputFormat["rate"] as? Int, 24_000)
        XCTAssertEqual(outputFormat["type"] as? String, "audio/pcm")
        XCTAssertNil(outputFormat["rate"])
        XCTAssertEqual(turnDetection["type"] as? String, "semantic_vad")
        XCTAssertEqual(turnDetection["eagerness"] as? String, "auto")
        XCTAssertEqual(turnDetection["create_response"] as? Bool, true)
        XCTAssertEqual(turnDetection["interrupt_response"] as? Bool, true)
        XCTAssertEqual(noiseReduction["type"] as? String, "near_field")
        XCTAssertEqual(transcription["model"] as? String, "gpt-4o-transcribe")
        XCTAssertEqual(output["voice"] as? String, "marin")
    }

    func testStrictToolManifestKeepsKnownToolNames() throws {
        let tools = AthenaToolManifest.tools(strict: true)

        XCTAssertEqual(tools.map(\.name), [
            "inspect_screen",
            "create_screen_image",
            "open_music_player"
        ])
        XCTAssertTrue(tools.allSatisfy { $0.type == "function" })
        XCTAssertTrue(tools.allSatisfy { $0.strict == true })
    }
}
