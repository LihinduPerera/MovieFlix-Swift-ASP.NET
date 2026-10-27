import Foundation

/// A single API call, independent of transport. Endpoint factories below are
/// the only place that knows API paths, keeping the contract centralized.
nonisolated struct APIEndpoint: Sendable {
    var method: HTTPMethod
    var path: String
    var queryItems: [URLQueryItem]
    var body: AnyEncodable?
    var requiresAuth: Bool
    var headers: [String: String]
    var cachePolicy: URLRequest.CachePolicy

    init(
        method: HTTPMethod = .get,
        path: String,
        queryItems: [URLQueryItem] = [],
        body: AnyEncodable? = nil,
        requiresAuth: Bool = false,
        headers: [String: String] = [:],
        cachePolicy: URLRequest.CachePolicy = .useProtocolCachePolicy
    ) {
        self.method = method
        self.path = path
        self.queryItems = queryItems
        self.body = body
        self.requiresAuth = requiresAuth
        self.headers = headers
        self.cachePolicy = cachePolicy
    }

    func url(relativeTo baseURL: URL) throws -> URL {
        guard var components = URLComponents(url: baseURL, resolvingAgainstBaseURL: false) else {
            throw APIError.invalidURL
        }

        var basePath = components.path
        if basePath.hasSuffix("/") {
            basePath.removeLast()
        }

        let suffix = path.hasPrefix("/") ? path : "/\(path)"
        components.path = basePath + suffix
        components.queryItems = queryItems.isEmpty ? nil : queryItems

        guard let url = components.url else { throw APIError.invalidURL }
        return url
    }

    static func query(_ pairs: [(String, String?)]) -> [URLQueryItem] {
        pairs.compactMap { pair in
            guard let value = pair.1, !value.isEmpty else { return nil }
            return URLQueryItem(name: pair.0, value: value)
        }
    }
}

// MARK: - Public discovery

