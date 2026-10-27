import SwiftUI

struct HeroHeader: View {
    let media: MediaSummary
    var imageURL: URL?
    var isFavorite = false
    var isInWatchlist = false
    var progress: Double?
    var onDetails: () -> Void
    var onFavorite: (() -> Void)?
    var onWatchlist: (() -> Void)?

    @Environment(\.horizontalSizeClass) private var horizontalSizeClass
    @Environment(\.verticalSizeClass) private var verticalSizeClass

    var body: some View {
        ZStack(alignment: .bottomLeading) {
            RemoteImage(url: imageURL, label: "\(media.title) backdrop")
                .frame(maxWidth: .infinity)
                .clipped()

            LinearGradient(
                stops: [
                    .init(color: .black.opacity(0.24), location: 0),
                    .init(color: .clear, location: 0.24),
                    .init(color: .clear, location: 0.38)
                ],
                startPoint: .top,
                endPoint: .bottom
            )

            MFColor.heroScrim
                .allowsHitTesting(false)

            VStack(alignment: .leading, spacing: MFSpace.sm) {
                HStack(spacing: 6) {
                    Circle()
                        .fill(MFColor.amber)
                        .frame(width: 6, height: 6)
                    Text("Featured")
                        .mfText(.overline)
                        .foregroundStyle(MFColor.textSecondary)
                }
                .accessibilityElement(children: .combine)
                .accessibilityLabel("Featured title")

                Text(media.title)
                    .mfText(.display)
                    .lineLimit(3)
                    .minimumScaleFactor(0.72)
                    .frame(maxWidth: 680, alignment: .leading)
                    .accessibilityAddTraits(.isHeader)

                HStack(spacing: MFSpace.xs) {
                    RatingBadge(value: media.voteAverage, style: .prominent)

                    if let year = media.yearText {
                        MFTagPill(title: year, systemImage: "calendar")
                    }

                    MFTagPill(title: media.mediaType.displayName, systemImage: media.mediaType.systemImage)
                }

                if !media.overview.isEmpty {
                    Text(media.overview)
                        .mfText(.callout)
                        .foregroundStyle(MFColor.textSecondary)
                        .lineLimit(3)
                        .frame(maxWidth: 560, alignment: .leading)
                        .fixedSize(horizontal: false, vertical: true)
                }

                HStack(spacing: MFSpace.sm) {
                    MFButton(
                        title: "View Details",
                        systemImage: "info.circle",
                        role: .primary,
                        action: onDetails
                    )

                    if let onFavorite {
                        MFIconButton(
                            systemImage: "heart",
                            label: isFavorite ? "Remove from favorites" : "Add to favorites",
                            style: .glass,
                            isSelected: isFavorite,
                            tint: MFColor.favorite,
                            action: onFavorite
                        )
                    }

                    if let onWatchlist {
                        MFIconButton(
                            systemImage: "bookmark",
                            label: isInWatchlist ? "Remove from watchlist" : "Add to watchlist",
                            style: .glass,
                            isSelected: isInWatchlist,
                            tint: MFColor.violet,
                            action: onWatchlist
                        )
                    }

                    Spacer(minLength: 0)
                }
                .padding(.top, MFSpace.xxs)

                if let progress {
                    HStack(spacing: MFSpace.xs) {
                        MFProgressBar(value: progress, tint: MFColor.amber)
                            .frame(maxWidth: 220)
                        Text("\(Int((min(max(progress, 0), 1) * 100).rounded()))% watched")
                            .mfText(.caption)
                            .foregroundStyle(MFColor.textTertiary)
                    }
                    .accessibilityElement(children: .combine)
                }
            }
            .padding(.horizontal, MFSpace.lg)
            .padding(.bottom, MFSpace.xxl)
            .frame(maxWidth: MFSize.contentMaxWidth, alignment: .leading)
        }
        .frame(height: heroHeight)
        .frame(maxWidth: .infinity)
        .clipped()
        .accessibilityElement(children: .contain)
    }

    private var heroHeight: CGFloat {
        if verticalSizeClass == .compact {
            return 380
        }
        return horizontalSizeClass == .regular ? MFSize.heroHeightPad : MFSize.heroHeightPhone
    }
}
