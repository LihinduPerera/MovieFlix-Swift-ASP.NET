import Foundation

// MARK: - Per-title state

nonisolated struct MediaState: Decodable, Hashable, Sendable {
    let tmdbId: Int
    let mediaType: MediaType
    let isFavorite: Bool
    let isInWatchlist: Bool
    let userRating: Int?
    let reviewId: Int?
    let hasReview: Bool
    let progressSeconds: Int
    let completed: Bool
    let lastEpisode: EpisodeSummary?
    let nextEpisode: EpisodeSummary?

    var id: MediaID { MediaID(mediaType: mediaType, tmdbId: tmdbId) }
    var hasProgress: Bool { progressSeconds > 0 && !completed }
    var isRated: Bool { userRating != nil }

    static func empty(for mediaID: MediaID) -> MediaState {
        MediaState(
            tmdbId: mediaID.tmdbId,
            mediaType: mediaID.mediaType,
            isFavorite: false,
            isInWatchlist: false,
            userRating: nil,
            reviewId: nil,
            hasReview: false,
            progressSeconds: 0,
            completed: false,
            lastEpisode: nil,
            nextEpisode: nil
        )
    }

    // MARK: Optimistic copies

    func settingFavorite(_ value: Bool) -> MediaState {
        copy(isFavorite: value)
    }

    func settingWatchlist(_ value: Bool) -> MediaState {
        copy(isInWatchlist: value)
    }

    func settingRating(_ value: Int?) -> MediaState {
        copy(userRating: value)
    }

    func settingCompleted(_ value: Bool) -> MediaState {
        copy(completed: value)
    }

    private func copy(
        isFavorite: Bool? = nil,
        isInWatchlist: Bool? = nil,
        userRating: Int?? = nil,
        completed: Bool? = nil
    ) -> MediaState {
        MediaState(
            tmdbId: tmdbId,
            mediaType: mediaType,
            isFavorite: isFavorite ?? self.isFavorite,
            isInWatchlist: isInWatchlist ?? self.isInWatchlist,
            userRating: userRating ?? self.userRating,
            reviewId: reviewId,
            hasReview: hasReview,
            progressSeconds: progressSeconds,
            completed: completed ?? self.completed,
            lastEpisode: lastEpisode,
            nextEpisode: nextEpisode
        )
    }
}

// MARK: - Library items

nonisolated struct SavedMedia: Decodable, Hashable, Sendable, Identifiable {
    let media: MediaSummary
    let savedAt: Date

    var id: MediaID { media.id }
}

nonisolated struct MediaRating: Decodable, Hashable, Sendable {
    let media: MediaSummary
    let rating: Int
    let ratedAt: Date

    var id: MediaID { media.id }
}

nonisolated struct CommunityRating: Decodable, Hashable, Sendable {
    let media: MediaSummary
    let averageRating: Double
    let ratingCount: Int
    let userRating: Int?

    var id: MediaID { media.id }
    var averageText: String {
        averageRating > 0
            ? averageRating.formatted(.number.precision(.fractionLength(1)))
            : "NR"
    }
}

// MARK: - Watch history

nonisolated struct WatchHistoryItem: Decodable, Hashable, Sendable, Identifiable {
    let media: MediaSummary
    let progressSeconds: Int
    let completed: Bool
    let lastWatchedAt: Date
    let lastEpisode: EpisodeSummary?
    let nextEpisode: EpisodeSummary?

    var id: MediaID { media.id }
    var hasProgress: Bool { progressSeconds > 0 || lastEpisode != nil }
}

nonisolated struct EpisodeProgress: Decodable, Hashable, Sendable {
    let media: MediaSummary
    let episode: Episode
    let nextEpisode: Episode?
    let showCompleted: Bool

    var id: MediaID { media.id }
}

nonisolated struct EpisodeWatchState: Decodable, Hashable, Sendable {
    let episode: Episode
    let watchedAt: Date
}

nonisolated struct TvHistory: Decodable, Hashable, Sendable {
    let media: MediaSummary
    let episodes: [EpisodeWatchState]
    let nextEpisode: Episode?
    let showCompleted: Bool

    var id: MediaID { media.id }

    var completedEpisodeCount: Int {
        episodes.filter(\.episode.completed).count
    }
}

// MARK: - Request bodies

nonisolated struct RatingRequest: Encodable, Sendable {
    let rating: Int
}

nonisolated struct RecordProgressRequest: Encodable, Sendable {
    let progressSeconds: Int
    let completed: Bool
    let seasonNumber: Int?
    let episodeNumber: Int?

    init(
        progressSeconds: Int,
        completed: Bool,
        seasonNumber: Int? = nil,
        episodeNumber: Int? = nil
    ) {
        self.progressSeconds = progressSeconds
        self.completed = completed
        self.seasonNumber = seasonNumber
        self.episodeNumber = episodeNumber
    }
}

nonisolated struct RecordEpisodeProgressRequest: Encodable, Sendable {
    let progressSeconds: Int
    let completed: Bool
}
