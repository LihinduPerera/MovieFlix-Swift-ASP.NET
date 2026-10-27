import Foundation
import Testing

@testable import MovieFlix

// MARK: - Fixtures

private enum Fixture {
    nonisolated static func summary(
        tmdbId: Int,
        mediaType: MediaType = .movie,
        title: String = "Arrival"
    ) -> MediaSummary {
        MediaSummary(
            tmdbId: tmdbId,
            mediaType: mediaType,
            title: title,
            originalTitle: title,
            overview: "A linguist works with the military to communicate with alien lifeforms.",
            posterPath: "/poster\(tmdbId).jpg",
            backdropPath: "/backdrop\(tmdbId).jpg",
            releaseDate: LocalDate(year: 2016, month: 11, day: 11),
            voteAverage: 7.6,
            voteCount: 12_345,
            popularity: 42.5
        )
    }

    nonisolated static func page(
        _ ids: [Int],
        page: Int = 1,
        pageSize: Int = 20,
        totalCount: Int? = nil
    ) -> PagedResponse<MediaSummary> {
        let total = totalCount ?? ids.count
        return PagedResponse(
            items: ids.map { summary(tmdbId: $0) },
            page: page,
            pageSize: pageSize,
            totalCount: total,
            totalPages: max(1, Int(ceil(Double(total) / Double(pageSize))))
        )
    }

    nonisolated static func history(
        _ id: Int,
        _ query: String
    ) -> SearchHistoryEntry {
        SearchHistoryEntry(
            id: id,
            query: query,
            searchedAt: Date(timeIntervalSince1970: 1_700_000_000 + Double(id))
        )
    }
}

// MARK: - Mocks

