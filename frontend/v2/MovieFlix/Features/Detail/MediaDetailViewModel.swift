import Foundation
import Observation

@MainActor
@Observable
final class MediaDetailViewModel {
    enum Phase: Equatable {
        case loading
        case loaded
        case failed(APIError)
    }

    let mediaID: MediaID

    private let repository: any MediaRepositoryProtocol
    private let libraryRepository: any LibraryRepositoryProtocol

    private(set) var phase: Phase = .loading
    private(set) var details: MediaDetails?
    private(set) var state: MediaState?
    private(set) var communityRating: CommunityRating?

    private(set) var reviews: [Review] = []
    private(set) var reviewsPage = 0
    private(set) var reviewsHasMore = false
    private(set) var isLoadingReviews = false

    private(set) var expandedSeasonNumber: Int?
    private(set) var episodesBySeason: [Int: [Episode]] = [:]
    private(set) var loadingSeasonNumber: Int?
    private(set) var seasonErrors: [Int: APIError] = [:]

    private(set) var isUpdatingFavorite = false
    private(set) var isUpdatingWatchlist = false
    private(set) var updatingEpisodeID: String?

    init(
        mediaID: MediaID,
        repository: any MediaRepositoryProtocol,
        libraryRepository: any LibraryRepositoryProtocol
    ) {
        self.mediaID = mediaID
        self.repository = repository
        self.libraryRepository = libraryRepository
    }

    var isTV: Bool { mediaID.mediaType == .tv }

    // MARK: Loading

    func load(force: Bool = false) async {
        if !force, details != nil { return }

        if details == nil {
            phase = .loading
        }

        do {
            let details = try await repository.details(
                mediaType: mediaID.mediaType,
                tmdbId: mediaID.tmdbId,
                language: nil
            )
            guard !Task.isCancelled else { return }
            self.details = details
            phase = .loaded
            await loadEnrichment()
        } catch let error as APIError {
            guard !Task.isCancelled else { return }
            if error == .cancelled { return }
            if details == nil {
                phase = .failed(error)
            }
        } catch is CancellationError {
            return
        } catch {
            if details == nil {
                phase = .failed(.transport(error.localizedDescription))
            }
        }
    }

    /// State, ratings, and reviews all depend on the title being cached by the
    /// details call. Failures are non-fatal: a title still renders without them.
    private func loadEnrichment() async {
        let repository = self.repository
        let mediaID = self.mediaID

        async let stateResult = Self.capture {
            try await repository.state(
                mediaType: mediaID.mediaType,
                tmdbId: mediaID.tmdbId
            )
        }

        async let ratingResult = Self.capture {
            try await repository.communityRating(
                mediaType: mediaID.mediaType,
                tmdbId: mediaID.tmdbId
            )
        }

        async let reviewsResult = Self.capture {
            try await repository.reviews(
                mediaType: mediaID.mediaType,
                tmdbId: mediaID.tmdbId,
                page: 1,
                pageSize: 8
            )
        }

        let (state, rating, reviews) = await (stateResult, ratingResult, reviewsResult)
        guard !Task.isCancelled else { return }

        if case let .success(value) = state {
            self.state = value
        }
        if case let .success(value) = rating {
            self.communityRating = value
        }
        if case let .success(page) = reviews {
            self.reviews = page.items
            self.reviewsPage = page.page
            self.reviewsHasMore = page.hasMore
        }
    }

    func loadMoreReviews() async {
        guard !isLoadingReviews, reviewsHasMore else { return }
        isLoadingReviews = true
        defer { isLoadingReviews = false }

        do {
            let page = try await repository.reviews(
                mediaType: mediaID.mediaType,
                tmdbId: mediaID.tmdbId,
                page: reviewsPage + 1,
                pageSize: 8
            )
            guard !Task.isCancelled else { return }
            let existing = Set(reviews.map(\.id))
            reviews.append(contentsOf: page.items.filter { !existing.contains($0.id) })
            reviewsPage = page.page
            reviewsHasMore = page.hasMore
        } catch {
            reviewsHasMore = false
        }
    }

    // MARK: Seasons

