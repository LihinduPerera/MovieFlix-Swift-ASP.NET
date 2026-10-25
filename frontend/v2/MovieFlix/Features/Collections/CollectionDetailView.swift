import SwiftUI

struct CollectionDetailView: View {
    let container: AppContainer
    let collectionID: Int
    var onSelectMedia: (MediaID) -> Void = { _ in }
    var onDeleted: () -> Void = {}

    @State private var viewModel: CollectionDetailViewModel
    @State private var showingRename = false
    @State private var showingDeleteConfirmation = false
    @Environment(\.dismiss) private var dismiss

    init(
        container: AppContainer,
        collectionID: Int,
        onSelectMedia: @escaping (MediaID) -> Void = { _ in },
        onDeleted: @escaping () -> Void = {}
    ) {
        self.container = container
        self.collectionID = collectionID
        self.onSelectMedia = onSelectMedia
        self.onDeleted = onDeleted
        _viewModel = State(
            initialValue: CollectionDetailViewModel(
                collectionID: collectionID,
                repository: container.libraryRepository
            )
        )
    }

    var body: some View {
        GeometryReader { proxy in
            ScrollView {
                LazyVStack(alignment: .leading, spacing: MFSpace.xl) {
                    switch viewModel.phase {
                    case .loading:
                        detailSkeleton
                    case let .failed(error):
                        MFErrorStateView(
                            error: error,
                            retry: { Task { await viewModel.load(force: true) } }
                        )
                        .frame(minHeight: max(proxy.size.height * 0.6, 360))
                        .padding(.top, proxy.size.height * 0.2)
                    case .loaded:
                        if let collection = viewModel.collection {
                            content(collection)
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
            await viewModel.refresh()
        }
        .sheet(isPresented: $showingRename) {
            if let collection = viewModel.collection {
                CollectionEditorSheet(
                    mode: .rename(
                        name: collection.name,
                        description: collection.description
                    )
                ) { name, description in
                    await viewModel.rename(name: name, description: description)
                }
            }
        }
        .alert("Delete Collection?", isPresented: $showingDeleteConfirmation) {
            Button("Delete", role: .destructive) {
                Task { await deleteCollection() }
            }
            Button("Cancel", role: .cancel) {}
        } message: {
            Text("This removes the collection. Its titles stay in your library.")
        }
    }

    // MARK: Chrome

    private var chrome: some View {
        HStack(spacing: MFSpace.sm) {
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

            Spacer(minLength: 0)

            if viewModel.isOwner, let collection = viewModel.collection {
                Menu {
                    Button {
                        showingRename = true
                    } label: {
                        Label("Edit Collection", systemImage: "pencil")
                    }

                    Button(role: .destructive) {
                        showingDeleteConfirmation = true
                    } label: {
                        Label("Delete Collection", systemImage: "trash")
                    }
                } label: {
                    Image(systemName: "ellipsis")
                        .font(.system(size: 16, weight: .bold))
                        .frame(width: MFSize.iconButton, height: MFSize.iconButton)
                        .background(Circle().fill(.ultraThinMaterial))
                        .overlay {
                            Circle().strokeBorder(MFColor.border, lineWidth: 1)
                        }
                        .contentShape(Circle())
                }
                .accessibilityLabel("Collection options")
                .accessibilityValue(collection.name)
            }
        }
        .foregroundStyle(MFColor.textPrimary)
        .padding(.horizontal, MFSpace.lg)
    }

    // MARK: Content

    @ViewBuilder
    private func content(_ collection: CollectionDetail) -> some View {
        header(collection)

        VStack(alignment: .leading, spacing: MFSpace.lg) {
            metaRow(collection)

            if !collection.description.isEmpty {
                Text(collection.description)
                    .mfText(.body)
                    .foregroundStyle(MFColor.textSecondary)
                    .fixedSize(horizontal: false, vertical: true)
            }

            Divider()
                .overlay(MFColor.separator)
        }
        .padding(.horizontal, MFSpace.lg)

        if collection.items.isEmpty {
            emptyState(collection)
                .padding(.top, MFSpace.md)
        } else {
            posterGrid(collection)
        }
    }

    private func header(_ collection: CollectionDetail) -> some View {
        ZStack(alignment: .bottom) {
            CollectionCover(
                imageURLs: container.images.urls(
                    for: collection.coverPosterPaths,
                    size: .poster
                ),
                count: nil,
                aspectRatio: 1.55,
                cornerRadius: 0,
                showsBorder: false
            )
            .frame(maxWidth: .infinity)

            MFColor.heroScrim
                .allowsHitTesting(false)
        }
        .overlay(alignment: .bottomLeading) {
            Text(collection.name)
                .mfText(.heroTitle)
                .lineLimit(3)
                .minimumScaleFactor(0.75)
                .padding(.horizontal, MFSpace.lg)
                .padding(.bottom, MFSpace.md)
                .accessibilityAddTraits(.isHeader)
        }
        .accessibilityElement(children: .contain)
    }

    private func metaRow(_ collection: CollectionDetail) -> some View {
        HStack(spacing: MFSpace.sm) {
            MFAvatar(initials: initials(for: collection.ownerDisplayName), size: 34)

            VStack(alignment: .leading, spacing: 1) {
                Text(collection.isOwner ? "Your collection" : collection.ownerDisplayName)
                    .mfText(.label)
                    .lineLimit(1)

                Text(metaText(collection))
                    .mfText(.caption)
                    .foregroundStyle(MFColor.textTertiary)
            }

            Spacer(minLength: 0)

            MFTagPill(
                title: collection.updatedAt.formatted(date: .abbreviated, time: .omitted),
                systemImage: "clock"
            )
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel(
            "\(collection.isOwner ? "Your collection" : "By \(collection.ownerDisplayName)"), \(metaText(collection))"
        )
    }

    private func metaText(_ collection: CollectionDetail) -> String {
        collection.itemCount == 1 ? "1 title" : "\(collection.itemCount) titles"
    }

    private func posterGrid(_ collection: CollectionDetail) -> some View {
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
            ForEach(collection.items) { item in
                MediaPosterCard(
                    media: item.media,
                    imageURL: container.posterURL(for: item.media),
                    width: nil,
                    action: { onSelectMedia(item.id) }
                )
                .contextMenu {
                    if collection.isOwner {
                        Button(role: .destructive) {
                            Task { await remove(item.id) }
                        } label: {
                            Label("Remove from Collection", systemImage: "minus.circle")
                        }
                    }
                }
                .accessibilityAction(named: "Remove from Collection") {
                    guard collection.isOwner else { return }
                    Task { await remove(item.id) }
                }
            }
        }
        .padding(.horizontal, MFSpace.lg)
    }

    private func emptyState(_ collection: CollectionDetail) -> some View {
        MFEmptyStateView(
            systemImage: "square.stack.3d.up",
            title: collection.isOwner ? "Add your first title" : "Nothing here yet",
            message: collection.isOwner
                ? "Open any movie or show and use the collection button to add it here."
                : "The owner hasn't added any titles to this collection yet.",
            primaryTitle: collection.isOwner ? "Browse Home" : nil,
            primaryAction: collection.isOwner
                ? { container.navigation.selectedTab = .home }
                : nil
        )
    }

    // MARK: Skeleton

    private var detailSkeleton: some View {
        VStack(alignment: .leading, spacing: MFSpace.lg) {
            SkeletonBlock(cornerRadius: 0)
                .frame(height: 250)

            VStack(alignment: .leading, spacing: MFSpace.sm) {
                SkeletonBlock()
                    .frame(width: 220, height: 26)
                SkeletonBlock()
                    .frame(width: 160, height: 14)
                SkeletonBlock()
                    .frame(height: 12)
                SkeletonBlock()
                    .frame(width: 260, height: 12)
            }
            .padding(.horizontal, MFSpace.lg)

            LazyVGrid(
                columns: [
                    GridItem(.adaptive(minimum: 104, maximum: 168), spacing: MFSpace.sm, alignment: .top)
                ],
                alignment: .leading,
                spacing: MFSpace.lg
            ) {
                ForEach(0..<6, id: \.self) { _ in
                    VStack(alignment: .leading, spacing: MFSpace.xs) {
                        SkeletonBlock()
                            .aspectRatio(MFSize.posterAspectRatio, contentMode: .fit)
                            .clipShape(RoundedRectangle(cornerRadius: MFRadius.poster, style: .continuous))
                        SkeletonBlock()
                            .frame(height: 10)
                    }
                }
            }
            .padding(.horizontal, MFSpace.lg)
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Loading collection")
    }

    // MARK: Actions

    private func remove(_ mediaID: MediaID) async {
        if let error = await viewModel.removeItem(mediaID) {
            container.toast.error(error.message)
        } else {
            container.toast.success("Removed from collection.")
        }
    }

    private func deleteCollection() async {
        if let error = await viewModel.delete() {
            container.toast.error(error.message)
            return
        }

        onDeleted()
        container.toast.success("Collection deleted.")
        dismiss()
    }

    private func initials(for name: String) -> String {
        let parts = name.split(separator: " ").prefix(2)
        let value = parts.compactMap(\.first).map(String.init).joined()
        return value.isEmpty ? "?" : value.uppercased()
    }
}