private actor MockSearchMediaRepository: MediaRepositoryProtocol {
    struct SearchCall: Sendable {
        let query: String
        let type: SearchType
        let page: Int
    }

    struct DiscoverCall: Sendable {
        let mediaType: MediaType
        let genreID: Int?
        let minRating: Double?
        let sortBy: String?
        let page: Int
    }

    private var searchResults: [String: Result<PagedResponse<MediaSummary>, APIError>] = [:]
    private var trendingResults: [String: Result<PagedResponse<MediaSummary>, APIError>] = [:]
    private var feedResults: [String: Result<PagedResponse<MediaSummary>, APIError>] = [:]
    private var genreResults: [String: Result<[Genre], APIError>] = [:]
    private var discoverResults: [Int: Result<PagedResponse<MediaSummary>, APIError>] = [:]

    private(set) var searchCalls: [SearchCall] = []
    private(set) var discoverCalls: [DiscoverCall] = []

    // MARK: Test setup

    func setSearch(
        _ query: String,
        type: SearchType,
        page: Int,
        _ result: Result<PagedResponse<MediaSummary>, APIError>
    ) {
        searchResults["\(query)|\(type.rawValue)|\(page)"] = result
    }

    func setTrending(
        _ window: TrendingWindow,
        _ result: Result<PagedResponse<MediaSummary>, APIError>
    ) {
        trendingResults[window.rawValue] = result
    }

    func setFeed(
        _ feed: MediaFeed,
        mediaType: MediaType,
        _ result: Result<PagedResponse<MediaSummary>, APIError>
    ) {
        feedResults["\(mediaType.rawValue)|\(feed.rawValue)"] = result
    }

    func setGenres(
        _ mediaType: MediaType,
        _ result: Result<[Genre], APIError>
    ) {
        genreResults[mediaType.rawValue] = result
    }

    func setDiscover(
        page: Int,
        _ result: Result<PagedResponse<MediaSummary>, APIError>
    ) {
        discoverResults[page] = result
    }

    // MARK: MediaRepositoryProtocol

    func search(
        query: String,
        type: SearchType,
        page: Int,
        language: String?,
        region: String?
    ) async throws -> PagedResponse<MediaSummary> {
        searchCalls.append(SearchCall(query: query, type: type, page: page))
        guard let result = searchResults["\(query)|\(type.rawValue)|\(page)"] else {
            throw APIError.notFound(nil)
        }
        return try result.get()
    }

    func trending(
        mediaType: MediaTypeFilter,
        window: TrendingWindow,
        page: Int,
        language: String?
    ) async throws -> PagedResponse<MediaSummary> {
        guard let result = trendingResults[window.rawValue] else {
            throw APIError.notFound(nil)
        }
        return try result.get()
    }

    func feed(
        _ feed: MediaFeed,
        mediaType: MediaType,
        page: Int,
        language: String?,
        region: String?
    ) async throws -> PagedResponse<MediaSummary> {
        let key = "\(mediaType.rawValue)|\(feed.rawValue)"
        guard let result = feedResults[key] else {
            throw APIError.notFound(nil)
        }
        return try result.get()
    }

    func discover(
        mediaType: MediaType,
        genreID: Int?,
        year: Int?,
        minRating: Double?,
        sortBy: String?,
        page: Int,
        language: String?,
        region: String?
    ) async throws -> PagedResponse<MediaSummary> {
        discoverCalls.append(
            DiscoverCall(
                mediaType: mediaType,
                genreID: genreID,
                minRating: minRating,
                sortBy: sortBy,
                page: page
            )
        )
        guard let result = discoverResults[page] else {
            throw APIError.notFound(nil)
        }
        return try result.get()
    }

    func genres(mediaType: MediaType, language: String?) async throws -> [Genre] {
        guard let result = genreResults[mediaType.rawValue] else {
            throw APIError.notFound(nil)
        }
        return try result.get()
    }

    func details(
        mediaType: MediaType,
        tmdbId: Int,
        language: String?
    ) async throws -> MediaDetails {
        throw APIError.notFound(nil)
    }

    func seasons(tmdbId: Int, language: String?) async throws -> [SeasonSummary] {
        throw APIError.notFound(nil)
    }

    func seasonDetails(
        tmdbId: Int,
        seasonNumber: Int,
        language: String?
    ) async throws -> SeasonDetails {
        throw APIError.notFound(nil)
    }

    func state(mediaType: MediaType, tmdbId: Int) async throws -> MediaState {
        throw APIError.notFound(nil)
    }

    func communityRating(mediaType: MediaType, tmdbId: Int) async throws -> CommunityRating {
        throw APIError.notFound(nil)
    }

    func reviews(
        mediaType: MediaType,
        tmdbId: Int,
        page: Int,
        pageSize: Int
    ) async throws -> PagedResponse<Review> {
        throw APIError.notFound(nil)
    }
}

private actor MockSearchHistoryRepository: SearchHistoryRepositoryProtocol {
    private var entries: [SearchHistoryEntry]
    private var nextID = 100

    private var historyError: APIError?
    private var recordError: APIError?
    private var deleteError: APIError?
    private var clearError: APIError?

    private(set) var recordedQueries: [String] = []
    private(set) var deletedIDs: [Int] = []
    private(set) var didClear = false

    init(entries: [SearchHistoryEntry] = []) {
        self.entries = entries
    }

    // MARK: Test setup

    func setEntries(_ value: [SearchHistoryEntry]) {
        entries = value
    }

    func setHistoryError(_ error: APIError?) {
        historyError = error
    }

    func setRecordError(_ error: APIError?) {
        recordError = error
    }

    func setDeleteError(_ error: APIError?) {
        deleteError = error
    }

    func setClearError(_ error: APIError?) {
        clearError = error
    }

    func currentEntries() -> [SearchHistoryEntry] {
        entries
    }

    // MARK: SearchHistoryRepositoryProtocol

    func history(limit: Int) async throws -> [SearchHistoryEntry] {
        if let historyError { throw historyError }
        return Array(entries.prefix(limit))
    }

    func record(query: String) async throws -> SearchHistoryEntry {
        recordedQueries.append(query)
        if let recordError { throw recordError }

        let trimmed = query.trimmingCharacters(in: .whitespacesAndNewlines)
        let searchedAt = Date()

        if let index = entries.firstIndex(where: {
            $0.query.caseInsensitiveCompare(trimmed) == .orderedSame
        }) {
            let updated = SearchHistoryEntry(
                id: entries[index].id,
                query: trimmed,
                searchedAt: searchedAt
            )
            entries.remove(at: index)
            entries.insert(updated, at: 0)
            return updated
        }

        let entry = SearchHistoryEntry(id: nextID, query: trimmed, searchedAt: searchedAt)
        nextID += 1
        entries.insert(entry, at: 0)
        return entry
    }

    func delete(id: Int) async throws {
        if let deleteError { throw deleteError }
        guard let index = entries.firstIndex(where: { $0.id == id }) else {
            throw APIError.notFound(nil)
        }
        entries.remove(at: index)
        deletedIDs.append(id)
    }

    func clear() async throws {
        if let clearError { throw clearError }
        entries.removeAll()
        didClear = true
    }
}

