import Foundation
import Observation

// MARK: - Filters

nonisolated enum GenreSort: String, CaseIterable, Sendable, Identifiable {
    case popular
    case topRated
    case newest

    var id: String { rawValue }

    var displayName: String {
        switch self {
        case .popular: "Popular"
        case .topRated: "Top Rated"
        case .newest: "Newest"
        }
    }

    func sortBy(for mediaType: MediaType) -> String {
        switch self {
        case .popular:
            "popularity.desc"
        case .topRated:
            "vote_average.desc"
        case .newest:
            mediaType == .movie ? "primary_release_date.desc" : "first_air_date.desc"
        }
    }
}

nonisolated struct GenreRatingFilter: Hashable, Sendable, Identifiable {
    let value: Double?
    let label: String

    var id: String { label }

    static let any = GenreRatingFilter(value: nil, label: "Any Rating")
    static let sevenPlus = GenreRatingFilter(value: 7, label: "7+")
    static let eightPlus = GenreRatingFilter(value: 8, label: "8+")
    static let ninePlus = GenreRatingFilter(value: 9, label: "9+")

    static let allCases: [GenreRatingFilter] = [
        .any,
        .sevenPlus,
        .eightPlus,
        .ninePlus
    ]
}

// MARK: - View model

@MainActor
@Observable
final class GenreResultsViewModel {
    enum Phase: Equatable {
        case idle
        case loading
        case loaded
        case failed(APIError)
    }

    let mediaType: MediaType
    let genreID: Int
    let genreName: String

    private let repository: any MediaRepositoryProtocol

    private(set) var phase: Phase = .idle
    private(set) var items: [MediaSummary] = []
    private(set) var totalCount = 0
    private(set) var hasMore = false
    private(set) var isLoadingMore = false
    private(set) var loadMoreError: APIError?
    private(set) var sort: GenreSort = .popular
    private(set) var rating: GenreRatingFilter = .any

    private var page = 0
    private var generation = 0

    init(
        mediaType: MediaType,
        genreID: Int,
        genreName: String,
        repository: any MediaRepositoryProtocol
    ) {
        self.mediaType = mediaType
        self.genreID = genreID
        self.genreName = genreName
        self.repository = repository
    }

    // MARK: Derived state

    var countSummary: String {
        let noun = totalCount == 1 ? "title" : "titles"
        return "\(totalCount) \(noun) in \(genreName)"
    }

    // MARK: Loading

    func load(force: Bool = false) async {
        if !force, phase == .loaded { return }
        await fetch(page: 1, reset: true)
    }

    func selectSort(_ newSort: GenreSort) async {
        guard newSort != sort else { return }
        sort = newSort
        await fetch(page: 1, reset: true)
    }

    func selectRating(_ newRating: GenreRatingFilter) async {
        guard newRating != rating else { return }
        rating = newRating
        await fetch(page: 1, reset: true)
    }

    func loadMore() async {
        guard phase == .loaded, hasMore, !isLoadingMore, !items.isEmpty else { return }

        isLoadingMore = true
        loadMoreError = nil
        defer { isLoadingMore = false }

        let token = generation
        let nextPage = page + 1

        do {
            let response = try await repository.discover(
                mediaType: mediaType,
                genreID: genreID,
                year: nil,
                minRating: rating.value,
                sortBy: sort.sortBy(for: mediaType),
                page: nextPage,
                language: nil,
                region: nil
            )

            guard token == generation, !Task.isCancelled else { return }

            let existing = Set(items.map(\.id))
            items.append(contentsOf: response.items.filter { !existing.contains($0.id) })
            page = response.page
            totalCount = response.totalCount
            hasMore = response.hasMore
        } catch let error as APIError where error != .cancelled {
            guard token == generation else { return }
            loadMoreError = error
        } catch {
            return
        }
    }

    func retryLoadMore() async {
        await loadMore()
    }

    func resetFilters() async {
        guard sort != .popular || rating != .any else { return }
        sort = .popular
        rating = .any
        await fetch(page: 1, reset: true)
    }

    // MARK: Private

    private func fetch(page: Int, reset: Bool) async {
        generation += 1
        let token = generation

        if reset {
            items = []
            totalCount = 0
            hasMore = false
            loadMoreError = nil
            phase = .loading
        }

        let sortBy = sort.sortBy(for: mediaType)
        let minRating = rating.value

        do {
            let response = try await repository.discover(
                mediaType: mediaType,
                genreID: genreID,
                year: nil,
                minRating: minRating,
                sortBy: sortBy,
                page: page,
                language: nil,
                region: nil
            )

            guard token == generation, !Task.isCancelled else { return }

            items = response.items
            self.page = response.page
            totalCount = response.totalCount
            hasMore = response.hasMore
            phase = .loaded
        } catch let error as APIError where error != .cancelled {
            guard token == generation else { return }
            phase = .failed(error)
        } catch {
            return
        }
    }
}