nonisolated extension APIEndpoint {
    static func search(
        query: String,
        type: SearchType = .multi,
        page: Int = 1,
        language: String? = nil,
        region: String? = nil
    ) -> APIEndpoint {
        APIEndpoint(
            path: "/api/v1/search",
            queryItems: APIEndpoint.query([
                ("query", query),
                ("type", type.rawValue),
                ("page", String(page)),
                ("language", language),
                ("region", region)
            ])
        )
    }

    static func trending(
        mediaType: MediaTypeFilter = .all,
        window: TrendingWindow = .week,
        page: Int = 1,
        language: String? = nil
    ) -> APIEndpoint {
        APIEndpoint(
            path: "/api/v1/media/trending",
            queryItems: APIEndpoint.query([
                ("mediaType", mediaType.rawValue),
                ("window", window.rawValue),
                ("page", String(page)),
                ("language", language)
            ])
        )
    }

    static func feed(
        _ feed: MediaFeed,
        mediaType: MediaType,
        page: Int = 1,
        language: String? = nil,
        region: String? = nil
    ) -> APIEndpoint {
        APIEndpoint(
            path: "/api/v1/media/\(mediaType.rawValue)/\(feed.rawValue)",
            queryItems: APIEndpoint.query([
                ("page", String(page)),
                ("language", language),
                ("region", region)
            ])
        )
    }

    static func discover(
        mediaType: MediaType,
        genreId: Int? = nil,
        year: Int? = nil,
        minRating: Double? = nil,
        sortBy: String? = nil,
        page: Int = 1,
        language: String? = nil,
        region: String? = nil
    ) -> APIEndpoint {
        let queryItems = APIEndpoint.query([
            ("genreId", genreId.map(String.init)),
            ("year", year.map(String.init)),
            ("minRating", minRating.map { String($0) }),
            ("sortBy", sortBy),
            ("page", String(page)),
            ("language", language),
            ("region", region)
        ])

        return APIEndpoint(
            path: "/api/v1/media/\(mediaType.rawValue)/discover",
            queryItems: queryItems
        )
    }

    static func genres(mediaType: MediaType, language: String? = nil) -> APIEndpoint {
        APIEndpoint(
            path: "/api/v1/genres/\(mediaType.rawValue)",
            queryItems: APIEndpoint.query([("language", language)])
        )
    }

    // MARK: Details

    static func details(
        mediaType: MediaType,
        tmdbId: Int,
        language: String? = nil
    ) -> APIEndpoint {
        APIEndpoint(
            path: "/api/v1/media/\(mediaType.rawValue)/\(tmdbId)",
            queryItems: APIEndpoint.query([("language", language)])
        )
    }

    static func seasons(
        tmdbId: Int,
        language: String? = nil
    ) -> APIEndpoint {
        APIEndpoint(
            path: "/api/v1/media/tv/\(tmdbId)/seasons",
            queryItems: APIEndpoint.query([("language", language)])
        )
    }

    static func seasonDetails(
        tmdbId: Int,
        seasonNumber: Int,
        language: String? = nil
    ) -> APIEndpoint {
        APIEndpoint(
            path: "/api/v1/media/tv/\(tmdbId)/seasons/\(seasonNumber)",
            queryItems: APIEndpoint.query([("language", language)])
        )
    }

    // MARK: State, ratings, reviews

    static func state(mediaType: MediaType, tmdbId: Int) -> APIEndpoint {
        APIEndpoint(path: "/api/v1/media/\(mediaType.rawValue)/\(tmdbId)/state")
    }

    static func communityRating(mediaType: MediaType, tmdbId: Int) -> APIEndpoint {
        APIEndpoint(path: "/api/v1/media/\(mediaType.rawValue)/\(tmdbId)/ratings")
    }

    static func reviews(
        mediaType: MediaType,
        tmdbId: Int,
        page: Int = 1,
        pageSize: Int = 20
    ) -> APIEndpoint {
        APIEndpoint(
            path: "/api/v1/media/\(mediaType.rawValue)/\(tmdbId)/reviews",
            queryItems: APIEndpoint.query([
                ("page", String(page)),
                ("pageSize", String(pageSize))
            ])
        )
    }

    static func createReview(
        mediaType: MediaType,
        tmdbId: Int,
        content: String
    ) -> APIEndpoint {
        APIEndpoint(
            method: .post,
            path: "/api/v1/media/\(mediaType.rawValue)/\(tmdbId)/reviews",
            body: AnyEncodable(CreateReviewRequest(content: content)),
            requiresAuth: true
        )
    }

    static func updateReview(reviewId: Int, content: String) -> APIEndpoint {
        APIEndpoint(
            method: .put,
            path: "/api/v1/reviews/\(reviewId)",
            body: AnyEncodable(UpdateReviewRequest(content: content)),
            requiresAuth: true
        )
    }

    static func deleteReview(reviewId: Int) -> APIEndpoint {
        APIEndpoint(method: .delete, path: "/api/v1/reviews/\(reviewId)", requiresAuth: true)
    }
}

// MARK: - Authentication

nonisolated extension APIEndpoint {
    static func register(email: String, displayName: String, password: String) -> APIEndpoint {
        APIEndpoint(
            method: .post,
            path: "/api/v1/auth/register",
            body: AnyEncodable(RegisterRequest(email: email, displayName: displayName, password: password))
        )
    }

    static func login(email: String, password: String) -> APIEndpoint {
        APIEndpoint(
            method: .post,
            path: "/api/v1/auth/login",
            body: AnyEncodable(LoginRequest(email: email, password: password))
        )
    }

    static func refresh(refreshToken: String) -> APIEndpoint {
        APIEndpoint(
            method: .post,
            path: "/api/v1/auth/refresh",
            body: AnyEncodable(RefreshRequest(refreshToken: refreshToken))
        )
    }

    static func logout() -> APIEndpoint {
        APIEndpoint(method: .post, path: "/api/v1/auth/logout", requiresAuth: true)
    }
}

// MARK: - Profile

nonisolated extension APIEndpoint {
    static func me() -> APIEndpoint {
        APIEndpoint(path: "/api/v1/me", requiresAuth: true)
    }

    static func updateMe(displayName: String) -> APIEndpoint {
        APIEndpoint(
            method: .patch,
            path: "/api/v1/me",
            body: AnyEncodable(UpdateProfileRequest(displayName: displayName)),
            requiresAuth: true
        )
    }

    static func stats() -> APIEndpoint {
        APIEndpoint(path: "/api/v1/me/stats", requiresAuth: true)
    }

    static func preferences() -> APIEndpoint {
        APIEndpoint(path: "/api/v1/me/preferences", requiresAuth: true)
    }

    static func updatePreferences(_ preferences: UpdatePreferencesRequest) -> APIEndpoint {
        APIEndpoint(
            method: .put,
            path: "/api/v1/me/preferences",
            body: AnyEncodable(preferences),
            requiresAuth: true
        )
    }
}

