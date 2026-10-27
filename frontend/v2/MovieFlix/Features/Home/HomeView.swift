import SwiftUI

struct HomeView: View {
    let container: AppContainer

    @State private var viewModel: HomeViewModel
    @State private var path: [AppRoute] = []
    @State private var showingAuth = false

    init(container: AppContainer) {
        self.container = container
        _viewModel = State(
            initialValue: HomeViewModel(
                repository: container.mediaRepository,
                libraryRepository: container.libraryRepository
            )
        )
    }

    var body: some View {
        GeometryReader { proxy in
            NavigationStack(path: $path) {
                ScrollView {
                    LazyVStack(alignment: .leading, spacing: MFSpace.xxl) {
                        switch viewModel.phase {
                        case .idle, .loading:
                            loadingContent
                        case let .failed(error):
                            failureContent(error, availableHeight: proxy.size.height)
                        case .loaded:
                            loadedContent
                        }
                    }
                    .padding(.bottom, MFSpace.xxxl)
                }
                .scrollIndicators(.hidden)
                .background(MFColor.canvas)
                .ignoresSafeArea(edges: .top)
                .refreshable {
                    await viewModel.load(force: true)
                }
                .safeAreaInset(edge: .top, spacing: 0) {
                    // Extend the chrome into the top safe area so the gradient
                    // starts at the screen edge, while the bar content stays
                    // below the status bar.
                    topBar
                        .padding(.top, 4)
                        .padding(.bottom, 6)
                        .padding(.top, proxy.safeAreaInsets.top)
                        .background {
                            topBarBackground(topInset: proxy.safeAreaInsets.top)
                        }
                        .ignoresSafeArea(edges: .top)
                }
                .toolbar(.hidden, for: .navigationBar)
                .navigationDestination(for: AppRoute.self) { route in
                    destination(for: route)
                }
            }
            .task {
                await viewModel.load()
            }
        }
        .background(MFColor.canvas)
        .sheet(isPresented: $showingAuth) {
            AuthFlowView(container: container)
        }
        .onChange(of: container.session.isSignedIn) { _, _ in
            Task { await viewModel.refreshHeroState() }
        }
        .onChange(of: path) { _, newPath in
            guard newPath.isEmpty else { return }
            Task { await viewModel.refreshHeroState() }
        }
    }

    // MARK: Content

    @ViewBuilder
    private var loadedContent: some View {
        if let hero = viewModel.hero {
            HeroHeader(
                media: hero,
                imageURL: container.backdropURL(for: hero, size: .backdropLarge),
                isFavorite: viewModel.heroIsFavorite,
                isInWatchlist: viewModel.heroIsInWatchlist,
                progress: nil,
                onDetails: { open(hero.id) },
                onFavorite: { toggleHeroFavorite() },
                onWatchlist: { toggleHeroWatchlist() }
            )
        }

        if let message = viewModel.partialErrorMessage {
            partialErrorBanner(message)
        }

        ForEach(viewModel.sections) { section in
            MediaRail(
                title: section.id.title,
                items: section.items,
                imageURL: { container.posterURL(for: $0) },
                onSelect: { media in open(media.id) }
            )
        }

        if viewModel.sections.isEmpty {
            MFEmptyStateView(
                systemImage: "popcorn",
                title: "Nothing to Watch Yet",
                message: "The movie service didn't return any titles. Pull down to try again.",
                primaryTitle: "Refresh",
                primaryAction: { Task { await viewModel.load(force: true) } }
            )
            .padding(.top, MFSpace.huge)
        }
    }

