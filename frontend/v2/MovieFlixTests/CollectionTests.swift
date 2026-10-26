import Foundation
import Testing

@testable import MovieFlix

// MARK: - Fixtures

private enum CollectionFixture {
    nonisolated static let mediaID = MediaID(mediaType: .movie, tmdbId: 155)

    nonisolated static func media(tmdbId: Int = 155, type: MediaType = .movie) -> MediaSummary {
        MediaSummary(
            tmdbId: tmdbId,
            mediaType: type,
            title: "The Dark Knight",
            originalTitle: "The Dark Knight",
            overview: "Batman raises the stakes in his war on crime.",
            posterPath: "/poster\(tmdbId).jpg",
            backdropPath: "/backdrop\(tmdbId).jpg",
            releaseDate: LocalDate(year: 2008, month: 7, day: 16),
            voteAverage: 8.5,
            voteCount: 34_500,
            popularity: 640.7
        )
    }

    nonisolated static func item(tmdbId: Int = 155) -> CollectionItem {
        CollectionItem(
            media: media(tmdbId: tmdbId),
            addedAt: Date(timeIntervalSince1970: 1_700_000_000)
        )
    }

    nonisolated static func summary(
        id: Int,
        name: String = "Weekend Noir",
        description: String = "Shadows and secrets.",
        itemCount: Int = 2,
        coverPaths: [String] = ["/poster155.jpg", "/poster496243.jpg"]
    ) -> CollectionSummary {
        CollectionSummary(
            id: id,
            name: name,
            description: description,
            itemCount: itemCount,
            coverPosterPaths: coverPaths,
            createdAt: Date(timeIntervalSince1970: 1_700_000_000),
            updatedAt: Date(timeIntervalSince1970: 1_710_000_000)
        )
    }

    nonisolated static func detail(
        id: Int,
        name: String = "Weekend Noir",
        description: String = "Shadows and secrets.",
        items: [CollectionItem] = [item()],
        isOwner: Bool = true
    ) -> CollectionDetail {
        CollectionDetail(
            id: id,
            name: name,
            description: description,
            itemCount: items.count,
            coverPosterPaths: items.compactMap(\.media.posterPath),
            createdAt: Date(timeIntervalSince1970: 1_700_000_000),
            updatedAt: Date(timeIntervalSince1970: 1_710_000_000),
            ownerDisplayName: isOwner ? "Ava Chen" : "Marcus Reed",
            isOwner: isOwner,
            items: items
        )
    }

    nonisolated static func page(
        _ items: [CollectionSummary],
        page: Int = 1,
        totalCount: Int? = nil
    ) -> PagedResponse<CollectionSummary> {
        let total = totalCount ?? items.count
        return PagedResponse(
            items: items,
            page: page,
            pageSize: 30,
            totalCount: total,
            totalPages: max(1, Int(ceil(Double(total) / 30.0)))
        )
    }
}

// MARK: - Library collections

@Suite("Library collections")
@MainActor
struct LibraryCollectionTests {
    @Test("Collections load alongside the rest of the library")
    func loadsCollections() async {
        let repository = MockCollectionRepository()
        await repository.setCollectionsPage(
            CollectionFixture.page([
                CollectionFixture.summary(id: 1),
                CollectionFixture.summary(id: 2, name: "Comfort Watch", itemCount: 1)
            ])
        )

        let viewModel = LibraryViewModel(repository: repository)
        await viewModel.loadCollections(reset: true)

        #expect(viewModel.collections.items.map(\.id) == [1, 2])
        #expect(viewModel.collections.totalCount == 2)
        #expect(viewModel.collections.hasLoaded)
        #expect(viewModel.collections.error == nil)
    }

