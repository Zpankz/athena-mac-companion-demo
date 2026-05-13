import CoreGraphics
import Foundation
import ImageIO
import UniformTypeIdentifiers

public struct NativeScreenCaptureService {
    public init() {}

    public func captureMainDisplayPNG(to directory: URL = FileManager.default.temporaryDirectory) throws -> URL {
        guard let image = CGDisplayCreateImage(CGMainDisplayID()) else {
            throw ScreenCaptureError.captureFailed
        }

        try FileManager.default.createDirectory(at: directory, withIntermediateDirectories: true)
        let outputURL = directory.appendingPathComponent("athena-screen-\(UUID().uuidString).png")
        guard let destination = CGImageDestinationCreateWithURL(
            outputURL as CFURL,
            UTType.png.identifier as CFString,
            1,
            nil
        ) else {
            throw ScreenCaptureError.destinationFailed
        }

        CGImageDestinationAddImage(destination, image, nil)
        guard CGImageDestinationFinalize(destination) else {
            throw ScreenCaptureError.writeFailed
        }

        return outputURL
    }
}

public enum ScreenCaptureError: Error, Equatable, LocalizedError {
    case captureFailed
    case destinationFailed
    case writeFailed

    public var errorDescription: String? {
        switch self {
        case .captureFailed:
            return "Unable to capture the main display."
        case .destinationFailed:
            return "Unable to create a PNG image destination."
        case .writeFailed:
            return "Unable to write the captured PNG."
        }
    }
}
