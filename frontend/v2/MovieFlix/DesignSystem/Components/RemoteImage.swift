import SwiftUI
import UIKit

extension EnvironmentValues {
    @Entry var imagePipeline: ImagePipeline?
}

nonisolated private struct RemoteImageLoadKey: Hashable {
    let url: URL?
    let width: Int
    let height: Int
    let scale: CGFloat
    let attempt: Int
}

struct RemoteImage: View {
    let url: URL?
    var label: String?
    var contentMode: ContentMode = .fill
    var showsLoadingShimmer = true

    @Environment(\.imagePipeline) private var pipeline
    @Environment(\.displayScale) private var displayScale
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @State private var image: UIImage?
    @State private var didFail = false
    @State private var attempt = 0

    var body: some View {
        GeometryReader { proxy in
            ZStack {
                if let image {
                    Image(uiImage: image)
                        .resizable()
                        .aspectRatio(contentMode: contentMode)
                        .frame(width: proxy.size.width, height: proxy.size.height)
                        .clipped()
                        .transition(.opacity)
                } else {
                    MFImagePlaceholder(
                        showsShimmer: showsLoadingShimmer && !didFail,
                        isFailure: didFail
                    )
                }
            }
            .frame(width: proxy.size.width, height: proxy.size.height)
            .contentShape(Rectangle())
            .onTapGesture {
                guard didFail else { return }
                attempt += 1
            }
            .task(id: RemoteImageLoadKey(
                url: url,
                width: Int(proxy.size.width.rounded()),
                height: Int(proxy.size.height.rounded()),
                scale: displayScale,
                attempt: attempt
            )) {
                await load(size: proxy.size)
            }
        }
        .accessibilityElement()
        .accessibilityLabel(label ?? "Image")
        .accessibilityHidden(label == nil)
        .accessibilityAddTraits(didFail ? .isButton : [])
        .accessibilityHint(didFail ? "Retry loading the image" : "")
        .animation(reduceMotion ? nil : .easeOut(duration: 0.28), value: image != nil)
    }

    private func load(size: CGSize) async {
        guard let url, let pipeline, size.width > 1, size.height > 1 else { return }
        didFail = false

        do {
            let loaded = try await pipeline.image(
                for: url,
                targetSize: size,
                scale: displayScale
            )
            guard !Task.isCancelled else { return }
            image = loaded
        } catch is CancellationError {
            return
        } catch ImageLoadingError.cancelled {
            return
        } catch {
            guard !Task.isCancelled else { return }
            didFail = true
        }
    }
}

private struct MFImagePlaceholder: View {
    let showsShimmer: Bool
    let isFailure: Bool

    var body: some View {
        ZStack {
            LinearGradient(
                colors: [MFColor.surfaceElevated, MFColor.surface],
                startPoint: .topLeading,
                endPoint: .bottomTrailing
            )

            if showsShimmer {
                SkeletonShimmer()
            }

            Image(systemName: isFailure ? "photo.badge.exclamationmark" : "film")
                .font(.system(size: 24, weight: .medium))
                .foregroundStyle(MFColor.textTertiary)
                .accessibilityHidden(true)
        }
        .clipped()
    }
}

extension ImagePipeline {
    static let preview: ImagePipeline = ImagePipeline(session: .shared)
}
