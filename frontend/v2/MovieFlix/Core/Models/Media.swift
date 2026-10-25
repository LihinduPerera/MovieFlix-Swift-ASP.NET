import Foundation

// MARK: - Identity

nonisolated enum MediaType: String, Codable, CaseIterable, Sendable, Identifiable {
    case movie
    case tv

    var id: String { rawValue }

    var displayName: String {
        switch self {
        case .movie: "Movie"
        case .tv: "TV Show"
        }
    }

    var pluralDisplayName: String {
        switch self {
        case .movie: "Movies"
        case .tv: "TV Shows"
        }
    }

    var systemImage: String {
        switch self {
        case .movie: "film"
        case .tv: "tv"
        }
    }
}

/// The public identity of a title. Local database ids are never exposed.
nonisolated struct MediaID: Hashable, Codable, Sendable, Identifiable, CustomStringConvertible {
    let mediaType: MediaType
    let tmdbId: Int

    var id: String { "\(mediaType.rawValue)-\(tmdbId)" }
    var description: String { id }
    var routePath: String { "\(mediaType.rawValue)/\(tmdbId)" }
}

// MARK: - Query enums

nonisolated enum MediaTypeFilter: String, Codable, CaseIterable, Sendable, Identifiable {
    case all
    case movie
    case tv

    var id: String { rawValue }

    var displayName: String {
        switch self {
        case .all: "All"
        case .movie: "Movies"
        case .tv: "TV"
        }
    }

    var mediaType: MediaType? {
        switch self {
        case .all: nil
        case .movie: .movie
        case .tv: .tv
        }
    }
}

nonisolated enum SearchType: String, Codable, CaseIterable, Sendable, Identifiable {
    case multi
    case movie
    case tv

    var id: String { rawValue }

    var displayName: String {
        switch self {
        case .multi: "All"
        case .movie: "Movies"
        case .tv: "TV"
        }
    }
}

nonisolated enum TrendingWindow: String, Codable, CaseIterable, Sendable, Identifiable {
    case day
    case week

    var id: String { rawValue }
    var displayName: String { self == .day ? "Today" : "This Week" }
}

/// Public TMDB-backed lists. `rawValue` is the path component in the API.
nonisolated enum MediaFeed: String, CaseIterable, Sendable, Identifiable {
    case popular
    case topRated = "top-rated"
    case nowPlaying = "now-playing"
    case upcoming
    case onTheAir = "on-the-air"
    case airingToday = "airing-today"

    var id: String { rawValue }

    var supportedMediaTypes: Set<MediaType> {
        switch self {
        case .popular, .topRated:
            [.movie, .tv]
        case .nowPlaying, .upcoming:
            [.movie]
        case .onTheAir, .airingToday:
            [.tv]
        }
    }

    func supports(_ mediaType: MediaType) -> Bool {
        supportedMediaTypes.contains(mediaType)
    }
}

nonisolated enum LibrarySort: String, Codable, CaseIterable, Sendable, Identifiable {
    case addedDesc = "added_desc"
    case addedAsc = "added_asc"
    case titleAsc = "title_asc"
    case titleDesc = "title_desc"
    case releaseDesc = "release_desc"

    var id: String { rawValue }

    var displayName: String {
        switch self {
        case .addedDesc: "Recently Added"
        case .addedAsc: "Oldest Added"
        case .titleAsc: "Title A–Z"
        case .titleDesc: "Title Z–A"
        case .releaseDesc: "Newest Release"
        }
    }
}

nonisolated enum ThemePreference: String, Codable, CaseIterable, Sendable, Identifiable {
    case system
    case light
    case dark

    var id: String { rawValue }

    var displayName: String {
        switch self {
        case .system: "System"
        case .light: "Light"
        case .dark: "Dark"
        }
    }
}

// MARK: - Summary

nonisolated struct MediaSummary: Decodable, Hashable, Sendable, Identifiable {
    let tmdbId: Int
    let mediaType: MediaType
    let title: String
    let originalTitle: String
    let overview: String
    let posterPath: String?
    let backdropPath: String?
    let releaseDate: LocalDate?
    let voteAverage: Double
    let voteCount: Int
    let popularity: Double

    var id: MediaID { MediaID(mediaType: mediaType, tmdbId: tmdbId) }

    var yearText: String? { releaseDate.map { String($0.year) } }

    var metadataLine: String {
        [yearText, mediaType.displayName].compactMap { $0 }.joined(separator: " • ")
    }

    var ratingText: String {
        voteAverage > 0 ? voteAverage.formatted(.number.precision(.fractionLength(1))) : "NR"
    }

    var hasRating: Bool { voteAverage > 0 }
}

// MARK: - Details

nonisolated struct Genre: Decodable, Hashable, Sendable, Identifiable {
    let id: Int
    let name: String
}

nonisolated struct CastMember: Decodable, Hashable, Sendable, Identifiable {
    let id: Int
    let name: String
    let character: String?
    let profilePath: String?
    let order: Int

    var roleText: String { character?.isEmpty == false ? character! : "Cast" }
}

nonisolated struct CrewMember: Decodable, Hashable, Sendable {
    let id: Int
    let name: String
    let job: String
    let department: String
    let profilePath: String?

    /// Crew ids are not unique across jobs in every payload; pair with the job.
    var uniqueID: String { "\(id)-\(job)" }
}

nonisolated struct Credits: Decodable, Hashable, Sendable {
    let cast: [CastMember]
    let crew: [CrewMember]

    var directors: [CrewMember] { crew.filter { $0.job == "Director" } }
    var writers: [CrewMember] { crew.filter { ["Writer", "Screenplay", "Story"].contains($0.job) } }
}

