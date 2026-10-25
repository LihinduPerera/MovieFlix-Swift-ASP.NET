import SwiftUI

/// A dedicated discover screen reached from a genre chip in Search. It keeps
/// its own sort and minimum-rating filters so the discovery page stays simple.
struct GenreResultsView: View {
    let container: AppContainer
    let mediaType: MediaType
    let genreID: Int
    let name: String
    var onSelectMedia: (MediaID) -> Void = { _ in }

    @State private var viewModel: GenreResultsViewModel
    @Environment(\.dismiss) private var dismiss

    init(
        container: AppContainer,
        mediaType: MediaType,
        genreID: Int,
        name: String,
        onSelectMedia: @escaping (MediaID) -> Void = { _ in }
    ) {
        self.container = container
        self.mediaType = mediaType
        self.genreID = genreID
        self.name = name
        self.onSelectMedia = onSelectMedia
        _viewModel = State(
            initialValue: GenreResultsViewModel(
                mediaType: mediaType,
                genreID: genreID,
                genreName: name,
                repository: container.mediaRepository
            )
        )
    }

    var body: some View {
        GeometryReader { proxy in
            ScrollView {
                LazyVStack(alignment: .leading, spacing: MFSpace.xl) {
                    header(topInset: proxy.safeAreaInsets.top)
                    filterBar

                    switch viewModel.phase {
                    case .idle, .loading:
                        gridSkeleton
                    case let .failed(error):
                        MFErrorStateView(
                            error: error,
                            retry: { Task { await viewModel.load(force: true) } }
                        )
                        .frame(minHeight: max(proxy.size.height * 0.5, 320))
                    case .loaded:
                        if viewModel.items.isEmpty {
                            emptyState
                        } else {
                            posterGrid
                            footer
                        }
                    }
                }
                .padding(.bottom, MFSpace.huge)
            }
            .scrollIndicators(.hidden)
            .background(MFColor.canvas)
            .ignoresSafeArea(edges: .top)
            .overlay(alignment: .top) {
                chrome
                    .padding(.top, proxy.safeAreaInsets.top)
            }
        }
        .toolbar(.hidden, for: .navigationBar)
        .toolbar(.hidden, for: .tabBar)
        .navigationBarBackButtonHidden(true)
        .task {
            await viewModel.load()
        }
        .refreshable {
            await viewModel.load(force: true)
        }
    }

    // MARK: Chrome

    private var chrome: some View {
        HStack {
            Button {
                dismiss()
            } label: {
                Image(systemName: "chevron.left")
                    .font(.system(size: 16, weight: .bold))
                    .frame(width: MFSize.iconButton, height: MFSize.iconButton)
                    .background(Circle().fill(.ultraThinMaterial))
                    .overlay {
                        Circle().strokeBorder(MFColor.border, lineWidth: 1)
                    }
                    .contentShape(Circle())
            }
            .buttonStyle(.plain)
            .accessibilityLabel("Back")

            Spacer()
        }
        .foregroundStyle(MFColor.textPrimary)
        .padding(.horizontal, MFSpace.lg)
    }

    // MARK: Header