// MARK: - Search

@Suite("Search")
@MainActor
struct SearchViewModelTests {
    private func makeViewModel(
        media: MockSearchMediaRepository,
        history: MockSearchHistoryRepository = MockSearchHistoryRepository()
    ) -> SearchViewModel {
        SearchViewModel(
            repository: media,
            historyRepository: history,
            debounce: .milliseconds(5)
        )
    }

    @Test("Discovery loads every shelf in display order")
    func discoveryLoadsShelves() async {
        let media = MockSearchMediaRepository()
        await media.setTrending(.week, .success(Fixture.page([1, 2])))
        await media.setFeed(.popular, mediaType: .movie, .success(Fixture.page([3])))
        await media.setFeed(.popular, mediaType: .tv, .success(Fixture.page([4])))
        await media.setFeed(.nowPlaying, mediaType: .movie, .success(Fixture.page([5])))
        await media.setFeed(.airingToday, mediaType: .tv, .success(Fixture.page([6])))
        await media.setGenres(.movie, .success([Genre(id: 28, name: "Action")]))
        await media.setGenres(.tv, .success([Genre(id: 18, name: "Drama")]))

        let viewModel = makeViewModel(media: media)
        await viewModel.loadDiscovery()

        #expect(viewModel.discoveryPhase == .loaded)
        #expect(viewModel.shelves.map(\.id) == [
            .trending,
            .popularMovies,
            .popularShows,
            .nowPlaying,
            .airingToday
        ])
        #expect(viewModel.trendingItems.count == 2)
        #expect(viewModel.discoveryShelves.count == 4)
        #expect(viewModel.genreSuggestions.first?.name == "Action")
    }

    @Test("Empty shelves are omitted and partial failures are surfaced")
    func discoverySkipsEmptyShelvesAndReportsPartialFailure() async {
        let media = MockSearchMediaRepository()
        await media.setTrending(.week, .success(Fixture.page([1])))
        await media.setFeed(.popular, mediaType: .movie, .success(Fixture.page([])))
        await media.setFeed(.popular, mediaType: .tv, .success(Fixture.page([2])))
        await media.setFeed(.nowPlaying, mediaType: .movie, .failure(.server(status: 500, nil)))
        await media.setFeed(.airingToday, mediaType: .tv, .success(Fixture.page([3])))
        await media.setGenres(.movie, .success([Genre(id: 28, name: "Action")]))
        await media.setGenres(.tv, .success([]))

        let viewModel = makeViewModel(media: media)
        await viewModel.loadDiscovery()

        #expect(viewModel.discoveryPhase == .loaded)
        #expect(viewModel.discoveryShelves.map(\.id) == [
            .popularShows,
            .airingToday
        ])
        #expect(viewModel.partialErrorMessage != nil)
    }

    @Test("Discovery fails when no shelf can load")
    func discoveryFailsWithNoShelves() async {
        let media = MockSearchMediaRepository()
        await media.setGenres(.movie, .success([Genre(id: 28, name: "Action")]))
        await media.setGenres(.tv, .success([]))

        let viewModel = makeViewModel(media: media)
        await viewModel.loadDiscovery()

        guard case .failed = viewModel.discoveryPhase else {
            Issue.record("Expected a failed discovery phase")
            return
        }
        #expect(viewModel.shelves.isEmpty)
    }

    @Test("Submitting trims the query and stores results")
    func submitSearches() async {
        let media = MockSearchMediaRepository()
        await media.setSearch(
            "Dune",
            type: .multi,
            page: 1,
            .success(Fixture.page([1, 2, 3], totalCount: 45))
        )

        let viewModel = makeViewModel(media: media)
        viewModel.query = "  Dune  "

        let didSearch = await viewModel.submit(recordHistory: false)

        #expect(didSearch)
        #expect(viewModel.activeQuery == "Dune")
        #expect(viewModel.results.map(\.tmdbId) == [1, 2, 3])
        #expect(viewModel.totalResults == 45)
        #expect(viewModel.hasMoreResults)
        #expect(viewModel.searchError == nil)
        #expect(await media.searchCalls.map(\.query) == ["Dune"])
    }

    @Test("An empty query does not search")
    func emptyQueryDoesNotSearch() async {
        let media = MockSearchMediaRepository()
        let viewModel = makeViewModel(media: media)
        viewModel.query = "   "

        let didSearch = await viewModel.submit(recordHistory: true)

        #expect(!didSearch)
        #expect(!viewModel.hasSearched)
        #expect(await media.searchCalls.isEmpty)
    }

    @Test("Changing type re-runs the active search")
    func changingTypeRepeatsSearch() async {
        let media = MockSearchMediaRepository()
        await media.setSearch(
            "Dune",
            type: .multi,
            page: 1,
            .success(Fixture.page([1]))
        )
        await media.setSearch(
            "Dune",
            type: .movie,
            page: 1,
            .success(Fixture.page([2]))
        )

        let viewModel = makeViewModel(media: media)
        viewModel.query = "Dune"
        _ = await viewModel.submit(recordHistory: false)

        await viewModel.selectSearchType(.movie)

        #expect(viewModel.searchType == .movie)
        #expect(viewModel.results.map(\.tmdbId) == [2])
        #expect(await media.searchCalls.last?.type == .movie)
    }

    @Test("Changing type while the debounce is pending still runs the search")
    func changingTypeBeforeDebounceRunsSearch() async {
        let media = MockSearchMediaRepository()
        await media.setSearch(
            "Dune",
            type: .movie,
            page: 1,
            .success(Fixture.page([7]))
        )

        let viewModel = SearchViewModel(
            repository: media,
            historyRepository: MockSearchHistoryRepository(),
            debounce: .seconds(5)
        )
        viewModel.query = "Dune"
        viewModel.queryChanged()

        await viewModel.selectSearchType(.movie)

        #expect(viewModel.activeQuery == "Dune")
        #expect(viewModel.results.map(\.tmdbId) == [7])
        #expect(!viewModel.isSearching)
    }

    @Test("Pagination appends new items and drops duplicates")
    func paginationDeduplicates() async {
        let media = MockSearchMediaRepository()
        await media.setSearch(
            "Dune",
            type: .multi,
            page: 1,
            .success(Fixture.page([1, 2], page: 1, pageSize: 2, totalCount: 3))
        )
        await media.setSearch(
            "Dune",
            type: .multi,
            page: 2,
            .success(Fixture.page([2, 3], page: 2, pageSize: 2, totalCount: 3))
        )

        let viewModel = makeViewModel(media: media)
        viewModel.query = "Dune"
        _ = await viewModel.submit(recordHistory: false)

        await viewModel.loadMoreResults()

        #expect(viewModel.results.map(\.tmdbId) == [1, 2, 3])
        #expect(!viewModel.hasMoreResults)
        #expect(!viewModel.isSearchingMore)
    }

    @Test("Search failures surface without losing the query")
    func searchFailureSurfaces() async {
        let media = MockSearchMediaRepository()
        await media.setSearch(
            "Dune",
            type: .multi,
            page: 1,
            .failure(.offline)
        )

        let viewModel = makeViewModel(media: media)
        viewModel.query = "Dune"

        let didSearch = await viewModel.submit(recordHistory: false)

        #expect(!didSearch)
        #expect(viewModel.searchError == .offline)
        #expect(viewModel.results.isEmpty)
        #expect(!viewModel.isSearching)
        #expect(viewModel.hasSearched)
    }

    @Test("Clearing the query resets search state")
    func clearingResetsSearch() async {
        let media = MockSearchMediaRepository()
        await media.setSearch(
            "Dune",
            type: .multi,
            page: 1,
            .success(Fixture.page([1]))
        )

        let viewModel = makeViewModel(media: media)
        viewModel.query = "Dune"
        _ = await viewModel.submit(recordHistory: false)

        viewModel.clearQuery()

        #expect(viewModel.query.isEmpty)
        #expect(viewModel.results.isEmpty)
        #expect(viewModel.activeQuery.isEmpty)
        #expect(!viewModel.hasSearched)
    }

    @Test("Changing the trending window reloads only trending")
    func changingTrendingWindowReloads() async {
        let media = MockSearchMediaRepository()
        await media.setTrending(.week, .success(Fixture.page([1])))
        await media.setFeed(.popular, mediaType: .movie, .success(Fixture.page([2])))
        await media.setFeed(.popular, mediaType: .tv, .success(Fixture.page([3])))
        await media.setFeed(.nowPlaying, mediaType: .movie, .success(Fixture.page([4])))
        await media.setFeed(.airingToday, mediaType: .tv, .success(Fixture.page([5])))
        await media.setGenres(.movie, .success([]))
        await media.setGenres(.tv, .success([]))
        await media.setTrending(.day, .success(Fixture.page([9])))

        let viewModel = makeViewModel(media: media)
        await viewModel.loadDiscovery()

        await viewModel.selectTrendingWindow(.day)

        #expect(viewModel.trendingWindow == .day)
        #expect(viewModel.trendingItems.map(\.tmdbId) == [9])
        #expect(viewModel.shelves.count == 5)
    }

    @Test("Switching the genre media type lazily loads genres")
    func genreMediaTypeLoadsLazily() async {
        let media = MockSearchMediaRepository()
        await media.setTrending(.week, .success(Fixture.page([1])))
        await media.setGenres(.movie, .success([Genre(id: 28, name: "Action")]))

        let viewModel = makeViewModel(media: media)
        await viewModel.loadDiscovery()
        #expect(viewModel.genreSuggestions.first?.name == "Action")

        await media.setGenres(.tv, .success([Genre(id: 18, name: "Drama")]))
        await viewModel.selectGenreMediaType(.tv)

        #expect(viewModel.selectedGenreMediaType == .tv)
        #expect(viewModel.genreSuggestions.first?.name == "Drama")
    }
}

