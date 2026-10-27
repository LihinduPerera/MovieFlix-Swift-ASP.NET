import Foundation

nonisolated enum TMDBImageSize: String, Sendable {
    case poster = "w342"
    case posterLarge = "w500"
    case backdrop = "w780"
    case backdropLarge = "w1280"
    case profile = "w185"
    case still = "w300"
    case original = "original"
}

/// The API returns raw TMDB paths such as `/n8b4c.jpg`; the client owns CDN
/// sizing. Centralizing that rule keeps models free of presentation concerns.
nonisolated struct TMDBImageURLBuilder: Sendable {
    let baseURL: URL

    func url(for path: String?, size: TMDBImageSize) -> URL? {
        guard let path else { return nil }
        let trimmed = path.trimmingCharacters(in: .whitespacesAndNewlines)
        guard !trimmed.isEmpty else { return nil }

        let withoutLeadingSlash = trimmed.hasPrefix("/") ? String(trimmed.dropFirst()) : trimmed
        guard !withoutLeadingSlash.isEmpty else { return nil }

        return baseURL
            .appending(path: size.rawValue)
            .appending(path: withoutLeadingSlash)
    }

    func urls(for paths: [String], size: TMDBImageSize) -> [URL] {
        paths.compactMap { url(for: $0, size: size) }
    }
}
