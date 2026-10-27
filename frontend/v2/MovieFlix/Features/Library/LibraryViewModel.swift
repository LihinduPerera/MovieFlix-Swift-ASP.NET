import Foundation
import Observation

/// Drives the Library tab. Each segment keeps its own paging and error state so
/// one failed feed never hides the rest of a user's library.
@MainActor
@Observable
final class LibraryViewModel {
    enum Segment: String, CaseIterable, Identifiable {
        case favorites
        case watchlist
        case watching
        case collections

        var id: String { rawValue }

        var title: String {
            switch self {
            case .favorites: "Favorites"
            case .watchlist: "Watchlist"
            case .watching: "Watching"
            case .collections: "Collections"
            }
        }

        var systemImage: String {
            switch self {
            case .favorites: "heart.fill"
            case .watchlist: "bookmark.fill"
            case .watching: "play.circle.fill"
            case .collections: "square.stack.fill"
            }
        }

        var emptyTitle: String {
            switch self {
            case .favorites: "No favorites yet"
            case .watchlist: "Your watchlist is empty"
            case .watching: "Nothing in progress"
            case .collections: "No collections yet"
            }
        }

        var emptyMessage: String {
            switch self {
            case .favorites:
                "Tap the heart on any title to keep it here."
            case .watchlist:
                "Save movies and shows you want to watch next."
            case .watching:
                "Start an episode or a movie and it will show up here."
            case .collections:
                "Group titles into lists for movie nights, marathons, or moods."
            }
        }
    }

    struct SegmentState<Item: Identifiable & Hashable> {
        var items: [Item] = []
        var page = 0
        var totalCount = 0
        var hasMore = false
        var isLoading = false
        var isLoadingMore = false
        var error: APIError?
        var hasLoaded = false

        var isEmpty: Bool { hasLoaded && items.isEmpty && error == nil }
    }

    enum Phase: Equatable {
        case idle
        case loading
        case loaded
        case failed(APIError)
    }

    /// How long a cached segment is considered fresh enough to skip a reload
    /// when the tab is revisited.
    private static let freshnessWindow: TimeInterval = 30

    private let repository: any LibraryRepositoryProtocol

    private(set) var phase: Phase = .idle
    private(set) var favorites = SegmentState<SavedMedia>()
    private(set) var watchlist = SegmentState<SavedMedia>()
    private(set) var watching = SegmentState<WatchHistoryItem>()
    private(set) var collections = SegmentState<CollectionSummary>()
    private(set) var lastLoadedAt: Date?

    var segment: Segment = .favorites

    init(repository: any LibraryRepositoryProtocol) {
        self.repository = repository
    }

    var currentError: APIError? {
        switch segment {
        case .favorites: favorites.error
        case .watchlist: watchlist.error
        case .watching: watching.error
        case .collections: collections.error
        }
    }

    var isCurrentEmpty: Bool {
        switch segment {
        case .favorites: favorites.isEmpty
        case .watchlist: watchlist.isEmpty
        case .watching: watching.isEmpty
        case .collections: collections.isEmpty
        }
    }

    var isCurrentLoading: Bool {
        switch segment {
        case .favorites: favorites.isLoading
        case .watchlist: watchlist.isLoading
        case .watching: watching.isLoading
        case .collections: collections.isLoading
        }
    }

    var favoriteIDs: Set<MediaID> {
        Set(favorites.items.map(\.id))
    }

    var totalSaved: Int {
        favorites.totalCount + watchlist.totalCount
    }

    // MARK: Loading

