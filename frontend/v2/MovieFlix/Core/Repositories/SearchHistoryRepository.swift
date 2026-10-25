import Foundation

/// Recent searches belong to the signed-in user, so they live in their own
/// repository rather than the public media catalog.
nonisolated protocol SearchHistoryRepositoryProtocol: Sendable {
    func history(limit: Int) async throws -> [SearchHistoryEntry]
    func record(query: String) async throws -> SearchHistoryEntry
    func delete(id: Int) async throws
    func clear() async throws
}

nonisolated struct SearchHistoryRepository: SearchHistoryRepositoryProtocol {
    let client: APIClient

    func history(limit: Int = 10) async throws -> [SearchHistoryEntry] {
        try await client.send(.searchHistory(limit: limit))
    }

    func record(query: String) async throws -> SearchHistoryEntry {
        try await client.send(.recordSearch(query: query))
    }

    func delete(id: Int) async throws {
        try await client.send(.deleteSearchHistoryEntry(id: id))
    }

    func clear() async throws {
        try await client.send(.clearSearchHistory())
    }
}
