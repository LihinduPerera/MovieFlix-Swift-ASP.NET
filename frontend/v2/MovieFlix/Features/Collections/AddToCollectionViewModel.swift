import Foundation
import Observation

@MainActor
@Observable
final class AddToCollectionViewModel {
    enum Phase: Equatable {
        case loading
        case loaded
        case failed(APIError)
    }

    let mediaID: MediaID

    private let repository: any LibraryRepositoryProtocol

    private(set) var phase: Phase = .loading
    private(set) var collections: [CollectionSummary] = []
    private(set) var memberCollectionIDs: Set<Int> = []
    private(set) var isResolvingMembership = false
    private(set) var updatingCollectionIDs: Set<Int> = []

    init(mediaID: MediaID, repository: any LibraryRepositoryProtocol) {
        self.mediaID = mediaID
        self.repository = repository
    }

    // MARK: Loading

    func load() async {
        phase = .loading

        do {
            let page = try await repository.collections(page: 1, pageSize: 100)
            guard !Task.isCancelled else { return }
            collections = page.items
            phase = .loaded
            await resolveMembership()
        } catch let error as APIError {
            guard !Task.isCancelled, error != .cancelled else { return }
            phase = .failed(error)
        } catch is CancellationError {
            return
        } catch {
            phase = .failed(.transport(error.localizedDescription))
        }
    }

    func retry() async {
        await load()
    }

    /// The list endpoint returns summaries without item ids, so membership is
    /// resolved from each collection detail concurrently.
    private func resolveMembership() async {
        guard !collections.isEmpty else { return }

        isResolvingMembership = true
        defer { isResolvingMembership = false }

        let repository = self.repository
        let mediaID = self.mediaID
        let ids = collections.map(\.id)

        await withTaskGroup(of: (Int, Bool).self) { group in
            for id in ids {
                group.addTask {
                    guard let detail = try? await repository.collection(id: id) else {
                        return (id, false)
                    }
                    return (id, detail.items.contains { $0.id == mediaID })
                }
            }

            for await (id, isMember) in group {
                guard !Task.isCancelled else { return }
                if isMember {
                    memberCollectionIDs.insert(id)
                } else {
                    memberCollectionIDs.remove(id)
                }
            }
        }
    }

    // MARK: Membership

    func contains(_ collectionID: Int) -> Bool {
        memberCollectionIDs.contains(collectionID)
    }

    @discardableResult
    func toggleMembership(in collection: CollectionSummary) async -> APIError? {
        guard !isResolvingMembership,
              !updatingCollectionIDs.contains(collection.id)
        else {
            return nil
        }

        let wasMember = contains(collection.id)
        updatingCollectionIDs.insert(collection.id)
        setMembership(!wasMember, for: collection.id, delta: wasMember ? -1 : 1)
        defer { updatingCollectionIDs.remove(collection.id) }

        do {
            if wasMember {
                try await repository.removeFromCollection(
                    id: collection.id,
                    media: mediaID
                )
            } else {
                _ = try await repository.addToCollection(
                    id: collection.id,
                    media: mediaID
                )
            }
            return nil
        } catch {
            setMembership(wasMember, for: collection.id, delta: wasMember ? 1 : -1)
            return Self.apiError(from: error)
        }
    }

    // MARK: Creation

    /// Creates a collection and leaves it in the picker so the caller can add
    /// the current title with one more tap.
    func createCollection(name: String, description: String) async -> Result<CollectionSummary, APIError> {
        let name = name.trimmingCharacters(in: .whitespacesAndNewlines)
        let description = description.trimmingCharacters(in: .whitespacesAndNewlines)

        do {
            let created = try await repository.createCollection(
                name: name,
                description: description
            )
            collections.insert(created, at: 0)
            return .success(created)
        } catch {
            return .failure(Self.apiError(from: error))
        }
    }

    // MARK: Helpers

    private func setMembership(_ isMember: Bool, for collectionID: Int, delta: Int) {
        if isMember {
            memberCollectionIDs.insert(collectionID)
        } else {
            memberCollectionIDs.remove(collectionID)
        }

        guard let index = collections.firstIndex(where: { $0.id == collectionID }) else {
            return
        }
        collections[index] = collections[index].adjustingItemCount(by: delta)
    }

    private nonisolated static func apiError(from error: Error) -> APIError {
        if let error = error as? APIError {
            return error
        }
        return .transport(error.localizedDescription)
    }
}
