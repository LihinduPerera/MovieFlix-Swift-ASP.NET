import SwiftUI

struct SearchView: View {
    let container: AppContainer

    @State private var viewModel: SearchViewModel
    @State private var path: [AppRoute] = []
    @State private var showingAuth = false
    @FocusState private var isSearchFocused: Bool

    init(container: AppContainer) {
        self.container = container
        _viewModel = State(
            initialValue: SearchViewModel(
                repository: container.mediaRepository,
                historyRepository: container.searchHistoryRepository
            )
        )
    }

    private var session: SessionStore { container.session }

    var body: some View {
        NavigationStack(path: $path) {
            VStack(spacing: 0) {
                header
                searchField
                    .padding(.horizontal, MFSpace.lg)
                    .padding(.top, MFSpace.xs)
                typeChips
                    .padding(.horizontal, MFSpace.lg)
                    .padding(.top, MFSpace.sm)
                    .padding(.bottom, MFSpace.sm)

                ScrollView {
                    LazyVStack(alignment: .leading, spacing: MFSpace.xxl) {
                        content
                    }
                    .padding(.top, MFSpace.xs)
                    .padding(.bottom, MFSpace.xxxl)
                }
                .scrollIndicators(.hidden)
                .scrollDismissesKeyboard(.immediately)
                .refreshable {
                    if viewModel.isSearchMode {
                        await viewModel.retrySearch()
                    } else {
                        await viewModel.loadDiscovery(force: true)
                    }
                }
            }
            .background(MFColor.canvas)
            .toolbar(.hidden, for: .navigationBar)
            .navigationDestination(for: AppRoute.self) { route in
                destination(for: route)
            }
            .sheet(isPresented: $showingAuth) {
                AuthFlowView(container: container)
            }
            .task(id: session.isSignedIn) {
                await viewModel.loadDiscovery()
                if session.isSignedIn {
                    await viewModel.loadRecentSearches()
                } else {
                    viewModel.clearLocalRecentSearches()
                }
            }
            .onChange(of: viewModel.query) { _, _ in
                viewModel.queryChanged()
            }
        }
    }

    // MARK: Header

    private var header: some View {
        HStack(alignment: .center, spacing: MFSpace.sm) {
            VStack(alignment: .leading, spacing: 2) {
                Text("Search")
                    .mfText(.title)
                    .accessibilityAddTraits(.isHeader)

                Text("Find your next obsession")
                    .mfText(.caption)
                    .foregroundStyle(MFColor.textSecondary)
            }

            Spacer(minLength: MFSpace.sm)

            Button {
                if session.isSignedIn {
                    container.navigation.selectedTab = .profile
                } else {
                    showingAuth = true
                }
            } label: {
                if let user = session.user {
                    MFAvatar(initials: user.initials, size: 32)
                        .contentShape(Circle())
                } else {
                    Image(systemName: "person.crop.circle")
                        .font(.system(size: 26, weight: .regular))
                        .frame(width: MFSize.avatar + 4, height: MFSize.avatar + 4)
                        .contentShape(Circle())
                }
            }
            .buttonStyle(.plain)
            .accessibilityLabel(session.isSignedIn ? "Open profile" : "Sign in")
        }
        .foregroundStyle(MFColor.textPrimary)
        .padding(.horizontal, MFSpace.lg)
        .padding(.top, MFSpace.xs)
    }

