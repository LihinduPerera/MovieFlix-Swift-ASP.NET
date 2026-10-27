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

    nonisolated static func saved(
        tmdbId: Int,
        mediaType: MediaType = .movie,
        title: String = "Arrival",
        savedAt: Date = Date(timeIntervalSince1970: 1_700_000_000)
    ) -> SavedMedia {
        SavedMedia(
            media: summary(tmdbId: tmdbId, mediaType: mediaType, title: title),
            savedAt: savedAt
        )
    }

    nonisolated static func watching(
        tmdbId: Int,
        mediaType: MediaType = .tv,
        progressSeconds: Int = 1_200,
        completed: Bool = false,
        lastEpisode: EpisodeSummary? = nil,
        nextEpisode: EpisodeSummary? = nil
    ) -> WatchHistoryItem {
        WatchHistoryItem(
            media: summary(tmdbId: tmdbId, mediaType: mediaType, title: "Severance"),
            progressSeconds: progressSeconds,
            completed: completed,
            lastWatchedAt: Date(timeIntervalSince1970: 1_700_000_000),
            lastEpisode: lastEpisode,
            nextEpisode: nextEpisode
        )
    }

    nonisolated static func episode(
        season: Int = 1,
        number: Int = 2,
        completed: Bool = false,
        runtimeMinutes: Int = 48
    ) -> Episode {
        Episode(
            seasonNumber: season,
            episodeNumber: number,
            name: "Half Loop",
            stillPath: "/still.jpg",
            tmdbId: 99,
            overview: "The team settles in.",
            airDate: LocalDate(year: 2022, month: 2, day: 25),
            runtimeMinutes: runtimeMinutes,
            voteAverage: 8.1,
            voteCount: 1_200,
            progressSeconds: completed ? runtimeMinutes * 60 : 0,
            completed: completed
        )
    }

    nonisolated static func details(
        mediaType: MediaType = .tv,
        tmdbId: Int = 95396
    ) -> MediaDetails {
        MediaDetails(
            tmdbId: tmdbId,
            mediaType: mediaType,
            title: "Severance",
            originalTitle: "Severance",
            overview: "Mark leads a team of office workers whose memories are surgically divided.",
            posterPath: "/poster.jpg",
            backdropPath: "/backdrop.jpg",
            releaseDate: nil,
            voteAverage: 8.4,
            voteCount: 2_100,
            popularity: 120.4,
            tagline: "Who are you?",
            originalLanguage: "en",
            adult: false,
            status: "Returning Series",
            runtimeMinutes: mediaType == .movie ? 108 : nil,
            numberOfSeasons: mediaType == .tv ? 2 : nil,
            numberOfEpisodes: mediaType == .tv ? 19 : nil,
            firstAirDate: LocalDate(year: 2022, month: 2, day: 18),
            lastAirDate: LocalDate(year: 2025, month: 3, day: 21),
            genres: [Genre(id: 18, name: "Drama")],
            credits: Credits(cast: [], crew: []),
            videos: [],
            similar: [],
            seasons: [
                SeasonSummary(
                    seasonNumber: 1,
                    name: "Season 1",
                    overview: "The first season.",
                    posterPath: nil,
                    airDate: LocalDate(year: 2022, month: 2, day: 18),
                    episodeCount: 9
                )
            ]
        )
    }

    nonisolated static func state(
        mediaID: MediaID,
        isFavorite: Bool = false,
        isInWatchlist: Bool = false,
        progressSeconds: Int = 0,
        completed: Bool = false
    ) -> MediaState {
        MediaState(
            tmdbId: mediaID.tmdbId,
            mediaType: mediaID.mediaType,
            isFavorite: isFavorite,
            isInWatchlist: isInWatchlist,
            userRating: nil,
            reviewId: nil,
            hasReview: false,
            progressSeconds: progressSeconds,
            completed: completed,
            lastEpisode: nil,
            nextEpisode: nil
        )
    }

    nonisolated static func page<Item: Decodable & Sendable>(
        _ items: [Item],
        page: Int,
        pageSize: Int = 30,
        totalCount: Int
    ) -> PagedResponse<Item> {
        PagedResponse(
            items: items,
            page: page,
            pageSize: pageSize,
            totalCount: totalCount,
            totalPages: max(1, Int(ceil(Double(totalCount) / Double(pageSize))))
        )
    }
}

// MARK: - Library view model

