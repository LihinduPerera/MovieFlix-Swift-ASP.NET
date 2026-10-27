import Foundation
import OSLog
import Security

// MARK: - Protocol

/// Persistence boundary for the signed-in session. Implementations must be
/// safe to call from any task; `APIClient` reads the access token off the main
/// actor for every request.
nonisolated protocol TokenStoring: Sendable {
    func load() async -> AuthResponse?
    func save(_ session: AuthResponse) async
    func clear() async
}

// MARK: - In-memory store

/// Used by previews and tests. Never persisted.
actor InMemoryTokenStore: TokenStoring, AccessTokenProviding {
    private var session: AuthResponse?

    init(session: AuthResponse? = nil) {
        self.session = session
    }

    func load() async -> AuthResponse? {
        session
    }

    func save(_ session: AuthResponse) async {
        self.session = session
    }

    func clear() async {
        session = nil
    }

    func accessToken() async -> String? {
        session?.accessToken
    }
}

// MARK: - Keychain store

/// Keychain-backed storage with an in-memory cache so the token is not read
/// from the keychain for every request.
actor KeychainTokenStore: TokenStoring, AccessTokenProviding {
    private enum Cache {
        case unknown
        case empty
        case session(AuthResponse)
    }

    private let service: String
    private let account: String
    private var cache: Cache = .unknown
    private let logger = Logger(
        subsystem: "com.LithiumStudios.MovieFlix",
        category: "keychain"
    )

    init(
        service: String = Bundle.main.bundleIdentifier.map { "\($0).auth" } ?? "MovieFlix.auth",
        account: String = "session"
    ) {
        self.service = service
        self.account = account
    }

    // MARK: TokenStoring

    func load() async -> AuthResponse? {
        if case let .session(session) = cache {
            return session
        }
        if case .empty = cache {
            return nil
        }

        guard let data = read() else {
            cache = .empty
            return nil
        }

        do {
            let session = try JSONCoding.makeDecoder().decode(AuthResponse.self, from: data)
            cache = .session(session)
            return session
        } catch {
            logger.error("Stored session could not be decoded; clearing it.")
            delete()
            cache = .empty
            return nil
        }
    }

    func save(_ session: AuthResponse) async {
        cache = .session(session)

        do {
            let data = try JSONCoding.makeEncoder().encode(session)
            delete()
            let status = add(data)
            if status != errSecSuccess {
                logger.error("Keychain write failed with status \(status).")
            }
        } catch {
            logger.error("Session could not be encoded: \(String(describing: error), privacy: .public)")
        }
    }

    func clear() async {
        cache = .empty
        delete()
    }

    // MARK: AccessTokenProviding

    func accessToken() async -> String? {
        await load()?.accessToken
    }

    // MARK: Keychain primitives

    private func baseQuery() -> [String: Any] {
        [
            kSecClass as String: kSecClassGenericPassword,
            kSecAttrService as String: service,
            kSecAttrAccount as String: account
        ]
    }

    private func read() -> Data? {
        var query = baseQuery()
        query[kSecReturnData as String] = true
        query[kSecMatchLimit as String] = kSecMatchLimitOne

        var result: CFTypeRef?
        let status = SecItemCopyMatching(query as CFDictionary, &result)
        guard status == errSecSuccess else {
            if status != errSecItemNotFound {
                logger.error("Keychain read failed with status \(status).")
            }
            return nil
        }
        return result as? Data
    }

    private func add(_ data: Data) -> OSStatus {
        var query = baseQuery()
        query[kSecValueData as String] = data
        query[kSecAttrAccessible as String] = kSecAttrAccessibleAfterFirstUnlockThisDeviceOnly
        return SecItemAdd(query as CFDictionary, nil)
    }

    private func delete() {
        let status = SecItemDelete(baseQuery() as CFDictionary)
        if status != errSecSuccess, status != errSecItemNotFound {
            logger.error("Keychain delete failed with status \(status).")
        }
    }
}
