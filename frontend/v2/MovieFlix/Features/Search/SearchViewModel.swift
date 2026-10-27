import Foundation
import Observation

// MARK: - Discovery shelves

nonisolated enum SearchShelfID: String, CaseIterable, Sendable, Identifiable {
    case trending
    case popularMovies
    case popularShows
    case nowPlaying
    case airingToday

    var id: String { rawValue }

    var title: String {
        switch self {
        case .trending: "Trending"
        case .popularMovies: "Popular Movies"
        case .popularShows: "Popular TV"
        case .nowPlaying: "In Theatres"
        case .airingToday: "Airing Today"
        }
    }
}

nonisolated struct SearchShelf: Identifiable, Sendable {
    let id: SearchShelfID
    let items: [MediaSummary]
}

nonisolated private struct ShelfResult: Sendable {
    let id: SearchShelfID
    let result: Result<[MediaSummary], APIError>
}

// MARK: - View model

@MainActor
@Observable
final class SearchViewModel {
    enum DiscoveryPhase: Equatable {
        case idle
        case loading
        case loaded
        case failed(APIError)
    }

    private let repository: any MediaRepositoryProtocol
    private let historyRepository: any SearchHistoryRepositoryProtocol
    private let debounce: Duration

    private(set) var discoveryPhase: DiscoveryPhase = .idle
    private(set) var shelves: [SearchShelf] = []
    private(set) var genresByMediaType: [MediaType: [Genre]] = [:]
    private(set) var selectedGenreMediaType: MediaType = .movie
    private(set) var partialErrorMessage: String?
    private(set) var isLoadingDiscovery = false
    private(set) var isLoadingTrending = false

    /// Bound to the text field. Changes are debounced through `queryChanged()`.
    var query = ""
    private(set) var searchType: SearchType = .multi
    private(set) var trendingWindow: TrendingWindow = .week

    private(set) var results: [MediaSummary] = []
    private(set) var activeQuery = ""
    private(set) var totalResults = 0
    private(set) var hasSearched = false
    private(set) var isSearching = false
    private(set) var isSearchingMore = false
    private(set) var searchError: APIError?
    private(set) var hasMoreResults = false

    private(set) var recentSearches: [SearchHistoryEntry] = []
    private(set) var isLoadingRecentSearches = false

    private var resultsPage = 0
    private var searchGeneration = 0
    private var searchTask: Task<Void, Never>?

    init(
        repository: any MediaRepositoryProtocol,
        historyRepository: any SearchHistoryRepositoryProtocol,
        debounce: Duration = .milliseconds(350)
    ) {
        self.repository = repository
        self.historyRepository = historyRepository
        self.debounce = debounce
    }

    // MARK: Derived state

    var trendingItems: [MediaSummary] {
        shelf(.trending)?.items ?? []
    }

    var genreSuggestions: [Genre] {
        genresByMediaType[selectedGenreMediaType] ?? []
    }