    @Test("Creating a collection inserts it at the top")
    func createInsertsAtTop() async {
        let repository = MockCollectionRepository(nextCollectionID: 9)
        await repository.setCollectionsPage(
            CollectionFixture.page([CollectionFixture.summary(id: 1)])
        )
        let viewModel = LibraryViewModel(repository: repository)
        await viewModel.loadCollections(reset: true)

        let result = await viewModel.createCollection(
            name: "  Movie Night  ",
            description: "  Big screen picks  "
        )

        guard case let .success(created) = result else {
            Issue.record("Expected a created collection")
            return
        }
        #expect(created.id == 9)
        #expect(viewModel.collections.items.first?.id == 9)
        #expect(viewModel.collections.items.count == 2)
        #expect(viewModel.collections.totalCount == 2)
        #expect(await repository.createdNames == ["Movie Night"])
    }

    @Test("A duplicate name leaves the grid untouched and returns the conflict")
    func duplicateNameFails() async {
        let repository = MockCollectionRepository()
        await repository.setCollectionsPage(
            CollectionFixture.page([CollectionFixture.summary(id: 1)])
        )
        await repository.setCreateFailure(.conflict(nil))
        let viewModel = LibraryViewModel(repository: repository)
        await viewModel.loadCollections(reset: true)

        let result = await viewModel.createCollection(name: "Weekend Noir", description: "")

        guard case let .failure(error) = result else {
            Issue.record("Expected a duplicate-name failure")
            return
        }
        #expect(error == .conflict(nil))
        #expect(viewModel.collections.items.count == 1)
        #expect(viewModel.collections.totalCount == 1)
    }

    @Test("Deleting a collection drops it after the write succeeds")
    func deleteRemovesLocally() async {
        let repository = MockCollectionRepository()
        await repository.setCollectionsPage(
            CollectionFixture.page([
                CollectionFixture.summary(id: 1),
                CollectionFixture.summary(id: 2)
            ])
        )
        let viewModel = LibraryViewModel(repository: repository)
        await viewModel.loadCollections(reset: true)

        let error = await viewModel.deleteCollection(id: 1)

        #expect(error == nil)
        #expect(viewModel.collections.items.map(\.id) == [2])
        #expect(viewModel.collections.totalCount == 1)
        #expect(await repository.deletedIDs == [1])
    }

    @Test("A failed delete keeps the collection")
    func failedDeleteKeepsCollection() async {
        let repository = MockCollectionRepository()
        await repository.setCollectionsPage(
            CollectionFixture.page([CollectionFixture.summary(id: 1)])
        )
        await repository.setDeleteFailure(.server(status: 503, nil))
        let viewModel = LibraryViewModel(repository: repository)
        await viewModel.loadCollections(reset: true)

        let error = await viewModel.deleteCollection(id: 1)

        #expect(error == .server(status: 503, nil))
        #expect(viewModel.collections.items.count == 1)
        #expect(viewModel.collections.totalCount == 1)
    }
}

// MARK: - Collection detail

@Suite("Collection detail")
@MainActor
struct CollectionDetailTests {
    @Test("Loads the collection and exposes ownership")
    func loadsDetail() async {
        let repository = MockCollectionRepository()
        await repository.setDetail(CollectionFixture.detail(id: 4), for: 4)
        let viewModel = CollectionDetailViewModel(collectionID: 4, repository: repository)

        await viewModel.load()

        #expect(viewModel.phase == .loaded)
        #expect(viewModel.collection?.name == "Weekend Noir")
        #expect(viewModel.isOwner)
    }

    @Test("A missing collection surfaces an error state")
    func missingCollectionFails() async {
        let repository = MockCollectionRepository()
        let viewModel = CollectionDetailViewModel(collectionID: 404, repository: repository)

        await viewModel.load()

        guard case let .failed(error) = viewModel.phase else {
            Issue.record("Expected a failed phase")
            return
        }
        #expect(error == .notFound(nil))
    }

    @Test("Removing a title updates the grid and item count")
    func removeItemSucceeds() async {
        let repository = MockCollectionRepository()
        let first = CollectionFixture.item(tmdbId: 155)
        let second = CollectionFixture.item(tmdbId: 496243)
        await repository.setDetail(
            CollectionFixture.detail(id: 1, items: [first, second]),
            for: 1
        )
        let viewModel = CollectionDetailViewModel(collectionID: 1, repository: repository)
        await viewModel.load()

        let error = await viewModel.removeItem(first.id)

        #expect(error == nil)
        #expect(viewModel.collection?.items.map(\.id) == [second.id])
        #expect(viewModel.collection?.itemCount == 1)
        #expect(await repository.removedItems.map { $0.collection } == [1])
        #expect(await repository.removedItems.map { $0.media } == [first.id])
    }

