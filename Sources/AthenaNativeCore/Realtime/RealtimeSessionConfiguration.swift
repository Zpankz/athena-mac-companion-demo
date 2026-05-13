import Foundation

public enum AthenaRealtimeModel {
    public static let current = "gpt-realtime-2"
    public static let defaultReasoningEffort = "low"
    public static let sampleRate = 24_000
}

public struct RealtimeSessionUpdateEvent: Encodable, Equatable, Sendable {
    public let type: String
    public let session: RealtimeSessionConfiguration

    public init(session: RealtimeSessionConfiguration) {
        self.type = "session.update"
        self.session = session
    }
}

public struct RealtimeSessionConfiguration: Encodable, Equatable, Sendable {
    public var type: String
    public var model: String
    public var instructions: String
    public var outputModalities: [String]
    public var reasoning: RealtimeReasoning
    public var audio: RealtimeAudioConfiguration
    public var tools: [RealtimeToolDefinition]
    public var toolChoice: String

    enum CodingKeys: String, CodingKey {
        case type
        case model
        case instructions
        case outputModalities = "output_modalities"
        case reasoning
        case audio
        case tools
        case toolChoice = "tool_choice"
    }

    public init(
        type: String = "realtime",
        model: String = AthenaRealtimeModel.current,
        instructions: String,
        outputModalities: [String] = ["audio"],
        reasoning: RealtimeReasoning = .init(effort: AthenaRealtimeModel.defaultReasoningEffort),
        audio: RealtimeAudioConfiguration,
        tools: [RealtimeToolDefinition],
        toolChoice: String = "auto"
    ) {
        self.type = type
        self.model = model
        self.instructions = instructions
        self.outputModalities = outputModalities
        self.reasoning = reasoning
        self.audio = audio
        self.tools = tools
        self.toolChoice = toolChoice
    }

    public static func athenaDefault(
        instructions: String,
        voice: String,
        turnDetection: RealtimeTurnDetection = .semantic(eagerness: "auto")
    ) -> Self {
        Self(
            instructions: instructions,
            audio: .init(
                input: .init(
                    format: .pcm24k,
                    noiseReduction: .init(type: "near_field"),
                    transcription: .init(model: "gpt-4o-transcribe", language: "en", prompt: "Athena macOS desktop companion conversation."),
                    turnDetection: turnDetection
                ),
                output: .init(format: .pcm, voice: voice, speed: 1.0)
            ),
            tools: AthenaToolManifest.tools(strict: true)
        )
    }
}

public struct RealtimeReasoning: Encodable, Equatable, Sendable {
    public var effort: String

    public init(effort: String) {
        self.effort = effort
    }
}

public struct RealtimeAudioConfiguration: Encodable, Equatable, Sendable {
    public var input: RealtimeAudioInputConfiguration
    public var output: RealtimeAudioOutputConfiguration

    public init(input: RealtimeAudioInputConfiguration, output: RealtimeAudioOutputConfiguration) {
        self.input = input
        self.output = output
    }
}

public struct RealtimeAudioInputConfiguration: Encodable, Equatable, Sendable {
    public var format: RealtimeAudioFormat
    public var noiseReduction: RealtimeNoiseReduction?
    public var transcription: RealtimeInputTranscription?
    public var turnDetection: RealtimeTurnDetection?

    enum CodingKeys: String, CodingKey {
        case format
        case noiseReduction = "noise_reduction"
        case transcription
        case turnDetection = "turn_detection"
    }

    public init(
        format: RealtimeAudioFormat,
        noiseReduction: RealtimeNoiseReduction?,
        transcription: RealtimeInputTranscription?,
        turnDetection: RealtimeTurnDetection?
    ) {
        self.format = format
        self.noiseReduction = noiseReduction
        self.transcription = transcription
        self.turnDetection = turnDetection
    }
}

public struct RealtimeAudioOutputConfiguration: Encodable, Equatable, Sendable {
    public var format: RealtimeAudioFormat
    public var voice: String
    public var speed: Double

    public init(format: RealtimeAudioFormat, voice: String, speed: Double) {
        self.format = format
        self.voice = voice
        self.speed = speed
    }
}

public struct RealtimeAudioFormat: Encodable, Equatable, Sendable {
    public static let pcm = RealtimeAudioFormat(type: "audio/pcm", rate: nil)
    public static let pcm24k = RealtimeAudioFormat(type: "audio/pcm", rate: AthenaRealtimeModel.sampleRate)

    public var type: String
    public var rate: Int?

    public init(type: String, rate: Int?) {
        self.type = type
        self.rate = rate
    }
}

public struct RealtimeNoiseReduction: Encodable, Equatable, Sendable {
    public var type: String

    public init(type: String) {
        self.type = type
    }
}

public struct RealtimeInputTranscription: Encodable, Equatable, Sendable {
    public var model: String
    public var language: String?
    public var prompt: String?

    public init(model: String, language: String?, prompt: String?) {
        self.model = model
        self.language = language
        self.prompt = prompt
    }
}

public struct RealtimeTurnDetection: Encodable, Equatable, Sendable {
    public var type: String
    public var threshold: Double?
    public var prefixPaddingMs: Int?
    public var silenceDurationMs: Int?
    public var eagerness: String?
    public var createResponse: Bool
    public var interruptResponse: Bool
    public var idleTimeoutMs: Int?

    enum CodingKeys: String, CodingKey {
        case type
        case threshold
        case prefixPaddingMs = "prefix_padding_ms"
        case silenceDurationMs = "silence_duration_ms"
        case eagerness
        case createResponse = "create_response"
        case interruptResponse = "interrupt_response"
        case idleTimeoutMs = "idle_timeout_ms"
    }

    public static func semantic(eagerness: String) -> Self {
        Self(
            type: "semantic_vad",
            threshold: nil,
            prefixPaddingMs: nil,
            silenceDurationMs: nil,
            eagerness: eagerness,
            createResponse: true,
            interruptResponse: true,
            idleTimeoutMs: nil
        )
    }

    public static func server(
        threshold: Double = 0.5,
        prefixPaddingMs: Int = 300,
        silenceDurationMs: Int = 500,
        idleTimeoutMs: Int? = nil
    ) -> Self {
        Self(
            type: "server_vad",
            threshold: threshold,
            prefixPaddingMs: prefixPaddingMs,
            silenceDurationMs: silenceDurationMs,
            eagerness: nil,
            createResponse: true,
            interruptResponse: true,
            idleTimeoutMs: idleTimeoutMs
        )
    }
}