    private var searchField: some View {
        @Bindable var viewModel = viewModel

        return HStack(spacing: MFSpace.xs) {
            Image(systemName: "magnifyingglass")
                .font(.system(size: 15, weight: .semibold))
                .foregroundStyle(isSearchFocused ? MFColor.violet : MFColor.textTertiary)
                .accessibilityHidden(true)

            TextField("Search movies and TV", text: $viewModel.query)
                .font(MFTypography.body)
                .foregroundStyle(MFColor.textPrimary)
                .textInputAutocapitalization(.never)
                .autocorrectionDisabled()
                .submitLabel(.search)
                .focused($isSearchFocused)
                .onSubmit {
                    Task {
                        await viewModel.submit(recordHistory: session.isSignedIn)
                    }
                }

            if viewModel.isSearching, viewModel.isSearchMode {
                ProgressView()
                    .controlSize(.mini)
                    .tint(MFColor.textSecondary)
                    .accessibilityLabel("Searching")
            }

            if !viewModel.query.isEmpty {
                Button {
                    viewModel.clearQuery()
                    isSearchFocused = true
                } label: {
                    Image(systemName: "xmark.circle.fill")
                        .font(.system(size: 16, weight: .semibold))
                        .foregroundStyle(MFColor.textTertiary)
                        .frame(width: 28, height: 28)
                        .contentShape(Rectangle())
                }
                .buttonStyle(.plain)
                .accessibilityLabel("Clear search")
            }
        }
        .padding(.leading, MFSpace.md)
        .padding(.trailing, MFSpace.xxs)
        .frame(minHeight: 46)
        .background {
            Capsule(style: .continuous)
                .fill(MFColor.surface)
        }
        .overlay {
            Capsule(style: .continuous)
                .strokeBorder(
                    isSearchFocused ? MFColor.violet.opacity(0.65) : MFColor.border,
                    lineWidth: 1
                )
        }
    }

    private var typeChips: some View {
        HStack(spacing: MFSpace.xs) {
            ForEach(SearchType.allCases) { type in
                MFChoiceChip(
                    title: type.displayName,
                    isSelected: viewModel.searchType == type
                ) {
                    Task { await viewModel.selectSearchType(type) }
                }
            }

            Spacer(minLength: 0)
        }
    }

    // MARK: Content

    @ViewBuilder
    private var content: some View {
        if viewModel.isSearchMode {
            searchResults
        } else {
            discoveryContent
        }
    }

    // MARK: Discovery

    @ViewBuilder
    private var discoveryContent: some View {
        if viewModel.shelves.isEmpty {
            if case let .failed(error) = viewModel.discoveryPhase {
                MFErrorStateView(
                    error: error,
                    retry: { Task { await viewModel.loadDiscovery(force: true) } }
                )
                .padding(.top, MFSpace.xl)
            } else {
                discoverySkeleton
            }
        } else {
            if let message = viewModel.partialErrorMessage {
                MFNoticeBanner(
                    message: message,
                    onDismiss: { viewModel.dismissPartialError() }
                )
                .padding(.horizontal, MFSpace.lg)
            }

            recentSearchesSection
            trendingSection
            genreSection

            ForEach(viewModel.discoveryShelves) { shelf in
                MediaRail(
                    title: shelf.id.title,
                    items: shelf.items,
                    imageURL: { container.posterURL(for: $0) },
                    onSelect: { media in open(media.id) }
                )
            }
        }
    }

    @ViewBuilder
    private var recentSearchesSection: some View {
        if session.isSignedIn,
           !viewModel.recentSearches.isEmpty || viewModel.isLoadingRecentSearches {
            VStack(alignment: .leading, spacing: MFSpace.sm) {
                HStack(alignment: .firstTextBaseline) {
                    Text("Recent Searches")
                        .mfText(.sectionTitle)

                    Spacer(minLength: MFSpace.sm)

                    if !viewModel.recentSearches.isEmpty {
                        Button("Clear") {
                            Task {
                                if let error = await viewModel.clearRecentSearches() {
                                    container.toast.error(error.message)
                                } else {
                                    container.toast.info("Recent searches cleared.")
                                }
                            }
                        }
                        .font(MFTypography.label)
                        .foregroundStyle(MFColor.textSecondary)
                        .frame(minHeight: MFSize.cardMinHeight)
                        .accessibilityLabel("Clear recent searches")
                    }
                }
                .padding(.horizontal, MFSpace.lg)

                if viewModel.recentSearches.isEmpty {
                    recentSearchesSkeleton
                } else {
                    ScrollView(.horizontal) {
                        HStack(spacing: MFSpace.xs) {
                            ForEach(viewModel.recentSearches) { entry in
                                recentSearchChip(entry)
                            }
                        }
                        .padding(.horizontal, MFSpace.lg)
                    }
                    .scrollIndicators(.hidden)
                }
            }
        }
    }