@Suite("Library view model")
@MainActor
struct LibraryViewModelTests {
    private let favoriteA = Fixture.summary(tmdbId: 1, title: "Arrival")
    private let favoriteB = Fixture.summary(tmdbId: 2, title: "Dune")

    @Test("Loads every segment and exposes saved counts")
    func loadsAllSegments() async {
        let repository = MockLibraryRepository(
            favorites: .success(Fixture.page(
                [Fixture.saved(tmdbId: 1), Fixture.saved(tmdbId: 2)],
                page: 1,
                totalCount: 2
            )),
            watchlist: .success(Fixture.page(
                [Fixture.saved(tmdbId: 3)],
                page: 1,
                totalCount: 1
            )),
            watching: .success([
                Fixture.watching(
                    tmdbId: 95396,
                    nextEpisode: EpisodeSummary(
                        seasonNumber: 1,
                        episodeNumber: 3,
                        name: "In Perpetuity",
                        stillPath: nil
                    )
                )
            ])
        )

        let viewModel = LibraryViewModel(repository: repository)
        await viewModel.load()

        #expect(viewModel.phase == .loaded)
        #expect(viewModel.favorites.items.count == 2)
        #expect(viewModel.watchlist.items.count == 1)
        #expect(viewModel.watching.items.count == 1)
        #expect(viewModel.totalSaved == 3)
        #expect(viewModel.favoriteIDs.contains(favoriteA.id))
        #expect(viewModel.lastLoadedAt != nil)
    }

    @Test("One failed segment doesn't hide the rest of the library")
    func partialFailureKeepsOtherSegments() async {
        let repository = MockLibraryRepository(
            favorites: .success(Fixture.page(
                [Fixture.saved(tmdbId: 1), Fixture.saved(tmdbId: 2)],
                page: 1,
                totalCount: 2
            )),
            watchlist: .failure(.server(status: 500, nil)),
            watching: .success([Fixture.watching(tmdbId: 95396)])
        )

        let viewModel = LibraryViewModel(repository: repository)
        await viewModel.load()

        #expect(viewModel.phase == .loaded)
        #expect(viewModel.favorites.items.count == 2)
        #expect(viewModel.watching.items.count == 1)

        viewModel.segment = .watchlist
        #expect(viewModel.currentError == .server(status: 500, nil))
        #expect(!viewModel.isCurrentEmpty, "An errored segment is not an empty segment")
    }

    @Test("Reports failure when every segment fails")
    func completeFailureIsReported() async {
        let repository = MockLibraryRepository(
            favorites: .failure(.offline),
            watchlist: .failure(.offline),
            watching: .failure(.offline)
        )

        let viewModel = LibraryViewModel(repository: repository)
        await viewModel.load()

        guard case .failed = viewModel.phase else {
            Issue.record("Expected a failed phase, received \(viewModel.phase)")
            return
        }
        #expect(viewModel.favorites.error == .offline)
    }

    @Test("Paging appends new titles without duplicating existing ones")
    func pagingDeduplicatesItems() async {
        let repository = MockLibraryRepository(
            favorites: .success(Fixture.page(
                [Fixture.saved(tmdbId: 1), Fixture.saved(tmdbId: 2)],
                page: 1,
                pageSize: 2,
                totalCount: 3
            ))
        )
        await repository.setNextFavoritesPage(
            Fixture.page(
                [Fixture.saved(tmdbId: 2), Fixture.saved(tmdbId: 3)],
                page: 2,
                pageSize: 2,
                totalCount: 3
            )
        )

        let viewModel = LibraryViewModel(repository: repository)
        await viewModel.loadFavorites(reset: true)
        #expect(viewModel.favorites.items.count == 2)
        #expect(viewModel.favorites.hasMore)

        await viewModel.loadFavorites(reset: false)

        #expect(viewModel.favorites.items.map(\.media.tmdbId) == [1, 2, 3])
        #expect(viewModel.favorites.page == 2)
        #expect(viewModel.favorites.totalCount == 3)
        #expect(!viewModel.favorites.hasMore)
    }

    @Test("Removing a favorite updates the grid and count")
    func removalDropsItem() async {
        let repository = MockLibraryRepository(
            favorites: .success(Fixture.page(
                [Fixture.saved(tmdbId: 1), Fixture.saved(tmdbId: 2)],
                page: 1,
                totalCount: 2
            ))
        )
        let viewModel = LibraryViewModel(repository: repository)
        await viewModel.loadFavorites(reset: true)

        let error = await viewModel.removeFavoriteItem(favoriteA.id)

        #expect(error == nil)
        #expect(viewModel.favorites.items.map(\.media.tmdbId) == [2])
        #expect(viewModel.favorites.totalCount == 1)
        #expect(await repository.removedFavorites == [favoriteA.id])
    }

