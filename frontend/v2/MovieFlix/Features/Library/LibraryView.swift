import SwiftUI

struct LibraryView: View {
    let container: AppContainer

    @State private var viewModel: LibraryViewModel
    @State private var path: [AppRoute] = []
    @State private var showingAuth = false
    @State private var showingCreateCollection = false
    @State private var showingDeleteCollection = false
    @State private var collectionToDelete: CollectionSummary?
    @State private var pendingCollectionToast: String?

    init(container: AppContainer) {
        self.container = container
        _viewModel = State(
            initialValue: LibraryViewModel(repository: container.libraryRepository)
        )
    }

    private var session: SessionStore { container.session }

    var body: some View {
        NavigationStack(path: $path) {
            ScrollView {
                LazyVStack(alignment: .leading, spacing: MFSpace.xl) {
                    header

                    switch session.phase {
                    case .restoring:
                        loadingCard
                    case .signedOut:
                        signedOutContent
                    case .signedIn:
                        libraryContent
                    }
                }
                .padding(.bottom, MFSpace.xxxl)
            }
            .scrollIndicators(.hidden)
            .background(MFColor.canvas)
            .refreshable {
                await viewModel.refresh()
            }
            .toolbar(.hidden, for: .navigationBar)
            .task(id: session.isSignedIn) {
                guard session.isSignedIn else {
                    viewModel.reset()
                    return
                }
                await viewModel.load()
            }
            .onAppear {
                guard session.isSignedIn else { return }
                Task { await viewModel.refreshIfStale() }
            }
            .onChange(of: path) { _, newPath in
                guard newPath.isEmpty, session.isSignedIn else { return }
                Task { await viewModel.refresh() }
            }
            .navigationDestination(for: AppRoute.self) { route in
                destination(for: route)
            }
        }
        .sheet(isPresented: $showingAuth) {
            AuthFlowView(container: container)
        }
        .alert(
            "Delete Collection?",
            isPresented: $showingDeleteCollection,
            presenting: collectionToDelete
        ) { collection in
            Button("Delete", role: .destructive) {
                Task { await deleteCollection(collection) }
            }
            Button("Cancel", role: .cancel) {
                collectionToDelete = nil
            }
        } message: { collection in
            Text("“\(collection.name)” will be removed. Its titles stay in your library.")
        }
    }

    // MARK: Header

    private var header: some View {
        HStack(alignment: .center, spacing: MFSpace.md) {
            VStack(alignment: .leading, spacing: 2) {
                Text("Library")
                    .mfText(.title)
                    .accessibilityAddTraits(.isHeader)

                Text(subtitle)
                    .mfText(.caption)
                    .foregroundStyle(MFColor.textTertiary)
            }

            Spacer(minLength: 0)

            if session.isSignedIn {
                if viewModel.segment == .collections {
                    Button {
                        showingCreateCollection = true
                    } label: {
                        Image(systemName: "plus")
                            .font(.system(size: 15, weight: .bold))
                            .frame(width: 40, height: 40)
                            .background(Circle().fill(MFColor.surface))
                            .overlay(Circle().strokeBorder(MFColor.border, lineWidth: 1))
                            .contentShape(Circle())
                    }
                    .buttonStyle(.plain)
                    .accessibilityLabel("New collection")
                }

                Button {
                    Task { await viewModel.refresh() }
                } label: {
                    Image(systemName: "arrow.clockwise")
                        .font(.system(size: 15, weight: .semibold))
                        .frame(width: 40, height: 40)
                        .background(Circle().fill(MFColor.surface))
                        .overlay(Circle().strokeBorder(MFColor.border, lineWidth: 1))
                        .contentShape(Circle())
                }
                .buttonStyle(.plain)
                .disabled(viewModel.isCurrentLoading)
                .accessibilityLabel("Refresh library")
            }
        }
        .padding(.horizontal, MFSpace.lg)
        .padding(.top, MFSpace.xs)
        .sheet(
            isPresented: $showingCreateCollection,
            onDismiss: {
                if let pendingCollectionToast {
                    container.toast.success(pendingCollectionToast)
                    self.pendingCollectionToast = nil
                }
            }
        ) {
            CollectionEditorSheet(mode: .create) { name, description in
                switch await viewModel.createCollection(name: name, description: description) {
                case let .success(collection):
                    pendingCollectionToast = "Created “\(collection.name)”."
                    return nil
                case let .failure(error):
                    return error
                }
            }
        }
    }

