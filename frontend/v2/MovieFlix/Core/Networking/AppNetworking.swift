import Foundation

nonisolated enum AppNetworking {
    static func makeSession(configuration: AppConfiguration, cacheName: String) -> URLSession {
        let sessionConfiguration = URLSessionConfiguration.default
        sessionConfiguration.requestCachePolicy = .useProtocolCachePolicy
        sessionConfiguration.timeoutIntervalForRequest = 30
        sessionConfiguration.timeoutIntervalForResource = 90
        sessionConfiguration.waitsForConnectivity = true
        sessionConfiguration.httpMaximumConnectionsPerHost = 6
        sessionConfiguration.httpShouldSetCookies = false
        sessionConfiguration.httpCookieStorage = nil
        sessionConfiguration.urlCache = URLCache(
            memoryCapacity: 32 * 1_024 * 1_024,
            diskCapacity: 256 * 1_024 * 1_024,
            diskPath: cacheName
        )

        #if DEBUG
        let delegate = configuration.apiBaseURL.host.map { host in
            DevServerTrustDelegate(allowedHost: host)
        }
        return URLSession(
            configuration: sessionConfiguration,
            delegate: delegate,
            delegateQueue: nil
        )
        #else
        return URLSession(configuration: sessionConfiguration)
        #endif
    }
}

#if DEBUG
/// Development convenience for a self-signed ASP.NET certificate. Scoped to
/// the configured host and compiled out of Release builds.
nonisolated final class DevServerTrustDelegate: NSObject, URLSessionDelegate, @unchecked Sendable {
    private let allowedHost: String

    init(allowedHost: String) {
        self.allowedHost = allowedHost
        super.init()
    }

    func urlSession(
        _ session: URLSession,
        didReceive challenge: URLAuthenticationChallenge,
        completionHandler: @escaping (URLSession.AuthChallengeDisposition, URLCredential?) -> Void
    ) {
        guard challenge.protectionSpace.authenticationMethod == NSURLAuthenticationMethodServerTrust,
              challenge.protectionSpace.host.caseInsensitiveCompare(allowedHost) == .orderedSame,
              let trust = challenge.protectionSpace.serverTrust
        else {
            completionHandler(.performDefaultHandling, nil)
            return
        }

        completionHandler(.useCredential, URLCredential(trust: trust))
    }
}
#endif
