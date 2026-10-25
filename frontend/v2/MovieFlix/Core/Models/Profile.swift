import Foundation

nonisolated struct UserStats: Decodable, Hashable, Sendable {
    let favorites: Int
    let watchlist: Int
    let ratings: Int
    let reviews: Int
    let collections: Int
    let moviesWatched: Int
    let showsWatched: Int
    let episodesWatched: Int
}

nonisolated struct UpdateProfileRequest: Encodable, Sendable {
    let displayName: String
}

nonisolated struct Preferences: Decodable, Hashable, Sendable {
    let preferredLanguage: String
    let preferredRegion: String
    let theme: ThemePreference
    let autoPlayTrailers: Bool
    let emailNotifications: Bool
    let matureContentFilter: Bool
    let updatedAt: Date
}

nonisolated struct UpdatePreferencesRequest: Encodable, Sendable {
    let preferredLanguage: String
    let preferredRegion: String
    let theme: ThemePreference
    let autoPlayTrailers: Bool
    let emailNotifications: Bool
    let matureContentFilter: Bool
}

nonisolated struct SearchHistoryEntry: Decodable, Hashable, Sendable, Identifiable {
    let id: Int
    let query: String
    let searchedAt: Date
}

nonisolated struct RecordSearchRequest: Encodable, Sendable {
    let query: String
}
