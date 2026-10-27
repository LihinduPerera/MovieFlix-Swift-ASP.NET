import Foundation

nonisolated struct Review: Decodable, Hashable, Sendable, Identifiable {
    let id: Int
    let media: MediaSummary
    let content: String
    let authorDisplayName: String
    let isMine: Bool
    let createdAt: Date
    let updatedAt: Date

    var wasEdited: Bool { updatedAt > createdAt }
}

nonisolated struct CreateReviewRequest: Encodable, Sendable {
    let content: String
}

nonisolated struct UpdateReviewRequest: Encodable, Sendable {
    let content: String
}
