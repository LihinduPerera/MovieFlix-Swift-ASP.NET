import SwiftUI

@main
struct MovieFlixApp: App {
    var body: some Scene {
        WindowGroup {
            AppRoot()
        }
    }
}

struct AppRoot: View {
    private let bootstrap: Result<AppContainer, Error>

    init() {
        bootstrap = Result { try AppContainer.live() }
    }

    var body: some View {
        Group {
            switch bootstrap {
            case let .success(container):
                AppShell(container: container)
                    .environment(\.imagePipeline, container.imagePipeline)
                    .mfToastOverlay(container.toast, bottomPadding: 68)
                    .task {
                        await container.session.restore()
                    }
            case let .failure(error):
                ConfigurationErrorView(error: error)
                    .environment(\.imagePipeline, ImagePipeline.preview)
            }
        }
        .tint(MFColor.violet)
        .background(MFColor.canvas)
    }
}

