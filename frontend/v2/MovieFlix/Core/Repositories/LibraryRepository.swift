import Foundation

/// All authenticated library writes. Membership operations are idempotent on the
/// server, so callers may retry them safely.
nonisolated protocol LibraryRepositoryProtocol: Sendable {
    // Favorites
    func favorites(
        mediaType: MediaType?,
        page: Int,
        pageSize: Int,
        sort: LibrarySort
    ) async throws -> PagedResponse<SavedMedia>
    func addFavorite(_ id: MediaID) async throws -> SavedMedia
    func removeFavorite(_ id: MediaID) async throws

    // Watchlist
    func watchlist(
        mediaType: MediaType?,
        page: Int,
        pageSize: Int,
        sort: LibrarySort
    ) async throws -> PagedResponse<SavedMedia>
    func addToWatchlist(_ id: MediaID) async throws -> SavedMedia
    func removeFromWatchlist(_ id: MediaID) async throws

    // Ratings
    func myRating(_ id: MediaID) async throws -> MediaRating
    func rate(_ id: MediaID, rating: Int) async throws -> MediaRating
    func removeRating(_ id: MediaID) async throws

    // Watch history
    func continueWatching(limit: Int) async throws -> [WatchHistoryItem]
    func history(
        mediaType: MediaType?,
        page: Int,
        pageSize: Int
    ) async throws -> PagedResponse<WatchHistoryItem>
    func tvHistory(tmdbId: Int) async throws -> TvHistory
    func recordProgress(
        _ id: MediaID,
        progressSeconds: Int,
        completed: Bool,
        seasonNumber: Int?,
        episodeNumber: Int?
    ) async throws -> WatchHistoryItem
    func recordEpisodeProgress(
        tmdbId: Int,
        seasonNumber: Int,
        episodeNumber: Int,
        progressSeconds: Int,
        completed: Bool
    ) async throws -> EpisodeProgress
    func clearHistory() async throws
    func clearTitleHistory(_ id: MediaID) async throws

    // Collections
    func collections(page: Int, pageSize: Int) async throws -> PagedResponse<CollectionSummary>
    func createCollection(name: String, description: String) async throws -> CollectionSummary
    func collection(id: Int) async throws -> CollectionDetail
    func updateCollection(
        id: Int,
        name: String,
        description: String
    ) async throws -> CollectionSummary
    func deleteCollection(id: Int) async throws
    func addToCollection(id: Int, media: MediaID) async throws -> CollectionDetail
    func removeFromCollection(id: Int, media: MediaID) async throws
}

