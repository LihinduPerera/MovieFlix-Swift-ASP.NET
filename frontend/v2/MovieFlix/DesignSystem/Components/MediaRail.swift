import SwiftUI

struct MediaPosterCard: View {
    let media: MediaSummary
    var imageURL: URL?
    /// `nil` lets the card fill the width proposed by its container (grids).
    var width: CGFloat? = MFSize.posterWidth
    var progress: Double?
    var isFavorite = false
    var showsMetadata = true
    /// Optional overlay such as an episode code for continue-watching cards.
    var badgeText: String?
    let action: () -> Void

    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        Button(action: action) {
            VStack(alignment: .leading, spacing: MFSpace.xs) {
                poster
                if showsMetadata {
                    metadata
                }
            }
            .frame(width: width, alignment: .leading)
            .contentShape(Rectangle())
        }
        .buttonStyle(.plain)
        .scaleEffect(1)
        .animation(reduceMotion ? nil : MFMotion.quick, value: isFavorite)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(accessibilityLabel)
        .accessibilityHint("Shows details")
        .accessibilityAddTraits(.isButton)
    }

    private var poster: some View {
        ZStack(alignment: .bottom) {
            RemoteImage(url: imageURL, label: "\(media.title) poster")
                .clipShape(RoundedRectangle(cornerRadius: MFRadius.poster, style: .continuous))
                .overlay {
                    RoundedRectangle(cornerRadius: MFRadius.poster, style: .continuous)
                        .strokeBorder(.white.opacity(0.10), lineWidth: 0.5)
                }

            LinearGradient(
                colors: [.clear, .black.opacity(progress == nil ? 0.10 : 0.58)],
                startPoint: .top,
                endPoint: .bottom
            )
            .clipShape(RoundedRectangle(cornerRadius: MFRadius.poster, style: .continuous))
            .allowsHitTesting(false)

            if let progress {
                MFProgressBar(value: progress, tint: MFColor.amber)
                    .padding(.horizontal, 8)
                    .padding(.bottom, 8)
            }
        }
        .aspectRatio(MFSize.posterAspectRatio, contentMode: .fit)
        .frame(width: width)
        .overlay(alignment: .topTrailing) {
            RatingBadge(value: media.voteAverage)
                .padding(6)
        }
        .overlay(alignment: .topLeading) {
            if isFavorite {
                Image(systemName: "heart.fill")
                    .font(.system(size: 11, weight: .bold))
                    .foregroundStyle(.white)
                    .frame(width: 24, height: 24)
                    .background(Circle().fill(MFColor.favorite))
                    .overlay {
                        Circle().strokeBorder(.white.opacity(0.22), lineWidth: 0.5)
                    }
                    .padding(6)
                    .accessibilityHidden(true)
            }
        }
        .overlay(alignment: .bottomLeading) {
            if let badgeText {
                Text(badgeText)
                    .font(.system(size: 10, weight: .semibold).monospacedDigit())
                    .foregroundStyle(.white)
                    .padding(.horizontal, 6)
                    .padding(.vertical, 3)
                    .background(Capsule(style: .continuous).fill(.black.opacity(0.62)))
                    .overlay {
                        Capsule(style: .continuous)
                            .strokeBorder(.white.opacity(0.18), lineWidth: 0.5)
                    }
                    .padding(.leading, 6)
                    .padding(.bottom, progress == nil ? 6 : 22)
                    .accessibilityHidden(true)
            }
        }
        .mfShadow(MFShadow.card)
    }

    private var metadata: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(media.title)
                .mfText(.label)
                .lineLimit(2)
                .multilineTextAlignment(.leading)
                .frame(maxWidth: .infinity, alignment: .leading)

            Text(media.metadataLine)
                .mfText(.caption)
                .foregroundStyle(MFColor.textSecondary)
                .lineLimit(1)
        }
    }

    private var accessibilityLabel: String {
        var parts = [media.title, media.mediaType.displayName]
        if let year = media.yearText { parts.append(year) }
        if media.hasRating {
            parts.append("rated \(media.ratingText) out of 10")
        }
        if isFavorite { parts.append("favorite") }
        return parts.joined(separator: ", ")
    }
}

struct MediaRail: View {
    let title: String
    var subtitle: String?
    let items: [MediaSummary]
    var isLoading = false
    let imageURL: (MediaSummary) -> URL?
    let onSelect: (MediaSummary) -> Void
    var onSeeAll: (() -> Void)?
    var progress: ((MediaSummary) -> Double?)?
    var favoriteIDs: Set<MediaID> = []

    @Environment(\.horizontalSizeClass) private var horizontalSizeClass
    @ScaledMetric(relativeTo: .body) private var scaledPosterWidth = MFSize.posterWidth

    private var cardWidth: CGFloat {
        horizontalSizeClass == .regular ? MFSize.posterWidthLarge : scaledPosterWidth
    }

    var body: some View {
        if isLoading && items.isEmpty {
            MediaRailSkeleton()
        } else if !items.isEmpty {
            VStack(alignment: .leading, spacing: MFSpace.sm) {
                MFSectionHeader(
                    title: title,
                    subtitle: subtitle,
                    action: onSeeAll
                )
                .padding(.horizontal, MFSpace.lg)

                ScrollView(.horizontal) {
                    LazyHStack(alignment: .top, spacing: MFSize.railSpacing) {
                        ForEach(items) { media in
                            MediaPosterCard(
                                media: media,
                                imageURL: imageURL(media),
                                width: cardWidth,
                                progress: progress?(media),
                                isFavorite: favoriteIDs.contains(media.id),
                                action: { onSelect(media) }
                            )
                        }
                    }
                    .scrollTargetLayout()
                }
                .contentMargins(.horizontal, MFSpace.lg, for: .scrollContent)
                .scrollIndicators(.hidden)
                .scrollTargetBehavior(.viewAligned)
            }
            .accessibilityElement(children: .contain)
            .accessibilityLabel(title)
        }
    }
}