// MARK: - Recent searches

@Suite("Search history")
@MainActor
struct SearchHistoryViewModelTests {
    private func makeViewModel(
        media: MockSearchMediaRepository,
        history: MockSearchHistoryRepository
    ) -> SearchViewModel {
        SearchViewModel(
            repository: media,
            historyRepository: history,
            debounce: .milliseconds(5)
        )
    }

    @Test("Recent searches load newest first")
    func loadsRecentSearches() async {
        let history = MockSearchHistoryRepository(
            entries: [Fixture.history(2, "Severance"), Fixture.history(1, "Dune")]
        )
        let viewModel = makeViewModel(media: MockSearchMediaRepository(), history: history)

        await viewModel.loadRecentSearches()

        #expect(viewModel.recentSearches.map(\.query) == ["Severance", "Dune"])
        #expect(!viewModel.isLoadingRecentSearches)
    }

    @Test("Submitting records and promotes a search")
    func submitRecordsHistory() async {
        let media = MockSearchMediaRepository()
        await media.setSearch(
            "Dune",
            type: .multi,
            page: 1,
            .success(Fixture.page([1]))
        )
        let history = MockSearchHistoryRepository(
            entries: [Fixture.history(1, "Severance")]
        )
        let viewModel = makeViewModel(media: media, history: history)
        await viewModel.loadRecentSearches()

        viewModel.query = "Dune"
        _ = await viewModel.submit(recordHistory: true)

        #expect(viewModel.recentSearches.map(\.query) == ["Dune", "Severance"])
        #expect(await history.recordedQueries == ["Dune"])
    }

