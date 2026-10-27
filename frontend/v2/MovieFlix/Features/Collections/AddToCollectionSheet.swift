import SwiftUI

struct AddToCollectionSheet: View {
    let container: AppContainer
    let mediaID: MediaID
    var mediaTitle: String?

    @Environment(\.dismiss) private var dismiss
    @State private var viewModel: AddToCollectionViewModel
    @State private var showingCreate = false
    @State private var errorMessage: String?

    init(container: AppContainer, mediaID: MediaID, mediaTitle: String? = nil) {
        self.container = container
        self.mediaID = mediaID
        self.mediaTitle = mediaTitle
        _viewModel = State(
            initialValue: AddToCollectionViewModel(
                mediaID: mediaID,
                repository: container.libraryRepository
            )
        )
    }

    var body: some View {
        VStack(spacing: 0) {
            header

            switch viewModel.phase {
            case .loading:
                loadingState
            case let .failed(error):
                MFErrorStateView(
                    error: error,
                    retry: { Task { await viewModel.retry() } }
                )
                .frame(maxHeight: .infinity)
            case .loaded:
                if viewModel.collections.isEmpty {
                    emptyState
                        .frame(maxHeight: .infinity)
                } else {
                    list
                }
            }

            footer
        }
        .background(MFColor.canvas)
        .presentationDetents([.medium, .large])
        .presentationDragIndicator(.visible)
        .task {
            await viewModel.load()
        }
        .sheet(isPresented: $showingCreate) {
            CollectionEditorSheet(mode: .create) { name, description in
                switch await viewModel.createCollection(name: name, description: description) {
                case .success:
                    return nil
                case let .failure(error):
                    return error
                }
            }
        }
    }

    // MARK: Chrome

    private var header: some View {
        VStack(alignment: .leading, spacing: MFSpace.sm) {
            HStack(alignment: .top, spacing: MFSpace.md) {
                VStack(alignment: .leading, spacing: 3) {
                    Text("Add to Collection")
                        .mfText(.title)
                        .accessibilityAddTraits(.isHeader)

                    Text(subtitle)
                        .mfText(.caption)
                        .foregroundStyle(MFColor.textTertiary)
                        .lineLimit(2)
                }

                Spacer(minLength: 0)

                Button {
                    dismiss()
                } label: {
                    Image(systemName: "xmark")
                        .font(.system(size: 13, weight: .bold))
                        .frame(width: 34, height: 34)
                        .background(Circle().fill(MFColor.surface))
                        .overlay(Circle().strokeBorder(MFColor.border, lineWidth: 1))
                        .contentShape(Circle())
                }
                .buttonStyle(.plain)
                .accessibilityLabel("Done")
            }

            if let errorMessage {
                errorBanner(errorMessage)
            }
        }
        .padding(.horizontal, MFSpace.lg)
        .padding(.top, MFSpace.lg)
        .padding(.bottom, MFSpace.sm)
    }

    private var subtitle: String {
        guard let mediaTitle, !mediaTitle.isEmpty else {
            return "Choose where this title should live."
        }
        return "Choose where “\(mediaTitle)” should live."
    }

    // MARK: List

    private var list: some View {
        ScrollView {
            LazyVStack(spacing: MFSpace.xs) {
                ForEach(viewModel.collections) { collection in
                    collectionRow(collection)
                }
            }
            .padding(.horizontal, MFSpace.lg)
            .padding(.vertical, MFSpace.sm)
        }
        .scrollIndicators(.hidden)
    }