    @Test("A failed removal keeps the title in the grid")
    func failedRemovalKeepsItem() async {
        let repository = MockLibraryRepository(
            favorites: .success(Fixture.page(
                [Fixture.saved(tmdbId: 1), Fixture.saved(tmdbId: 2)],
                page: 1,
                totalCount: 2
            ))
        )
        await repository.setRemoveFavoriteResult(.failure(.server(status: 503, nil)))

        let viewModel = LibraryViewModel(repository: repository)
        await viewModel.loadFavorites(reset: true)

        let error = await viewModel.removeFavoriteItem(favoriteA.id)

        #expect(error == .server(status: 503, nil))
        #expect(viewModel.favorites.items.count == 2)
        #expect(viewModel.favorites.totalCount == 2)
    }

    @Test("Clearing progress removes the continue-watching card")
    func clearingProgressDropsTitle() async {
        let repository = MockLibraryRepository(
            watching: .success([
                Fixture.watching(tmdbId: 95396),
                Fixture.watching(tmdbId: 95397)
            ])
        )
        let viewModel = LibraryViewModel(repository: repository)
        await viewModel.loadWatching()

        let error = await viewModel.removeProgressItem(MediaID(mediaType: .tv, tmdbId: 95396))

        #expect(error == nil)
        #expect(viewModel.watching.items.map(\.media.tmdbId) == [95397])
        #expect(await repository.clearedTitles == [MediaID(mediaType: .tv, tmdbId: 95396)])
    }

    @Test("Sign out resets every segment")
    func resetClearsSegments() async {
        let repository = MockLibraryRepository(
            favorites: .success(Fixture.page([Fixture.saved(tmdbId: 1)], page: 1, totalCount: 1)),
            watchlist: .success(Fixture.page([Fixture.saved(tmdbId: 2)], page: 1, totalCount: 1)),
            watching: .success([Fixture.watching(tmdbId: 3)])
        )
        let viewModel = LibraryViewModel(repository: repository)
        await viewModel.load()

        viewModel.reset()

        #expect(viewModel.phase == .idle)
        #expect(viewModel.favorites.items.isEmpty)
        #expect(viewModel.watchlist.items.isEmpty)
        #expect(viewModel.watching.items.isEmpty)
        #expect(viewModel.lastLoadedAt == nil)
    }

    @Test("A fresh cache skips reloads inside the freshness window")
    func freshnessWindowSkipsReload() async {
        let repository = MockLibraryRepository(
            favorites: .success(Fixture.page([Fixture.saved(tmdbId: 1)], page: 1, totalCount: 1))
        )
        let viewModel = LibraryViewModel(repository: repository)
        await viewModel.load()

        await viewModel.refreshIfStale()

        #expect(await repository.favoritesCallCount == 1)
    }
}

// MARK: - Detail library actions

@Suite("Detail library actions")
@MainActor
struct MediaDetailLibraryTests {
    private let mediaID = MediaID(mediaType: .tv, tmdbId: 95396)

    private func makeLoadedViewModel(
        media: MockMediaRepository,
        library: MockLibraryRepository
    ) async -> MediaDetailViewModel {
        let viewModel = MediaDetailViewModel(
            mediaID: mediaID,
            repository: media,
            libraryRepository: library
        )
        await viewModel.load()
        return viewModel
    }

    @Test("Favoriting flips optimistically, then rolls back on failure")
    func favoriteRollsBack() async {
        let media = MockMediaRepository(state: Fixture.state(mediaID: mediaID))
        let library = MockLibraryRepository(favoriteDelay: .milliseconds(250))
        await library.setAddFavoriteResult(.failure(.server(status: 500, nil)))

        let viewModel = await makeLoadedViewModel(media: media, library: library)
        #expect(viewModel.state?.isFavorite == false)

        let task = Task { await viewModel.toggleFavorite() }
        try? await Task.sleep(for: .milliseconds(60))
        #expect(viewModel.state?.isFavorite == true, "The heart should flip before the write finishes")
        #expect(viewModel.isUpdatingFavorite)

        let error = await task.value

        #expect(error == .server(status: 500, nil))
        #expect(viewModel.state?.isFavorite == false)
        #expect(!viewModel.isUpdatingFavorite)
    }