    @Test("Recording deduplicates case-insensitively")
    func recordDeduplicates() async {
        let media = MockSearchMediaRepository()
        await media.setSearch(
            "dune",
            type: .multi,
            page: 1,
            .success(Fixture.page([1]))
        )
        let history = MockSearchHistoryRepository(
            entries: [Fixture.history(1, "Dune"), Fixture.history(2, "Severance")]
        )
        let viewModel = makeViewModel(media: media, history: history)
        await viewModel.loadRecentSearches()

        viewModel.query = "dune"
        _ = await viewModel.submit(recordHistory: true)

        #expect(viewModel.recentSearches.count == 2)
        #expect(viewModel.recentSearches.first?.id == 1)
        #expect(viewModel.recentSearches.first?.query == "dune")
    }

    @Test("Removing a recent search restores it when the delete fails")
    func removeRollsBack() async {
        let history = MockSearchHistoryRepository(
            entries: [Fixture.history(1, "Dune"), Fixture.history(2, "Severance")]
        )
        let viewModel = makeViewModel(media: MockSearchMediaRepository(), history: history)
        await viewModel.loadRecentSearches()
        await history.setDeleteError(.offline)

        let error = await viewModel.removeRecentSearch(Fixture.history(1, "Dune"))

        #expect(error == .offline)
        #expect(viewModel.recentSearches.map(\.query) == ["Dune", "Severance"])
    }

