import Foundation

public enum RealtimeClientStatus: Equatable, Sendable {
    case idle
    case connecting
    case connected
    case listening
    case thinking
    case speaking
    case usingTool
    case disconnected
}

public struct RealtimeServerEvent: Equatable {
    public var type: String
    public var payload: [String: AnyHashable]

    public init(type: String, payload: [String: AnyHashable]) {
        self.type = type
        self.payload = payload
    }
}

@MainActor
public final class RealtimeWebSocketClient {
    private let apiKeyProvider: () throws -> String?
    private let urlSession: URLSession
    private let encoder: JSONEncoder
    private var webSocketTask: URLSessionWebSocketTask?

    public private(set) var status: RealtimeClientStatus = .idle
    public var onStatusChanged: ((RealtimeClientStatus) -> Void)?
    public var onEvent: ((RealtimeServerEvent) -> Void)?
    public var onAudioDelta: ((Data) -> Void)?
    public var onError: ((Error) -> Void)?

    public init(
        apiKeyProvider: @escaping () throws -> String?,
        urlSession: URLSession = .shared
    ) {
        self.apiKeyProvider = apiKeyProvider
        self.urlSession = urlSession
        self.encoder = JSONEncoder()
        self.encoder.outputFormatting = [.sortedKeys]
    }

    public func connect(configuration: RealtimeSessionConfiguration) async throws {
        guard webSocketTask == nil else {
            return
        }

        guard let apiKey = try apiKeyProvider(), !apiKey.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty else {
            throw RealtimeClientError.missingAPIKey
        }

        setStatus(.connecting)
        var components = URLComponents(string: "wss://api.openai.com/v1/realtime")
        components?.queryItems = [
            URLQueryItem(name: "model", value: configuration.model)
        ]

        guard let url = components?.url else {
            throw RealtimeClientError.invalidURL
        }

        var request = URLRequest(url: url)
        request.setValue("Bearer \(apiKey)", forHTTPHeaderField: "Authorization")
        let task = urlSession.webSocketTask(with: request)
        webSocketTask = task
        task.resume()

        try await send(RealtimeSessionUpdateEvent(session: configuration))
        setStatus(.connected)
    }

    public func disconnect() async {
        guard let webSocketTask else {
            return
        }

        webSocketTask.cancel(with: .normalClosure, reason: nil)
        self.webSocketTask = nil
        setStatus(.disconnected)
    }

    public func receiveLoop() async {
        guard let webSocketTask else {
            return
        }

        do {
            while !Task.isCancelled {
                let message = try await webSocketTask.receive()
                switch message {
                case .string(let json):
                    try handle(json: json)
                case .data(let data):
                    if let json = String(data: data, encoding: .utf8) {
                        try handle(json: json)
                    }
                @unknown default:
                    continue
                }
            }
        } catch {
            onError?(error)
            setStatus(.disconnected)
        }
    }

    public func appendInputPCM16(_ data: Data) async throws {
        guard !data.isEmpty else {
            return
        }

        try await send([
            "type": "input_audio_buffer.append",
            "audio": data.base64EncodedString()
        ])
    }

    public func cancelResponse() async throws {
        try await send(["type": "response.cancel"])
    }

    public func createResponse(instructions: String? = nil) async throws {
        if let instructions {
            try await send([
                "type": "response.create",
                "response": [
                    "instructions": instructions
                ]
            ])
        } else {
            try await send(["type": "response.create"])
        }
    }

    public func sendFunctionCallOutput(callID: String, output: String) async throws {
        try await send([
            "type": "conversation.item.create",
            "item": [
                "type": "function_call_output",
                "call_id": callID,
                "output": output
            ]
        ])
    }

    public func send<T: Encodable>(_ payload: T) async throws {
        let data = try encoder.encode(payload)
        guard let json = String(data: data, encoding: .utf8) else {
            throw RealtimeClientError.encodingFailed
        }

        try await sendJSONString(json)
    }

    public func send(_ payload: [String: Any]) async throws {
        let data = try JSONSerialization.data(withJSONObject: payload, options: [.sortedKeys])
        guard let json = String(data: data, encoding: .utf8) else {
            throw RealtimeClientError.encodingFailed
        }

        try await sendJSONString(json)
    }

    private func sendJSONString(_ json: String) async throws {
        guard let webSocketTask else {
            throw RealtimeClientError.notConnected
        }

        try await webSocketTask.send(.string(json))
    }

    private func handle(json: String) throws {
        guard let data = json.data(using: .utf8),
              let object = try JSONSerialization.jsonObject(with: data) as? [String: Any],
              let type = object["type"] as? String
        else {
            return
        }

        switch type {
        case "session.created", "session.updated":
            setStatus(.listening)
        case "input_audio_buffer.speech_started":
            setStatus(.listening)
        case "input_audio_buffer.speech_stopped", "response.created":
            setStatus(.thinking)
        case "response.output_item.added":
            setStatus(.thinking)
        case "response.output_audio.delta", "response.audio.delta":
            setStatus(.speaking)
            if let base64 = object["delta"] as? String,
               let audio = Data(base64Encoded: base64) {
                onAudioDelta?(audio)
            }
        case "response.function_call_arguments.done", "response.output_item.done":
            setStatus(.usingTool)
        case "response.done":
            setStatus(.listening)
        default:
            break
        }

        onEvent?(RealtimeServerEvent(type: type, payload: Self.hashablePayload(from: object)))
    }

    private func setStatus(_ newStatus: RealtimeClientStatus) {
        guard status != newStatus else {
            return
        }

        status = newStatus
        onStatusChanged?(newStatus)
    }

    private static func hashablePayload(from object: [String: Any]) -> [String: AnyHashable] {
        var payload: [String: AnyHashable] = [:]
        for (key, value) in object {
            if let hashable = value as? AnyHashable {
                payload[key] = hashable
            } else if let string = value as? CustomStringConvertible {
                payload[key] = string.description
            }
        }

        return payload
    }
}

public enum RealtimeClientError: Error, Equatable, LocalizedError {
    case missingAPIKey
    case invalidURL
    case notConnected
    case encodingFailed

    public var errorDescription: String? {
        switch self {
        case .missingAPIKey:
            return "Missing OPENAI_API_KEY or saved Keychain credential."
        case .invalidURL:
            return "Unable to create the Realtime WebSocket URL."
        case .notConnected:
            return "Realtime client is not connected."
        case .encodingFailed:
            return "Unable to encode the Realtime event."
        }
    }
}
