import Foundation
import Testing

@testable import MovieFlix

@Suite("Session store")
@MainActor
struct SessionStoreTests {
    // MARK: Fixtures

    private func makeUser(name: String = "Ava Chen") -> UserProfile {
        UserProfile(
            id: "user-1",
            email: "ava@example.com",
            displayName: name,
            createdAt: Date(timeIntervalSince1970: 1_700_000_000),
            roles: ["User"]
        )
    }

    private func makeResponse(
        accessToken: String = "access-1",
        accessExpiresIn: TimeInterval = 3_600,
        refreshToken: String = "refresh-1",
        refreshExpiresIn: TimeInterval = 604_800,
        user: UserProfile? = nil
    ) -> AuthResponse {
        AuthResponse(
            accessToken: accessToken,
            accessTokenExpiresAt: Date().addingTimeInterval(accessExpiresIn),
            refreshToken: refreshToken,
            refreshTokenExpiresAt: Date().addingTimeInterval(refreshExpiresIn),
            tokenType: "Bearer",
            user: user ?? makeUser()
        )
    }

    // MARK: Sign in

    @Test("Sign in persists the session and publishes the user")
    func signInPersistsSession() async throws {
        let auth = makeResponse()
        let store = InMemoryTokenStore()
        let repository = MockAuthRepository(login: .success(auth))
        let session = SessionStore(repository: repository, tokenStore: store)

        let succeeded = await session.signIn(email: " ava@example.com ", password: "Password1!")

        #expect(succeeded)
        #expect(session.user == auth.user)
        let persisted = try #require(await store.load())
        #expect(persisted.accessToken == auth.accessToken)
        #expect(await repository.loginCount == 1)
    }

    @Test("A failed sign in surfaces the API message and keeps the guest state")
    func failedSignInSurfacesMessage() async {
        let store = InMemoryTokenStore()
        let repository = MockAuthRepository(login: .failure(.unauthorized(nil)))
        let session = SessionStore(repository: repository, tokenStore: store)
        await session.restore()

        let succeeded = await session.signIn(email: "ava@example.com", password: "wrong")

        #expect(!succeeded)
        #expect(session.phase == .signedOut)
        #expect(session.errorMessage?.isEmpty == false)
        #expect(await store.load() == nil)
    }

    // MARK: Restore

    @Test("Restore accepts a fresh access token and refreshes the profile")
    func restoreUsesCachedSession() async {
        let auth = makeResponse(accessExpiresIn: 3_600)
        let store = InMemoryTokenStore(session: auth)
        let repository = MockAuthRepository(
            me: .success(makeUser(name: "Ava Updated"))
        )
        let session = SessionStore(repository: repository, tokenStore: store)

        await session.restore()

        #expect(session.user?.displayName == "Ava Updated")
        #expect(await repository.refreshCount == 0)
        #expect(await repository.meCount == 1)
    }

    @Test("Restore refreshes an expired access token")
    func restoreRefreshesExpiredAccessToken() async throws {
        let expired = makeResponse(accessToken: "old", accessExpiresIn: -60)
        let refreshed = makeResponse(accessToken: "new")
        let store = InMemoryTokenStore(session: expired)
        let repository = MockAuthRepository(refresh: .success(refreshed))
        let session = SessionStore(repository: repository, tokenStore: store)

        await session.restore()

        #expect(session.isSignedIn)
        #expect(await repository.refreshCount == 1)
        let persisted = try #require(await store.load())
        #expect(persisted.accessToken == "new")
    }

    @Test("A transient refresh failure keeps the cached user, even during restore")
    func transientRefreshKeepsSession() async throws {
        let expired = makeResponse(accessExpiresIn: -60)
        let store = InMemoryTokenStore(session: expired)
        let repository = MockAuthRepository(refresh: .failure(.offline))
        let session = SessionStore(repository: repository, tokenStore: store)

        await session.restore()

        #expect(session.user == expired.user)
        #expect(await repository.refreshCount == 1)
        #expect(try #require(await store.load()).accessToken == expired.accessToken)
    }