    /// Loads every segment the first time the tab is shown and refreshes all of
    /// them when `force` is set.
    func load(force: Bool = false) async {
        if !force && hasLoadedSegments && lastLoadedAt != nil {
            if Date().timeIntervalSince(lastLoadedAt!) < Self.freshnessWindow {
                return
            }
        }
        if !force && hasLoadedSegments {
            // allow refresh if forced
        }

        phase = .loading
        await withTaskGroup(of: Void.self) { group in
            group.addTask { await self.loadFavorites(reset: true) }
            group.addTask { await self.loadWatchlist(reset: true) }
            group.addTask { await self.loadWatching() }
            group.addTask { await self.loadCollections(reset: true) }
        }

        lastLoadedAt = Date()
        phase = hasLoadedSegments || favorites.hasLoaded || watchlist.hasLoaded || watching.hasLoaded || collections.hasLoaded
            ? .loaded
            : .failed(firstError ?? .unknown)
    }

    func refreshIfStale() async {
        guard let lastLoadedAt else {
            await load()
            return
        }
        guard Date().timeIntervalSince(lastLoadedAt) > Self.freshnessWindow else { return }
        await load(force: true)
    }

    func refresh() async {
        // Allow refresh even if tasks might be in flight; ensure we get fresh data
        let wasLoading = favorites.isLoading || watchlist.isLoading || watching.isLoading || collections.isLoading
        if wasLoading {
            // Give a moment and still force refresh
        }
        await load(force: true)
    }

    func retryCurrentSegment() async {
        switch segment {
        case .favorites: await loadFavorites(reset: true)
        case .watchlist: await loadWatchlist(reset: true)
        case .watching: await loadWatching()
        case .collections: await loadCollections(reset: true)
        }
    }

    private var hasLoadedSegments: Bool {
        favorites.hasLoaded
            || watchlist.hasLoaded
            || watching.hasLoaded
            || collections.hasLoaded
    }

    private var firstError: APIError? {
        favorites.error ?? watchlist.error ?? watching.error ?? collections.error
    }

    // MARK: Favorites

    func loadFavorites(reset: Bool) async {
        guard !favorites.isLoading else { return }
        if !reset, !favorites.hasMore { return }

        favorites.isLoading = true
        favorites.error = nil
        defer { favorites.isLoading = false }

        do {
            let page = try await repository.favorites(
                mediaType: nil,
                page: reset ? 1 : favorites.page + 1,
                pageSize: 30,
                sort: .addedDesc
            )
            favorites.items = reset ? page.items : merging(favorites.items, with: page.items)
            favorites.page = page.page
            favorites.totalCount = page.totalCount
            favorites.hasMore = page.hasMore
            favorites.hasLoaded = true
        } catch let error as APIError {
            favorites.error = error
            if reset { favorites.items = [] }
        } catch {
            favorites.error = .transport(error.localizedDescription)
        }
    }

    // MARK: Watchlist

    func loadWatchlist(reset: Bool) async {
        guard !watchlist.isLoading else { return }
        if !reset, !watchlist.hasMore { return }

        watchlist.isLoading = true
        watchlist.error = nil
        defer { watchlist.isLoading = false }

        do {
            let page = try await repository.watchlist(
                mediaType: nil,
                page: reset ? 1 : watchlist.page + 1,
                pageSize: 30,
                sort: .addedDesc
            )
            watchlist.items = reset ? page.items : merging(watchlist.items, with: page.items)
            watchlist.page = page.page
            watchlist.totalCount = page.totalCount
            watchlist.hasMore = page.hasMore
            watchlist.hasLoaded = true
        } catch let error as APIError {
            watchlist.error = error
            if reset { watchlist.items = [] }
        } catch {
            watchlist.error = .transport(error.localizedDescription)
        }
    }

    // MARK: Continue watching

    func loadWatching() async {
        guard !watching.isLoading else { return }

        watching.isLoading = true
        watching.error = nil
        defer { watching.isLoading = false }

        do {
            let items = try await repository.continueWatching(limit: 30)
            watching.items = items
            watching.page = 1
            watching.totalCount = items.count
            watching.hasMore = false
            watching.hasLoaded = true
        } catch let error as APIError {
            watching.error = error
            watching.items = []
        } catch {
            watching.error = .transport(error.localizedDescription)
        }
    }

    // MARK: Collections

