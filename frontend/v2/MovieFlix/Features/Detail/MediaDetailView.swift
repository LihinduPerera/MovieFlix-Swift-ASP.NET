import SwiftUI

struct MediaDetailView: View {
    let container: AppContainer
    let mediaID: MediaID
    var onSelectMedia: (MediaID) -> Void = { _ in }

    @State private var viewModel: MediaDetailViewModel
    @State private var showingAuth = false
    @State private var showingAddToCollection = false
    @Environment(\.dismiss) private var dismiss
    @Environment(\.openURL) private var openURL

    init(
        container: AppContainer,
        mediaID: MediaID,
        onSelectMedia: @escaping (MediaID) -> Void = { _ in }
    ) {
        self.container = container
        self.mediaID = mediaID
        self.onSelectMedia = onSelectMedia
        _viewModel = State(
            initialValue: MediaDetailViewModel(
                mediaID: mediaID,
                repository: container.mediaRepository,
                libraryRepository: container.libraryRepository
            )
        )
    }

    var body: some View {
        GeometryReader { proxy in
            ScrollView {
                LazyVStack(alignment: .leading, spacing: MFSpace.xxl) {
                    switch viewModel.phase {
                    case .loading:
                        detailSkeleton
                    case let .failed(error):
                        MFErrorStateView(
                            error: error,
                            retry: { Task { await viewModel.load(force: true) } }
                        )
                        .frame(minHeight: max(proxy.size.height * 0.7, 420))
                        .padding(.top, proxy.size.height * 0.15)
                    case .loaded:
                        if let details = viewModel.details {
                            content(details)
                        }
                    }
                }
                .padding(.bottom, MFSpace.huge)
            }
            .scrollIndicators(.hidden)
            .background(MFColor.canvas)
            .ignoresSafeArea(edges: .top)
            .overlay(alignment: .top) {
                chrome
                    .padding(.top, proxy.safeAreaInsets.top)
            }
        }
        .toolbar(.hidden, for: .navigationBar)
        .toolbar(.hidden, for: .tabBar)
        .navigationBarBackButtonHidden(true)
        .sheet(isPresented: $showingAuth) {
            AuthFlowView(container: container)
        }
        .task(id: mediaID) {
            await viewModel.load()
        }
        .onChange(of: container.session.isSignedIn) { _, isSignedIn in
            guard isSignedIn else { return }
            Task { await viewModel.refreshState() }
        }
    }

    // MARK: Bookmarks and hero

    private var chrome: some View {
        HStack {
            Button {
                dismiss()
            } label: {
                Image(systemName: "chevron.left")
                    .font(.system(size: 16, weight: .bold))
                    .frame(width: MFSize.iconButton, height: MFSize.iconButton)
                    .background(Circle().fill(.ultraThinMaterial))
                    .overlay {
                        Circle().strokeBorder(MFColor.border, lineWidth: 1)
                    }
                    .contentShape(Circle())
            }
            .buttonStyle(.plain)
            .accessibilityLabel("Back")

            Spacer()

            ShareLink(item: shareURL) {
                Image(systemName: "square.and.arrow.up")
                    .font(.system(size: 16, weight: .semibold))
                    .frame(width: MFSize.iconButton, height: MFSize.iconButton)
                    .background(Circle().fill(.ultraThinMaterial))
                    .overlay {
                        Circle().strokeBorder(MFColor.border, lineWidth: 1)
                    }
                    .contentShape(Circle())
            }
            .buttonStyle(.plain)
            .accessibilityLabel("Share")
        }
        .foregroundStyle(MFColor.textPrimary)
        .padding(.horizontal, MFSpace.lg)
    }

    @ViewBuilder
    private func content(_ details: MediaDetails) -> some View {
        header(details)

        VStack(alignment: .leading, spacing: MFSpace.xxl) {
            overviewSection(details)

            if !details.topCast.isEmpty {
                castSection(details)
            }

            infoSection(details)

            if details.mediaType == .tv, !details.seasons.isEmpty {
                seasonsSection(details)
            }

            reviewsSection

            if !details.similar.isEmpty {
                MediaRail(
                    title: "More Like This",
                    items: details.similar,
                    imageURL: { container.posterURL(for: $0) },
                    onSelect: { media in onSelectMedia(media.id) }
                )
                .padding(.horizontal, -MFSpace.lg)
            }

            creditsFooter
        }
        .padding(.horizontal, MFSpace.lg)
    }

    // MARK: Header