nonisolated struct LibraryRepository: LibraryRepositoryProtocol {
    let client: APIClient

    // MARK: Favorites

    func favorites(
        mediaType: MediaType? = nil,
        page: Int = 1,
        pageSize: Int = 20,
        sort: LibrarySort = .addedDesc
    ) async throws -> PagedResponse<SavedMedia> {
        try await client.send(
            .favorites(
                mediaType: mediaType,
                page: page,
                pageSize: pageSize,
                sort: sort
            )
        )
    }

    func addFavorite(_ id: MediaID) async throws -> SavedMedia {
        try await client.send(
            .addFavorite(mediaType: id.mediaType, tmdbId: id.tmdbId),
            as: SavedMedia.self
        )
    }

    func removeFavorite(_ id: MediaID) async throws {
        try await client.send(
            .removeFavorite(mediaType: id.mediaType, tmdbId: id.tmdbId)
        )
    }

    // MARK: Watchlist

    func watchlist(
        mediaType: MediaType? = nil,
        page: Int = 1,
        pageSize: Int = 20,
        sort: LibrarySort = .addedDesc
    ) async throws -> PagedResponse<SavedMedia> {
        try await client.send(
            .watchlist(
                mediaType: mediaType,
                page: page,
                pageSize: pageSize,
                sort: sort
            )
        )
    }

    func addToWatchlist(_ id: MediaID) async throws -> SavedMedia {
        try await client.send(
            .addToWatchlist(mediaType: id.mediaType, tmdbId: id.tmdbId),
            as: SavedMedia.self
        )
    }

    func removeFromWatchlist(_ id: MediaID) async throws {
        try await client.send(
            .removeFromWatchlist(mediaType: id.mediaType, tmdbId: id.tmdbId)
        )
    }

    // MARK: Ratings

    func myRating(_ id: MediaID) async throws -> MediaRating {
        try await client.send(
            .myRating(mediaType: id.mediaType, tmdbId: id.tmdbId),
            as: MediaRating.self
        )
    }

    func rate(_ id: MediaID, rating: Int) async throws -> MediaRating {
        try await client.send(
            .rate(mediaType: id.mediaType, tmdbId: id.tmdbId, rating: rating),
            as: MediaRating.self
        )
    }

    func removeRating(_ id: MediaID) async throws {
        try await client.send(
            .removeRating(mediaType: id.mediaType, tmdbId: id.tmdbId)
        )
    }

    // MARK: Watch history

    func continueWatching(limit: Int = 10) async throws -> [WatchHistoryItem] {
        try await client.send(.continueWatching(limit: limit), as: [WatchHistoryItem].self)
    }

    func history(
        mediaType: MediaType? = nil,
        page: Int = 1,
        pageSize: Int = 20
    ) async throws -> PagedResponse<WatchHistoryItem> {
        try await client.send(
            .history(mediaType: mediaType, page: page, pageSize: pageSize)
        )
    }

    func tvHistory(tmdbId: Int) async throws -> TvHistory {
        try await client.send(.tvHistory(tmdbId: tmdbId), as: TvHistory.self)
    }

    func recordProgress(
        _ id: MediaID,
        progressSeconds: Int,
        completed: Bool,
        seasonNumber: Int? = nil,
        episodeNumber: Int? = nil
    ) async throws -> WatchHistoryItem {
        try await client.send(
            .recordProgress(
                mediaType: id.mediaType,
                tmdbId: id.tmdbId,
                request: RecordProgressRequest(
                    progressSeconds: progressSeconds,
                    completed: completed,
                    seasonNumber: seasonNumber,
                    episodeNumber: episodeNumber
                )
            ),
            as: WatchHistoryItem.self
        )
    }

    func recordEpisodeProgress(
        tmdbId: Int,
        seasonNumber: Int,
        episodeNumber: Int,
        progressSeconds: Int,
        completed: Bool
    ) async throws -> EpisodeProgress {
        try await client.send(
            .recordEpisodeProgress(
                tmdbId: tmdbId,
                seasonNumber: seasonNumber,
                episodeNumber: episodeNumber,
                request: RecordEpisodeProgressRequest(
                    progressSeconds: progressSeconds,
                    completed: completed
                )
            ),
            as: EpisodeProgress.self
        )
    }

    func clearHistory() async throws {
        try await client.send(.clearHistory())
    }

    func clearTitleHistory(_ id: MediaID) async throws {
        try await client.send(
            .clearTitleHistory(mediaType: id.mediaType, tmdbId: id.tmdbId)
        )
    }

    // MARK: Collections

    func collections(
        page: Int = 1,
        pageSize: Int = 20
    ) async throws -> PagedResponse<CollectionSummary> {
        try await client.send(.collections(page: page, pageSize: pageSize))
    }

    func createCollection(name: String, description: String = "") async throws -> CollectionSummary {
        try await client.send(
            .createCollection(name: name, description: description),
            as: CollectionSummary.self
        )
    }

    func collection(id: Int) async throws -> CollectionDetail {
        try await client.send(.collection(id: id), as: CollectionDetail.self)
    }

    func updateCollection(
        id: Int,
        name: String,
        description: String
    ) async throws -> CollectionSummary {
        try await client.send(
            .updateCollection(id: id, name: name, description: description),
            as: CollectionSummary.self
        )
    }

    func deleteCollection(id: Int) async throws {
        try await client.send(.deleteCollection(id: id))
    }

    func addToCollection(id: Int, media: MediaID) async throws -> CollectionDetail {
        try await client.send(
            .addCollectionItem(
                id: id,
                mediaType: media.mediaType,
                tmdbId: media.tmdbId
            ),
            as: CollectionDetail.self
        )
    }

    func removeFromCollection(id: Int, media: MediaID) async throws {
        try await client.send(
            .removeCollectionItem(
                id: id,
                mediaType: media.mediaType,
                tmdbId: media.tmdbId
            )
        )
    }
}
