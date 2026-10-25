import Foundation

// MARK: - Requests

nonisolated struct LoginRequest: Encodable, Sendable {
    let email: String
    let password: String
}

nonisolated struct RegisterRequest: Encodable, Sendable {
    let email: String
    let displayName: String
    let password: String
}

nonisolated struct RefreshRequest: Encodable, Sendable {
    let refreshToken: String
}

// MARK: - Profile

nonisolated struct UserProfile: Codable, Hashable, Sendable, Identifiable {
    let id: String
    let email: String
    let displayName: String
    let createdAt: Date
    let roles: [String]

    var initials: String {
        let parts = displayName.split(separator: " ").prefix(2)
        let value = parts.compactMap(\.first).map(String.init).joined()
        return value.isEmpty ? "MF" : value.uppercased()
    }

    var isAdmin: Bool { roles.contains("Admin") }

    var memberSince: String {
        createdAt.formatted(.dateTime.month(.wide).year())
    }
}

// MARK: - Token pair

nonisolated struct AuthResponse: Codable, Hashable, Sendable {
    let accessToken: String
    let accessTokenExpiresAt: Date
    let refreshToken: String
    let refreshTokenExpiresAt: Date
    let tokenType: String
    let user: UserProfile

    var isAccessTokenExpired: Bool { accessTokenExpiresAt <= Date() }

    var isRefreshTokenExpired: Bool { refreshTokenExpiresAt <= Date() }

    func replacingUser(_ user: UserProfile) -> AuthResponse {
        AuthResponse(
            accessToken: accessToken,
            accessTokenExpiresAt: accessTokenExpiresAt,
            refreshToken: refreshToken,
            refreshTokenExpiresAt: refreshTokenExpiresAt,
            tokenType: tokenType,
            user: user
        )
    }
}
