import SwiftUI

struct AppShell: View {
    let container: AppContainer

    var body: some View {
        @Bindable var navigation = container.navigation

        TabView(selection: $navigation.selectedTab) {
            Tab(AppTab.home.title, systemImage: AppTab.home.selectedSystemImage, value: AppTab.home) {
                HomeView(container: container)
            }

            Tab(AppTab.search.title, systemImage: AppTab.search.systemImage, value: AppTab.search) {
                SearchView(container: container)
            }

            Tab(AppTab.library.title, systemImage: AppTab.library.systemImage, value: AppTab.library) {
                LibraryView(container: container)
            }

            Tab(AppTab.profile.title, systemImage: AppTab.profile.systemImage, value: AppTab.profile) {
                ProfileView(container: container)
            }
        }
        .tint(MFColor.violet)
        .background(MFColor.canvas)
    }
}
