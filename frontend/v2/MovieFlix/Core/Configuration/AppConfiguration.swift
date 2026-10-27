import Foundation

nonisolated enum AppEnvironment: String, Sendable {
    case debug
    case release
    case preview
}

nonisolated enum AppConfigurationError: LocalizedError, Sendable, Equatable {
    case missingValue(String)
    case invalidURL(String)
    case insecureURL(String)

    var errorDescription: String? {
        switch self {
        case let .missingValue(key):
            "Missing \(key) in Info.plist. Set it in Config/Debug.xcconfig."
        case let .invalidURL(value):
            "'\(value)' is not a valid API base URL."
        case let .insecureURL(value):
            "Release builds require HTTPS, but '\(value)' uses an insecure scheme."
        }
    }
}

nonisolated struct AppConfiguration: Sendable {
    let apiBaseURL: URL
    let imageBaseURL: URL
    let environment: AppEnvironment
    let appVersion: String
    let buildNumber: String
    let userAgent: String

    var isDebug: Bool { environment == .debug }

    var displayHost: String {
        var value = apiBaseURL.host ?? apiBaseURL.absoluteString
        if let port = apiBaseURL.port {
            value += ":\(port)"
        }
        return value
    }

    static func live(
        bundle: Bundle = .main,
        environment: AppEnvironment = {
            #if DEBUG
            .debug
            #else
            .release
            #endif
        }()
    ) throws -> AppConfiguration {
        let apiURL = try url(
            forInfoKey: "APIBaseURL",
            bundle: bundle,
            environment: environment
        )
        let imageURL = try url(
            forInfoKey: "TMDBImageBaseURL",
            bundle: bundle,
            environment: .release
        )

        let version = bundle.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "1.0"
        let build = bundle.object(forInfoDictionaryKey: "CFBundleVersion") as? String ?? "1"
        let osVersion = ProcessInfo.processInfo.operatingSystemVersionString
        let userAgent = "MovieFlix/\(version) (iOS; \(osVersion); build \(build))"

        return AppConfiguration(
            apiBaseURL: apiURL,
            imageBaseURL: imageURL,
            environment: environment,
            appVersion: version,
            buildNumber: build,
            userAgent: userAgent
        )
    }

    private static func url(
        forInfoKey key: String,
        bundle: Bundle,
        environment: AppEnvironment
    ) throws -> URL {
        guard let raw = bundle.object(forInfoDictionaryKey: key) as? String else {
            throw AppConfigurationError.missingValue(key)
        }

        let trimmed = raw.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else {
            throw AppConfigurationError.missingValue(key)
        }

        guard let components = URLComponents(string: trimmed),
              let scheme = components.scheme?.lowercased(),
              ["http", "https"].contains(scheme),
              let host = components.host,
              !host.isEmpty
        else {
            throw AppConfigurationError.invalidURL(trimmed)
        }

        if environment != .debug && scheme != "https" {
            throw AppConfigurationError.insecureURL(trimmed)
        }

        guard let url = components.url else {
            throw AppConfigurationError.invalidURL(trimmed)
        }
        return url
    }
}
