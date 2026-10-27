import Foundation
import Observation

/// Owns the signed-in session and the single-flight refresh used by `APIClient`
/// when a protected request receives 401.
@MainActor
@Observable
final class SessionStore {
    enum Phase: Equatable {
        case restoring
        case signedOut
        case signedIn(UserProfile)
    }

    private let repository: any AuthRepositoryProtocol
    private let tokenStore: any TokenStoring

    private var refreshTask: Task<RefreshOutcome, Never>?
    private var didRestore = false

    private(set) var phase: Phase = .restoring
    private(set) var isSubmitting = false
    private(set) var isSigningOut = false
    private(set) var isRefreshingProfile = false
    private(set) var errorMessage: String?
    private(set) var fieldErrors: [String: [String]] = [:]

    init(
        repository: any AuthRepositoryProtocol,
        tokenStore: any TokenStoring
    ) {
        self.repository = repository
        self.tokenStore = tokenStore
    }

    // MARK: Derived state

    var user: UserProfile? {
        if case let .signedIn(user) = phase { return user }
        return nil
    }

    var isSignedIn: Bool { user != nil }

    func clearError() {
        errorMessage = nil
        fieldErrors = [:]
    }

    // MARK: Restore

    /// Called once when the root view appears. Restores a persisted session and
    /// refreshes it when the access token is about to expire.
    func restore() async {
        guard !didRestore else { return }
        didRestore = true

        guard let session = await tokenStore.load() else {
            phase = .signedOut
            return
        }

        guard !session.isRefreshTokenExpired else {
            await tokenStore.clear()
            phase = .signedOut
            return
        }

        if session.accessTokenExpiresAt > Date().addingTimeInterval(60) {
            phase = .signedIn(session.user)
            await refreshProfile()
        } else {
            _ = await recoverUnauthorized()
        }
    }

    // MARK: Sign in / register

    @discardableResult
    func signIn(email: String, password: String) async -> Bool {
        await authenticate {
            try await self.repository.login(
                email: email.trimmingCharacters(in: .whitespacesAndNewlines),
                password: password
            )
        }
    }

    @discardableResult
    func register(email: String, displayName: String, password: String) async -> Bool {
        await authenticate {
            try await self.repository.register(
                email: email.trimmingCharacters(in: .whitespacesAndNewlines),
                displayName: displayName.trimmingCharacters(in: .whitespacesAndNewlines),
                password: password
            )
        }
    }

    private func authenticate(
        _ operation: () async throws -> AuthResponse
    ) async -> Bool {
        guard !isSubmitting else { return false }
        isSubmitting = true
        errorMessage = nil
        fieldErrors = [:]
        defer { isSubmitting = false }

        do {
            let session = try await operation()
            await tokenStore.save(session)
            phase = .signedIn(session.user)
            return true
        } catch {
            if let apiError = error as? APIError {
                fieldErrors = apiError.fieldErrors
            }
            errorMessage = Self.message(for: error)
            return false
        }
    }

    // MARK: Sign out

    func signOut() async {
        guard !isSigningOut else { return }
        isSigningOut = true
        defer { isSigningOut = false }

        if let session = await tokenStore.load(),
           session.accessTokenExpiresAt <= Date().addingTimeInterval(30) {
            _ = await recoverUnauthorized()
        }

        try? await repository.logout()
        await tokenStore.clear()
        phase = .signedOut
        errorMessage = nil
    }

    // MARK: Profile

    func refreshProfile() async {
        guard isSignedIn, !isRefreshingProfile else { return }
        isRefreshingProfile = true
        defer { isRefreshingProfile = false }

        do {
            let user = try await repository.me()
            guard isSignedIn else { return }
            if let session = await tokenStore.load() {
                await tokenStore.save(session.replacingUser(user))
            }
            phase = .signedIn(user)
        } catch let error as APIError where error.needsAuthentication {
            _ = await recoverUnauthorized()
        } catch {
            // Profile enrichment is best-effort; the cached user remains valid.
        }
    }

    // MARK: Single-flight refresh

    /// Invoked by `APIClient` after a protected request returns 401. Concurrent
    /// callers share one refresh so a rotated token can never be replayed.
    @discardableResult
    func recoverUnauthorized() async -> Bool {
        if let existing = refreshTask {
            return await apply(existing.value)
        }

        let repository = self.repository
        let tokenStore = self.tokenStore
        let task = Task<RefreshOutcome, Never> {
            guard let session = await tokenStore.load(),
                  !session.isRefreshTokenExpired
            else {
                return .invalidSession
            }

            do {
                let refreshed = try await repository.refresh(refreshToken: session.refreshToken)
                return .success(refreshed)
            } catch {
                return Self.isInvalidSession(error) ? .invalidSession : .transient
            }
        }

        refreshTask = task
        let outcome = await task.value
        refreshTask = nil
        return await apply(outcome)
    }

    private func apply(_ outcome: RefreshOutcome) async -> Bool {
        switch outcome {
        case let .success(session):
            await tokenStore.save(session)
            phase = .signedIn(session.user)
            errorMessage = nil
            return true

        case .invalidSession:
            await tokenStore.clear()
            phase = .signedOut
            return false

        case .transient:
            // A network blip must not sign the user out. If the session was
            // being restored, fall back to the cached profile so the app stays
            // usable offline.
            if case .restoring = phase,
               let session = await tokenStore.load(),
               !session.isRefreshTokenExpired {
                phase = .signedIn(session.user)
            }
            return false
        }
    }

    // MARK: Helpers

    private nonisolated static func isInvalidSession(_ error: Error) -> Bool {
        guard let error = error as? APIError else { return false }
        switch error {
        case .unauthorized, .forbidden, .notFound:
            return true
        default:
            return false
        }
    }

    private nonisolated static func message(for error: Error) -> String {
        if let error = error as? APIError {
            return error.message
        }
        return error.localizedDescription
    }

    private enum RefreshOutcome: Sendable {
        case success(AuthResponse)
        case invalidSession
        case transient
    }
}