    private func recentSearchChip(_ entry: SearchHistoryEntry) -> some View {
        HStack(spacing: 2) {
            Button {
                viewModel.query = entry.query
                isSearchFocused = false
            } label: {
                HStack(spacing: 6) {
                    Image(systemName: "clock.arrow.circlepath")
                        .font(.system(size: 11, weight: .semibold))
                        .accessibilityHidden(true)

                    Text(entry.query)
                        .font(MFTypography.label)
                        .lineLimit(1)
                }
                .foregroundStyle(MFColor.textSecondary)
                .padding(.leading, MFSpace.sm)
                .padding(.trailing, MFSpace.xxs)
                .frame(minHeight: 38)
                .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .accessibilityLabel("Search again for \(entry.query)")

            Button {
                Task {
                    if let error = await viewModel.removeRecentSearch(entry) {
                        container.toast.error(error.message)
                    }
                }
            } label: {
                Image(systemName: "xmark")
                    .font(.system(size: 9, weight: .bold))
                    .foregroundStyle(MFColor.textTertiary)
                    .frame(width: 26, height: 30)
                    .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .accessibilityLabel("Remove \(entry.query) from recent searches")
        }
        .padding(.trailing, MFSpace.xxs)
        .background {
            Capsule(style: .continuous)
                .fill(MFColor.surface)
        }
        .overlay {
            Capsule(style: .continuous)
                .strokeBorder(MFColor.border, lineWidth: 1)
        }
    }

    private var recentSearchesSkeleton: some View {
        ScrollView(.horizontal) {
            HStack(spacing: MFSpace.xs) {
                ForEach(0..<3, id: \.self) { index in
                    SkeletonBlock(cornerRadius: MFRadius.pill)
                        .frame(width: index == 1 ? 148 : 116, height: 38)
                }
            }
            .padding(.horizontal, MFSpace.lg)
        }
        .scrollDisabled(true)
        .scrollIndicators(.hidden)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Loading recent searches")
    }

    private var trendingSection: some View {
        VStack(alignment: .leading, spacing: MFSpace.sm) {
            HStack(alignment: .center) {
                Text("Trending")
                    .mfText(.sectionTitle)

                Spacer(minLength: MFSpace.sm)

                MFCompactSegmentedControl(
                    options: TrendingWindow.allCases,
                    title: { $0.displayName },
                    selection: viewModel.trendingWindow,
                    onSelect: { window in
                        Task { await viewModel.selectTrendingWindow(window) }
                    }
                )
            }
            .padding(.horizontal, MFSpace.lg)

            if viewModel.isLoadingTrending, viewModel.trendingItems.isEmpty {
                trendingSkeleton
            } else if !viewModel.trendingItems.isEmpty {
                ScrollView(.horizontal) {
                    LazyHStack(alignment: .top, spacing: MFSize.railSpacing) {
                        ForEach(viewModel.trendingItems) { media in
                            MediaPosterCard(
                                media: media,
                                imageURL: container.posterURL(for: media),
                                width: trendingCardWidth,
                                action: { open(media.id) }
                            )
                        }
                    }
                    .scrollTargetLayout()
                }
                .contentMargins(.horizontal, MFSpace.lg, for: .scrollContent)
                .scrollIndicators(.hidden)
                .scrollTargetBehavior(.viewAligned)
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Trending titles")
    }

    private var trendingSkeleton: some View {
        ScrollView(.horizontal) {
            HStack(alignment: .top, spacing: MFSize.railSpacing) {
                ForEach(0..<3, id: \.self) { _ in
                    PosterSkeleton()
                }
            }
            .padding(.horizontal, MFSpace.lg)
        }
        .scrollDisabled(true)
        .scrollIndicators(.hidden)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Loading trending titles")
    }

    @ViewBuilder
    private var genreSection: some View {
        if !viewModel.genreSuggestions.isEmpty {
            VStack(alignment: .leading, spacing: MFSpace.sm) {
                HStack(alignment: .center) {
                    Text("Browse by Genre")
                        .mfText(.sectionTitle)

                    Spacer(minLength: MFSpace.sm)

                    MFCompactSegmentedControl(
                        options: [MediaType.movie, .tv],
                        title: { $0 == .movie ? "Movies" : "TV" },
                        selection: viewModel.selectedGenreMediaType,
                        onSelect: { mediaType in
                            Task { await viewModel.selectGenreMediaType(mediaType) }
                        }
                    )
                }
                .padding(.horizontal, MFSpace.lg)

                MFFlowLayout(spacing: MFSpace.xs, lineSpacing: MFSpace.xs) {
                    ForEach(viewModel.genreSuggestions) { genre in
                        genreChip(genre)
                    }
                }
                .padding(.horizontal, MFSpace.lg)
            }
            .accessibilityElement(children: .contain)
            .accessibilityLabel("Browse by genre")
        }
    }

    private func genreChip(_ genre: Genre) -> some View {
        Button {
            path.append(
                .genre(
                    mediaType: viewModel.selectedGenreMediaType,
                    genreID: genre.id,
                    name: genre.name
                )
            )
        } label: {
            HStack(spacing: 5) {
                Text(genre.name)
                    .font(MFTypography.label)
                    .lineLimit(1)

                Image(systemName: "chevron.right")
                    .font(.system(size: 9, weight: .bold))
                    .foregroundStyle(MFColor.textTertiary)
            }
            .foregroundStyle(MFColor.textSecondary)
            .padding(.horizontal, MFSpace.sm)
            .frame(minHeight: 36)
            .background {
                Capsule(style: .continuous)
                    .fill(MFColor.surface)
            }
            .overlay {
                Capsule(style: .continuous)
                    .strokeBorder(MFColor.border, lineWidth: 1)
            }
            .contentShape(Capsule(style: .continuous))
        }
        .buttonStyle(.plain)
        .accessibilityLabel(
            "Browse \(genre.name) \(viewModel.selectedGenreMediaType.pluralDisplayName)"
        )
    }

    private var discoverySkeleton: some View {
        VStack(alignment: .leading, spacing: MFSpace.xxl) {
            VStack(alignment: .leading, spacing: MFSpace.sm) {
                HStack {
                    SkeletonBlock(cornerRadius: MFRadius.xs)
                        .frame(width: 120, height: 20)
                    Spacer()
                    SkeletonBlock(cornerRadius: MFRadius.pill)
                        .frame(width: 106, height: 32)
                }
                .padding(.horizontal, MFSpace.lg)

                trendingSkeleton
            }

            VStack(alignment: .leading, spacing: MFSpace.sm) {
                HStack {
                    SkeletonBlock(cornerRadius: MFRadius.xs)
                        .frame(width: 160, height: 20)
                    Spacer()
                    SkeletonBlock(cornerRadius: MFRadius.pill)
                        .frame(width: 116, height: 32)
                }
                .padding(.horizontal, MFSpace.lg)

                HStack(spacing: MFSpace.xs) {
                    ForEach(0..<4, id: \.self) { index in
                        SkeletonBlock(cornerRadius: MFRadius.pill)
                            .frame(width: index.isMultiple(of: 2) ? 74 : 96, height: 36)
                    }
                }
                .padding(.horizontal, MFSpace.lg)
            }

            MediaRailSkeleton()
            MediaRailSkeleton()
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Loading discovery")
    }

    // MARK: Search results

    @ViewBuilder
    private var searchResults: some View {
        if viewModel.results.isEmpty {
            if viewModel.isSearching {
                searchSkeleton
            } else if let error = viewModel.searchError {
                MFErrorStateView(
                    error: error,
                    retry: { Task { await viewModel.retrySearch() } }
                )
                .padding(.top, MFSpace.xl)
            } else if viewModel.hasSearched {
                searchEmptyState
            } else {
                searchSkeleton
            }
        } else {
            resultsHeader
            resultsGrid
            resultsFooter
        }
    }

    private var resultsHeader: some View {
        VStack(alignment: .leading, spacing: 2) {
            Text("Results")
                .mfText(.sectionTitle)

            Text(viewModel.resultSummary)
                .mfText(.caption)
                .foregroundStyle(MFColor.textSecondary)
        }
        .padding(.horizontal, MFSpace.lg)
        .accessibilityElement(children: .combine)
    }

    private var resultsGrid: some View {
        LazyVGrid(
            columns: [
                GridItem(
                    .adaptive(minimum: 104, maximum: 168),
                    spacing: MFSpace.sm,
                    alignment: .top
                )
            ],
            alignment: .leading,
            spacing: MFSpace.lg
        ) {
            ForEach(viewModel.results) { media in
                MediaPosterCard(
                    media: media,
                    imageURL: container.posterURL(for: media),
                    width: nil,
                    action: { open(media.id) }
                )
                .onAppear {
                    guard media.id == viewModel.results.last?.id else { return }
                    Task { await viewModel.loadMoreResults() }
                }
            }
        }
        .padding(.horizontal, MFSpace.lg)
    }

    @ViewBuilder
    private var resultsFooter: some View {
        if viewModel.isSearchingMore {
            ProgressView()
                .controlSize(.small)
                .frame(maxWidth: .infinity)
                .padding(.top, MFSpace.xs)
                .accessibilityLabel("Loading more results")
        } else if let error = viewModel.searchError {
            VStack(spacing: MFSpace.sm) {
                MFNoticeBanner(
                    message: error.message,
                    systemImage: "arrow.clockwise"
                )
                MFButton(title: "Try Again", role: .secondary) {
                    Task { await viewModel.loadMoreResults() }
                }
            }
            .padding(.horizontal, MFSpace.lg)
        } else if viewModel.hasMoreResults {
            MFButton(
                title: "Load More",
                systemImage: "arrow.down",
                role: .secondary,
                isFullWidth: true
            ) {
                Task { await viewModel.loadMoreResults() }
            }
            .padding(.horizontal, MFSpace.lg)
        }
    }

    private var searchEmptyState: some View {
        MFEmptyStateView(
            systemImage: "magnifyingglass",
            title: "No Matches",
            message: "Nothing matched “\(viewModel.activeQuery)” with this filter. Try another spelling or switch between All, Movies, and TV.",
            primaryTitle: "Clear Search",
            primaryAction: { viewModel.clearQuery() }
        )
        .padding(.top, MFSpace.md)
    }

    private var searchSkeleton: some View {
        LazyVGrid(
            columns: [
                GridItem(
                    .adaptive(minimum: 104, maximum: 168),
                    spacing: MFSpace.sm,
                    alignment: .top
                )
            ],
            alignment: .leading,
            spacing: MFSpace.lg
        ) {
            ForEach(0..<9, id: \.self) { _ in
                VStack(alignment: .leading, spacing: MFSpace.xs) {
                    SkeletonBlock(cornerRadius: MFRadius.poster)
                        .aspectRatio(MFSize.posterAspectRatio, contentMode: .fit)

                    SkeletonBlock(cornerRadius: MFRadius.xs)
                        .frame(height: 12)

                    SkeletonBlock(cornerRadius: MFRadius.xs)
                        .frame(width: 58, height: 10)
                }
            }
        }
        .padding(.horizontal, MFSpace.lg)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Searching")
    }

    // MARK: Navigation

    @ViewBuilder
    private func destination(for route: AppRoute) -> some View {
        switch route {
        case let .media(mediaID):
            MediaDetailView(
                container: container,
                mediaID: mediaID,
                onSelectMedia: { next in path.append(.media(next)) }
            )
        case let .genre(mediaType, genreID, name):
            GenreResultsView(
                container: container,
                mediaType: mediaType,
                genreID: genreID,
                name: name,
                onSelectMedia: { next in path.append(.media(next)) }
            )
        case .season, .collection, .reviews:
            EmptyView()
        }
    }

    private func open(_ mediaID: MediaID) {
        isSearchFocused = false
        path.append(.media(mediaID))
    }

    // MARK: Layout

    @Environment(\.horizontalSizeClass) private var horizontalSizeClass
    @ScaledMetric(relativeTo: .body) private var scaledTrendingWidth = MFSize.posterWidth

    private var trendingCardWidth: CGFloat {
        horizontalSizeClass == .regular ? MFSize.posterWidthLarge : scaledTrendingWidth
    }
}