    @Test("A failed removal restores the title")
    func removeItemRollsBack() async {
        let repository = MockCollectionRepository()
        let first = CollectionFixture.item(tmdbId: 155)
        await repository.setDetail(CollectionFixture.detail(id: 1, items: [first]), for: 1)
        await repository.setRemoveItemFailure(.server(status: 500, nil))
        let viewModel = CollectionDetailViewModel(collectionID: 1, repository: repository)
        await viewModel.load()

        let error = await viewModel.removeItem(first.id)

        #expect(error == .server(status: 500, nil))
        #expect(viewModel.collection?.items.map(\.id) == [first.id])
        #expect(viewModel.collection?.itemCount == 1)
        #expect(viewModel.removingMediaID == nil)
    }

    @Test("Renaming updates the header without a reload")
    func renameUpdatesDetail() async {
        let repository = MockCollectionRepository()
        await repository.setDetail(CollectionFixture.detail(id: 2), for: 2)
        let viewModel = CollectionDetailViewModel(collectionID: 2, repository: repository)
        await viewModel.load()

        let error = await viewModel.rename(
            name: "Comfort Rewatch",
            description: "Warm and familiar."
        )

        #expect(error == nil)
        #expect(viewModel.collection?.name == "Comfort Rewatch")
        #expect(viewModel.collection?.description == "Warm and familiar.")
        #expect(await repository.updatedNames == ["Comfort Rewatch"])
    }

    @Test("A failed rename keeps the previous name")
    func renameRollsBack() async {
        let repository = MockCollectionRepository()
        await repository.setDetail(CollectionFixture.detail(id: 2), for: 2)
        await repository.setUpdateFailure(.conflict(nil))
        let viewModel = CollectionDetailViewModel(collectionID: 2, repository: repository)
        await viewModel.load()

        let error = await viewModel.rename(name: "Duplicate", description: "")

        #expect(error == .conflict(nil))
        #expect(viewModel.collection?.name == "Weekend Noir")
    }

    @Test("Deleting reports success and failure distinctly")
    func deleteReportsResult() async {
        let repository = MockCollectionRepository()
        await repository.setDetail(CollectionFixture.detail(id: 2), for: 2)
        let viewModel = CollectionDetailViewModel(collectionID: 2, repository: repository)
        await viewModel.load()

        #expect(await viewModel.delete() == nil)
        #expect(await repository.deletedIDs == [2])

        await repository.setDeleteFailure(.forbidden(nil))
        let error = await viewModel.delete()

        #expect(error == .forbidden(nil))
        #expect(!viewModel.isDeleting)
    }
}

// MARK: - Add to collection

@Suite("Add to collection")
@MainActor
struct AddToCollectionTests {
    private let mediaID = CollectionFixture.mediaID

    @Test("Membership is resolved from each collection detail")
    func resolvesMembership() async {
        let repository = MockCollectionRepository()
        await repository.setCollectionsPage(
            CollectionFixture.page([
                CollectionFixture.summary(id: 1, itemCount: 1),
                CollectionFixture.summary(id: 2, itemCount: 3)
            ])
        )
        await repository.setDetail(
            CollectionFixture.detail(id: 1, items: [CollectionFixture.item()]),
            for: 1
        )
        await repository.setDetail(
            CollectionFixture.detail(id: 2, items: [CollectionFixture.item(tmdbId: 129)]),
            for: 2
        )

        let viewModel = AddToCollectionViewModel(mediaID: mediaID, repository: repository)
        await viewModel.load()

        #expect(viewModel.phase == .loaded)
        #expect(viewModel.contains(1))
        #expect(!viewModel.contains(2))
        #expect(!viewModel.isResolvingMembership)
    }