    @Test("Clearing recent searches restores them when the clear fails")
    func clearRollsBack() async {
        let history = MockSearchHistoryRepository(
            entries: [Fixture.history(1, "Dune")]
        )
        let viewModel = makeViewModel(media: MockSearchMediaRepository(), history: history)
        await viewModel.loadRecentSearches()
        await history.setClearError(.server(status: 500, nil))

        let error = await viewModel.clearRecentSearches()

        #expect(error != nil)
        #expect(viewModel.recentSearches.count == 1)
    }

    @Test("Signing out clears local recent searches")
    func signOutClearsLocalHistory() async {
        let history = MockSearchHistoryRepository(
            entries: [Fixture.history(1, "Dune")]
        )
        let viewModel = makeViewModel(media: MockSearchMediaRepository(), history: history)
        await viewModel.loadRecentSearches()

        viewModel.clearLocalRecentSearches()

        #expect(viewModel.recentSearches.isEmpty)
    }
}

// MARK: - Genre results

@Suite("Genre results")
@MainActor
struct GenreResultsViewModelTests {
    private func makeViewModel(
        media: MockSearchMediaRepository,
        mediaType: MediaType = .movie
    ) -> GenreResultsViewModel {
        GenreResultsViewModel(
            mediaType: mediaType,
            genreID: 28,
            genreName: "Action",
            repository: media
        )
    }

