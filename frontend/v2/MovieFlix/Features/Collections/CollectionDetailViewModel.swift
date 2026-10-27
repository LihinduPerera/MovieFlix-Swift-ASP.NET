import Foundation
import Observation

@MainActor
@Observable
final class CollectionDetailViewModel {
    enum Phase: Equatable {
        case loading
        case loaded
        case failed(APIError)
    }

    let collectionID: Int

    private let repository: any LibraryRepositoryProtocol

    private(set) var phase: Phase = .loading
    private(set) var collection: CollectionDetail?
    private(set) var isDeleting = false
    private(set) var removingMediaID: MediaID?

    init(collectionID: Int, repository: any LibraryRepositoryProtocol) {
        self.collectionID = collectionID
        self.repository = repository
    }

    var isOwner: Bool {
        collection?.isOwner ?? false
    }

    // MARK: Loading

    func load(force: Bool = false) async {
        if !force, collection != nil { return }

        if collection == nil {
            phase = .loading
        }

        do {
            let detail = try await repository.collection(id: collectionID)
            guard !Task.isCancelled else { return }
            collection = detail
            phase = .loaded
        } catch let error as APIError {
            guard !Task.isCancelled, error != .cancelled else { return }
            if collection == nil {
                phase = .failed(error)
            }
        } catch is CancellationError {
            return
        } catch {
            if collection == nil {
                phase = .failed(.transport(error.localizedDescription))
            }
        }
    }

    /// Refreshes quietly after a successful write. A failure keeps the local
    /// copy rather than replacing a working screen with an error.
    func refresh() async {
        guard let detail = try? await repository.collection(id: collectionID) else {
            return
        }
        guard !Task.isCancelled else { return }
        collection = detail
        phase = .loaded
    }

    // MARK: Owner actions

    @discardableResult
    func rename(name: String, description: String) async -> APIError? {
        guard let current = collection else { return nil }

        do {
            let summary = try await repository.updateCollection(
                id: collectionID,
                name: name,
                description: description
            )
            collection = current.replacing(
                name: summary.name,
                description: summary.description
            )
            return nil
        } catch {
            return Self.apiError(from: error)
        }
    }

    @discardableResult
    func delete() async -> APIError? {
        guard !isDeleting else { return nil }
        isDeleting = true
        defer { isDeleting = false }

        do {
            try await repository.deleteCollection(id: collectionID)
            return nil
        } catch {
            return Self.apiError(from: error)
        }
    }

    /// Removes a title optimistically, then re-reads the collection so derived
    /// fields such as the cover collage and item count stay authoritative.
    @discardableResult
    func removeItem(_ mediaID: MediaID) async -> APIError? {
        guard removingMediaID == nil, let current = collection else { return nil }

        removingMediaID = mediaID
        collection = current.removing(mediaID)
        defer { removingMediaID = nil }

        do {
            try await repository.removeFromCollection(id: collectionID, media: mediaID)
            await refresh()
            return nil
        } catch {
            collection = current
            return Self.apiError(from: error)
        }
    }

    // MARK: Helpers

    private nonisolated static func apiError(from error: Error) -> APIError {
        if let error = error as? APIError {
            return error
        }
        return .transport(error.localizedDescription)
    }
}
