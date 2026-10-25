import Foundation
import Observation

// MARK: - Sections

nonisolated enum HomeSectionID: String, Sendable, CaseIterable, Identifiable {
    case trending
    case popularMovies
    case popularShows
    case nowPlaying
    case airingToday

    var id: String { rawValue }

    var title: String {
        switch self {
        case .trending: "Trending This Week"
        case .popularMovies: "Popular Movies"
        case .popularShows: "Popular TV Shows"
        case .nowPlaying: "In Theatres"
        case .airingToday: "Airing Today"
        }
    }
}

nonisolated struct HomeSection: Identifiable, Sendable {
    let id: HomeSectionID
    let items: [MediaSummary]
}

/// A feed definition is deliberately outside the view model: feed closures run
/// in detached child tasks and therefore must not inherit MainActor isolation.
nonisolated struct HomeFeedSpec: Sendable {
    let section: HomeSectionID
    let fetch: @Sendable (any MediaRepositoryProtocol) async throws -> [MediaSummary]
}

nonisolated enum HomeFeeds {
    static let all: [HomeFeedSpec] = [
        HomeFeedSpec(section: .trending) { repository in
            try await repository.trending(
                mediaType: .all,
                window: .week,
                page: 1,
                language: nil
            ).items
        },
        HomeFeedSpec(section: .popularMovies) { repository in
            try await repository.feed(
                .popular,
                mediaType: .movie,
                page: 1,
                language: nil,
                region: nil
            ).items
        },
        HomeFeedSpec(section: .popularShows) { repository in
            try await repository.feed(
                .popular,
                mediaType: .tv,
                page: 1,
                language: nil,
                region: nil
            ).items
        },
        HomeFeedSpec(section: .nowPlaying) { repository in
            try await repository.feed(
                .nowPlaying,
                mediaType: .movie,
                page: 1,
                language: nil,
                region: nil
            ).items
        },
        HomeFeedSpec(section: .airingToday) { repository in
            try await repository.feed(
                .airingToday,
                mediaType: .tv,
                page: 1,
                language: nil,
                region: nil
            ).items
        }
    ]
}

// MARK: - View model

@MainActor
@Observable
final class HomeViewModel {
    enum Phase: Equatable {
        case idle
        case loading
        case loaded
        case failed(APIError)
    }

    private let repository: any MediaRepositoryProtocol
    private let libraryRepository: any LibraryRepositoryProtocol

    private(set) var phase: Phase = .idle
    private(set) var hero: MediaSummary?
    private(set) var sections: [HomeSection] = []
    private(set) var partialErrorMessage: String?
    private(set) var isRefreshing = false

    private(set) var heroIsFavorite = false
    private(set) var heroIsInWatchlist = false
    private(set) var isUpdatingHeroFavorite = false
    private(set) var isUpdatingHeroWatchlist = false

    init(
        repository: any MediaRepositoryProtocol,
        libraryRepository: any LibraryRepositoryProtocol
    ) {
        self.repository = repository
        self.libraryRepository = libraryRepository
    }