    func toggleSeason(_ seasonNumber: Int) async {
        if expandedSeasonNumber == seasonNumber {
            expandedSeasonNumber = nil
            return
        }

        guard isTV else { return }

        expandedSeasonNumber = seasonNumber
        seasonErrors[seasonNumber] = nil

        guard episodesBySeason[seasonNumber] == nil,
              loadingSeasonNumber == nil
        else {
            return
        }

        loadingSeasonNumber = seasonNumber
        defer {
            if loadingSeasonNumber == seasonNumber {
                loadingSeasonNumber = nil
            }
        }

        do {
            let season = try await repository.seasonDetails(
                tmdbId: mediaID.tmdbId,
                seasonNumber: seasonNumber,
                language: nil
            )
            guard !Task.isCancelled else { return }
            episodesBySeason[seasonNumber] = season.episodes
        } catch let error as APIError {
            guard !Task.isCancelled, error != .cancelled else { return }
            seasonErrors[seasonNumber] = error
        } catch {
            guard !Task.isCancelled else { return }
            seasonErrors[seasonNumber] = .transport(error.localizedDescription)
        }
    }

    func retrySeason(_ seasonNumber: Int) async {
        episodesBySeason[seasonNumber] = nil
        expandedSeasonNumber = nil
        await toggleSeason(seasonNumber)
    }

    // MARK: Library actions

    /// Toggles favorite membership optimistically and reverts when the write
    /// fails. The caller is responsible for requiring authentication.
    @discardableResult
    func toggleFavorite() async -> APIError? {
        guard !isUpdatingFavorite else { return nil }
        let current = state ?? .empty(for: mediaID)

        let target = !current.isFavorite
        isUpdatingFavorite = true
        state = current.settingFavorite(target)
        defer { isUpdatingFavorite = false }

        do {
            if target {
                _ = try await libraryRepository.addFavorite(mediaID)
            } else {
                try await libraryRepository.removeFavorite(mediaID)
            }
            NotificationCenter.default.post(name: .libraryDidChange, object: nil)
            return nil
        } catch {
            state = current
            return Self.apiError(from: error)
        }
    }

    @discardableResult
    func toggleWatchlist() async -> APIError? {
        guard !isUpdatingWatchlist else { return nil }
        let current = state ?? .empty(for: mediaID)

        let target = !current.isInWatchlist
        isUpdatingWatchlist = true
        state = current.settingWatchlist(target)
        defer { isUpdatingWatchlist = false }

        do {
            if target {
                _ = try await libraryRepository.addToWatchlist(mediaID)
            } else {
                try await libraryRepository.removeFromWatchlist(mediaID)
            }
            NotificationCenter.default.post(name: .libraryDidChange, object: nil)
            return nil
        } catch {
            state = current
            return Self.apiError(from: error)
        }
    }

    /// Marks an episode watched or unwatched. The row updates immediately; the
    /// server response replaces the optimistic copy with authoritative progress.
    @discardableResult
    func setWatched(_ watched: Bool, episode: Episode) async -> APIError? {
        guard updatingEpisodeID == nil else { return nil }
        guard let season = episodesBySeason[episode.seasonNumber],
              let index = season.firstIndex(where: { $0.episodeNumber == episode.episodeNumber })
        else {
            return nil
        }

        let previous = season[index]
        updatingEpisodeID = episode.id
        applyEpisode(previous.settingWatched(watched), in: previous.seasonNumber)
        defer { updatingEpisodeID = nil }

        do {
            let result = try await libraryRepository.recordEpisodeProgress(
                tmdbId: mediaID.tmdbId,
                seasonNumber: episode.seasonNumber,
                episodeNumber: episode.episodeNumber,
                progressSeconds: watched ? episode.progressSeconds : 0,
                completed: watched
            )
            applyEpisode(result.episode, in: result.episode.seasonNumber)
            await refreshState()
            return nil
        } catch {
            applyEpisode(previous, in: previous.seasonNumber)
            return Self.apiError(from: error)
        }
    }

    /// Re-reads per-title state so derived UI (next episode, completion) stays
    /// accurate after a write. Failures are ignored; the cached value remains.
    func refreshState() async {
        guard let value = try? await repository.state(
            mediaType: mediaID.mediaType,
            tmdbId: mediaID.tmdbId
        ) else {
            return
        }
        state = value
    }

    private func applyEpisode(_ episode: Episode, in seasonNumber: Int) {
        guard let season = episodesBySeason[seasonNumber],
              let index = season.firstIndex(where: { $0.episodeNumber == episode.episodeNumber })
        else {
            return
        }
        var updated = season
        updated[index] = episode
        episodesBySeason[seasonNumber] = updated
    }

    // MARK: Helpers

    private nonisolated static func apiError(from error: Error) -> APIError {
        if let error = error as? APIError {
            return error
        }
        return .transport(error.localizedDescription)
    }

    private nonisolated static func capture<T: Sendable>(
        _ operation: @Sendable () async throws -> T
    ) async -> Result<T, APIError> {
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
}
