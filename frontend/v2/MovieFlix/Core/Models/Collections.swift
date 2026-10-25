import Foundation

nonisolated struct CollectionSummary: Decodable, Hashable, Sendable, Identifiable {
    let id: Int
    let name: String
    let description: String
    let itemCount: Int
    let coverPosterPaths: [String]
    let createdAt: Date
    let updatedAt: Date
}

nonisolated struct CollectionItem: Decodable, Hashable, Sendable, Identifiable {
    let media: MediaSummary
    let addedAt: Date

    var id: MediaID { media.id }
}

nonisolated struct CollectionDetail: Decodable, Hashable, Sendable, Identifiable {
    let id: Int
    let name: String
    let description: String
    let itemCount: Int
    let coverPosterPaths: [String]
    let createdAt: Date
    let updatedAt: Date
    let ownerDisplayName: String
    let isOwner: Bool
    let items: [CollectionItem]
}

nonisolated struct CollectionRequest: Encodable, Sendable {
    let name: String
    let description: String
}

// MARK: - Local mutations

nonisolated extension CollectionSummary {
    /// Mirrors a membership change before the next list refresh.
    func adjustingItemCount(by delta: Int) -> CollectionSummary {
        CollectionSummary(
            id: id,
            name: name,
            description: description,
            itemCount: max(0, itemCount + delta),
            coverPosterPaths: coverPosterPaths,
            createdAt: createdAt,
            updatedAt: updatedAt
        )
    }
}

nonisolated extension CollectionDetail {
    func replacing(name: String, description: String) -> CollectionDetail {
        CollectionDetail(
            id: id,
            name: name,
            description: description,
            itemCount: itemCount,
            coverPosterPaths: coverPosterPaths,
            createdAt: createdAt,
            updatedAt: Date(),
            ownerDisplayName: ownerDisplayName,
            isOwner: isOwner,
            items: items
        )
    }

    func removing(_ mediaID: MediaID) -> CollectionDetail {
        CollectionDetail(
            id: id,
            name: name,
            description: description,
            itemCount: max(0, itemCount - 1),
            coverPosterPaths: coverPosterPaths,
            createdAt: createdAt,
            updatedAt: Date(),
            ownerDisplayName: ownerDisplayName,
            isOwner: isOwner,
            items: items.filter { $0.id != mediaID }
        )
    }
}
