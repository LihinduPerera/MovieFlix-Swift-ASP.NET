import Observation

/// Shared, typed app-level navigation state. Feature views use this instead of
/// reaching into the tab view.
@MainActor
@Observable
final class AppNavigation {
    var selectedTab: AppTab = .home
}
