import Foundation

nonisolated enum AppTab: String, CaseIterable, Sendable, Identifiable {
    case home
    case search
    case library
    case profile

    var id: String { rawValue }

    var title: String {
        switch self {
        case .home: "Home"
        case .search: "Search"
        case .library: "Library"
        case .profile: "Profile"
        }
    }

    var systemImage: String {
        switch self {
        case .home: "house"
        case .search: "magnifyingglass"
        case .library: "rectangle.stack"
        case .profile: "person.crop.circle"
        }
    }

    var selectedSystemImage: String {
        switch self {
        case .home: "house.fill"
        case .search: "magnifyingglass"
        case .library: "rectangle.stack.fill"
        case .profile: "person.crop.circle.fill"
        }
    }
}

nonisolated enum AppRoute: Hashable, Sendable {
    case media(MediaID)
    case season(mediaID: MediaID, seasonNumber: Int)
    case collection(id: Int)
    case reviews(mediaID: MediaID)
    case genre(mediaType: MediaType, genreID: Int, name: String)
}