    @Test("Adding and removing a title keeps counts and checkmarks in sync")
    func togglesMembership() async {
        let repository = MockCollectionRepository()
        await repository.setCollectionsPage(
            CollectionFixture.page([
                CollectionFixture.summary(id: 1, itemCount: 1),
                CollectionFixture.summary(id: 2, itemCount: 2)
            ])
        )
        await repository.setDetail(
            CollectionFixture.detail(id: 1, items: [CollectionFixture.item()]),
            for: 1
        )
        await repository.setDetail(
            CollectionFixture.detail(id: 2, items: [CollectionFixture.item(tmdbId: 129)]),
            for: 2
        )
        let viewModel = AddToCollectionViewModel(mediaID: mediaID, repository: repository)
        await viewModel.load()

        let addError = await viewModel.toggleMembership(
            in: CollectionFixture.summary(id: 2, itemCount: 2)
        )
        #expect(addError == nil)
        #expect(viewModel.contains(2))
        #expect(viewModel.collections.first { $0.id == 2 }?.itemCount == 3)
        #expect(await repository.addedItems.map { $0.media } == [mediaID])

        let removeError = await viewModel.toggleMembership(
            in: CollectionFixture.summary(id: 1, itemCount: 1)
        )
        #expect(removeError == nil)
        #expect(!viewModel.contains(1))
        #expect(viewModel.collections.first { $0.id == 1 }?.itemCount == 0)
        #expect(await repository.removedItems.map { $0.media } == [mediaID])
    }

    @Test("A failed toggle restores the previous checkmark")
    func toggleRollsBack() async {
        let repository = MockCollectionRepository()
        await repository.setCollectionsPage(
            CollectionFixture.page([CollectionFixture.summary(id: 1, itemCount: 1)])
        )
        await repository.setDetail(
            CollectionFixture.detail(id: 1, items: [CollectionFixture.item()]),
            for: 1
        )
        await repository.setRemoveItemFailure(.offline)
        let viewModel = AddToCollectionViewModel(mediaID: mediaID, repository: repository)
        await viewModel.load()

        let error = await viewModel.toggleMembership(in: CollectionFixture.summary(id: 1, itemCount: 1))

        #expect(error == .offline)
        #expect(viewModel.contains(1))
        #expect(viewModel.collections.first?.itemCount == 1)
        #expect(viewModel.updatingCollectionIDs.isEmpty)
    }

    @Test("A new collection is offered at the top of the picker")
    func createInsertsAtTop() async {
        let repository = MockCollectionRepository(nextCollectionID: 77)
        await repository.setCollectionsPage(
            CollectionFixture.page([CollectionFixture.summary(id: 1)])
        )
        await repository.setDetail(CollectionFixture.detail(id: 1, items: []), for: 1)
        let viewModel = AddToCollectionViewModel(mediaID: mediaID, repository: repository)
        await viewModel.load()

        let result = await viewModel.createCollection(name: "Watch Soon", description: "")

        guard case let .success(created) = result else {
            Issue.record("Expected a created collection")
            return
        }
        #expect(created.id == 77)
        #expect(viewModel.collections.first?.id == 77)
        #expect(!viewModel.contains(77))
        #expect(await repository.createdNames == ["Watch Soon"])
    }
}

// MARK: - Mock

