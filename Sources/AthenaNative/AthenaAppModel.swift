import AthenaNativeCore
import Combine
import Foundation

@MainActor
final class AthenaAppModel: ObservableObject {
    @Published var statusText = "Ready"
    @Published var settings = AthenaNativeSettings.load()
    @Published var apiKeyInput = ""
    @Published var isVoiceRunning = false
    @Published var lastError: String?

    private let keyStore: OpenAIKeyStore
    private let audioBridge: CoreAudioRealtimeBridge
    private var realtimeClient: RealtimeWebSocketClient?
    private var receiveTask: Task<Void, Never>?

    init(
        keyStore: OpenAIKeyStore = CompositeOpenAIKeyStore(),
        audioBridge: CoreAudioRealtimeBridge = CoreAudioRealtimeBridge()
    ) {
        self.keyStore = keyStore
        self.audioBridge = audioBridge
    }

    var hasAPIKey: Bool {
        guard let apiKey = try? keyStore.readAPIKey() else {
            return false
        }

        return !apiKey.isEmpty
    }

    func saveAPIKey() {
        do {
            try keyStore.saveAPIKey(apiKeyInput)
            apiKeyInput = ""
            lastError = nil
            statusText = "API key saved"
        } catch {
            lastError = error.localizedDescription
        }
    }

    func deleteAPIKey() {
        do {
            try keyStore.deleteAPIKey()
            statusText = "API key deleted"
        } catch {
            lastError = error.localizedDescription
        }
    }

    func saveSettings() {
        do {
            try settings.save()
            statusText = "Settings saved"
        } catch {
            lastError = error.localizedDescription
        }
    }

    func toggleVoice() {
        if isVoiceRunning {
            Task { await stopVoice() }
        } else {
            Task { await startVoice() }
        }
    }

    func startVoice() async {
        guard !isVoiceRunning else {
            return
        }

        let instructions = AthenaPromptFactory.voiceInstructions()
        let configuration = RealtimeSessionConfiguration.athenaDefault(
            instructions: instructions,
            voice: settings.voice
        )

        let client = RealtimeWebSocketClient(apiKeyProvider: { [keyStore] in
            try keyStore.readAPIKey()
        })

        client.onStatusChanged = { [weak self] status in
            Task { @MainActor in
                self?.statusText = status.displayText
            }
        }
        client.onAudioDelta = { [weak self] audio in
            do {
                try self?.audioBridge.enqueueOutputPCM16(audio)
            } catch {
                Task { @MainActor in
                    self?.lastError = error.localizedDescription
                }
            }
        }
        client.onError = { [weak self] error in
            Task { @MainActor in
                self?.lastError = error.localizedDescription
            }
        }
        audioBridge.onInputPCM16 = { pcm16 in
            Task {
                try? await client.appendInputPCM16(pcm16)
            }
        }

        do {
            try await client.connect(configuration: configuration)
            try audioBridge.startPlayback()
            try audioBridge.startCapture()
            realtimeClient = client
            receiveTask = Task { await client.receiveLoop() }
            isVoiceRunning = true
            statusText = "Listening"
            lastError = nil
        } catch {
            lastError = error.localizedDescription
            statusText = "Voice unavailable"
            audioBridge.stopAll()
            await client.disconnect()
        }
    }

    func stopVoice() async {
        receiveTask?.cancel()
        receiveTask = nil
        audioBridge.stopAll()
        await realtimeClient?.disconnect()
        realtimeClient = nil
        isVoiceRunning = false
        statusText = "Voice off"
    }
}

private extension RealtimeClientStatus {
    var displayText: String {
        switch self {
        case .idle:
            return "Ready"
        case .connecting:
            return "Connecting"
        case .connected:
            return "Connected"
        case .listening:
            return "Listening"
        case .thinking:
            return "Thinking"
        case .speaking:
            return "Speaking"
        case .usingTool:
            return "Using tool"
        case .disconnected:
            return "Disconnected"
        }
    }
}
