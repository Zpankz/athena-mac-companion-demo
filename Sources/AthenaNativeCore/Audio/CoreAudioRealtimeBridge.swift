@preconcurrency import AVFoundation
import Foundation

public final class CoreAudioRealtimeBridge {
    public static let sampleRate = Double(AthenaRealtimeModel.sampleRate)

    private let captureEngine = AVAudioEngine()
    private let playbackEngine = AVAudioEngine()
    private let playerNode = AVAudioPlayerNode()
    private let realtimeFormat: AVAudioFormat
    private var isPlaybackConfigured = false

    public var onInputPCM16: ((Data) -> Void)?

    public init() {
        guard let format = AVAudioFormat(
            commonFormat: .pcmFormatInt16,
            sampleRate: Self.sampleRate,
            channels: 1,
            interleaved: false
        ) else {
            fatalError("Unable to create 24 kHz mono PCM format.")
        }

        realtimeFormat = format
    }

    public func startCapture() throws {
        let inputNode = captureEngine.inputNode
        let inputFormat = inputNode.outputFormat(forBus: 0)
        inputNode.removeTap(onBus: 0)
        inputNode.installTap(onBus: 0, bufferSize: 1_024, format: inputFormat) { [weak self] buffer, _ in
            guard let self,
                  let pcm16 = Self.convert(buffer: buffer, from: inputFormat, to: self.realtimeFormat)
            else {
                return
            }

            self.onInputPCM16?(pcm16)
        }

        captureEngine.prepare()
        try captureEngine.start()
    }

    public func stopCapture() {
        captureEngine.inputNode.removeTap(onBus: 0)
        captureEngine.stop()
    }

    public func startPlayback() throws {
        guard !isPlaybackConfigured else {
            if !playbackEngine.isRunning {
                try playbackEngine.start()
            }
            if !playerNode.isPlaying {
                playerNode.play()
            }
            return
        }

        playbackEngine.attach(playerNode)
        playbackEngine.connect(playerNode, to: playbackEngine.mainMixerNode, format: realtimeFormat)
        playbackEngine.prepare()
        try playbackEngine.start()
        playerNode.play()
        isPlaybackConfigured = true
    }

    public func enqueueOutputPCM16(_ data: Data) throws {
        guard !data.isEmpty else {
            return
        }

        if !isPlaybackConfigured || !playbackEngine.isRunning {
            try startPlayback()
        }

        let frameCount = AVAudioFrameCount(data.count / MemoryLayout<Int16>.size)
        guard frameCount > 0,
              let buffer = AVAudioPCMBuffer(pcmFormat: realtimeFormat, frameCapacity: frameCount),
              let channel = buffer.int16ChannelData?[0]
        else {
            return
        }

        buffer.frameLength = frameCount
        data.withUnsafeBytes { rawBuffer in
            guard let source = rawBuffer.bindMemory(to: Int16.self).baseAddress else {
                return
            }

            channel.update(from: source, count: Int(frameCount))
        }

        playerNode.scheduleBuffer(buffer, completionHandler: nil)
    }

    public func clearPlayback() {
        playerNode.stop()
        if playbackEngine.isRunning {
            playerNode.play()
        }
    }

    public func stopPlayback() {
        playerNode.stop()
        playbackEngine.stop()
    }

    public func stopAll() {
        stopCapture()
        stopPlayback()
    }

    private static func convert(buffer: AVAudioPCMBuffer, from sourceFormat: AVAudioFormat, to targetFormat: AVAudioFormat) -> Data? {
        guard sourceFormat != targetFormat else {
            return pcm16Data(from: buffer)
        }

        guard let converter = AVAudioConverter(from: sourceFormat, to: targetFormat) else {
            return nil
        }

        let ratio = targetFormat.sampleRate / sourceFormat.sampleRate
        let frameCapacity = AVAudioFrameCount((Double(buffer.frameLength) * ratio).rounded(.up)) + 1
        guard let output = AVAudioPCMBuffer(pcmFormat: targetFormat, frameCapacity: frameCapacity) else {
            return nil
        }

        let converterInput = ConverterInput(buffer: buffer)
        var conversionError: NSError?
        converter.convert(to: output, error: &conversionError) { _, status in
            converterInput.next(status: status)
        }

        guard conversionError == nil else {
            return nil
        }

        return pcm16Data(from: output)
    }

    private static func pcm16Data(from buffer: AVAudioPCMBuffer) -> Data? {
        guard let channel = buffer.int16ChannelData?[0] else {
            return nil
        }

        return Data(bytes: channel, count: Int(buffer.frameLength) * MemoryLayout<Int16>.size)
    }
}

private final class ConverterInput: @unchecked Sendable {
    private let lock = NSLock()
    private let buffer: AVAudioPCMBuffer
    private var didRead = false

    init(buffer: AVAudioPCMBuffer) {
        self.buffer = buffer
    }

    func next(status: UnsafeMutablePointer<AVAudioConverterInputStatus>) -> AVAudioBuffer? {
        lock.lock()
        defer { lock.unlock() }

        if didRead {
            status.pointee = .noDataNow
            return nil
        }

        didRead = true
        status.pointee = .haveData
        return buffer
    }
}