// MARK: - Library

nonisolated extension APIEndpoint {
    static func favorites(
        mediaType: MediaType? = nil,
        page: Int = 1,
        pageSize: Int = 20,
        sort: LibrarySort = .addedDesc
    ) -> APIEndpoint {
        APIEndpoint(
            path: "/api/v1/me/favorites",
            queryItems: APIEndpoint.query([
                ("mediaType", mediaType?.rawValue),
                ("page", String(page)),
                ("pageSize", String(pageSize)),
                ("sort", sort.rawValue)
            ]),
            requiresAuth: true
        )
    }

    static func addFavorite(mediaType: MediaType, tmdbId: Int) -> APIEndpoint {
        APIEndpoint(
            method: .put,
            path: "/api/v1/me/favorites/\(mediaType.rawValue)/\(tmdbId)",
            requiresAuth: true
        )
    }

    static func removeFavorite(mediaType: MediaType, tmdbId: Int) -> APIEndpoint {
        APIEndpoint(
            method: .delete,
            path: "/api/v1/me/favorites/\(mediaType.rawValue)/\(tmdbId)",
            requiresAuth: true
        )
    }

    static func watchlist(
        mediaType: MediaType? = nil,
        page: Int = 1,
        pageSize: Int = 20,
        sort: LibrarySort = .addedDesc
    ) -> APIEndpoint {
        APIEndpoint(
            path: "/api/v1/me/watchlist",
            queryItems: APIEndpoint.query([
                ("mediaType", mediaType?.rawValue),
                ("page", String(page)),
                ("pageSize", String(pageSize)),
                ("sort", sort.rawValue)
            ]),
            requiresAuth: true
        )
    }

    static func addToWatchlist(mediaType: MediaType, tmdbId: Int) -> APIEndpoint {
        APIEndpoint(
            method: .put,
            path: "/api/v1/me/watchlist/\(mediaType.rawValue)/\(tmdbId)",
            requiresAuth: true
        )
    }

    static func removeFromWatchlist(mediaType: MediaType, tmdbId: Int) -> APIEndpoint {
        APIEndpoint(
            method: .delete,
            path: "/api/v1/me/watchlist/\(mediaType.rawValue)/\(tmdbId)",
            requiresAuth: true
        )
    }

    static func myRating(mediaType: MediaType, tmdbId: Int) -> APIEndpoint {
        APIEndpoint(
            path: "/api/v1/me/ratings/\(mediaType.rawValue)/\(tmdbId)",
            requiresAuth: true
        )
    }

    static func rate(mediaType: MediaType, tmdbId: Int, rating: Int) -> APIEndpoint {
        APIEndpoint(
            method: .put,
            path: "/api/v1/me/ratings/\(mediaType.rawValue)/\(tmdbId)",
            body: AnyEncodable(RatingRequest(rating: rating)),
            requiresAuth: true
        )
    }

    static func removeRating(mediaType: MediaType, tmdbId: Int) -> APIEndpoint {
        APIEndpoint(
            method: .delete,
            path: "/api/v1/me/ratings/\(mediaType.rawValue)/\(tmdbId)",
            requiresAuth: true
        )
    }

    static func history(
        mediaType: MediaType? = nil,
        page: Int = 1,
        pageSize: Int = 20
    ) -> APIEndpoint {
        APIEndpoint(
            path: "/api/v1/me/history",
            queryItems: APIEndpoint.query([
                ("mediaType", mediaType?.rawValue),
                ("page", String(page)),
                ("pageSize", String(pageSize))
            ]),
            requiresAuth: true
        )
    }

    static func continueWatching(limit: Int = 10) -> APIEndpoint {
        APIEndpoint(
            path: "/api/v1/me/history/continue",
            queryItems: APIEndpoint.query([("limit", String(limit))]),
            requiresAuth: true
        )
    }

    static func tvHistory(tmdbId: Int) -> APIEndpoint {
        APIEndpoint(path: "/api/v1/me/history/tv/\(tmdbId)", requiresAuth: true)
    }

    static func recordProgress(
        mediaType: MediaType,
        tmdbId: Int,
        request: RecordProgressRequest
    ) -> APIEndpoint {
        APIEndpoint(
            method: .put,
            path: "/api/v1/me/history/\(mediaType.rawValue)/\(tmdbId)",
            body: AnyEncodable(request),
            requiresAuth: true
        )
    }

    static func recordEpisodeProgress(
        tmdbId: Int,
        seasonNumber: Int,
        episodeNumber: Int,
        request: RecordEpisodeProgressRequest
    ) -> APIEndpoint {
        APIEndpoint(
            method: .put,
            path: "/api/v1/me/history/tv/\(tmdbId)/episodes/\(seasonNumber)/\(episodeNumber)",
            body: AnyEncodable(request),
            requiresAuth: true
        )
    }

    static func clearTitleHistory(mediaType: MediaType, tmdbId: Int) -> APIEndpoint {
        APIEndpoint(
            method: .delete,
            path: "/api/v1/me/history/\(mediaType.rawValue)/\(tmdbId)",
            requiresAuth: true
        )
    }

    static func clearHistory() -> APIEndpoint {
        APIEndpoint(method: .delete, path: "/api/v1/me/history", requiresAuth: true)
    }
}