    @Test("A successful favorite write sticks and only calls the server once")
    func favoriteSucceeds() async {
        let media = MockMediaRepository(state: Fixture.state(mediaID: mediaID))
        let library = MockLibraryRepository()

        let viewModel = await makeLoadedViewModel(media: media, library: library)
        let error = await viewModel.toggleFavorite()

        #expect(error == nil)
        #expect(viewModel.state?.isFavorite == true)
        #expect(await library.addedFavorites == [mediaID])
        #expect(await library.addedWatchlist.isEmpty)
    }

    @Test("Watchlist membership survives a successful write")
    func watchlistSucceeds() async {
        let media = MockMediaRepository(state: Fixture.state(mediaID: mediaID))
        let library = MockLibraryRepository()

        let viewModel = await makeLoadedViewModel(media: media, library: library)
        let error = await viewModel.toggleWatchlist()

        #expect(error == nil)
        #expect(viewModel.state?.isInWatchlist == true)
        #expect(await library.addedWatchlist == [mediaID])
    }

    @Test("Unwatchlisting an item removes it and keeps the title in favorites")
    func watchlistRemovalIsIndependent() async {
        let media = MockMediaRepository(
            state: Fixture.state(
                mediaID: mediaID,
                isFavorite: true,
                isInWatchlist: true
            )
        )
        let library = MockLibraryRepository()

        let viewModel = await makeLoadedViewModel(media: media, library: library)
        let error = await viewModel.toggleWatchlist()

        #expect(error == nil)
        #expect(viewModel.state?.isInWatchlist == false)
        #expect(viewModel.state?.isFavorite == true)
        #expect(await library.removedWatchlist == [mediaID])
    }

    @Test("Marking an episode watched applies the server copy and refreshes state")
    func episodeWatchRefreshesState() async {
        let episode = Fixture.episode(number: 2)
        let media = MockMediaRepository(state: Fixture.state(mediaID: mediaID))
        let library = MockLibraryRepository(
            episodeResult: .success(
                EpisodeProgress(
                    media: Fixture.summary(tmdbId: mediaID.tmdbId, mediaType: .tv),
                    episode: episode.settingWatched(true),
                    nextEpisode: nil,
                    showCompleted: false
                )
            )
        )

        let viewModel = await makeLoadedViewModel(media: media, library: library)
        await viewModel.toggleSeason(1)
        #expect(viewModel.episodesBySeason[1]?.count == 1)

        let error = await viewModel.setWatched(true, episode: episode)

        #expect(error == nil)
        let stored = viewModel.episodesBySeason[1]?.first
        #expect(stored?.completed == true)
        #expect(stored?.progressSeconds == 48 * 60)
        #expect(await library.episodeWrites.count == 1)
        #expect(await media.stateCallCount >= 2, "Derived state is re-read after the write")
    }

    @Test("A failed episode write restores the previous row")
    func episodeWatchRollsBack() async {
        let episode = Fixture.episode(number: 2)
        let media = MockMediaRepository(state: Fixture.state(mediaID: mediaID))
        let library = MockLibraryRepository()
        await library.setEpisodeResult(.failure(.server(status: 500, nil)))

        let viewModel = await makeLoadedViewModel(media: media, library: library)
        await viewModel.toggleSeason(1)

        let error = await viewModel.setWatched(true, episode: episode)

        #expect(error == .server(status: 500, nil))
        #expect(viewModel.episodesBySeason[1]?.first?.completed == false)
        #expect(viewModel.episodesBySeason[1]?.first?.progressSeconds == 0)
        #expect(viewModel.updatingEpisodeID == nil)
    }

    @Test("Reload uses the cached detail payload")
    func reloadUsesCache() async {
        let media = MockMediaRepository(state: Fixture.state(mediaID: mediaID))
        let viewModel = await makeLoadedViewModel(media: media, library: MockLibraryRepository())
        #expect(viewModel.phase == .loaded)

        await viewModel.load()

        #expect(await media.detailsCallCount == 1)
    }
}

// MARK: - Mocks