    var trimmedQuery: String {
        query.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    /// Search mode starts as soon as the field has text, so the discovery page
    /// does not flash back in while the debounce is pending.
    var isSearchMode: Bool {
        !trimmedQuery.isEmpty
    }

    var discoveryShelves: [SearchShelf] {
        shelves.filter { $0.id != .trending }
    }

    var resultSummary: String {
        let count = totalResults
        let noun = count == 1 ? "title" : "titles"
        if activeQuery.isEmpty {
            return "\(count) \(noun)"
        }
        return "\(count) \(noun) for “\(activeQuery)”"
    }

    func shelf(_ id: SearchShelfID) -> SearchShelf? {
        shelves.first { $0.id == id }
    }

    // MARK: Discovery

    func loadDiscovery(force: Bool = false) async {
        guard !isLoadingDiscovery else { return }
        if !force, discoveryPhase == .loaded { return }

        if shelves.isEmpty {
            discoveryPhase = .loading
        }
        isLoadingDiscovery = true
        partialErrorMessage = nil

        let repository = self.repository
        let filter = MediaTypeFilter(rawValue: searchType.rawValue) ?? .all
        let window = trendingWindow

        async let shelvesTask = Self.fetchShelves(
            repository: repository,
            filter: filter,
            window: window
        )
        async let genresTask = Self.fetchGenres(repository: repository)

        let (shelfResults, genres) = await (shelvesTask, genresTask)
        isLoadingDiscovery = false

        guard !Task.isCancelled else { return }
        guard filter == (MediaTypeFilter(rawValue: searchType.rawValue) ?? .all),
              window == trendingWindow
        else {
            return
        }

        var loaded: [SearchShelf] = []
        var failures = 0
        var firstError: APIError?

        for result in shelfResults {
            switch result.result {
            case let .success(items):
                if !items.isEmpty {
                    loaded.append(SearchShelf(id: result.id, items: items))
                }
            case let .failure(error):
                if error != .cancelled {
                    failures += 1
                    if firstError == nil {
                        firstError = error
                    }
                }
            }
        }

        loaded.sort { lhs, rhs in
            (Self.shelfOrder.firstIndex(of: lhs.id) ?? 0)
                < (Self.shelfOrder.firstIndex(of: rhs.id) ?? 0)
        }

        shelves = loaded
        for (mediaType, values) in genres {
            genresByMediaType[mediaType] = values
        }

        discoveryPhase = loaded.isEmpty ? .failed(firstError ?? .invalidResponse) : .loaded

        if failures > 0, !loaded.isEmpty {
            partialErrorMessage = "Some discovery shelves couldn't load. Pull to refresh."
        }

        if !loaded.isEmpty,
           !genres.keys.contains(.movie),
           !genres.keys.contains(.tv) {
            partialErrorMessage = "Genres are unavailable right now."
        }
    }

    /// Trending is the only shelf that reacts to the type and window filters;
    /// reloading it alone keeps the rest of the page stable.
    func reloadTrending() async {
        guard !isLoadingTrending else { return }

        let filter = MediaTypeFilter(rawValue: searchType.rawValue) ?? .all
        let window = trendingWindow
        isLoadingTrending = true
        defer { isLoadingTrending = false }

        do {
            let response = try await repository.trending(
                mediaType: filter,
                window: window,
                page: 1,
                language: nil
            )
            guard !Task.isCancelled,
                  filter == (MediaTypeFilter(rawValue: searchType.rawValue) ?? .all),
                  window == trendingWindow
            else {
                return
            }
            apply(response.items, to: .trending)
        } catch let error as APIError where error != .cancelled {
            if shelf(.trending) == nil {
                partialErrorMessage = "Trending is unavailable right now."
            }
        } catch {
            return
        }
    }

    func selectSearchType(_ type: SearchType) async {
        guard type != searchType else { return }
        searchType = type

        // Use the trimmed query rather than `activeQuery`: a type change can
        // land while the debounced first search is still pending.
        let pendingQuery = trimmedQuery
        if !pendingQuery.isEmpty {
            searchTask?.cancel()
            searchGeneration += 1
            _ = await performSearch(
                query: pendingQuery,
                page: 1,
                generation: searchGeneration,
                recordHistory: false
            )
        }

        if discoveryPhase == .loaded {
            await reloadTrending()
        }
    }

    func selectTrendingWindow(_ window: TrendingWindow) async {
        guard window != trendingWindow else { return }
        trendingWindow = window
        if discoveryPhase == .loaded {
            await reloadTrending()
        }
    }

    func selectGenreMediaType(_ mediaType: MediaType) async {
        selectedGenreMediaType = mediaType
        guard genresByMediaType[mediaType] == nil else { return }

        if let genres = try? await repository.genres(mediaType: mediaType, language: nil) {
            guard !Task.isCancelled else { return }
            genresByMediaType[mediaType] = genres
        }
    }

    func dismissPartialError() {
        partialErrorMessage = nil
    }

    // MARK: Search

    /// Called from the text field's change handler. Debounces keystrokes and
    /// invalidates any older in-flight search.
    func queryChanged() {
        searchGeneration += 1
        let generation = searchGeneration
        searchTask?.cancel()

        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            resetSearchResults()
            return
        }

        isSearching = true
        searchError = nil

        searchTask = Task { [weak self, debounce] in
            try? await Task.sleep(for: debounce)
            guard !Task.isCancelled, let self else { return }
            _ = await self.performSearch(
                query: trimmed,
                page: 1,
                generation: generation,
                recordHistory: false
            )
        }
    }

