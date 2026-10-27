import Foundation
import OSLog

nonisolated protocol AccessTokenProviding: Sendable {
    func accessToken() async -> String?
}

nonisolated struct AnonymousTokenProvider: AccessTokenProviding {
    func accessToken() async -> String? { nil }
}

nonisolated struct StaticTokenProvider: AccessTokenProviding {
    let token: String?

    func accessToken() async -> String? { token }
}

/// Invoked once when a protected request receives 401. The handler is expected
/// to refresh and persist credentials, then return `true` so the request can be
/// replayed. F1 supplies a single-flight implementation.
typealias UnauthorizedRecovery = @Sendable () async -> Bool

nonisolated struct APIClientConfiguration: Sendable {
    var baseURL: URL
    var userAgent: String
    var timeout: TimeInterval = 30

    init(baseURL: URL, userAgent: String, timeout: TimeInterval = 30) {
        self.baseURL = baseURL
        self.userAgent = userAgent
        self.timeout = timeout
    }
}

nonisolated final class APIClient: Sendable {
    private let configuration: APIClientConfiguration
    private let session: URLSession
    private let tokenProvider: any AccessTokenProviding
    private let unauthorizedRecovery: UnauthorizedRecovery?
    private let logger = Logger(subsystem: "com.LithiumStudios.MovieFlix", category: "networking")

    init(
        configuration: APIClientConfiguration,
        session: URLSession,
        tokenProvider: any AccessTokenProviding = AnonymousTokenProvider(),
        unauthorizedRecovery: UnauthorizedRecovery? = nil
    ) {
        self.configuration = configuration
        self.session = session
        self.tokenProvider = tokenProvider
        self.unauthorizedRecovery = unauthorizedRecovery
    }

    // MARK: Public API

    func send<T: Decodable & Sendable>(
        _ endpoint: APIEndpoint,
        as type: T.Type = T.self
    ) async throws -> T {
        let (data, _) = try await perform(endpoint, allowRecovery: true)
        guard !data.isEmpty else {
            throw APIError.decoding("The server returned an empty response. Please try again.")
        }

        do {
            return try JSONCoding.makeDecoder().decode(T.self, from: data)
        } catch {
            logger.error("Decoding failed for \(endpoint.path, privacy: .public): \(String(describing: error), privacy: .public)")
            throw APIError.decoding("We couldn't read the server's response. Please try again.")
        }
    }

    func send(_ endpoint: APIEndpoint) async throws {
        _ = try await perform(endpoint, allowRecovery: true)
    }

    func sendRaw(_ endpoint: APIEndpoint) async throws -> Data {
        let (data, _) = try await perform(endpoint, allowRecovery: true)
        return data
    }

    // MARK: Request execution

    private func perform(
        _ endpoint: APIEndpoint,
        allowRecovery: Bool
    ) async throws -> (Data, HTTPURLResponse) {
        let request: URLRequest
        do {
            request = try await makeRequest(for: endpoint)
        } catch let error as APIError {
            throw error
        } catch {
            throw APIError.transport(error.localizedDescription)
        }

        let startedAt = ContinuousClock.now

        do {
            let (data, response) = try await session.data(for: request)
            guard let http = response as? HTTPURLResponse else {
                throw APIError.invalidResponse
            }

            log(request: request, response: http, startedAt: startedAt)

            if (200..<300).contains(http.statusCode) {
                return (data, http)
            }

            if http.statusCode == 401,
               endpoint.requiresAuth,
               allowRecovery,
               let unauthorizedRecovery,
               await unauthorizedRecovery() {
                return try await perform(endpoint, allowRecovery: false)
            }

            throw Self.error(status: http.statusCode, data: data, response: http)
        } catch let error as APIError {
            throw error
        } catch let error as URLError {
            throw Self.mapTransport(error)
        } catch is CancellationError {
            throw APIError.cancelled
        } catch {
            throw APIError.transport(error.localizedDescription)
        }
    }

    private func makeRequest(for endpoint: APIEndpoint) async throws -> URLRequest {
        let url = try endpoint.url(relativeTo: configuration.baseURL)

        var request = URLRequest(
            url: url,
            cachePolicy: endpoint.cachePolicy,
            timeoutInterval: configuration.timeout
        )
        request.httpMethod = endpoint.method.rawValue
        request.setValue("application/json", forHTTPHeaderField: "Accept")
        request.setValue(configuration.userAgent, forHTTPHeaderField: "User-Agent")

        for (name, value) in endpoint.headers {
            request.setValue(value, forHTTPHeaderField: name)
        }

        if let body = endpoint.body {
            do {
                request.httpBody = try JSONCoding.makeEncoder().encode(body)
            } catch {
                throw APIError.decoding("We couldn't prepare the request. Please try again.")
            }
            request.setValue("application/json", forHTTPHeaderField: "Content-Type")
        }

        let token = await tokenProvider.accessToken()
        if let token, !token.isEmpty {
            request.setValue("Bearer \(token)", forHTTPHeaderField: "Authorization")
        } else if endpoint.requiresAuth {
            throw APIError.unauthorized(nil)
        }

        return request
    }

    // MARK: Error mapping

    static func error(
        status: Int,
        data: Data,
        response: HTTPURLResponse
    ) -> APIError {
        let problem = decodeProblem(from: data)

        switch status {
        case 400:
            if let problem, problem.isValidationFailure {
                return .validation(problem)
            }
            return .badRequest(problem)
        case 401:
            return .unauthorized(problem)
        case 403:
            return .forbidden(problem)
        case 404:
            return .notFound(problem)
        case 409:
            return .conflict(problem)
        case 423:
            return .locked(problem)
        case 429:
            return .rateLimited(
                retryAfter: retryAfter(from: response.value(forHTTPHeaderField: "Retry-After")),
                problem
            )
        case 500..<600:
            return .server(status: status, problem)
        default:
            let body = String(data: data.prefix(512), encoding: .utf8)
            return .unexpected(status: status, body: body)
        }
    }

    private static func decodeProblem(from data: Data) -> ProblemDetails? {
        guard !data.isEmpty else { return nil }
        return try? JSONCoding.makeDecoder().decode(ProblemDetails.self, from: data)
    }

    static func retryAfter(from raw: String?) -> TimeInterval? {
        guard let raw else { return nil }
        let value = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !value.isEmpty else { return nil }

        if let seconds = TimeInterval(value) {
            return max(0, seconds)
        }

        let formatter = DateFormatter()
        formatter.locale = Locale(identifier: "en_US_POSIX")
        formatter.timeZone = TimeZone(secondsFromGMT: 0)
        formatter.dateFormat = "EEE',' dd MMM yyyy HH':'mm':'ss z"
        guard let date = formatter.date(from: value) else { return nil }
        return max(0, date.timeIntervalSinceNow)
    }

    private static func mapTransport(_ error: URLError) -> APIError {
        switch error.code {
        case .cancelled:
            .cancelled
        case .timedOut:
            .timeout
        case .notConnectedToInternet,
             .networkConnectionLost,
             .dataNotAllowed,
             .internationalRoamingOff:
            .offline
        default:
            .transport(error.localizedDescription)
        }
    }

    private func log(request: URLRequest, response: HTTPURLResponse, startedAt: ContinuousClock.Instant) {
        let elapsed = startedAt.duration(to: .now)
        let milliseconds = Int(elapsed.components.seconds * 1_000)
            + Int(elapsed.components.attoseconds / 1_000_000_000_000_000)
        logger.debug(
            "\(request.httpMethod ?? "GET", privacy: .public) \(request.url?.path ?? "", privacy: .public) -> \(response.statusCode, privacy: .public) (\(milliseconds, privacy: .public)ms)"
        )
    }
}
