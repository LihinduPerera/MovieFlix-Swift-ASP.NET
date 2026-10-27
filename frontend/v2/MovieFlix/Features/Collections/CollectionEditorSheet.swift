import SwiftUI

nonisolated enum CollectionEditorMode: Sendable {
    case create
    case rename(name: String, description: String)

    var title: String {
        switch self {
        case .create: "New Collection"
        case .rename: "Edit Collection"
        }
    }

    var subtitle: String {
        switch self {
        case .create: "Group titles for a mood, a marathon, or movie night."
        case .rename: "Update the name and notes for this collection."
        }
    }

    var actionTitle: String {
        switch self {
        case .create: "Create Collection"
        case .rename: "Save Changes"
        }
    }

    var initialName: String {
        switch self {
        case .create: ""
        case let .rename(name, _): name
        }
    }

    var initialDescription: String {
        switch self {
        case .create: ""
        case let .rename(_, description): description
        }
    }
}

struct CollectionEditorSheet: View {
    private enum Field: Hashable {
        case name
        case description
    }

    let mode: CollectionEditorMode
    let onSubmit: (String, String) async -> APIError?

    @Environment(\.dismiss) private var dismiss
    @State private var name: String
    @State private var summary: String
    @State private var isSubmitting = false
    @State private var didAttemptSubmit = false
    @State private var serverError: String?
    @FocusState private var focusedField: Field?

    init(
        mode: CollectionEditorMode,
        onSubmit: @escaping (String, String) async -> APIError?
    ) {
        self.mode = mode
        self.onSubmit = onSubmit
        _name = State(initialValue: mode.initialName)
        _summary = State(initialValue: mode.initialDescription)
    }

    var body: some View {
        VStack(spacing: 0) {
            header

            ScrollView {
                VStack(alignment: .leading, spacing: MFSpace.lg) {
                    intro
                    nameField
                    descriptionField

                    if let serverError {
                        errorBanner(serverError)
                    }
                }
                .padding(.horizontal, MFSpace.lg)
                .padding(.top, MFSpace.md)
                .padding(.bottom, MFSpace.xxl)
            }
            .scrollIndicators(.hidden)
            .scrollDismissesKeyboard(.interactively)

            footer
        }
        .background(MFColor.canvas)
        .presentationDetents([.large])
        .presentationDragIndicator(.visible)
        .interactiveDismissDisabled(isSubmitting)
        .onAppear {
            if case .create = mode {
                focusedField = .name
            }
        }
    }

    // MARK: Header

