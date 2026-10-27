import Foundation

nonisolated protocol MediaRepositoryProtocol: Sendable {
    func search(
        query: String,
        type: SearchType,
        page: Int,
        language: String?,
        region: String?
    ) async throws -> PagedResponse<MediaSummary>

    func trending(
        mediaType: MediaTypeFilter,
        window: TrendingWindow,
        page: Int,
        language: String?
    ) async throws -> PagedResponse<MediaSummary>

    func feed(
        _ feed: MediaFeed,
        mediaType: MediaType,
        page: Int,
        language: String?,
        region: String?
    ) async throws -> PagedResponse<MediaSummary>

    func discover(
        mediaType: MediaType,
        genreID: Int?,
        year: Int?,
        minRating: Double?,
        sortBy: String?,
        page: Int,
        language: String?,
        region: String?
    ) async throws -> PagedResponse<MediaSummary>

    func genres(mediaType: MediaType, language: String?) async throws -> [Genre]

    func details(
        mediaType: MediaType,
        tmdbId: Int,
        language: String?
    ) async throws -> MediaDetails

    func seasons(tmdbId: Int, language: String?) async throws -> [SeasonSummary]

    func seasonDetails(
        tmdbId: Int,
        seasonNumber: Int,
        language: String?
    ) async throws -> SeasonDetails

    func state(mediaType: MediaType, tmdbId: Int) async throws -> MediaState

    func communityRating(mediaType: MediaType, tmdbId: Int) async throws -> CommunityRating

    func reviews(
        mediaType: MediaType,
        tmdbId: Int,
        page: Int,
        pageSize: Int
    ) async throws -> PagedResponse<Review>
}

nonisolated struct MediaRepository: MediaRepositoryProtocol {
    let client: APIClient

    func search(
        query: String,
        type: SearchType = .multi,
        page: Int = 1,
        language: String? = nil,
        region: String? = nil
    ) async throws -> PagedResponse<MediaSummary> {
        try await client.send(
            .search(
                query: query,
                type: type,
                page: page,
                language: language,
                region: region
            )
        )
    }

    func trending(
        mediaType: MediaTypeFilter = .all,
        window: TrendingWindow = .week,
        page: Int = 1,
        language: String? = nil
    ) async throws -> PagedResponse<MediaSummary> {
        try await client.send(
            .trending(
                mediaType: mediaType,
                window: window,
                page: page,
                language: language
            )
        )
    }

    func feed(
        _ feed: MediaFeed,
        mediaType: MediaType,
        page: Int = 1,
        language: String? = nil,
        region: String? = nil
    ) async throws -> PagedResponse<MediaSummary> {
        try await client.send(
            .feed(
                feed,
                mediaType: mediaType,
                page: page,
                language: language,
                region: region
            )
        )
    }

    func discover(
        mediaType: MediaType,
        genreID: Int? = nil,
        year: Int? = nil,
        minRating: Double? = nil,
        sortBy: String? = nil,
        page: Int = 1,
        language: String? = nil,
        region: String? = nil
    ) async throws -> PagedResponse<MediaSummary> {
        try await client.send(
            .discover(
                mediaType: mediaType,
                genreId: genreID,
                year: year,
                minRating: minRating,
                sortBy: sortBy,
                page: page,
                language: language,
                region: region
            )
        )
    }

    func genres(mediaType: MediaType, language: String? = nil) async throws -> [Genre] {
        try await client.send(.genres(mediaType: mediaType, language: language))
    }

    func details(
        mediaType: MediaType,
        tmdbId: Int,
        language: String? = nil
    ) async throws -> MediaDetails {
        try await client.send(
            .details(mediaType: mediaType, tmdbId: tmdbId, language: language)
        )
    }

    func seasons(tmdbId: Int, language: String? = nil) async throws -> [SeasonSummary] {
        try await client.send(.seasons(tmdbId: tmdbId, language: language))
    }

    func seasonDetails(
        tmdbId: Int,
        seasonNumber: Int,
        language: String? = nil
    ) async throws -> SeasonDetails {
        try await client.send(
            .seasonDetails(
                tmdbId: tmdbId,
                seasonNumber: seasonNumber,
                language: language
            )
        )
    }

    func state(mediaType: MediaType, tmdbId: Int) async throws -> MediaState {
        try await client.send(.state(mediaType: mediaType, tmdbId: tmdbId))
    }

    func communityRating(mediaType: MediaType, tmdbId: Int) async throws -> CommunityRating {
        try await client.send(.communityRating(mediaType: mediaType, tmdbId: tmdbId))
    }

    func reviews(
        mediaType: MediaType,
        tmdbId: Int,
        page: Int = 1,
        pageSize: Int = 20
    ) async throws -> PagedResponse<Review> {
        try await client.send(
            .reviews(
                mediaType: mediaType,
                tmdbId: tmdbId,
                page: page,
                pageSize: pageSize
            )
        )
    }
}
