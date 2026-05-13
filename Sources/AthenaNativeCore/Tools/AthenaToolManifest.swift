import Foundation

public enum AthenaToolManifest {
    public static func tools(strict: Bool) -> [RealtimeToolDefinition] {
        [
            inspectScreen(strict: strict),
            createScreenImage(strict: strict),
            openMusicPlayer(strict: strict)
        ]
    }

    private static func inspectScreen(strict: Bool) -> RealtimeToolDefinition {
        RealtimeToolDefinition(
            name: "inspect_screen",
            description: "Capture the user's current primary screen and answer a concise question about what is visible. Use only after the user explicitly asks about their screen.",
            strict: strict,
            parameters: .object(
                properties: [
                    "question": .string(description: "The user's screen-related question.")
                ],
                required: ["question"],
                additionalProperties: strict ? false : nil
            )
        )
    }

    private static func createScreenImage(strict: Bool) -> RealtimeToolDefinition {
        RealtimeToolDefinition(
            name: "create_screen_image",
            description: "Capture the user's current primary screen, summarize it, generate an image, and open it in a lightbox. Use only after explicit user request.",
            strict: strict,
            parameters: .object(
                properties: [
                    "prompt": .string(description: "The user's requested generated-image instruction.")
                ],
                required: ["prompt"],
                additionalProperties: strict ? false : nil
            )
        )
    }

    private static func openMusicPlayer(strict: Bool) -> RealtimeToolDefinition {
        RealtimeToolDefinition(
            name: "open_music_player",
            description: "Open Athena's local music player for the configured music directory. Use when the user asks to play, browse, or open their local music. Voice mode stops when this opens.",
            strict: strict,
            parameters: .object(
                properties: [
                    "query": .string(description: "Filename, partial relative path, or a generic request such as 'play music'. Use an empty string to open the library."),
                    "autoplay": .boolean(description: "True when the user asked to start playback; false when they only asked to browse/open the player.")
                ],
                required: ["query", "autoplay"],
                additionalProperties: strict ? false : nil
            )
        )
    }
}

public struct RealtimeToolDefinition: Encodable, Equatable, Sendable {
    public var type: String
    public var name: String
    public var description: String
    public var strict: Bool?
    public var parameters: JSONSchema

    public init(
        type: String = "function",
        name: String,
        description: String,
        strict: Bool?,
        parameters: JSONSchema
    ) {
        self.type = type
        self.name = name
        self.description = description
        self.strict = strict
        self.parameters = parameters
    }
}

public struct JSONSchema: Encodable, Equatable, Sendable {
    public var type: String
    public var description: String?
    public var properties: [String: JSONSchema]?
    public var required: [String]?
    public var additionalProperties: Bool?

    enum CodingKeys: String, CodingKey {
        case type
        case description
        case properties
        case required
        case additionalProperties
    }

    public static func object(
        properties: [String: JSONSchema],
        required: [String],
        additionalProperties: Bool?
    ) -> JSONSchema {
        JSONSchema(
            type: "object",
            description: nil,
            properties: properties,
            required: required,
            additionalProperties: additionalProperties
        )
    }

    public static func string(description: String) -> JSONSchema {
        JSONSchema(
            type: "string",
            description: description,
            properties: nil,
            required: nil,
            additionalProperties: nil
        )
    }

    public static func boolean(description: String) -> JSONSchema {
        JSONSchema(
            type: "boolean",
            description: description,
            properties: nil,
            required: nil,
            additionalProperties: nil
        )
    }
}