    private var subtitle: String {
        guard session.isSignedIn else { return "Sign in to sync your library" }
        switch viewModel.phase {
        case .idle, .loading:
            return "Loading your library…"
        case .failed:
            return "Couldn't load everything"
        case .loaded:
            let saved = viewModel.totalSaved
            if saved == 0 {
                return "Nothing saved yet"
            }
            return saved == 1 ? "1 title saved" : "\(saved) titles saved"
        }
    }

    // MARK: Signed out

    private var signedOutContent: some View {
        MFEmptyStateView(
            systemImage: "rectangle.stack.badge.plus",
            title: "Your library lives here",
            message: "Sign in to keep favorites, watchlists, ratings, and episode progress in sync across every device.",
            primaryTitle: "Sign In",
            primaryAction: { showingAuth = true },
            secondaryTitle: "Browse Home",
            secondaryAction: { container.navigation.selectedTab = .home }
        )
        .padding(.top, MFSpace.huge)
    }

    private var loadingCard: some View {
        HStack(spacing: MFSpace.sm) {
            ProgressView()
                .controlSize(.small)

            Text("Restoring your session…")
                .mfText(.callout)
                .foregroundStyle(MFColor.textSecondary)
        }
        .padding(MFSpace.lg)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background {
            RoundedRectangle(cornerRadius: MFRadius.lg, style: .continuous)
                .fill(MFColor.surface)
        }
        .padding(.horizontal, MFSpace.lg)
        .accessibilityElement(children: .combine)
    }

    // MARK: Library

    @ViewBuilder
    private var libraryContent: some View {
        segmentPicker

        if let error = viewModel.currentError, !hasItems {
            MFErrorStateView(
                error: error,
                retry: { Task { await viewModel.retryCurrentSegment() } },
                signIn: { container.navigation.selectedTab = .profile }
            )
            .padding(.top, MFSpace.lg)
        } else if viewModel.isCurrentLoading, !hasItems {
            if viewModel.segment == .collections {
                CollectionGridSkeleton()
                    .padding(.horizontal, MFSpace.lg)
            } else {
                LibraryGridSkeleton()
                    .padding(.horizontal, MFSpace.lg)
            }
        } else if viewModel.isCurrentEmpty {
            emptyState
                .padding(.top, MFSpace.lg)
        } else {
            if viewModel.segment == .collections {
                collectionsGrid
            } else {
                posterGrid
            }

            if isLoadingMore {
                ProgressView()
                    .controlSize(.small)
                    .frame(maxWidth: .infinity)
                    .padding(.top, MFSpace.sm)
                    .accessibilityLabel("Loading more")
            }
        }
    }

    private var hasItems: Bool {
        switch viewModel.segment {
        case .favorites: !viewModel.favorites.items.isEmpty
        case .watchlist: !viewModel.watchlist.items.isEmpty
        case .watching: !viewModel.watching.items.isEmpty
        case .collections: !viewModel.collections.items.isEmpty
        }
    }

    private var segmentPicker: some View {
        ScrollView(.horizontal) {
            HStack(spacing: 6) {
                ForEach(LibraryViewModel.Segment.allCases) { segment in
                    let selected = viewModel.segment == segment
                    Button {
                        withAnimation(.snappy(duration: 0.22)) {
                            viewModel.segment = segment
                        }
                    } label: {
                        HStack(spacing: 4) {
                            Image(systemName: segment.systemImage)
                                .font(.system(size: 11, weight: .semibold))

                            Text(segment.title)
                                .font(MFTypography.label)
                                .lineLimit(1)

                            if let count = count(for: segment), count > 0 {
                                Text("\(count)")
                                    .font(.system(size: 10, weight: .bold).monospacedDigit())
                                    .padding(.horizontal, 5)
                                    .padding(.vertical, 1)
                                    .background {
                                        Capsule(style: .continuous)
                                            .fill(selected ? Color.white.opacity(0.20) : MFColor.surfaceElevated)
                                    }
                            }
                        }
                        .fixedSize(horizontal: true, vertical: false)
                        .foregroundStyle(selected ? Color.white : MFColor.textSecondary)
                        .padding(.horizontal, 10)
                        .frame(height: 36)
                        .background {
                            if selected {
                                Capsule(style: .continuous)
                                    .fill(MFColor.primaryButtonGradient)
                            } else {
                                Capsule(style: .continuous)
                                    .fill(MFColor.surface)
                            }
                        }
                        .overlay {
                            Capsule(style: .continuous)
                                .strokeBorder(
                                    selected ? Color.clear : MFColor.border,
                                    lineWidth: 1
                                )
                        }
                        .contentShape(Capsule())
                    }
                    .buttonStyle(.plain)
                    .accessibilityAddTraits(selected ? .isSelected : [])
                }
            }
            .padding(.vertical, 2)
        }
        .scrollIndicators(.hidden)
        .contentMargins(.horizontal, MFSpace.lg, for: .scrollContent)
    }