    private func header(topInset: CGFloat) -> some View {
        ZStack(alignment: .bottomLeading) {
            LinearGradient(
                colors: [
                    MFColor.violet.opacity(0.34),
                    MFColor.magenta.opacity(0.16),
                    MFColor.canvas
                ],
                startPoint: .topLeading,
                endPoint: .bottom
            )

            Circle()
                .fill(MFColor.magenta.opacity(0.22))
                .frame(width: 240, height: 240)
                .blur(radius: 80)
                .offset(x: 150, y: -60)
                .accessibilityHidden(true)

            VStack(alignment: .leading, spacing: MFSpace.xs) {
                Text(mediaType.pluralDisplayName.uppercased())
                    .mfText(.overline)
                    .foregroundStyle(MFColor.textSecondary)
                    .tracking(1.2)

                Text(name)
                    .mfText(.display)
                    .lineLimit(2)
                    .minimumScaleFactor(0.72)
                    .accessibilityAddTraits(.isHeader)

                Text(headerSubtitle)
                    .mfText(.caption)
                    .foregroundStyle(MFColor.textSecondary)
            }
            .padding(.horizontal, MFSpace.lg)
            .padding(.top, max(76, topInset + 56))
            .padding(.bottom, MFSpace.lg)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .accessibilityElement(children: .contain)
    }

    private var headerSubtitle: String {
        switch viewModel.phase {
        case .loaded:
            viewModel.countSummary
        case .failed:
            "Discover \(mediaType.pluralDisplayName.lowercased()) in \(name)"
        case .idle, .loading:
            "Finding the best of \(name)"
        }
    }

    // MARK: Filters

    private var filterBar: some View {
        VStack(alignment: .leading, spacing: MFSpace.md) {
            filterRow(title: "Sort") {
                ForEach(GenreSort.allCases) { option in
                    MFChoiceChip(
                        title: option.displayName,
                        isSelected: viewModel.sort == option
                    ) {
                        Task { await viewModel.selectSort(option) }
                    }
                }
            }

            filterRow(title: "Minimum Rating") {
                ForEach(GenreRatingFilter.allCases) { option in
                    MFChoiceChip(
                        title: option.label,
                        isSelected: viewModel.rating == option
                    ) {
                        Task { await viewModel.selectRating(option) }
                    }
                }
            }
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Discover filters")
    }

    private func filterRow<Content: View>(
        title: String,
        @ViewBuilder content: () -> Content
    ) -> some View {
        VStack(alignment: .leading, spacing: MFSpace.xs) {
            Text(title)
                .mfText(.overline)
                .foregroundStyle(MFColor.textTertiary)
                .padding(.horizontal, MFSpace.lg)

            ScrollView(.horizontal) {
                HStack(spacing: MFSpace.xs) {
                    content()
                }
                .padding(.horizontal, MFSpace.lg)
            }
            .scrollIndicators(.hidden)
        }
    }

    // MARK: Results

    private var posterGrid: some View {
        LazyVGrid(
            columns: [
                GridItem(
                    .adaptive(minimum: 104, maximum: 168),
                    spacing: MFSpace.sm,
                    alignment: .top
                )
            ],
            alignment: .leading,
            spacing: MFSpace.lg
        ) {
            ForEach(viewModel.items) { media in
                MediaPosterCard(
                    media: media,
                    imageURL: container.posterURL(for: media),
                    width: nil,
                    action: { onSelectMedia(media.id) }
                )
                .onAppear {
                    guard media.id == viewModel.items.last?.id else { return }
                    Task { await viewModel.loadMore() }
                }
            }
        }
        .padding(.horizontal, MFSpace.lg)
    }

    @ViewBuilder
    private var footer: some View {
        if viewModel.isLoadingMore {
            ProgressView()
                .controlSize(.small)
                .frame(maxWidth: .infinity)
                .padding(.top, MFSpace.xs)
                .accessibilityLabel("Loading more titles")
        } else if let error = viewModel.loadMoreError {
            VStack(spacing: MFSpace.sm) {
                MFNoticeBanner(
                    message: error.message,
                    systemImage: "arrow.clockwise"
                )
                MFButton(title: "Try Again", role: .secondary) {
                    Task { await viewModel.retryLoadMore() }
                }
            }
            .padding(.horizontal, MFSpace.lg)
        } else if viewModel.hasMore {
            MFButton(
                title: "Load More",
                systemImage: "arrow.down",
                role: .secondary,
                isFullWidth: true
            ) {
                Task { await viewModel.loadMore() }
            }
            .padding(.horizontal, MFSpace.lg)
        }
    }

    private var emptyState: some View {
        MFEmptyStateView(
            systemImage: "film.stack",
            title: "No Titles Found",
            message: "Nothing in \(name) matches these filters. Try a lower minimum rating or a different sort.",
            primaryTitle: "Clear Filters",
            primaryAction: {
                Task { await viewModel.resetFilters() }
            }
        )
        .padding(.top, MFSpace.md)
    }

    // MARK: Skeleton

    private var gridSkeleton: some View {
        LazyVGrid(
            columns: [
                GridItem(
                    .adaptive(minimum: 104, maximum: 168),
                    spacing: MFSpace.sm,
                    alignment: .top
                )
            ],
            alignment: .leading,
            spacing: MFSpace.lg
        ) {
            ForEach(0..<9, id: \.self) { _ in
                VStack(alignment: .leading, spacing: MFSpace.xs) {
                    SkeletonBlock(cornerRadius: MFRadius.poster)
                        .aspectRatio(MFSize.posterAspectRatio, contentMode: .fit)

                    SkeletonBlock(cornerRadius: MFRadius.xs)
                        .frame(height: 12)

                    SkeletonBlock(cornerRadius: MFRadius.xs)
                        .frame(width: 60, height: 10)
                }
            }
        }
        .padding(.horizontal, MFSpace.lg)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Loading titles")
    }
}