    private func collectionRow(_ collection: CollectionSummary) -> some View {
        let isMember = viewModel.contains(collection.id)
        let isUpdating = viewModel.updatingCollectionIDs.contains(collection.id)
        let isResolving = viewModel.isResolvingMembership

        return Button {
            Task { await toggle(collection) }
        } label: {
            HStack(spacing: MFSpace.sm) {
                CollectionCover(
                    imageURLs: container.images.urls(
                        for: collection.coverPosterPaths,
                        size: .poster
                    ),
                    count: nil,
                    cornerRadius: MFRadius.sm
                )
                .frame(width: 66)

                VStack(alignment: .leading, spacing: 2) {
                    Text(collection.name)
                        .mfText(.label)
                        .lineLimit(2)
                        .multilineTextAlignment(.leading)

                    Text(collection.itemCount == 1 ? "1 title" : "\(collection.itemCount) titles")
                        .mfText(.caption)
                        .foregroundStyle(MFColor.textTertiary)
                }

                Spacer(minLength: MFSpace.sm)

                membershipIndicator(
                    isMember: isMember,
                    isUpdating: isUpdating,
                    isResolving: isResolving
                )
            }
            .padding(MFSpace.xs)
            .background {
                RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                    .fill(MFColor.surface)
            }
            .overlay {
                RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                    .strokeBorder(
                        isMember ? MFColor.violet.opacity(0.45) : MFColor.border,
                        lineWidth: 1
                    )
            }
            .contentShape(RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous))
        }
        .buttonStyle(.plain)
        .disabled(isResolving || isUpdating)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("\(collection.name), \(collection.itemCount == 1 ? "1 title" : "\(collection.itemCount) titles")")
        .accessibilityValue(isMember ? "Included" : "Not included")
        .accessibilityHint(isMember ? "Removes this title from the collection" : "Adds this title to the collection")
        .accessibilityAddTraits(isMember ? .isSelected : [])
    }

    @ViewBuilder
    private func membershipIndicator(
        isMember: Bool,
        isUpdating: Bool,
        isResolving: Bool
    ) -> some View {
        ZStack {
            if isMember {
                Circle()
                    .fill(MFColor.primaryButtonGradient)
                Image(systemName: "checkmark")
                    .font(.system(size: 12, weight: .heavy))
                    .foregroundStyle(.white)
            } else {
                Circle()
                    .strokeBorder(MFColor.borderStrong, lineWidth: 1.4)
                Image(systemName: "plus")
                    .font(.system(size: 12, weight: .bold))
                    .foregroundStyle(MFColor.textSecondary)
            }

            if isUpdating || isResolving {
                Circle()
                    .fill(MFColor.surface.opacity(0.85))
                ProgressView()
                    .controlSize(.mini)
            }
        }
        .frame(width: 30, height: 30)
        .accessibilityHidden(true)
    }

    // MARK: States

    private var loadingState: some View {
        VStack(spacing: MFSpace.sm) {
            ForEach(0..<4, id: \.self) { _ in
                HStack(spacing: MFSpace.sm) {
                    SkeletonBlock(cornerRadius: MFRadius.sm)
                        .frame(width: 66, height: 44)
                    VStack(alignment: .leading, spacing: 6) {
                        SkeletonBlock()
                            .frame(width: 140, height: 12)
                        SkeletonBlock()
                            .frame(width: 70, height: 9)
                    }
                    Spacer()
                    SkeletonBlock(cornerRadius: 15)
                        .frame(width: 30, height: 30)
                }
                .padding(MFSpace.xs)
                .background {
                    RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                        .fill(MFColor.surface)
                }
            }
        }
        .padding(.horizontal, MFSpace.lg)
        .padding(.vertical, MFSpace.sm)
        .frame(maxHeight: .infinity, alignment: .top)
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Loading collections")
    }

    private var emptyState: some View {
        MFEmptyStateView(
            systemImage: "square.stack.3d.up",
            title: "No collections yet",
            message: "Create a collection first, then add this title to it.",
            primaryTitle: "New Collection",
            primaryAction: { showingCreate = true }
        )
    }

    private func errorBanner(_ message: String) -> some View {
        HStack(alignment: .top, spacing: MFSpace.xs) {
            Image(systemName: "exclamationmark.triangle.fill")
                .font(.system(size: 14, weight: .semibold))
                .foregroundStyle(MFColor.danger)
                .accessibilityHidden(true)

            Text(message)
                .mfText(.caption)
                .foregroundStyle(MFColor.textSecondary)
                .fixedSize(horizontal: false, vertical: true)
        }
        .padding(MFSpace.sm)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background {
            RoundedRectangle(cornerRadius: MFRadius.sm, style: .continuous)
                .fill(MFColor.danger.opacity(0.10))
        }
        .overlay {
            RoundedRectangle(cornerRadius: MFRadius.sm, style: .continuous)
                .strokeBorder(MFColor.danger.opacity(0.35), lineWidth: 1)
        }
        .accessibilityElement(children: .combine)
    }

    // MARK: Footer

    private var footer: some View {
        VStack(spacing: 0) {
            Rectangle()
                .fill(MFColor.separator)
                .frame(height: 1)

            MFButton(
                title: "New Collection",
                systemImage: "plus",
                role: .secondary,
                isFullWidth: true
            ) {
                showingCreate = true
            }
            .padding(.horizontal, MFSpace.lg)
            .padding(.vertical, MFSpace.md)
        }
        .background(MFColor.canvas)
    }

    // MARK: Actions

    private func toggle(_ collection: CollectionSummary) async {
        if let error = await viewModel.toggleMembership(in: collection) {
            errorMessage = error.message
        } else {
            errorMessage = nil
        }
    }
}