    private func count(for segment: LibraryViewModel.Segment) -> Int? {
        switch segment {
        case .favorites: viewModel.favorites.totalCount
        case .watchlist: viewModel.watchlist.totalCount
        case .watching: viewModel.watching.items.count
        case .collections: viewModel.collections.totalCount
        }
    }

    private var posterGrid: some View {
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
            switch viewModel.segment {
            case .favorites:
                ForEach(viewModel.favorites.items) { item in
                    savedCard(item, isFavorite: true)
                        .onAppear {
                            guard item.id == viewModel.favorites.items.last?.id else { return }
                            Task { await viewModel.loadFavorites(reset: false) }
                        }
                        .contextMenu {
                            Button(role: .destructive) {
                                Task { await removeFavorite(item.id) }
                            } label: {
                                Label("Remove from Favorites", systemImage: "heart.slash")
                            }
                        }
                }
            case .watchlist:
                ForEach(viewModel.watchlist.items) { item in
                    savedCard(item, isFavorite: viewModel.favoriteIDs.contains(item.id))
                        .onAppear {
                            guard item.id == viewModel.watchlist.items.last?.id else { return }
                            Task { await viewModel.loadWatchlist(reset: false) }
                        }
                        .contextMenu {
                            Button(role: .destructive) {
                                Task { await removeWatchlist(item.id) }
                            } label: {
                                Label("Remove from Watchlist", systemImage: "bookmark.slash")
                            }
                        }
                }
            case .watching:
                ForEach(viewModel.watching.items) { item in
                    MediaPosterCard(
                        media: item.media,
                        imageURL: container.posterURL(for: item.media),
                        width: nil,
                        badgeText: badge(for: item),
                        action: { path.append(.media(item.id)) }
                    )
                    .contextMenu {
                        Button(role: .destructive) {
                            Task { await removeProgress(item.id) }
                        } label: {
                            Label("Remove from Watching", systemImage: "xmark.circle")
                        }
                    }
                }
            case .collections:
                EmptyView()
            }
        }
        .padding(.horizontal, MFSpace.lg)
    }

    private var collectionsGrid: some View {
        LazyVGrid(
            columns: [
                GridItem(
                    .adaptive(minimum: 148, maximum: 260),
                    spacing: MFSpace.md,
                    alignment: .top
                )
            ],
            alignment: .leading,
            spacing: MFSpace.lg
        ) {
            ForEach(viewModel.collections.items) { collection in
                CollectionCard(
                    collection: collection,
                    imageURLs: container.images.urls(
                        for: collection.coverPosterPaths,
                        size: .poster
                    )
                ) {
                    path.append(.collection(id: collection.id))
                }
                .onAppear {
                    guard collection.id == viewModel.collections.items.last?.id else { return }
                    Task { await viewModel.loadCollections(reset: false) }
                }
                .contextMenu {
                    Button(role: .destructive) {
                        collectionToDelete = collection
                        showingDeleteCollection = true
                    } label: {
                        Label("Delete Collection", systemImage: "trash")
                    }
                }
            }
        }
        .padding(.horizontal, MFSpace.lg)
    }

    private func savedCard(_ item: SavedMedia, isFavorite: Bool) -> some View {
        MediaPosterCard(
            media: item.media,
            imageURL: container.posterURL(for: item.media),
            width: nil,
            isFavorite: isFavorite,
            action: { path.append(.media(item.id)) }
        )
    }

    private var isLoadingMore: Bool {
        switch viewModel.segment {
        case .favorites: viewModel.favorites.isLoading && !viewModel.favorites.items.isEmpty
        case .watchlist: viewModel.watchlist.isLoading && !viewModel.watchlist.items.isEmpty
        case .watching: false
        case .collections: viewModel.collections.isLoading && !viewModel.collections.items.isEmpty
        }
    }

    private var emptyState: some View {
        Group {
            if viewModel.segment == .collections {
                MFEmptyStateView(
                    systemImage: viewModel.segment.systemImage,
                    title: viewModel.segment.emptyTitle,
                    message: viewModel.segment.emptyMessage,
                    primaryTitle: "New Collection",
                    primaryAction: { showingCreateCollection = true },
                    secondaryTitle: "Browse Home",
                    secondaryAction: { container.navigation.selectedTab = .home }
                )
            } else {
                MFEmptyStateView(
                    systemImage: viewModel.segment.systemImage,
                    title: viewModel.segment.emptyTitle,
                    message: viewModel.segment.emptyMessage,
                    primaryTitle: "Browse Home",
                    primaryAction: { container.navigation.selectedTab = .home }
                )
            }
        }
    }

    private func badge(for item: WatchHistoryItem) -> String? {
        if let next = item.nextEpisode {
            return "Up next \(next.code)"
        }
        if let last = item.lastEpisode {
            return last.code
        }
        if item.progressSeconds > 0, !item.completed {
            return "Resume"
        }
        return item.completed ? "Watched" : nil
    }

    // MARK: Actions

    private func removeFavorite(_ id: MediaID) async {
        if let error = await viewModel.removeFavoriteItem(id) {
            container.toast.error(error.message)
        } else {
            container.toast.success("Removed from favorites.")
        }
    }

    private func removeWatchlist(_ id: MediaID) async {
        if let error = await viewModel.removeWatchlistItem(id) {
            container.toast.error(error.message)
        } else {
            container.toast.success("Removed from watchlist.")
        }
    }

    private func removeProgress(_ id: MediaID) async {
        if let error = await viewModel.removeProgressItem(id) {
            container.toast.error(error.message)
        } else {
            container.toast.success("Removed from continue watching.")
        }
    }

    private func deleteCollection(_ collection: CollectionSummary) async {
        if let error = await viewModel.deleteCollection(id: collection.id) {
            container.toast.error(error.message)
        } else {
            container.toast.success("Collection deleted.")
        }
        collectionToDelete = nil
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
        case let .collection(id):
            CollectionDetailView(
                container: container,
                collectionID: id,
                onSelectMedia: { next in path.append(.media(next)) },
                onDeleted: { viewModel.removeCollection(id: id) }
            )
        case .season, .reviews, .genre:
            EmptyView()
        }
    }
}