    @Test("Loading fills the first page")
    func loadsFirstPage() async {
        let media = MockSearchMediaRepository()
        await media.setDiscover(
            page: 1,
            .success(Fixture.page([1, 2], page: 1, pageSize: 2, totalCount: 3))
        )
        let viewModel = makeViewModel(media: media)

        await viewModel.load()

        #expect(viewModel.phase == .loaded)
        #expect(viewModel.items.map(\.tmdbId) == [1, 2])
        #expect(viewModel.totalCount == 3)
        #expect(viewModel.hasMore)
        #expect(viewModel.countSummary == "3 titles in Action")
    }

    @Test("Loading more appends without duplicates")
    func loadsMore() async {
        let media = MockSearchMediaRepository()
        await media.setDiscover(
            page: 1,
            .success(Fixture.page([1, 2], page: 1, pageSize: 2, totalCount: 3))
        )
        await media.setDiscover(
            page: 2,
            .success(Fixture.page([2, 3], page: 2, pageSize: 2, totalCount: 3))
        )
        let viewModel = makeViewModel(media: media)
        await viewModel.load()

        await viewModel.loadMore()

        #expect(viewModel.items.map(\.tmdbId) == [1, 2, 3])
        #expect(!viewModel.hasMore)
        #expect(!viewModel.isLoadingMore)
    }

    @Test("Filters are sent to discover")
    func filtersAreSent() async {
        let media = MockSearchMediaRepository()
        await media.setDiscover(page: 1, .success(Fixture.page([1])))
        let viewModel = makeViewModel(media: media, mediaType: .tv)

        await viewModel.load()
        await viewModel.selectSort(.topRated)
        await viewModel.selectRating(.eightPlus)

        #expect(viewModel.sort == .topRated)
        #expect(viewModel.rating == .eightPlus)

        let calls = await media.discoverCalls
        #expect(calls.last?.sortBy == "vote_average.desc")
        #expect(calls.last?.minRating == 8)
        #expect(calls.last?.genreID == 28)
    }

    @Test("Newest sort uses the right date field per media type")
    func newestSortUsesMediaTypeField() {
        #expect(GenreSort.newest.sortBy(for: .movie) == "primary_release_date.desc")
        #expect(GenreSort.newest.sortBy(for: .tv) == "first_air_date.desc")
    }

    @Test("Resetting filters returns to defaults")
    func resetFilters() async {
        let media = MockSearchMediaRepository()
        await media.setDiscover(page: 1, .success(Fixture.page([1])))
        let viewModel = makeViewModel(media: media)
        await viewModel.load()
        await viewModel.selectSort(.newest)
        await viewModel.selectRating(.ninePlus)

        await viewModel.resetFilters()

        #expect(viewModel.sort == .popular)
        #expect(viewModel.rating == .any)
        #expect(await media.discoverCalls.last?.sortBy == "popularity.desc")
        #expect(await media.discoverCalls.last?.minRating == nil)
    }

    @Test("Initial failures surface as a failed phase")
    func initialFailureSurfaces() async {
        let media = MockSearchMediaRepository()
        await media.setDiscover(page: 1, .failure(.offline))
        let viewModel = makeViewModel(media: media)

        await viewModel.load()

        #expect(viewModel.phase == .failed(.offline))
        #expect(viewModel.items.isEmpty)
    }

    @Test("Pagination failures keep the existing items")
    func paginationFailureKeepsItems() async {
        let media = MockSearchMediaRepository()
        await media.setDiscover(
            page: 1,
            .success(Fixture.page([1, 2], page: 1, pageSize: 2, totalCount: 5))
        )
        await media.setDiscover(page: 2, .failure(.offline))
        let viewModel = makeViewModel(media: media)
        await viewModel.load()

        await viewModel.loadMore()

        #expect(viewModel.items.count == 2)
        #expect(viewModel.loadMoreError == .offline)
        #expect(!viewModel.isLoadingMore)
    }
}
