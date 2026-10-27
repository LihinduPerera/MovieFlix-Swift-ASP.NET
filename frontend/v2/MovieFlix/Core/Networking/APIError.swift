import Foundation

/// Every failure surfaced by the networking layer. UI can render this directly
/// without knowing about HTTP or transport details.
nonisolated enum APIError: Error, Sendable, Equatable {
    case invalidConfiguration(String)
    case invalidURL
    case invalidResponse
    case transport(String)
    case offline
    case timeout
    case cancelled
    case badRequest(ProblemDetails?)
    case validation(ProblemDetails)
    case unauthorized(ProblemDetails?)
    case forbidden(ProblemDetails?)
    case notFound(ProblemDetails?)
    case conflict(ProblemDetails?)
    case locked(ProblemDetails?)
    case rateLimited(retryAfter: TimeInterval?, ProblemDetails?)
    case server(status: Int, ProblemDetails?)
    case decoding(String)
    case unexpected(status: Int, body: String?)

    // MARK: Presentation

    var title: String {
        switch self {
        case .invalidConfiguration, .invalidURL, .invalidResponse:
            "Can't Reach MovieFlix"
        case .transport, .offline:
            "You're Offline"
        case .timeout:
            "Taking Too Long"
        case .cancelled:
            "Cancelled"
        case .badRequest, .validation:
            "Check Your Details"
        case .unauthorized:
            "Sign In Required"
        case .forbidden:
            "Not Allowed"
        case .notFound:
            "Not Found"
        case .conflict:
            "Already Exists"
        case .locked:
            "Account Locked"
        case .rateLimited:
            "Slow Down"
        case .server:
            "Service Unavailable"
        case .decoding:
            "Something Went Wrong"
        case .unexpected:
            "Unexpected Error"
        }
    }

    var message: String {
        switch self {
        case let .invalidConfiguration(detail):
            return detail
        case .invalidURL:
            return "The request address is invalid."
        case .invalidResponse:
            return "The server returned a response MovieFlix couldn't understand."
        case let .transport(detail):
            return detail
        case .offline:
            return "Check your Wi-Fi or cellular connection and try again."
        case .timeout:
            return "The server took too long to respond. Please try again."
        case .cancelled:
            return "The request was cancelled."
        case let .badRequest(problem):
            return problem?.bestMessage ?? "The server couldn't accept that request."
        case let .validation(problem):
            return problem.validationSummary ?? "Please correct the highlighted fields."
        case let .unauthorized(problem):
            return problem?.bestMessage ?? "Your session has expired. Sign in to continue."
        case let .forbidden(problem):
            return problem?.bestMessage ?? "You don't have permission to do that."
        case let .notFound(problem):
            return problem?.bestMessage ?? "We couldn't find what you were looking for."
        case let .conflict(problem):
            return problem?.bestMessage ?? "That item already exists."
        case let .locked(problem):
            return problem?.bestMessage ?? "Too many failed attempts. Try again in a few minutes."
        case let .rateLimited(retryAfter, problem):
            if let retryAfter {
                let seconds = max(1, Int(retryAfter.rounded(.up)))
                return "Too many requests. Try again in \(seconds) second\(seconds == 1 ? "" : "s")."
            }
            return problem?.bestMessage ?? "Too many requests. Please wait a moment."
        case let .server(status, problem):
            if status == 502 || status == 503 {
                return "The movie service is temporarily unavailable. Please try again shortly."
            }
            return problem?.bestMessage ?? "The server couldn't complete the request."
        case let .decoding(detail):
            return detail
        case let .unexpected(status, body):
            if let body, !body.isEmpty {
                return "The server returned an unexpected response (\(status))."
            }
            return "The server returned an unexpected response (\(status))."
        }
    }

    var recoverySuggestion: String? {
        switch self {
        case .offline, .timeout, .transport:
            "Check your connection, then try again."
        case .invalidConfiguration:
            "Open Config/Debug.xcconfig and verify API_BASE_URL."
        case .unauthorized:
            "Sign in again to refresh your session."
        case .rateLimited:
            "Wait for the timer to expire before trying again."
        case .server:
            "This is usually temporary. Try again in a moment."
        default:
            nil
        }
    }

    var systemImage: String {
        switch self {
        case .offline, .transport, .invalidConfiguration, .invalidURL:
            "wifi.exclamationmark"
        case .timeout:
            "clock.badge.exclamationmark"
        case .cancelled:
            "xmark.circle"
        case .unauthorized:
            "person.crop.circle.badge.exclamationmark"
        case .forbidden:
            "hand.raised"
        case .notFound:
            "questionmark.circle"
        case .conflict:
            "rectangle.on.rectangle"
        case .locked:
            "lock"
        case .rateLimited:
            "hourglass"
        case .server:
            "server.rack"
        case .badRequest, .validation:
            "exclamationmark.bubble"
        case .decoding, .invalidResponse, .unexpected:
            "exclamationmark.triangle"
        }
    }

    var isRetryable: Bool {
        switch self {
        case .offline, .timeout, .transport, .rateLimited, .server, .invalidResponse:
            true
        default:
            false
        }
    }

    var needsAuthentication: Bool {
        if case .unauthorized = self { return true }
        return false
    }

    var problemDetails: ProblemDetails? {
        switch self {
        case let .badRequest(problem),
             let .unauthorized(problem),
             let .forbidden(problem),
             let .notFound(problem),
             let .conflict(problem),
             let .locked(problem),
             let .server(_, problem):
            problem
        case let .validation(problem):
            problem
        case let .rateLimited(_, problem):
            problem
        default:
            nil
        }
    }

    var fieldErrors: [String: [String]] {
        problemDetails?.fieldErrors ?? [:]
    }
}

extension APIError: LocalizedError {
    var errorDescription: String? { title }
    var failureReason: String? { message }
}

extension APIError: CustomStringConvertible {
    var description: String { "\(title): \(message)" }
}
