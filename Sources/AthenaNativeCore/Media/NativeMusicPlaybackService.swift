import AVFoundation
import Foundation

public final class NativeMusicPlaybackService: NSObject, AVAudioPlayerDelegate {
    private var player: AVAudioPlayer?

    public private(set) var currentURL: URL?
    public var onFinished: (() -> Void)?

    public func play(url: URL) throws {
        stop()
        let player = try AVAudioPlayer(contentsOf: url)
        player.delegate = self
        player.prepareToPlay()
        player.play()
        self.player = player
        currentURL = url
    }

    public func pause() {
        player?.pause()
    }

    public func resume() {
        player?.play()
    }

    public func stop() {
        player?.stop()
        player = nil
        currentURL = nil
    }

    public func audioPlayerDidFinishPlaying(_ player: AVAudioPlayer, successfully flag: Bool) {
        currentURL = nil
        self.player = nil
        onFinished?()
    }
}