    private func header(_ details: MediaDetails) -> some View {
        VStack(alignment: .leading, spacing: MFSpace.md) {
            ZStack(alignment: .bottom) {
                RemoteImage(
                    url: container.images.url(for: details.backdropPath, size: .backdropLarge),
                    label: "\(details.title) backdrop"
                )
                .frame(height: 340)
                .frame(maxWidth: .infinity)
                .clipped()

                LinearGradient(
                    colors: [
                        Color.black.opacity(0.6),
                        .clear
                    ],
                    startPoint: .top,
                    endPoint: .bottom
                )
                .frame(height: 120)
                .frame(maxWidth: .infinity, maxHeight: .infinity, alignment: .top)
                .allowsHitTesting(false)

                LinearGradient(
                    stops: [
                        .init(color: .clear, location: 0.0),
                        .init(color: Color.black.opacity(0.4), location: 0.5),
                        .init(color: Color.black.opacity(0.9), location: 0.85),
                        .init(color: Color.black, location: 1.0)
                    ],
                    startPoint: .top,
                    endPoint: .bottom
                )
                .allowsHitTesting(false)
            }

            HStack(alignment: .top, spacing: MFSpace.md) {
                RemoteImage(
                    url: container.images.url(for: details.posterPath, size: .posterLarge),
                    label: "\(details.title) poster"
                )
                .frame(width: 112, height: 168)
                .clipShape(RoundedRectangle(cornerRadius: MFRadius.poster, style: .continuous))
                .overlay {
                    RoundedRectangle(cornerRadius: MFRadius.poster, style: .continuous)
                        .strokeBorder(.white.opacity(0.12), lineWidth: 0.5)
                }
                .mfShadow(MFShadow.card)

                VStack(alignment: .leading, spacing: MFSpace.xs) {
                    Text(details.title)
                        .mfText(.heroTitle)
                        .foregroundStyle(.white)
                        .shadow(color: .black.opacity(0.85), radius: 4, x: 0, y: 1)
                        .lineLimit(3)
                        .minimumScaleFactor(0.8)
                        .accessibilityAddTraits(.isHeader)

                    if let tagline = details.tagline, !tagline.isEmpty {
                        Text(tagline)
                            .mfText(.callout)
                            .italic()
                            .foregroundStyle(.white.opacity(0.95))
                            .shadow(color: .black.opacity(0.8), radius: 3, x: 0, y: 1)
                            .lineLimit(2)
                    }

                    if let releaseLabel {
                        Text(releaseLabel)
                            .mfText(.caption)
                            .foregroundStyle(.white.opacity(0.9))
                            .shadow(color: .black.opacity(0.75), radius: 2, x: 0, y: 1)
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
            }
            .padding(.top, -90)
            .padding(.horizontal, MFSpace.lg)

            metadataRow(details)
                .padding(.horizontal, MFSpace.lg)

            actions(details)
                .padding(.horizontal, MFSpace.lg)
                .sheet(isPresented: $showingAddToCollection) {
                    AddToCollectionSheet(
                        container: container,
                        mediaID: mediaID,
                        mediaTitle: details.title
                    )
                }
        }
    }

    private func metadataRow(_ details: MediaDetails) -> some View {
        ScrollView(.horizontal) {
            HStack(spacing: MFSpace.xs) {
                RatingBadge(value: displayRating, style: .prominent)

                if let runtime = details.runtimeText {
                    MFTagPill(title: runtime, systemImage: "clock")
                }

                if details.mediaType == .tv, let episodes = details.episodesText {
                    MFTagPill(title: episodes, systemImage: "rectangle.stack")
                }

                ForEach(details.genres) { genre in
                    MFTagPill(title: genre.name)
                }
            }
        }
        .scrollIndicators(.hidden)
    }

    private func actions(_ details: MediaDetails) -> some View {
        HStack(spacing: MFSpace.sm) {
            if let trailer = details.trailer, let url = trailer.youtubeURL {
                MFButton(title: "Watch Trailer", systemImage: "play.fill", role: .primary) {
                    openURL(url)
                }
                .accessibilityHint("Opens YouTube")
            }

            Spacer(minLength: 0)

            MFIconButton(
                systemImage: "heart",
                label: isFavorite ? "Remove from favorites" : "Add to favorites",
                style: .glass,
                isSelected: isFavorite,
                tint: MFColor.favorite
            ) {
                toggleFavorite()
            }
            .disabled(viewModel.isUpdatingFavorite)

            MFIconButton(
                systemImage: "bookmark",
                label: isInWatchlist ? "Remove from watchlist" : "Add to watchlist",
                style: .glass,
                isSelected: isInWatchlist,
                tint: MFColor.violet
            ) {
                toggleWatchlist()
            }
            .disabled(viewModel.isUpdatingWatchlist)

            MFIconButton(
                systemImage: "square.stack",
                label: "Add to collection",
                style: .glass,
                tint: MFColor.amber
            ) {
                addToCollection()
            }
        }
    }

    private var isFavorite: Bool {
        viewModel.state?.isFavorite ?? false
    }

    private var isInWatchlist: Bool {
        viewModel.state?.isInWatchlist ?? false
    }

    private func toggleFavorite() {
        guard container.session.isSignedIn else {
            showingAuth = true
            return
        }

        let wasFavorite = isFavorite
        Task {
            if let error = await viewModel.toggleFavorite() {
                container.toast.error(error.message)
            } else {
                container.toast.success(
                    wasFavorite ? "Removed from favorites." : "Added to favorites."
                )
            }
        }
    }

    private func toggleWatchlist() {
        guard container.session.isSignedIn else {
            showingAuth = true
            return
        }

        let wasSaved = isInWatchlist
        Task {
            if let error = await viewModel.toggleWatchlist() {
                container.toast.error(error.message)
            } else {
                container.toast.success(
                    wasSaved ? "Removed from watchlist." : "Added to watchlist."
                )
            }
        }
    }

    private func toggleWatched(_ episode: Episode) {
        guard container.session.isSignedIn else {
            showingAuth = true
            return
        }

        let target = !episode.completed
        Task {
            if let error = await viewModel.setWatched(target, episode: episode) {
                container.toast.error(error.message)
            }
        }
    }

    private func addToCollection() {
        guard container.session.isSignedIn else {
            showingAuth = true
            return
        }
        showingAddToCollection = true
    }

    private var releaseLabel: String? {
        guard let details = viewModel.details else { return nil }
        let date = details.mediaType == .tv ? details.firstAirDate : details.releaseDate
        guard let date else { return nil }
        let prefix = details.mediaType == .tv ? "First aired" : "Released"
        return "\(prefix) \(date.formatted(style: .dateTime.month(.wide).day().year()))"
    }

    private var displayRating: Double {
        if let community = viewModel.communityRating, community.averageRating > 0 {
            return community.averageRating
        }
        return viewModel.details?.voteAverage ?? 0
    }

    // MARK: Overview

    @ViewBuilder
    private func overviewSection(_ details: MediaDetails) -> some View {
        if !details.overview.isEmpty {
            VStack(alignment: .leading, spacing: MFSpace.xs) {
                Text("Overview")
                    .mfText(.sectionTitle)
                    .accessibilityAddTraits(.isHeader)

                Text(details.overview)
                    .mfText(.body)
                    .foregroundStyle(MFColor.textSecondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
    }

    // MARK: Cast

    private func castSection(_ details: MediaDetails) -> some View {
        VStack(alignment: .leading, spacing: MFSpace.sm) {
            Text("Top Cast")
                .mfText(.sectionTitle)
                .accessibilityAddTraits(.isHeader)

            ScrollView(.horizontal) {
                LazyHStack(alignment: .top, spacing: MFSpace.md) {
                    ForEach(details.topCast) { member in
                        VStack(spacing: MFSpace.xs) {
                            RemoteImage(
                                url: container.profileURL(for: member.profilePath),
                                label: member.name
                            )
                            .frame(width: 72, height: 72)
                            .clipShape(Circle())
                            .overlay {
                                Circle().strokeBorder(MFColor.borderStrong, lineWidth: 1)
                            }

                            Text(member.name)
                                .mfText(.caption)
                                .multilineTextAlignment(.center)
                                .lineLimit(2)

                            Text(member.roleText)
                                .mfText(.caption)
                                .foregroundStyle(MFColor.textTertiary)
                                .multilineTextAlignment(.center)
                                .lineLimit(2)
                        }
                        .frame(width: 92)
                        .accessibilityElement(children: .combine)
                    }
                }
            }
            .scrollIndicators(.hidden)
            .contentMargins(.horizontal, MFSpace.lg, for: .scrollContent)
        }
        .padding(.horizontal, -MFSpace.lg)
    }

    // MARK: Info

    private func infoSection(_ details: MediaDetails) -> some View {
        VStack(alignment: .leading, spacing: MFSpace.sm) {
            Text("Details")
                .mfText(.sectionTitle)
                .accessibilityAddTraits(.isHeader)

            MFGlassPanel(cornerRadius: MFRadius.lg) {
                VStack(spacing: 0) {
                    ForEach(Array(infoRows(details).enumerated()), id: \.offset) { index, row in
                        if index > 0 {
                            Divider()
                                .overlay(MFColor.separator)
                        }

                        HStack(alignment: .firstTextBaseline, spacing: MFSpace.md) {
                            Text(row.label)
                                .mfText(.callout)
                                .foregroundStyle(MFColor.textTertiary)

                            Spacer(minLength: MFSpace.sm)

                            Text(row.value)
                                .mfText(.callout)
                                .multilineTextAlignment(.trailing)
                        }
                        .padding(.vertical, MFSpace.xs)
                    }
                }
            }
        }
    }

    private func infoRows(_ details: MediaDetails) -> [(label: String, value: String)] {
        var rows: [(String, String)] = []

        if let status = details.status, !status.isEmpty {
            rows.append(("Status", status))
        }
        if !details.originalLanguage.isEmpty {
            rows.append(("Original Language", details.originalLanguage.uppercased()))
        }
        if let date = details.releaseDate ?? details.firstAirDate {
            rows.append(("Release Date", date.formatted(style: .dateTime.month(.abbreviated).day().year())))
        }
        if let last = details.lastAirDate {
            rows.append(("Last Air Date", last.formatted(style: .dateTime.month(.abbreviated).day().year())))
        }
        if let seasons = details.numberOfSeasons, seasons > 0 {
            rows.append(("Seasons", "\(seasons)"))
        }
        if let episodes = details.numberOfEpisodes, episodes > 0 {
            rows.append(("Episodes", "\(episodes)"))
        }
        if let director = details.directorNames {
            rows.append(("Director", director))
        }
        let writers = Array(details.credits.writers.prefix(3)).map(\.name)
        if !writers.isEmpty {
            rows.append(("Writing", writers.joined(separator: ", ")))
        }

        return rows.map { (label: $0.0, value: $0.1) }
    }

    // MARK: Seasons

    private func seasonsSection(_ details: MediaDetails) -> some View {
        VStack(alignment: .leading, spacing: MFSpace.sm) {
            Text("Seasons")
                .mfText(.sectionTitle)
                .accessibilityAddTraits(.isHeader)

            VStack(spacing: MFSpace.xs) {
                ForEach(details.seasons) { season in
                    seasonRow(season, showTitle: details.title)
                }
            }
        }
    }

    private func seasonRow(_ season: SeasonSummary, showTitle: String) -> some View {
        let isExpanded = viewModel.expandedSeasonNumber == season.seasonNumber
        let isLoading = viewModel.loadingSeasonNumber == season.seasonNumber
        let episodes = viewModel.episodesBySeason[season.seasonNumber]

        return VStack(alignment: .leading, spacing: 0) {
            Button {
                Task { await viewModel.toggleSeason(season.seasonNumber) }
            } label: {
                HStack(spacing: MFSpace.sm) {
                    VStack(alignment: .leading, spacing: 2) {
                        Text(season.name)
                            .mfText(.headline)
                            .multilineTextAlignment(.leading)

                        Text(episodeCountText(season))
                            .mfText(.caption)
                            .foregroundStyle(MFColor.textSecondary)
                    }

                    Spacer(minLength: MFSpace.sm)

                    if isLoading {
                        ProgressView()
                            .controlSize(.small)
                    } else {
                        Image(systemName: "chevron.down")
                            .font(.system(size: 13, weight: .bold))
                            .foregroundStyle(MFColor.textTertiary)
                            .rotationEffect(.degrees(isExpanded ? 180 : 0))
                    }
                }
                .padding(MFSpace.md)
                .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .accessibilityLabel("\(showTitle), \(season.name), \(episodeCountText(season))")
            .accessibilityValue(isExpanded ? "Expanded" : "Collapsed")
            .accessibilityHint("Shows episodes")

            if isExpanded {
                VStack(alignment: .leading, spacing: MFSpace.sm) {
                    if let error = viewModel.seasonErrors[season.seasonNumber] {
                        HStack(spacing: MFSpace.xs) {
                            Text(error.message)
                                .mfText(.caption)
                                .foregroundStyle(MFColor.textSecondary)
                            Spacer(minLength: 0)
                            Button("Retry") {
                                Task { await viewModel.retrySeason(season.seasonNumber) }
                            }
                            .font(MFTypography.label)
                            .foregroundStyle(MFColor.violet)
                        }
                        .padding(.horizontal, MFSpace.md)
                        .padding(.bottom, MFSpace.sm)
                    } else if let episodes, !episodes.isEmpty {
                        ForEach(episodes) { episode in
                            episodeRow(episode)
                            if episode.id != episodes.last?.id {
                                Divider()
                                    .overlay(MFColor.separator)
                                    .padding(.horizontal, MFSpace.md)
                            }
                        }
                    } else if !isLoading {
                        Text("No episodes available for this season.")
                            .mfText(.caption)
                            .foregroundStyle(MFColor.textTertiary)
                            .padding(.horizontal, MFSpace.md)
                            .padding(.bottom, MFSpace.md)
                    }
                }
                .transition(.opacity)
            }
        }
        .background {
            RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                .fill(MFColor.surface)
        }
        .overlay {
            RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                .strokeBorder(MFColor.border, lineWidth: 1)
        }
        .animation(.snappy(duration: 0.24), value: isExpanded)
    }

    private func episodeCountText(_ season: SeasonSummary) -> String {
        let count = season.episodeCount == 1 ? "1 episode" : "\(season.episodeCount) episodes"
        if let airDate = season.airDate {
            return "\(count) • \(airDate.year)"
        }
        return count
    }

    private func episodeRow(_ episode: Episode) -> some View {
        HStack(alignment: .top, spacing: MFSpace.sm) {
            RemoteImage(
                url: container.stillURL(for: episode.stillPath),
                label: "\(episode.name) still"
            )
            .frame(width: 116, height: 66)
            .clipShape(RoundedRectangle(cornerRadius: MFRadius.xs, style: .continuous))

            VStack(alignment: .leading, spacing: 3) {
                HStack(spacing: MFSpace.xs) {
                    Text(episode.code)
                        .mfText(.mono)
                        .foregroundStyle(MFColor.violet)

                    if episode.completed {
                        Image(systemName: "checkmark.circle.fill")
                            .font(.system(size: 11, weight: .semibold))
                            .foregroundStyle(MFColor.success)
                            .accessibilityLabel("Watched")
                    }
                }

                Text(episode.name)
                    .mfText(.label)
                    .lineLimit(2)

                HStack(spacing: MFSpace.xs) {
                    if let runtime = episode.runtimeText {
                        Text(runtime)
                    }
                    if let airDate = episode.airDate {
                        Text(airDate.formatted(style: .dateTime.month(.abbreviated).day().year()))
                    }
                }
                .mfText(.caption)
                .foregroundStyle(MFColor.textTertiary)

                if let overview = episode.overview, !overview.isEmpty {
                    Text(overview)
                        .mfText(.caption)
                        .foregroundStyle(MFColor.textSecondary)
                        .lineLimit(2)
                }
            }

            Spacer(minLength: 0)

            Button {
                toggleWatched(episode)
            } label: {
                ZStack {
                    Image(systemName: episode.completed ? "checkmark.circle.fill" : "circle")
                        .font(.system(size: 22, weight: .regular))
                        .foregroundStyle(episode.completed ? MFColor.success : MFColor.textTertiary)

                    if viewModel.updatingEpisodeID == episode.id {
                        ProgressView()
                            .controlSize(.mini)
                            .tint(MFColor.success)
                    }
                }
                .frame(width: 40, height: 40)
                .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .disabled(viewModel.updatingEpisodeID != nil)
            .accessibilityLabel(episode.completed ? "Mark \(episode.code) unwatched" : "Mark \(episode.code) watched")
            .accessibilityAddTraits(episode.completed ? .isSelected : [])
        }
        .padding(.horizontal, MFSpace.md)
        .padding(.bottom, MFSpace.sm)
        .accessibilityElement(children: .contain)
    }

    // MARK: Reviews

    private var reviewsSection: some View {
        VStack(alignment: .leading, spacing: MFSpace.sm) {
            HStack(alignment: .firstTextBaseline) {
                Text("Reviews")
                    .mfText(.sectionTitle)
                    .accessibilityAddTraits(.isHeader)

                Spacer()

                if let count = viewModel.communityRating?.ratingCount, count > 0 {
                    Text("\(count) community rating\(count == 1 ? "" : "s")")
                        .mfText(.caption)
                        .foregroundStyle(MFColor.textTertiary)
                }
            }

            if viewModel.reviews.isEmpty {
                HStack(spacing: MFSpace.sm) {
                    Image(systemName: "text.bubble")
                        .font(.system(size: 18, weight: .medium))
                        .foregroundStyle(MFColor.textTertiary)

                    Text("No reviews yet. Be the first after you sign in.")
                        .mfText(.callout)
                        .foregroundStyle(MFColor.textSecondary)
                }
                .padding(MFSpace.md)
                .frame(maxWidth: .infinity, alignment: .leading)
                .background {
                    RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                        .fill(MFColor.surface)
                }
                .overlay {
                    RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                        .strokeBorder(MFColor.border, lineWidth: 1)
                }
            } else {
                VStack(spacing: MFSpace.sm) {
                    ForEach(viewModel.reviews) { review in
                        reviewCard(review)
                    }
                }

                if viewModel.reviewsHasMore {
                    MFButton(
                        title: viewModel.isLoadingReviews ? "Loading…" : "Show More Reviews",
                        role: .secondary,
                        isLoading: viewModel.isLoadingReviews,
                        isFullWidth: true
                    ) {
                        Task { await viewModel.loadMoreReviews() }
                    }
                }
            }
        }
    }

    private func reviewCard(_ review: Review) -> some View {
        VStack(alignment: .leading, spacing: MFSpace.xs) {
            HStack(spacing: MFSpace.xs) {
                MFAvatar(initials: initials(for: review.authorDisplayName), size: 30)

                VStack(alignment: .leading, spacing: 1) {
                    HStack(spacing: 6) {
                        Text(review.authorDisplayName)
                            .mfText(.label)

                        if review.isMine {
                            Text("You")
                                .mfText(.overline)
                                .foregroundStyle(MFColor.violet)
                        }
                    }

                    Text(review.createdAt.formatted(date: .abbreviated, time: .omitted))
                        .mfText(.caption)
                        .foregroundStyle(MFColor.textTertiary)
                }

                Spacer(minLength: 0)
            }

            Text(review.content)
                .mfText(.callout)
                .foregroundStyle(MFColor.textSecondary)
                .fixedSize(horizontal: false, vertical: true)
        }
        .padding(MFSpace.md)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background {
            RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                .fill(MFColor.surface)
        }
        .overlay {
            RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                .strokeBorder(MFColor.border, lineWidth: 1)
        }
        .accessibilityElement(children: .combine)
    }

    private func initials(for name: String) -> String {
        let parts = name.split(separator: " ").prefix(2)
        let value = parts.compactMap(\.first).map(String.init).joined()
        return value.isEmpty ? "?" : value.uppercased()
    }

    // MARK: Footer

    private var creditsFooter: some View {
        HStack(spacing: MFSpace.xs) {
            Image(systemName: "film.stack")
                .font(.system(size: 11, weight: .semibold))
            Text("Metadata and artwork from TMDB")
                .mfText(.caption)
        }
        .foregroundStyle(MFColor.textTertiary)
        .frame(maxWidth: .infinity, alignment: .center)
        .padding(.top, MFSpace.xs)
    }

    // MARK: Placeholders

    private var detailSkeleton: some View {
        VStack(alignment: .leading, spacing: MFSpace.xl) {
            SkeletonBlock(cornerRadius: 0)
                .frame(height: 300)

            HStack(alignment: .top, spacing: MFSpace.md) {
                SkeletonBlock(cornerRadius: MFRadius.poster)
                    .frame(width: 112, height: 168)

                VStack(alignment: .leading, spacing: MFSpace.sm) {
                    SkeletonBlock()
                        .frame(height: 26)
                    SkeletonBlock()
                        .frame(width: 180, height: 14)
                    SkeletonBlock()
                        .frame(width: 140, height: 14)
                    SkeletonBlock(cornerRadius: MFRadius.pill)
                        .frame(width: 120, height: 32)
                }
            }
            .padding(.horizontal, MFSpace.lg)

            VStack(alignment: .leading, spacing: MFSpace.sm) {
                SkeletonBlock()
                    .frame(width: 100, height: 18)
                SkeletonBlock()
                    .frame(height: 13)
                SkeletonBlock()
                    .frame(height: 13)
                SkeletonBlock()
                    .frame(width: 240, height: 13)
            }
            .padding(.horizontal, MFSpace.lg)
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Loading details")
    }

    private var shareURL: URL {
        URL(string: "https://www.themoviedb.org/\(mediaID.mediaType.rawValue)/\(mediaID.tmdbId)")!
    }
}