    private var header: some View {
        HStack(alignment: .top, spacing: MFSpace.md) {
            VStack(alignment: .leading, spacing: 3) {
                Text(mode.title)
                    .mfText(.title)
                    .accessibilityAddTraits(.isHeader)

                Text(mode.subtitle)
                    .mfText(.caption)
                    .foregroundStyle(MFColor.textTertiary)
                    .fixedSize(horizontal: false, vertical: true)
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
            .disabled(isSubmitting)
            .accessibilityLabel("Cancel")
        }
        .padding(.horizontal, MFSpace.lg)
        .padding(.top, MFSpace.lg)
        .padding(.bottom, MFSpace.sm)
    }

    private var intro: some View {
        Text("Collections keep related titles together. Only you can change this one.")
            .mfText(.caption)
            .foregroundStyle(MFColor.textTertiary)
            .fixedSize(horizontal: false, vertical: true)
    }

    // MARK: Fields

    private var nameField: some View {
        VStack(alignment: .leading, spacing: MFSpace.xs) {
            HStack(alignment: .firstTextBaseline) {
                Text("Name")
                    .mfText(.label)

                Spacer(minLength: MFSpace.sm)

                Text("\(name.count)/100")
                    .mfText(.caption)
                    .monospacedDigit()
                    .foregroundStyle(name.count > 100 ? MFColor.danger : MFColor.textTertiary)
            }

            TextField("e.g. Sunday Night Sci-Fi", text: $name)
                .mfText(.body)
                .textInputAutocapitalization(.sentences)
                .autocorrectionDisabled()
                .submitLabel(.next)
                .focused($focusedField, equals: .name)
                .onSubmit { focusedField = .description }
                .padding(.horizontal, MFSpace.md)
                .frame(height: 50)
                .background {
                    RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                        .fill(MFColor.surface)
                }
                .overlay {
                    RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                        .strokeBorder(fieldBorderColor(showsError: nameError != nil), lineWidth: 1)
                }

            if let nameError, didAttemptSubmit {
                Text(nameError)
                    .mfText(.caption)
                    .foregroundStyle(MFColor.danger)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
    }

    private var descriptionField: some View {
        VStack(alignment: .leading, spacing: MFSpace.xs) {
            HStack(alignment: .firstTextBaseline) {
                Text("Description")
                    .mfText(.label)

                Text("Optional")
                    .mfText(.caption)
                    .foregroundStyle(MFColor.textTertiary)

                Spacer(minLength: MFSpace.sm)

                Text("\(summary.count)/500")
                    .mfText(.caption)
                    .monospacedDigit()
                    .foregroundStyle(summary.count > 500 ? MFColor.danger : MFColor.textTertiary)
            }

            ZStack(alignment: .topLeading) {
                if summary.isEmpty {
                    Text("What ties these titles together?")
                        .mfText(.body)
                        .foregroundStyle(MFColor.textTertiary)
                        .padding(.horizontal, MFSpace.md + 4)
                        .padding(.vertical, MFSpace.md)
                        .allowsHitTesting(false)
                }

                TextEditor(text: $summary)
                    .mfText(.body)
                    .scrollContentBackground(.hidden)
                    .focused($focusedField, equals: .description)
                    .padding(.horizontal, MFSpace.sm)
                    .padding(.vertical, MFSpace.xs)
                    .frame(minHeight: 128)
                    .background(Color.clear)
            }
            .background {
                RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                    .fill(MFColor.surface)
            }
            .overlay {
                RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                    .strokeBorder(fieldBorderColor(showsError: summary.count > 500), lineWidth: 1)
            }

            if summary.count > 500 {
                Text("Descriptions are limited to 500 characters.")
                    .mfText(.caption)
                    .foregroundStyle(MFColor.danger)
            }
        }
    }

    private func fieldBorderColor(showsError: Bool) -> Color {
        if showsError {
            return MFColor.danger.opacity(0.75)
        }
        return MFColor.border
    }

    private func errorBanner(_ message: String) -> some View {
        HStack(alignment: .top, spacing: MFSpace.xs) {
            Image(systemName: "exclamationmark.triangle.fill")
                .font(.system(size: 14, weight: .semibold))
                .foregroundStyle(MFColor.danger)
                .accessibilityHidden(true)

            Text(message)
                .mfText(.callout)
                .foregroundStyle(MFColor.textSecondary)
                .fixedSize(horizontal: false, vertical: true)
        }
        .padding(MFSpace.md)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background {
            RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                .fill(MFColor.danger.opacity(0.10))
        }
        .overlay {
            RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
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
                title: mode.actionTitle,
                role: .primary,
                isLoading: isSubmitting,
                isFullWidth: true
            ) {
                submit()
            }
            .disabled(isSubmitting)
            .padding(.horizontal, MFSpace.lg)
            .padding(.vertical, MFSpace.md)
        }
        .background(MFColor.canvas)
    }

    // MARK: Submission

    private var trimmedName: String {
        name.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private var trimmedSummary: String {
        summary.trimmingCharacters(in: .whitespacesAndNewlines)
    }

    private var nameError: String? {
        if trimmedName.isEmpty {
            return "Give your collection a name."
        }
        if trimmedName.count > 100 {
            return "Names are limited to 100 characters."
        }
        return nil
    }

    private func submit() {
        didAttemptSubmit = true
        serverError = nil

        guard nameError == nil, trimmedSummary.count <= 500 else { return }

        focusedField = nil
        isSubmitting = true

        Task {
            let error = await onSubmit(trimmedName, trimmedSummary)
            isSubmitting = false

            if let error {
                serverError = error.message
            } else {
                dismiss()
            }
        }
    }
}