nonisolated struct Video: Decodable, Hashable, Sendable, Identifiable {
    let id: String
    let key: String
    let name: String
    let site: String
    let type: String
    let official: Bool
    let language: String?
    let publishedAt: Date?

    var isYouTube: Bool { site.caseInsensitiveCompare("YouTube") == .orderedSame }
    var isTrailer: Bool { type.caseInsensitiveCompare("Trailer") == .orderedSame }

    var youtubeURL: URL? {
        guard isYouTube else { return nil }
        return URL(string: "https://www.youtube.com/watch?v=\(key)")
    }
}

nonisolated struct SeasonSummary: Decodable, Hashable, Sendable, Identifiable {
    let seasonNumber: Int
    let name: String
    let overview: String?
    let posterPath: String?
    let airDate: LocalDate?
    let episodeCount: Int

    var id: Int { seasonNumber }
}

nonisolated struct EpisodeSummary: Decodable, Hashable, Sendable, Identifiable {
    let seasonNumber: Int
    let episodeNumber: Int
    let name: String
    let stillPath: String?

    var id: String { "\(seasonNumber)-\(episodeNumber)" }
    var code: String { String(format: "S%02dE%02d", seasonNumber, episodeNumber) }
}

nonisolated struct Episode: Decodable, Hashable, Sendable, Identifiable {
    let seasonNumber: Int
    let episodeNumber: Int
    let name: String
    let stillPath: String?
    let tmdbId: Int?
    let overview: String?
    let airDate: LocalDate?
    let runtimeMinutes: Int?
    let voteAverage: Double
    let voteCount: Int
    let progressSeconds: Int
    let completed: Bool

    var id: String { "\(seasonNumber)-\(episodeNumber)" }
    var code: String { String(format: "S%02dE%02d", seasonNumber, episodeNumber) }
    var summary: EpisodeSummary {
        EpisodeSummary(
            seasonNumber: seasonNumber,
            episodeNumber: episodeNumber,
            name: name,
            stillPath: stillPath
        )
    }

    var runtimeText: String? {
        guard let runtimeMinutes, runtimeMinutes > 0 else { return nil }
        return "\(runtimeMinutes) min"
    }

    /// Optimistic copy used before the server confirms an episode watch toggle.
    func settingWatched(_ watched: Bool) -> Episode {
        Episode(
            seasonNumber: seasonNumber,
            episodeNumber: episodeNumber,
            name: name,
            stillPath: stillPath,
            tmdbId: tmdbId,
            overview: overview,
            airDate: airDate,
            runtimeMinutes: runtimeMinutes,
            voteAverage: voteAverage,
            voteCount: voteCount,
            progressSeconds: watched
                ? max(progressSeconds, (runtimeMinutes ?? 0) * 60)
                : 0,
            completed: watched
        )
    }
}

nonisolated struct SeasonDetails: Decodable, Hashable, Sendable, Identifiable {
    let seasonNumber: Int
    let name: String
    let overview: String?
    let posterPath: String?
    let airDate: LocalDate?
    let episodes: [Episode]

    var id: Int { seasonNumber }
}

nonisolated struct MediaDetails: Decodable, Hashable, Sendable, Identifiable {
    let tmdbId: Int
    let mediaType: MediaType
    let title: String
    let originalTitle: String
    let overview: String
    let posterPath: String?
    let backdropPath: String?
    let releaseDate: LocalDate?
    let voteAverage: Double
    let voteCount: Int
    let popularity: Double

    let tagline: String?
    let originalLanguage: String
    let adult: Bool
    let status: String?
    let runtimeMinutes: Int?
    let numberOfSeasons: Int?
    let numberOfEpisodes: Int?
    let firstAirDate: LocalDate?
    let lastAirDate: LocalDate?
    let genres: [Genre]
    let credits: Credits
    let videos: [Video]
    let similar: [MediaSummary]
    let seasons: [SeasonSummary]

    var id: MediaID { MediaID(mediaType: mediaType, tmdbId: tmdbId) }

    var summary: MediaSummary {
        MediaSummary(
            tmdbId: tmdbId,
            mediaType: mediaType,
            title: title,
            originalTitle: originalTitle,
            overview: overview,
            posterPath: posterPath,
            backdropPath: backdropPath,
            releaseDate: releaseDate,
            voteAverage: voteAverage,
            voteCount: voteCount,
            popularity: popularity
        )
    }

    var yearText: String? {
        (releaseDate ?? firstAirDate).map { String($0.year) }
    }

    var runtimeText: String? {
        if let runtimeMinutes, runtimeMinutes > 0 {
            return "\(runtimeMinutes) min"
        }
        if let numberOfSeasons, numberOfSeasons > 0 {
            return numberOfSeasons == 1 ? "1 season" : "\(numberOfSeasons) seasons"
        }
        return nil
    }

    var episodesText: String? {
        guard let numberOfEpisodes, numberOfEpisodes > 0 else { return nil }
        return numberOfEpisodes == 1 ? "1 episode" : "\(numberOfEpisodes) episodes"
    }

    var metadataLine: String {
        [yearText, mediaType.displayName, runtimeText]
            .compactMap { $0 }
            .joined(separator: " • ")
    }

    var ratingText: String {
        voteAverage > 0 ? voteAverage.formatted(.number.precision(.fractionLength(1))) : "NR"
    }

    var trailer: Video? {
        videos.first(where: { $0.isYouTube && $0.isTrailer && $0.official })
            ?? videos.first(where: { $0.isYouTube && $0.isTrailer })
            ?? videos.first(where: { $0.isYouTube })
    }

    var topCast: [CastMember] {
        Array(credits.cast.sorted { $0.order < $1.order }.prefix(20))
    }

    var directorNames: String? {
        let names = credits.directors.map(\.name)
        return names.isEmpty ? nil : names.joined(separator: ", ")
    }
}