// MARK: - Skeleton

private struct LibraryGridSkeleton: View {
    private let columns = [
        GridItem(.adaptive(minimum: 104, maximum: 168), spacing: MFSpace.sm, alignment: .top)
    ]

    var body: some View {
        LazyVGrid(columns: columns, alignment: .leading, spacing: MFSpace.lg) {
            ForEach(0..<9, id: \.self) { _ in
                VStack(alignment: .leading, spacing: MFSpace.xs) {
                    SkeletonBlock()
                        .aspectRatio(MFSize.posterAspectRatio, contentMode: .fit)
                        .clipShape(RoundedRectangle(cornerRadius: MFRadius.poster, style: .continuous))

                    SkeletonBlock()
                        .frame(height: 10)

                    SkeletonBlock()
                        .frame(width: 56, height: 8)
                }
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Loading library")
    }
}

private struct CollectionGridSkeleton: View {
    private let columns = [
        GridItem(.adaptive(minimum: 148, maximum: 260), spacing: MFSpace.md, alignment: .top)
    ]

    var body: some View {
        LazyVGrid(columns: columns, alignment: .leading, spacing: MFSpace.lg) {
            ForEach(0..<4, id: \.self) { _ in
                VStack(alignment: .leading, spacing: MFSpace.xs) {
                    SkeletonBlock(cornerRadius: MFRadius.poster)
                        .aspectRatio(3.0 / 2.0, contentMode: .fit)

                    SkeletonBlock()
                        .frame(height: 11)

                    SkeletonBlock()
                        .frame(width: 80, height: 8)
                }
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Loading collections")
    }
}

#Preview {
    let configuration = AppConfiguration(
        apiBaseURL: URL(string: "https://10.35.255.69:7014")!,
        imageBaseURL: URL(string: "https://image.tmdb.org/t/p")!,
        environment: .preview,
        appVersion: "1.0",
        buildNumber: "1",
        userAgent: "MovieFlix Preview"
    )
    LibraryView(container: AppContainer(configuration: configuration))
        .environment(\.imagePipeline, ImagePipeline.preview)
}