    @discardableResult
    func submit(recordHistory: Bool) async -> Bool {
        searchTask?.cancel()
        searchGeneration += 1
        let generation = searchGeneration

        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            resetSearchResults()
            return false
        }

        return await performSearch(
            query: trimmed,
            page: 1,
            generation: generation,
            recordHistory: recordHistory
        )
    }

    func loadMoreResults() async {
        guard hasSearched,
              hasMoreResults,
              !isSearching,
              !isSearchingMore,
              !activeQuery.isEmpty
        else {
            return
        }

        searchTask?.cancel()
        searchGeneration += 1
        _ = await performSearch(
            query: activeQuery,
            page: resultsPage + 1,
            generation: searchGeneration,
            recordHistory: false
        )
    }

    func retrySearch() async {
        guard !activeQuery.isEmpty else { return }
        searchTask?.cancel()
        searchGeneration += 1
        _ = await performSearch(
            query: activeQuery,
            page: 1,
            generation: searchGeneration,
            recordHistory: false
        )
    }

    func clearQuery() {
        searchTask?.cancel()
        searchGeneration += 1
        query = ""
        resetSearchResults()
    }

    // MARK: Recent searches

    func loadRecentSearches() async {
        isLoadingRecentSearches = true
        defer { isLoadingRecentSearches = false }

        guard let entries = try? await historyRepository.history(limit: 8),
              !Task.isCancelled
        else {
            return
        }
        recentSearches = entries
    }

    func removeRecentSearch(_ entry: SearchHistoryEntry) async -> APIError? {
        guard let index = recentSearches.firstIndex(where: { $0.id == entry.id }) else {
            return nil
        }

        recentSearches.remove(at: index)

        do {
            try await historyRepository.delete(id: entry.id)
            return nil
        } catch {
            recentSearches.insert(entry, at: min(index, recentSearches.count))
            return Self.apiError(from: error)
        }
    }

    func clearRecentSearches() async -> APIError? {
        guard !recentSearches.isEmpty else { return nil }
        let previous = recentSearches
        recentSearches = []

        do {
            try await historyRepository.clear()
            return nil
        } catch {
            recentSearches = previous
            return Self.apiError(from: error)
        }
    }

    /// Called when the session signs out: recent searches are user-owned and
    /// must not linger on screen.
    func clearLocalRecentSearches() {
        recentSearches = []
    }

    // MARK: Private

    private func performSearch(
        query: String,
        page: Int,
        generation: Int,
        recordHistory: Bool
    ) async -> Bool {
        guard generation == searchGeneration else { return false }

        let type = searchType
        let isFirstPage = page == 1

        if isFirstPage {
            activeQuery = query
            hasSearched = true
            searchError = nil
            results = []
            totalResults = 0
            resultsPage = 0
            hasMoreResults = false
            isSearching = true
        } else {
            guard !isSearchingMore else { return false }
            isSearchingMore = true
        }

        defer {
            if generation == searchGeneration {
                if isFirstPage {
                    isSearching = false
                } else {
                    isSearchingMore = false
                }
            }
        }

        do {
            let response = try await repository.search(
                query: query,
                type: type,
                page: page,
                language: nil,
                region: nil
            )

            guard generation == searchGeneration,
                  !Task.isCancelled,
                  activeQuery == query,
                  searchType == type
            else {
                return false
            }

            if isFirstPage {
                results = response.items
            } else {
                let existing = Set(results.map(\.id))
                results.append(
                    contentsOf: response.items.filter { !existing.contains($0.id) }
                )
            }

            resultsPage = response.page
            totalResults = response.totalCount
            hasMoreResults = response.hasMore

            if recordHistory {
                await record(query: query)
            }
            return true
        } catch let error as APIError {
            guard generation == searchGeneration, error != .cancelled else { return false }
            searchError = error
            return false
        } catch is CancellationError {
            return false
        } catch {
            guard generation == searchGeneration else { return false }
            searchError = .transport(error.localizedDescription)
            return false
        }
    }

    private func record(query: String) async {
        guard let entry = try? await historyRepository.record(query: query) else {
            return
        }

        recentSearches.removeAll {
            $0.query.caseInsensitiveCompare(entry.query) == .orderedSame
        }
        recentSearches.insert(entry, at: 0)
        if recentSearches.count > 8 {
            recentSearches.removeLast(recentSearches.count - 8)
        }
    }

    private func resetSearchResults() {
        activeQuery = ""
        results = []
        totalResults = 0
        resultsPage = 0
        hasMoreResults = false
        hasSearched = false
        searchError = nil
        isSearching = false
        isSearchingMore = false
    }

    private func apply(_ items: [MediaSummary], to id: SearchShelfID) {
        if let index = shelves.firstIndex(where: { $0.id == id }) {
            shelves[index] = SearchShelf(id: id, items: items)
        } else if !items.isEmpty {
            shelves.append(SearchShelf(id: id, items: items))
            shelves.sort { lhs, rhs in
                (Self.shelfOrder.firstIndex(of: lhs.id) ?? 0)
                    < (Self.shelfOrder.firstIndex(of: rhs.id) ?? 0)
            }
        }
    }

    // MARK: Fetch helpers

    private nonisolated static let shelfOrder: [SearchShelfID] = [
        .trending,
        .popularMovies,
        .popularShows,
        .nowPlaying,
        .airingToday
    ]

    private nonisolated static func fetchShelves(
        repository: any MediaRepositoryProtocol,
        filter: MediaTypeFilter,
        window: TrendingWindow
    ) async -> [ShelfResult] {
        await withTaskGroup(of: ShelfResult.self, returning: [ShelfResult].self) { group in
            group.addTask {
                ShelfResult(
                    id: .trending,
                    result: await result {
                        try await repository.trending(
                            mediaType: filter,
                            window: window,
                            page: 1,
                            language: nil
                        ).items
                    }
                )
            }

            group.addTask {
                ShelfResult(
                    id: .popularMovies,
                    result: await result {
                        try await repository.feed(
                            .popular,
                            mediaType: .movie,
                            page: 1,
                            language: nil,
                            region: nil
                        ).items
                    }
                )
            }
            group.addTask {
                ShelfResult(
                    id: .popularShows,
                    result: await result {
                        try await repository.feed(
                            .popular,
                            mediaType: .tv,
                            page: 1,
                            language: nil,
                            region: nil
                        ).items
                    }
                )
            }
            group.addTask {
                ShelfResult(
                    id: .nowPlaying,
                    result: await result {
                        try await repository.feed(
                            .nowPlaying,
                            mediaType: .movie,
                            page: 1,
                            language: nil,
                            region: nil
                        ).items
                    }
                )
            }
            group.addTask {
                ShelfResult(
                    id: .airingToday,
                    result: await result {
                        try await repository.feed(
                            .airingToday,
                            mediaType: .tv,
                            page: 1,
                            language: nil,
                            region: nil
                        ).items
                    }
                )
            }

            var collected: [ShelfResult] = []
            for await value in group {
                collected.append(value)
            }
            return collected
        }
    }

    private nonisolated static func fetchGenres(
        repository: any MediaRepositoryProtocol
    ) async -> [MediaType: [Genre]] {
        await withTaskGroup(
            of: (MediaType, [Genre]?).self,
            returning: [MediaType: [Genre]].self
        ) { group in
            for mediaType in [MediaType.movie, .tv] {
                group.addTask {
                    let genres = try? await repository.genres(
                        mediaType: mediaType,
                        language: nil
                    )
                    return (mediaType, genres)
                }
            }

            var collected: [MediaType: [Genre]] = [:]
            for await (mediaType, genres) in group {
                if let genres {
                    collected[mediaType] = genres
                }
            }
            return collected
        }
    }

    private nonisolated static func result(
        from operation: () async throws -> [MediaSummary]
    ) async -> Result<[MediaSummary], APIError> {
        do {
            return .success(try await operation())
        } catch let error as APIError {
            return .failure(error)
        } catch is CancellationError {
            return .failure(.cancelled)
        } catch {
            return .failure(.transport(error.localizedDescription))
        }
    }

    private nonisolated static func apiError(from error: Error) -> APIError {
        if let error = error as? APIError {
            return error
        }
        return .transport(error.localizedDescription)
    }
}