    @Test("An expired refresh token signs the user out locally")
    func expiredRefreshTokenSignsOut() async {
        let stale = makeResponse(refreshExpiresIn: -60)
        let store = InMemoryTokenStore(session: stale)
        let repository = MockAuthRepository()
        let session = SessionStore(repository: repository, tokenStore: store)

        await session.restore()

        #expect(session.phase == .signedOut)
        #expect(await store.load() == nil)
        #expect(await repository.refreshCount == 0)
    }

    // MARK: Refresh

    @Test("Invalid refresh credentials end the session")
    func invalidRefreshEndsSession() async {
        let stale = makeResponse(accessExpiresIn: -60)
        let store = InMemoryTokenStore(session: stale)
        let repository = MockAuthRepository(refresh: .failure(.unauthorized(nil)))
        let session = SessionStore(repository: repository, tokenStore: store)

        let recovered = await session.recoverUnauthorized()

        #expect(!recovered)
        #expect(session.phase == .signedOut)
        #expect(await store.load() == nil)
    }

    @Test("Concurrent 401s share one refresh")
    func refreshIsSingleFlight() async {
        let auth = makeResponse()
        let store = InMemoryTokenStore(session: auth)
        let refreshed = makeResponse(accessToken: "rotated", refreshToken: "refresh-2")
        let repository = MockAuthRepository(refresh: .success(refreshed), refreshDelay: .milliseconds(80))
        let session = SessionStore(repository: repository, tokenStore: store)

        let results = await withTaskGroup(of: Bool.self) { group in
            for _ in 0..<6 {
                group.addTask { await session.recoverUnauthorized() }
            }

            var values: [Bool] = []
            for await value in group {
                values.append(value)
            }
            return values
        }

        #expect(results.allSatisfy { $0 })
        #expect(await repository.refreshCount == 1)
        #expect(session.user == refreshed.user)
    }

    // MARK: Sign out

    @Test("Sign out revokes the session and clears storage")
    func signOutClearsSession() async {
        let auth = makeResponse()
        let store = InMemoryTokenStore(session: auth)
        let repository = MockAuthRepository()
        let session = SessionStore(repository: repository, tokenStore: store)
        await session.restore()

        await session.signOut()

        #expect(session.phase == .signedOut)
        #expect(await store.load() == nil)
        #expect(await repository.logoutCount == 1)
    }
}

// MARK: - Mock

private actor MockAuthRepository: AuthRepositoryProtocol {
    private let loginResult: Result<AuthResponse, APIError>
    private let registerResult: Result<AuthResponse, APIError>
    private let refreshResult: Result<AuthResponse, APIError>
    private let meResult: Result<UserProfile, APIError>
    private let refreshDelay: Duration?

    private(set) var loginCount = 0
    private(set) var refreshCount = 0
    private(set) var meCount = 0
    private(set) var logoutCount = 0

    init(
        login: Result<AuthResponse, APIError> = .failure(.unauthorized(nil)),
        register: Result<AuthResponse, APIError> = .failure(.unauthorized(nil)),
        refresh: Result<AuthResponse, APIError> = .failure(.unauthorized(nil)),
        me: Result<UserProfile, APIError> = .failure(.unauthorized(nil)),
        refreshDelay: Duration? = nil
    ) {
        loginResult = login
        registerResult = register
        refreshResult = refresh
        meResult = me
        self.refreshDelay = refreshDelay
    }

    func login(email: String, password: String) async throws -> AuthResponse {
        loginCount += 1
        return try loginResult.get()
    }

    func register(email: String, displayName: String, password: String) async throws -> AuthResponse {
        try registerResult.get()
    }

    func refresh(refreshToken: String) async throws -> AuthResponse {
        refreshCount += 1
        if let refreshDelay {
            try? await Task.sleep(for: refreshDelay)
        }
        return try refreshResult.get()
    }

    func logout() async throws {
        logoutCount += 1
    }

    func me() async throws -> UserProfile {
        meCount += 1
        return try meResult.get()
    }
}