    private var loadingContent: some View {
        VStack(alignment: .leading, spacing: MFSpace.xxl) {
            HeroSkeleton()
            MediaRailSkeleton()
            MediaRailSkeleton()
            MediaRailSkeleton()
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Loading home")
    }

    private func failureContent(_ error: APIError, availableHeight: CGFloat) -> some View {
        MFErrorStateView(
            error: error,
            retry: { Task { await viewModel.load(force: true) } }
        )
        .frame(minHeight: max(availableHeight * 0.66, 420))
        .padding(.top, MFSpace.xxl)
    }

    private func partialErrorBanner(_ message: String) -> some View {
        HStack(spacing: MFSpace.xs) {
            Image(systemName: "wifi.exclamationmark")
                .font(.system(size: 14, weight: .semibold))
                .foregroundStyle(MFColor.warning)
                .accessibilityHidden(true)

            Text(message)
                .mfText(.caption)
                .foregroundStyle(MFColor.textSecondary)

            Spacer(minLength: MFSpace.xs)

            Button {
                viewModel.dismissPartialError()
            } label: {
                Image(systemName: "xmark")
                    .font(.system(size: 11, weight: .bold))
                    .foregroundStyle(MFColor.textTertiary)
                    .frame(width: 30, height: 30)
                    .contentShape(Rectangle())
            }
            .buttonStyle(.plain)
            .accessibilityLabel("Dismiss")
        }
        .padding(.leading, MFSpace.sm)
        .padding(.trailing, MFSpace.xxs)
        .padding(.vertical, MFSpace.xxs)
        .background {
            RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                .fill(MFColor.surface)
        }
        .overlay {
            RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                .strokeBorder(MFColor.border, lineWidth: 1)
        }
        .padding(.horizontal, MFSpace.lg)
    }

    // MARK: Chrome

    private var topBar: some View {
        HStack(spacing: MFSpace.sm) {
            MFLogo(size: 19, showsMark: true)

            Spacer(minLength: MFSpace.sm)

            Button {
                if container.session.isSignedIn {
                    container.navigation.selectedTab = .profile
                } else {
                    showingAuth = true
                }
            } label: {
                if let user = container.session.user {
                    MFAvatar(initials: user.initials, size: 36)
                        .contentShape(Circle())
                } else {
                    Image(systemName: "person.crop.circle.fill")
                        .font(.system(size: 30, weight: .regular))
                        .foregroundStyle(.white)
                        .frame(width: MFSize.avatar + 8, height: MFSize.avatar + 8)
                        .background(
                            Circle()
                                .fill(.ultraThinMaterial)
                        )
                        .overlay(
                            Circle()
                                .strokeBorder(.white.opacity(0.35), lineWidth: 1)
                        )
                        .contentShape(Circle())
                }
            }
            .buttonStyle(.plain)
            .accessibilityLabel(container.session.isSignedIn ? "Open profile" : "Sign in")
        }
        .foregroundStyle(.white)
        .padding(.horizontal, MFSpace.lg)
        .padding(.top, 2)
        .padding(.bottom, 12)
    }

    private func topBarBackground(topInset: CGFloat) -> some View {
        VStack(spacing: 0) {
            Color.black.opacity(0.9)
                .frame(height: topInset)

            LinearGradient(
                colors: [
                    Color.black.opacity(0.9),
                    Color.black.opacity(0.55),
                    Color.black.opacity(0.2),
                    .clear
                ],
                startPoint: .top,
                endPoint: .bottom
            )
        }
        .allowsHitTesting(false)
    }

    // MARK: Navigation

    @ViewBuilder
    private func destination(for route: AppRoute) -> some View {
        switch route {
        case let .media(mediaID):
            MediaDetailView(
                container: container,
                mediaID: mediaID,
                onSelectMedia: { next in path.append(.media(next)) }
            )
        case .season, .collection, .reviews, .genre:
            EmptyView()
        }
    }

    private func open(_ mediaID: MediaID) {
        path.append(.media(mediaID))
    }

    private func toggleHeroFavorite() {
        guard container.session.isSignedIn else {
            showingAuth = true
            return
        }

        let wasFavorite = viewModel.heroIsFavorite
        Task {
            if let error = await viewModel.toggleHeroFavorite() {
                container.toast.error(error.message)
            } else {
                container.toast.success(
                    wasFavorite ? "Removed from favorites." : "Added to favorites."
                )
            }
        }
    }

    private func toggleHeroWatchlist() {
        guard container.session.isSignedIn else {
            showingAuth = true
            return
        }

        let wasSaved = viewModel.heroIsInWatchlist
        Task {
            if let error = await viewModel.toggleHeroWatchlist() {
                container.toast.error(error.message)
            } else {
                container.toast.success(
                    wasSaved ? "Removed from watchlist." : "Added to watchlist."
                )
            }
        }
    }
}

#Preview {
    let configuration = AppConfiguration(
        apiBaseURL: URL(string: "https://10.35.255.69:7014")!,
        imageBaseURL: URL(string: "https://image.tmdb.org/t/p")!,
        environment: .preview,
        appVersion: "1.0",
        buildNumber: "1",
        userAgent: "MovieFlix Preview"
    )
    HomeView(container: AppContainer(configuration: configuration))
        .environment(\.imagePipeline, ImagePipeline.preview)
}