private actor MockMediaRepository: MediaRepositoryProtocol {
    private let detailsResult: Result<MediaDetails, APIError>
    private var stateResult: Result<MediaState, APIError>
    private var seasonResult: Result<SeasonDetails, APIError>

    private(set) var detailsCallCount = 0
    private(set) var stateCallCount = 0
    private(set) var seasonCallCount = 0

    init(
        state: MediaState,
        details: Result<MediaDetails, APIError> = .success(Fixture.details()),
        season: Result<SeasonDetails, APIError> = .success(
            SeasonDetails(
                seasonNumber: 1,
                name: "Season 1",
                overview: nil,
                posterPath: nil,
                airDate: nil,
                episodes: [Fixture.episode()]
            )
        )
    ) {
        self.detailsResult = details
        self.stateResult = .success(state)
        self.seasonResult = season
    }

    func setState(_ value: MediaState) {
        stateResult = .success(value)
    }

    func search(
        query: String,
        type: SearchType,
        page: Int,
        language: String?,
        region: String?
    ) async throws -> PagedResponse<MediaSummary> {
        throw APIError.notFound(nil)
    }

    func trending(
        mediaType: MediaTypeFilter,
        window: TrendingWindow,
        page: Int,
        language: String?
    ) async throws -> PagedResponse<MediaSummary> {
        throw APIError.notFound(nil)
    }

    func feed(
        _ feed: MediaFeed,
        mediaType: MediaType,
        page: Int,
        language: String?,
        region: String?
    ) async throws -> PagedResponse<MediaSummary> {
        throw APIError.notFound(nil)
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
        throw APIError.notFound(nil)
    }

    func genres(mediaType: MediaType, language: String?) async throws -> [Genre] {
        throw APIError.notFound(nil)
    }

    func details(
        mediaType: MediaType,
        tmdbId: Int,
        language: String?
    ) async throws -> MediaDetails {
        detailsCallCount += 1
        return try detailsResult.get()
    }

    func seasons(tmdbId: Int, language: String?) async throws -> [SeasonSummary] {
        throw APIError.notFound(nil)
    }

    func seasonDetails(
        tmdbId: Int,
        seasonNumber: Int,
        language: String?
    ) async throws -> SeasonDetails {
        seasonCallCount += 1
        return try seasonResult.get()
    }

    func state(mediaType: MediaType, tmdbId: Int) async throws -> MediaState {
        stateCallCount += 1
        return try stateResult.get()
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

private actor MockLibraryRepository: LibraryRepositoryProtocol {
    private var favoritesResult: Result<PagedResponse<SavedMedia>, APIError>
    private var nextFavoritesResult: Result<PagedResponse<SavedMedia>, APIError>?
    private var watchlistResult: Result<PagedResponse<SavedMedia>, APIError>
    private var watchingResult: Result<[WatchHistoryItem], APIError>

    private var addFavoriteResult: Result<SavedMedia, APIError>
    private var removeFavoriteResult: Result<Void, APIError>
    private var addWatchlistResult: Result<SavedMedia, APIError>
    private var removeWatchlistResult: Result<Void, APIError>
    private var episodeResult: Result<EpisodeProgress, APIError>
    private let favoriteDelay: Duration?

    private(set) var favoritesCallCount = 0
    private(set) var addedFavorites: [MediaID] = []
    private(set) var removedFavorites: [MediaID] = []
    private(set) var addedWatchlist: [MediaID] = []
    private(set) var removedWatchlist: [MediaID] = []
    private(set) var clearedTitles: [MediaID] = []
    private(set) var episodeWrites: [(season: Int, episode: Int)] = []

    init(
        favorites: Result<PagedResponse<SavedMedia>, APIError> = .success(
            Fixture.page([], page: 1, totalCount: 0)
        ),
        watchlist: Result<PagedResponse<SavedMedia>, APIError> = .success(
            Fixture.page([], page: 1, totalCount: 0)
        ),
        watching: Result<[WatchHistoryItem], APIError> = .success([]),
        episodeResult: Result<EpisodeProgress, APIError> = .failure(.server(status: 500, nil)),
        favoriteDelay: Duration? = nil
    ) {
        self.favoritesResult = favorites
        self.watchlistResult = watchlist
        self.watchingResult = watching
        self.episodeResult = episodeResult
        self.favoriteDelay = favoriteDelay

        let media = Fixture.summary(tmdbId: 1)
        self.addFavoriteResult = .success(
            SavedMedia(media: media, savedAt: Date(timeIntervalSince1970: 1_700_000_000))
        )
        self.removeFavoriteResult = .success(())
        self.addWatchlistResult = .success(
            SavedMedia(media: media, savedAt: Date(timeIntervalSince1970: 1_700_000_000))
        )
        self.removeWatchlistResult = .success(())
    }

    // MARK: Test controls

    func setNextFavoritesPage(_ page: PagedResponse<SavedMedia>) {
        nextFavoritesResult = .success(page)
    }

    func setAddFavoriteResult(_ result: Result<SavedMedia, APIError>) {
        addFavoriteResult = result
    }

    func setRemoveFavoriteResult(_ result: Result<Void, APIError>) {
        removeFavoriteResult = result
    }

    func setEpisodeResult(_ result: Result<EpisodeProgress, APIError>) {
        episodeResult = result
    }

    // MARK: Favorites

    func favorites(
        mediaType: MediaType?,
        page: Int,
        pageSize: Int,
        sort: LibrarySort
    ) async throws -> PagedResponse<SavedMedia> {
        favoritesCallCount += 1
        if page > 1, let nextFavoritesResult {
            return try nextFavoritesResult.get()
        }
        return try favoritesResult.get()
    }

    func addFavorite(_ id: MediaID) async throws -> SavedMedia {
        addedFavorites.append(id)
        if let favoriteDelay {
            try? await Task.sleep(for: favoriteDelay)
        }
        return try addFavoriteResult.get()
    }

    func removeFavorite(_ id: MediaID) async throws {
        removedFavorites.append(id)
        try removeFavoriteResult.get()
    }

    // MARK: Watchlist

    func watchlist(
        mediaType: MediaType?,
        page: Int,
        pageSize: Int,
        sort: LibrarySort
    ) async throws -> PagedResponse<SavedMedia> {
        try watchlistResult.get()
    }

    func addToWatchlist(_ id: MediaID) async throws -> SavedMedia {
        addedWatchlist.append(id)
        return try addWatchlistResult.get()
    }

    func removeFromWatchlist(_ id: MediaID) async throws {
        removedWatchlist.append(id)
        try removeWatchlistResult.get()
    }

    // MARK: Ratings

    func myRating(_ id: MediaID) async throws -> MediaRating {
        throw APIError.notFound(nil)
    }

    func rate(_ id: MediaID, rating: Int) async throws -> MediaRating {
        throw APIError.notFound(nil)
    }

    func removeRating(_ id: MediaID) async throws {
        throw APIError.notFound(nil)
    }

    // MARK: Watch history

    func continueWatching(limit: Int) async throws -> [WatchHistoryItem] {
        try watchingResult.get()
    }

    func history(
        mediaType: MediaType?,
        page: Int,
        pageSize: Int
    ) async throws -> PagedResponse<WatchHistoryItem> {
        throw APIError.notFound(nil)
    }

    func tvHistory(tmdbId: Int) async throws -> TvHistory {
        throw APIError.notFound(nil)
    }

    func recordProgress(
        _ id: MediaID,
        progressSeconds: Int,
        completed: Bool,
        seasonNumber: Int?,
        episodeNumber: Int?
    ) async throws -> WatchHistoryItem {
        throw APIError.notFound(nil)
    }

    func recordEpisodeProgress(
        tmdbId: Int,
        seasonNumber: Int,
        episodeNumber: Int,
        progressSeconds: Int,
        completed: Bool
    ) async throws -> EpisodeProgress {
        episodeWrites.append((season: seasonNumber, episode: episodeNumber))
        return try episodeResult.get()
    }

    func clearHistory() async throws {
        throw APIError.notFound(nil)
    }

    func clearTitleHistory(_ id: MediaID) async throws {
        clearedTitles.append(id)
    }

    // MARK: Collections

    func collections(page: Int, pageSize: Int) async throws -> PagedResponse<CollectionSummary> {
        throw APIError.notFound(nil)
    }

    func createCollection(name: String, description: String) async throws -> CollectionSummary {
        throw APIError.notFound(nil)
    }

    func collection(id: Int) async throws -> CollectionDetail {
        throw APIError.notFound(nil)
    }

    func updateCollection(
        id: Int,
        name: String,
        description: String
    ) async throws -> CollectionSummary {
        throw APIError.notFound(nil)
    }

    func deleteCollection(id: Int) async throws {
        throw APIError.notFound(nil)
    }

    func addToCollection(id: Int, media: MediaID) async throws -> CollectionDetail {
        throw APIError.notFound(nil)
    }

    func removeFromCollection(id: Int, media: MediaID) async throws {
        throw APIError.notFound(nil)
    }
}