    func loadCollections(reset: Bool) async {
        guard !collections.isLoading else { return }
        if !reset, !collections.hasMore { return }

        collections.isLoading = true
        collections.error = nil
        defer { collections.isLoading = false }

        do {
            let page = try await repository.collections(
                page: reset ? 1 : collections.page + 1,
                pageSize: 30
            )
            guard !Task.isCancelled else { return }
            collections.items = reset
                ? page.items
                : merging(collections.items, with: page.items)
            collections.page = page.page
            collections.totalCount = page.totalCount
            collections.hasMore = page.hasMore
            collections.hasLoaded = true
        } catch let error as APIError {
            collections.error = error
            if reset { collections.items = [] }
        } catch {
            collections.error = .transport(error.localizedDescription)
        }
    }

    /// Creates a collection and drops it at the top of the grid. Returns the
    /// API error so the editor sheet can keep the form open on failure.
    @discardableResult
    func createCollection(name: String, description: String) async -> Result<CollectionSummary, APIError> {
        let name = name.trimmingCharacters(in: .whitespacesAndNewlines)
        let description = description.trimmingCharacters(in: .whitespacesAndNewlines)

        do {
            let created = try await repository.createCollection(
                name: name,
                description: description
            )
            collections.items.insert(created, at: 0)
            collections.totalCount += 1
            collections.hasLoaded = true
            return .success(created)
        } catch {
            return .failure(Self.apiError(from: error))
        }
    }

    /// Removes a collection locally after the detail screen deletes it.
    func removeCollection(id: Int) {
        collections.items.removeAll { $0.id == id }
        collections.totalCount = max(0, collections.totalCount - 1)
    }

    @discardableResult
    func deleteCollection(id: Int) async -> APIError? {
        do {
            try await repository.deleteCollection(id: id)
            removeCollection(id: id)
            return nil
        } catch {
            return Self.apiError(from: error)
        }
    }

    // MARK: Removal

    @discardableResult
    func removeFavoriteItem(_ id: MediaID) async -> APIError? {
        do {
            try await repository.removeFavorite(id)
            removeFavorite(id)
            return nil
        } catch {
            return Self.apiError(from: error)
        }
    }

    @discardableResult
    func removeWatchlistItem(_ id: MediaID) async -> APIError? {
        do {
            try await repository.removeFromWatchlist(id)
            removeFromWatchlist(id)
            return nil
        } catch {
            return Self.apiError(from: error)
        }
    }

    @discardableResult
    func removeProgressItem(_ id: MediaID) async -> APIError? {
        do {
            try await repository.clearTitleHistory(id)
            watching.items.removeAll { $0.id == id }
            watching.totalCount = watching.items.count
            return nil
        } catch {
            return Self.apiError(from: error)
        }
    }

    /// Removes an item locally after a successful write from another screen so
    /// the grid updates without a full round trip.
    func removeFavorite(_ id: MediaID) {
        favorites.items.removeAll { $0.id == id }
        favorites.totalCount = max(0, favorites.totalCount - 1)
    }

    func removeFromWatchlist(_ id: MediaID) {
        watchlist.items.removeAll { $0.id == id }
        watchlist.totalCount = max(0, watchlist.totalCount - 1)
    }

    func reset() {
        phase = .idle
        favorites = SegmentState()
        watchlist = SegmentState()
        watching = SegmentState()
        collections = SegmentState()
        lastLoadedAt = nil
    }

    // MARK: Helpers

    private nonisolated static func apiError(from error: Error) -> APIError {
        if let error = error as? APIError {
            return error
        }
        return .transport(error.localizedDescription)
    }

    private func merging<Item: Identifiable & Hashable>(
        _ existing: [Item],
        with page: [Item]
    ) -> [Item] {
        let identifiers = Set(existing.map(\.id))
        return existing + page.filter { !identifiers.contains($0.id) }
    }
}

private extension APIError {
    static var unknown: APIError {
        .transport("Something went wrong while loading your library.")
    }
}
