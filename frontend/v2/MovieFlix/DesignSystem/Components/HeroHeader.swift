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
            Button(action: onDetails) {
                RemoteImage(url: imageURL, label: "\(media.title) backdrop")
                    .frame(maxWidth: .infinity)
                    .clipped()
            }
            .buttonStyle(.plain)
            .accessibilityLabel("View details for \(media.title)")

            LinearGradient(
                colors: [
                    Color.black.opacity(0.65),
                    .clear
                ],
                startPoint: .top,
                endPoint: .bottom
            )
            .frame(height: heroHeight * 0.32)
            .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
            .allowsHitTesting(false)

            LinearGradient(
                stops: [
                    .init(color: .clear, location: 0.0),
                    .init(color: Color.black.opacity(0.4), location: 0.45),
                    .init(color: Color.black.opacity(0.85), location: 0.8),
                    .init(color: Color.black, location: 1.0)
                ],
                startPoint: .top,
                endPoint: .bottom
            )
            .allowsHitTesting(false)

            VStack(alignment: .leading, spacing: MFSpace.sm) {
                HStack(spacing: 6) {
                    Circle()
                        .fill(MFColor.amber)
                        .frame(width: 6, height: 6)
                    Text("Featured")
                        .mfText(.overline)
                        .foregroundStyle(.white)
                }
                .accessibilityElement(children: .combine)
                .accessibilityLabel("Featured title")

                Button(action: onDetails) {
                    Text(media.title)
                        .mfText(.display)
                        .foregroundStyle(.white)
                        .lineLimit(3)
                        .minimumScaleFactor(0.72)
                        .frame(maxWidth: 680, alignment: .leading)
                        .multilineTextAlignment(.leading)
                        .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .accessibilityAddTraits(.isHeader)
                .accessibilityHint("View details")

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
                        .foregroundStyle(.white.opacity(0.92))
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
                            .foregroundStyle(.white.opacity(0.8))
                    }
                    .accessibilityElement(children: .combine)
                }
            }
            .padding(.horizontal, MFSpace.lg)
            .padding(.bottom, MFSpace.xxl)
            .padding(.top, heroHeight * 0.35)
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
