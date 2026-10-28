import SwiftUI

/// A 2×2 poster collage that stands in for a collection everywhere it appears.
/// It never fetches beyond the four cover paths the API already derived.
struct CollectionCover: View {
    let imageURLs: [URL]
    var count: Int?
    var aspectRatio: CGFloat = 3.0 / 2.0
    var cornerRadius: CGFloat = MFRadius.lg
    var showsBorder = true

    var body: some View {
        ZStack(alignment: .bottomLeading) {
            content

            LinearGradient(
                colors: [.clear, .black.opacity(count == nil ? 0.18 : 0.62)],
                startPoint: .top,
                endPoint: .bottom
            )
            .allowsHitTesting(false)

            if let count {
                HStack(spacing: 4) {
                    Image(systemName: "square.stack.fill")
                        .font(.system(size: 9, weight: .bold))

                    Text("\(count)")
                        .font(.system(size: 10, weight: .semibold).monospacedDigit())
                }
                .foregroundStyle(.white)
                .padding(.horizontal, 7)
                .padding(.vertical, 3)
                .background(Capsule(style: .continuous).fill(.black.opacity(0.58)))
                .overlay {
                    Capsule(style: .continuous)
                        .strokeBorder(.white.opacity(0.18), lineWidth: 0.5)
                }
                .padding(8)
                .accessibilityHidden(true)
            }
        }
        .aspectRatio(aspectRatio, contentMode: .fit)
        .clipShape(RoundedRectangle(cornerRadius: cornerRadius, style: .continuous))
        .overlay {
            if showsBorder {
                RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
                    .strokeBorder(.white.opacity(0.10), lineWidth: 0.5)
            }
        }
    }

    @ViewBuilder
    private var content: some View {
        switch imageURLs.count {
        case 0:
            ZStack {
                MFColor.subtleGradient
                Image(systemName: "square.stack.3d.up.fill")
                    .font(.system(size: 26, weight: .medium))
                    .foregroundStyle(MFColor.brandGradient)
            }
        case 1:
            tile(imageURLs[0])
        case 2:
            HStack(spacing: 1) {
                tile(imageURLs[0])
                tile(imageURLs[1])
            }
        case 3:
            HStack(spacing: 1) {
                tile(imageURLs[0])

                VStack(spacing: 1) {
                    tile(imageURLs[1])
                    tile(imageURLs[2])
                }
            }
        default:
            VStack(spacing: 1) {
                HStack(spacing: 1) {
                    tile(imageURLs[0])
                    tile(imageURLs[1])
                }

                HStack(spacing: 1) {
                    tile(imageURLs[2])
                    tile(imageURLs[3])
                }
            }
        }
    }

    private func tile(_ url: URL) -> some View {
        RemoteImage(url: url, label: nil, allowsRetryOnTap: false)
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .clipped()
    }
}

struct CollectionCard: View {
    let collection: CollectionSummary
    let imageURLs: [URL]
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            VStack(alignment: .leading, spacing: MFSpace.xs) {
                CollectionCover(
                    imageURLs: imageURLs,
                    count: collection.itemCount,
                    cornerRadius: MFRadius.poster
                )
                .mfShadow(MFShadow.card)

                VStack(alignment: .leading, spacing: 2) {
                    Text(collection.name)
                        .mfText(.label)
                        .lineLimit(2)
                        .multilineTextAlignment(.leading)

                    Text(metaText)
                        .mfText(.caption)
                        .foregroundStyle(MFColor.textSecondary)
                        .lineLimit(1)
                }
                .frame(maxWidth: .infinity, alignment: .leading)
            }
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("\(collection.name), \(metaText)")
        .accessibilityHint("Opens the collection")
        .accessibilityAddTraits(.isButton)
    }

    private var metaText: String {
        let titles = collection.itemCount == 1 ? "1 title" : "\(collection.itemCount) titles"
        guard !collection.description.isEmpty else { return titles }
        return "\(titles) • \(collection.description)"
    }
}