// MARK: - Owned reviews

nonisolated extension APIEndpoint {
    static func myReviews(page: Int = 1, pageSize: Int = 20) -> APIEndpoint {
        APIEndpoint(
            path: "/api/v1/me/reviews",
            queryItems: APIEndpoint.query([
                ("page", String(page)),
                ("pageSize", String(pageSize))
            ]),
            requiresAuth: true
        )
    }
}

// MARK: - Collections

nonisolated extension APIEndpoint {
    static func collections(page: Int = 1, pageSize: Int = 20) -> APIEndpoint {
        APIEndpoint(
            path: "/api/v1/me/collections",
            queryItems: APIEndpoint.query([
                ("page", String(page)),
                ("pageSize", String(pageSize))
            ]),
            requiresAuth: true
        )
    }

    static func createCollection(name: String, description: String) -> APIEndpoint {
        APIEndpoint(
            method: .post,
            path: "/api/v1/me/collections",
            body: AnyEncodable(CollectionRequest(name: name, description: description)),
            requiresAuth: true
        )
    }

    static func collection(id: Int) -> APIEndpoint {
        APIEndpoint(path: "/api/v1/collections/\(id)")
    }

    static func updateCollection(id: Int, name: String, description: String) -> APIEndpoint {
        APIEndpoint(
            method: .put,
            path: "/api/v1/me/collections/\(id)",
            body: AnyEncodable(CollectionRequest(name: name, description: description)),
            requiresAuth: true
        )
    }

    static func deleteCollection(id: Int) -> APIEndpoint {
        APIEndpoint(method: .delete, path: "/api/v1/me/collections/\(id)", requiresAuth: true)
    }

    static func addCollectionItem(id: Int, mediaType: MediaType, tmdbId: Int) -> APIEndpoint {
        APIEndpoint(
            method: .put,
            path: "/api/v1/me/collections/\(id)/items/\(mediaType.rawValue)/\(tmdbId)",
            requiresAuth: true
        )
    }

    static func removeCollectionItem(id: Int, mediaType: MediaType, tmdbId: Int) -> APIEndpoint {
        APIEndpoint(
            method: .delete,
            path: "/api/v1/me/collections/\(id)/items/\(mediaType.rawValue)/\(tmdbId)",
            requiresAuth: true
        )
    }
}

// MARK: - Search history

nonisolated extension APIEndpoint {
    static func searchHistory(limit: Int = 10) -> APIEndpoint {
        APIEndpoint(
            path: "/api/v1/me/search-history",
            queryItems: APIEndpoint.query([("limit", String(limit))]),
            requiresAuth: true
        )
    }

    static func recordSearch(query: String) -> APIEndpoint {
        APIEndpoint(
            method: .post,
            path: "/api/v1/me/search-history",
            body: AnyEncodable(RecordSearchRequest(query: query)),
            requiresAuth: true
        )
    }

    static func deleteSearchHistoryEntry(id: Int) -> APIEndpoint {
        APIEndpoint(
            method: .delete,
            path: "/api/v1/me/search-history/\(id)",
            requiresAuth: true
        )
    }

    static func clearSearchHistory() -> APIEndpoint {
        APIEndpoint(method: .delete, path: "/api/v1/me/search-history", requiresAuth: true)
    }
}
