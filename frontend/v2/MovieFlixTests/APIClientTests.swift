import Foundation
import Testing

@testable import MovieFlix

@Suite("Authenticated requests", .serialized)
struct AuthenticatedRequestTests {
    @Test("A 401 triggers one refresh and replays the request with the new token")
    func refreshAndReplay() async throws {
        let provider = RotatingTokenProvider(token: "old")
        let recorder = HeaderRecorder()
        let userJSON = Self.userJSON

        StubURLProtocol.setHandler { request in
            let header = request.value(forHTTPHeaderField: "Authorization")
            recorder.record(header)

            if header == "Bearer old" {
                return (
                    401,
                    Data(#"{"title":"Unauthorized","status":401}"#.utf8)
                )
            }

            return (200, userJSON)
        }

        defer { StubURLProtocol.setHandler(nil) }

        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [StubURLProtocol.self]
        let networkingSession = URLSession(configuration: configuration)

        let client = APIClient(
            configuration: APIClientConfiguration(
                baseURL: URL(string: "https://movieflix.test")!,
                userAgent: "MovieFlixTests"
            ),
            session: networkingSession,
            tokenProvider: provider,
            unauthorizedRecovery: {
                await provider.setToken("new")
                return true
            }
        )

        let user = try await client.send(.me(), as: UserProfile.self)

        #expect(user.email == "ava@example.com")
        #expect(recorder.headers == ["Bearer old", "Bearer new"])
    }

    @Test("Protected endpoints fail fast when no token is available")
    func missingTokenFailsFast() async {
        let configuration = URLSessionConfiguration.ephemeral
        configuration.protocolClasses = [StubURLProtocol.self]
        StubURLProtocol.setHandler { _ in
            Issue.record("The request should not reach the network.")
            return (200, Data())
        }
        defer { StubURLProtocol.setHandler(nil) }

        let client = APIClient(
            configuration: APIClientConfiguration(
                baseURL: URL(string: "https://movieflix.test")!,
                userAgent: "MovieFlixTests"
            ),
            session: URLSession(configuration: configuration)
        )

        await #expect(throws: APIError.unauthorized(nil)) {
            try await client.send(.me(), as: UserProfile.self)
        }
    }

    @Test("Auth responses decode the absolute expiry timestamps")
    func decodesAuthResponse() throws {
        let data = Data(Self.authJSON.utf8)
        let response = try JSONCoding.makeDecoder().decode(AuthResponse.self, from: data)

        #expect(response.accessToken == "access-token")
        #expect(response.user.displayName == "Ava Chen")
        #expect(response.user.initials == "AC")
        #expect(response.refreshTokenExpiresAt > response.accessTokenExpiresAt)
    }

    // MARK: Fixtures

    private static let userJSON = Data(
        #"""
        {
          "id": "user-1",
          "email": "ava@example.com",
          "displayName": "Ava Chen",
          "createdAt": "2026-10-20T08:00:00Z",
          "roles": ["User"]
        }
        """#.utf8
    )

    private static let authJSON = """
    {
      "accessToken": "access-token",
      "accessTokenExpiresAt": "2026-10-26T12:00:00Z",
      "refreshToken": "refresh-token",
      "refreshTokenExpiresAt": "2026-11-02T11:00:00Z",
      "tokenType": "Bearer",
      "user": {
        "id": "user-1",
        "email": "ava@example.com",
        "displayName": "Ava Chen",
        "createdAt": "2026-10-20T08:00:00Z",
        "roles": ["User"]
      }
    }
    """
}

// MARK: - Helpers

private actor RotatingTokenProvider: AccessTokenProviding {
    private var token: String?

    init(token: String?) {
        self.token = token
    }

    func accessToken() async -> String? {
        token
    }

    func setToken(_ newValue: String?) {
        token = newValue
    }
}

private nonisolated final class HeaderRecorder: @unchecked Sendable {
    private let lock = NSLock()
    private var storage: [String?] = []

    var headers: [String?] {
        lock.lock()
        defer { lock.unlock() }
        return storage
    }

    func record(_ header: String?) {
        lock.lock()
        storage.append(header)
        lock.unlock()
    }
}

private nonisolated final class StubURLProtocol: URLProtocol {
    private static let lock = NSLock()
    nonisolated(unsafe) private static var handler: (@Sendable (URLRequest) -> (Int, Data))?

    static func setHandler(_ newValue: (@Sendable (URLRequest) -> (Int, Data))?) {
        lock.lock()
        handler = newValue
        lock.unlock()
    }

    private static func currentHandler() -> (@Sendable (URLRequest) -> (Int, Data))? {
        lock.lock()
        defer { lock.unlock() }
        return handler
    }

    override class func canInit(with request: URLRequest) -> Bool { true }

    override class func canonicalRequest(for request: URLRequest) -> URLRequest { request }

    override func startLoading() {
        guard let handler = Self.currentHandler(),
              let url = request.url
        else {
            client?.urlProtocol(self, didFailWithError: URLError(.badServerResponse))
            return
        }

        let (status, data) = handler(request)
        let response = HTTPURLResponse(
            url: url,
            statusCode: status,
            httpVersion: "HTTP/1.1",
            headerFields: ["Content-Type": "application/json"]
        )!

        client?.urlProtocol(self, didReceive: response, cacheStoragePolicy: .notAllowed)
        client?.urlProtocol(self, didLoad: data)
        client?.urlProtocolDidFinishLoading(self)
    }

    override func stopLoading() {}
}