private actor MockCollectionRepository: LibraryRepositoryProtocol {
    private var collectionsPage: PagedResponse<CollectionSummary>
    private var createFailure: APIError?
    private var updateFailure: APIError?
    private var deleteFailure: APIError?
    private var removeItemFailure: APIError?
    private var details: [Int: CollectionDetail] = [:]
    private var nextCollectionID: Int

    private(set) var createdNames: [String] = []
    private(set) var updatedNames: [String] = []
    private(set) var deletedIDs: [Int] = []
    private(set) var addedItems: [(collection: Int, media: MediaID)] = []
    private(set) var removedItems: [(collection: Int, media: MediaID)] = []

    init(nextCollectionID: Int = 100) {
        self.nextCollectionID = nextCollectionID
        self.collectionsPage = CollectionFixture.page([])
    }

    // MARK: Test controls

    func setCollectionsPage(_ page: PagedResponse<CollectionSummary>) {
        collectionsPage = page
    }

    func setDetail(_ detail: CollectionDetail, for id: Int) {
        details[id] = detail
    }

    func setCreateFailure(_ error: APIError) {
        createFailure = error
    }

    func setUpdateFailure(_ error: APIError) {
        updateFailure = error
    }

    func setDeleteFailure(_ error: APIError) {
        deleteFailure = error
    }

    func setRemoveItemFailure(_ error: APIError) {
        removeItemFailure = error
    }

    // MARK: Favorites

    func favorites(
        mediaType: MediaType?,
        page: Int,
        pageSize: Int,
        sort: LibrarySort
    ) async throws -> PagedResponse<SavedMedia> {
        throw APIError.notFound(nil)
    }

    func addFavorite(_ id: MediaID) async throws -> SavedMedia {
        throw APIError.notFound(nil)
    }

    func removeFavorite(_ id: MediaID) async throws {
        throw APIError.notFound(nil)
    }

    // MARK: Watchlist

    func watchlist(
        mediaType: MediaType?,
        page: Int,
        pageSize: Int,
        sort: LibrarySort
    ) async throws -> PagedResponse<SavedMedia> {
        throw APIError.notFound(nil)
    }

    func addToWatchlist(_ id: MediaID) async throws -> SavedMedia {
        throw APIError.notFound(nil)
    }

    func removeFromWatchlist(_ id: MediaID) async throws {
        throw APIError.notFound(nil)
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
        throw APIError.notFound(nil)
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
        throw APIError.notFound(nil)
    }

    func clearHistory() async throws {
        throw APIError.notFound(nil)
    }

    func clearTitleHistory(_ id: MediaID) async throws {
        throw APIError.notFound(nil)
    }

    // MARK: Collections

    func collections(page: Int, pageSize: Int) async throws -> PagedResponse<CollectionSummary> {
        collectionsPage
    }

    func createCollection(name: String, description: String) async throws -> CollectionSummary {
        if let createFailure {
            throw createFailure
        }

        createdNames.append(name)
        let summary = CollectionFixture.summary(
            id: nextCollectionID,
            name: name,
            description: description,
            itemCount: 0,
            coverPaths: []
        )
        nextCollectionID += 1
        return summary
    }

    func collection(id: Int) async throws -> CollectionDetail {
        guard let detail = details[id] else {
            throw APIError.notFound(nil)
        }
        return detail
    }

    func updateCollection(
        id: Int,
        name: String,
        description: String
    ) async throws -> CollectionSummary {
        if let updateFailure {
            throw updateFailure
        }

        updatedNames.append(name)
        return CollectionFixture.summary(id: id, name: name, description: description)
    }

    func deleteCollection(id: Int) async throws {
        if let deleteFailure {
            throw deleteFailure
        }
        deletedIDs.append(id)
    }

    func addToCollection(id: Int, media: MediaID) async throws -> CollectionDetail {
        addedItems.append((id, media))

        guard var detail = details[id] else {
            return CollectionFixture.detail(
                id: id,
                items: [CollectionItem(media: CollectionFixture.media(tmdbId: media.tmdbId), addedAt: Date())]
            )
        }

        if !detail.items.contains(where: { $0.id == media }) {
            detail = CollectionDetail(
                id: detail.id,
                name: detail.name,
                description: detail.description,
                itemCount: detail.itemCount + 1,
                coverPosterPaths: detail.coverPosterPaths,
                createdAt: detail.createdAt,
                updatedAt: Date(),
                ownerDisplayName: detail.ownerDisplayName,
                isOwner: detail.isOwner,
                items: detail.items + [
                    CollectionItem(
                        media: CollectionFixture.media(tmdbId: media.tmdbId),
                        addedAt: Date()
                    )
                ]
            )
            details[id] = detail
        }

        return detail
    }

    func removeFromCollection(id: Int, media: MediaID) async throws {
        if let removeItemFailure {
            throw removeItemFailure
        }

        removedItems.append((id, media))
        if let detail = details[id] {
            details[id] = detail.removing(media)
        }
    }
}
