import CoreGraphics
import Foundation
import ImageIO
import OSLog
import UIKit

nonisolated enum ImageLoadingError: Error, Sendable, Equatable {
    case invalidData
    case httpStatus(Int)
    case decodingFailed
    case cancelled
}

/// UIImage is documented as immutable and safe to use from any thread. The
/// wrapper makes that guarantee explicit to Swift 6's sendability checker.
nonisolated struct SendableImage: @unchecked Sendable {
    let image: UIImage
}

nonisolated final class ImagePipeline: Sendable {
    private let session: URLSession
    private let cache = ImageMemoryCache()
    private let inFlight = InFlightRegistry()
    private let logger = Logger(subsystem: "com.LithiumStudios.MovieFlix", category: "images")

    init(session: URLSession) {
        self.session = session
    }

    func image(
        for url: URL,
        targetSize: CGSize,
        scale: CGFloat
    ) async throws -> UIImage {
        let pixelSize = Self.pixelSize(for: targetSize, scale: scale)
        let key = ImageRequestKey(url: url, maxPixelSize: pixelSize)

        if let cached = cache.image(for: key) {
            return cached
        }

        let loaded = try await inFlight.value(for: key) { [session, logger] in
            var request = URLRequest(url: url)
            request.timeoutInterval = 30
            request.cachePolicy = .returnCacheDataElseLoad
            request.setValue("image/*", forHTTPHeaderField: "Accept")

            let (data, response) = try await session.data(for: request)
            if let http = response as? HTTPURLResponse,
               !(200..<300).contains(http.statusCode) {
                throw ImageLoadingError.httpStatus(http.statusCode)
            }

            let decoded: SendableImage
            do {
                decoded = try ImageDownsampler.downsample(data, maxPixelSize: pixelSize)
            } catch {
                logger.error("Image decode failed for \(url.absoluteString, privacy: .public): \(String(describing: error), privacy: .public)")
                throw ImageLoadingError.decodingFailed
            }
            return decoded
        }

        cache.insert(loaded.image, for: key)
        return loaded.image
    }

    func prefetch(urls: [URL], targetSize: CGSize, scale: CGFloat) {
        for url in urls {
            Task(priority: .utility) { [weak self] in
                _ = try? await self?.image(for: url, targetSize: targetSize, scale: scale)
            }
        }
    }

    private static func pixelSize(for targetSize: CGSize, scale: CGFloat) -> Int {
        let longestEdge = max(targetSize.width, targetSize.height) * max(scale, 1)
        guard longestEdge.isFinite, longestEdge > 0 else { return 512 }
        return min(2_048, max(64, Int(longestEdge.rounded(.up))))
    }
}

// MARK: - Request key

nonisolated struct ImageRequestKey: Hashable, Sendable {
    let url: URL
    let maxPixelSize: Int
}

// MARK: - Memory cache

nonisolated final class ImageMemoryCache: @unchecked Sendable {
    private let cache = NSCache<NSString, UIImage>()

    init() {
        cache.countLimit = 250
        cache.totalCostLimit = 96 * 1_024 * 1_024
    }

    func image(for key: ImageRequestKey) -> UIImage? {
        cache.object(forKey: key.cacheKey)
    }

    func insert(_ image: UIImage, for key: ImageRequestKey) {
        let cost = image.cgImage.map { $0.bytesPerRow * $0.height } ?? 0
        cache.setObject(image, forKey: key.cacheKey, cost: cost)
    }
}

private nonisolated extension ImageRequestKey {
    var cacheKey: NSString {
        "\(url.absoluteString)#\(maxPixelSize)" as NSString
    }
}

// MARK: - In-flight deduplication

actor InFlightRegistry {
    private var tasks: [ImageRequestKey: Task<SendableImage, Error>] = [:]

    func value(
        for key: ImageRequestKey,
        operation: @Sendable @escaping () async throws -> SendableImage
    ) async throws -> SendableImage {
        if let existing = tasks[key] {
            return try await existing.value
        }

        let task = Task.detached(priority: .utility) {
            try await operation()
        }
        tasks[key] = task

        do {
            let value = try await task.value
            tasks[key] = nil
            return value
        } catch {
            tasks[key] = nil
            throw error
        }
    }
}

// MARK: - Downsampling

nonisolated enum ImageDownsampler {
    static func downsample(_ data: Data, maxPixelSize: Int) throws -> SendableImage {
        guard let source = CGImageSourceCreateWithData(data as CFData, nil) else {
            throw ImageLoadingError.invalidData
        }

        let options: [CFString: Any] = [
            kCGImageSourceCreateThumbnailFromImageAlways: true,
            kCGImageSourceCreateThumbnailWithTransform: true,
            kCGImageSourceShouldCacheImmediately: true,
            kCGImageSourceThumbnailMaxPixelSize: maxPixelSize
        ]

        guard let image = CGImageSourceCreateThumbnailAtIndex(
            source,
            0,
            options as CFDictionary
        ) else {
            throw ImageLoadingError.decodingFailed
        }

        return SendableImage(image: UIImage(cgImage: image))
    }
}
