import Foundation

nonisolated protocol AuthRepositoryProtocol: Sendable {
    func register(email: String, displayName: String, password: String) async throws -> AuthResponse
    func login(email: String, password: String) async throws -> AuthResponse
    func refresh(refreshToken: String) async throws -> AuthResponse
    func logout() async throws
    func me() async throws -> UserProfile
}

nonisolated struct AuthRepository: AuthRepositoryProtocol {
    private let client: APIClient

    init(client: APIClient) {
        self.client = client
    }

    func register(email: String, displayName: String, password: String) async throws -> AuthResponse {
        try await client.send(
            .register(email: email, displayName: displayName, password: password),
            as: AuthResponse.self
        )
    }

    func login(email: String, password: String) async throws -> AuthResponse {
        try await client.send(
            .login(email: email, password: password),
            as: AuthResponse.self
        )
    }

    func refresh(refreshToken: String) async throws -> AuthResponse {
        try await client.send(
            .refresh(refreshToken: refreshToken),
            as: AuthResponse.self
        )
    }

    func logout() async throws {
        try await client.send(.logout())
    }

    func me() async throws -> UserProfile {
        try await client.send(.me(), as: UserProfile.self)
    }
}