    func load(force: Bool = false) async {
        guard !isRefreshing else { return }
        if !force, phase == .loaded { return }

        let hasContent = !sections.isEmpty
        if !hasContent {
            phase = .loading
        }
        isRefreshing = true
        partialErrorMessage = nil

        let repository = self.repository
        let specs = HomeFeeds.all

        let results = await withTaskGroup(
            of: HomeSectionResult.self,
            returning: [HomeSectionResult].self
        ) { group in
            for spec in specs {
                group.addTask {
                    do {
                        let items = try await spec.fetch(repository)
                        return HomeSectionResult(section: spec.section, result: .success(items))
                    } catch let error as APIError {
                        return HomeSectionResult(section: spec.section, result: .failure(error))
                    } catch is CancellationError {
                        return HomeSectionResult(section: spec.section, result: .failure(.cancelled))
                    } catch {
                        return HomeSectionResult(
                            section: spec.section,
                            result: .failure(.transport(error.localizedDescription))
                        )
                    }
                }
            }

            var collected: [HomeSectionResult] = []
            for await result in group {
                collected.append(result)
            }
            return collected
        }

        isRefreshing = false

        guard !Task.isCancelled else { return }

        var loadedSections: [HomeSection] = []
        var failures: [HomeSectionID: APIError] = [:]

        for result in results {
            switch result.result {
            case let .success(items):
                if !items.isEmpty {
                    loadedSections.append(HomeSection(id: result.section, items: items))
                }
            case let .failure(error):
                if error != .cancelled {
                    failures[result.section] = error
                }
            }
        }

        loadedSections.sort { lhs, rhs in
            (Self.order[lhs.id] ?? 0) < (Self.order[rhs.id] ?? 0)
        }

        let trending = loadedSections.first(where: { $0.id == .trending })?.items ?? []
        let fallback = loadedSections.flatMap(\.items)
        hero = trending.first(where: { $0.backdropPath != nil })
            ?? trending.first
            ?? fallback.first

        sections = loadedSections
        phase = loadedSections.isEmpty ? .failed(primaryFailure(failures)) : .loaded

        if !failures.isEmpty, !loadedSections.isEmpty {
            partialErrorMessage = "Some shelves couldn't load. Pull to refresh."
        }

        await refreshHeroState()
    }

    // MARK: Hero library state

    /// The state endpoint is guest-safe and returns defaults for anonymous
    /// callers, so this never fails the home screen.
    func refreshHeroState() async {
        guard let hero else {
            heroIsFavorite = false
            heroIsInWatchlist = false
            return
        }

        guard let state = try? await repository.state(
            mediaType: hero.mediaType,
            tmdbId: hero.tmdbId
        ) else {
            return
        }

        heroIsFavorite = state.isFavorite
        heroIsInWatchlist = state.isInWatchlist
    }

    @discardableResult
    func toggleHeroFavorite() async -> APIError? {
        guard let hero, !isUpdatingHeroFavorite else { return nil }
        let target = !heroIsFavorite

        isUpdatingHeroFavorite = true
        heroIsFavorite = target
        defer { isUpdatingHeroFavorite = false }

        do {
            if target {
                _ = try await libraryRepository.addFavorite(hero.id)
            } else {
                try await libraryRepository.removeFavorite(hero.id)
            }
            return nil
        } catch {
            heroIsFavorite = !target
            return Self.apiError(from: error)
        }
    }

    @discardableResult
    func toggleHeroWatchlist() async -> APIError? {
        guard let hero, !isUpdatingHeroWatchlist else { return nil }
        let target = !heroIsInWatchlist

        isUpdatingHeroWatchlist = true
        heroIsInWatchlist = target
        defer { isUpdatingHeroWatchlist = false }

        do {
            if target {
                _ = try await libraryRepository.addToWatchlist(hero.id)
            } else {
                try await libraryRepository.removeFromWatchlist(hero.id)
            }
            return nil
        } catch {
            heroIsInWatchlist = !target
            return Self.apiError(from: error)
        }
    }

    func dismissPartialError() {
        partialErrorMessage = nil
    }

    // MARK: Helpers

    private nonisolated static func apiError(from error: Error) -> APIError {
        if let error = error as? APIError {
            return error
        }
        return .transport(error.localizedDescription)
    }

    private func primaryFailure(_ failures: [HomeSectionID: APIError]) -> APIError {
        for section in HomeSectionID.allCases {
            if let error = failures[section] {
                return error
            }
        }
        return .transport("The movie service didn't return any titles.")
    }

    private static let order: [HomeSectionID: Int] = [
        .trending: 0,
        .popularMovies: 1,
        .popularShows: 2,
        .nowPlaying: 3,
        .airingToday: 4
    ]
}

private struct HomeSectionResult: Sendable {
    let section: HomeSectionID
    let result: Result<[MediaSummary], APIError>
}
