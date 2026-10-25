import Foundation

/// RFC 7807 payload returned by the API for validation and failure responses.
nonisolated struct ProblemDetails: Decodable, Hashable, Sendable {
    let type: String?
    let title: String?
    let status: Int?
    let detail: String?
    let instance: String?
    let errors: [String: [String]]?
    let traceId: String?

    var fieldErrors: [String: [String]] { errors ?? [:] }

    var isValidationFailure: Bool {
        errors?.isEmpty == false
    }

    /// A compact, deterministic summary suitable for a form or alert.
    var validationSummary: String? {
        guard let errors, !errors.isEmpty else { return nil }
        return errors
            .sorted { $0.key.localizedStandardCompare($1.key) == .orderedAscending }
            .flatMap { key, messages in
                messages.map { "\(key): \($0)" }
            }
            .joined(separator: "\n")
    }

    var bestMessage: String? {
        validationSummary ?? detail ?? title
    }
}
