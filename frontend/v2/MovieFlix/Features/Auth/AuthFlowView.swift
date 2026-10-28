import SwiftUI

nonisolated enum AuthMode: String, CaseIterable, Identifiable, Sendable {
    case signIn
    case register

    var id: String { rawValue }

    var switchTitle: String {
        switch self {
        case .signIn: "Sign In"
        case .register: "Create Account"
        }
    }

    var title: String {
        switch self {
        case .signIn: "Welcome back"
        case .register: "Join MovieFlix"
        }
    }

    var subtitle: String {
        switch self {
        case .signIn: "Your library, ratings, and progress are waiting."
        case .register: "Create a free account to sync everything you watch."
        }
    }

    var submitTitle: String {
        switch self {
        case .signIn: "Sign In"
        case .register: "Create Account"
        }
    }
}

struct AuthFlowView: View {
    let container: AppContainer

    @Environment(\.dismiss) private var dismiss
    @State private var mode: AuthMode = .signIn
    @State private var email = ""
    @State private var displayName = ""
    @State private var password = ""
    @State private var confirmPassword = ""
    @State private var revealedFields: Set<AuthField> = []
    @State private var fieldErrors: [AuthField: String] = [:]
    @FocusState private var focusedField: AuthField?

    private var session: SessionStore { container.session }

    var body: some View {
        ZStack {
            MFColor.canvas.ignoresSafeArea()

            ScrollView {
                VStack(alignment: .leading, spacing: MFSpace.xl) {
                    header
                    modeSwitcher
                    form
                    serverErrorBanner
                    submitButton
                    legalFooter
                }
                .padding(.horizontal, MFSpace.lg)
                .padding(.top, MFSpace.xxl)
                .padding(.bottom, MFSpace.xxxl)
                .frame(maxWidth: 520, alignment: .leading)
                .frame(maxWidth: .infinity)
            }
            .scrollIndicators(.hidden)
            .scrollDismissesKeyboard(.interactively)
        }
        .overlay(alignment: .topTrailing) {
            closeButton
        }
        .presentationBackground(MFColor.canvas)
        .interactiveDismissDisabled(session.isSubmitting)
        .onAppear {
            session.clearError()
            focusedField = .email
        }
        .onChange(of: mode) { _, _ in
            clearErrors()
            focusedField = .email
        }
        .onChange(of: email) { _, _ in clearErrors() }
        .onChange(of: displayName) { _, _ in clearErrors() }
        .onChange(of: password) { _, _ in clearErrors() }
        .onChange(of: confirmPassword) { _, _ in clearErrors() }
    }

    // MARK: Header

    private var header: some View {
        VStack(alignment: .leading, spacing: MFSpace.md) {
            MFLogo(size: 17, showsMark: true)
                .padding(.bottom, MFSpace.xs)

            VStack(alignment: .leading, spacing: MFSpace.xs) {
                Text(mode.title)
                    .mfText(.title)
                    .accessibilityAddTraits(.isHeader)

                Text(mode.subtitle)
                    .mfText(.callout)
                    .foregroundStyle(MFColor.textSecondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
    }

    // MARK: Mode

    private var modeSwitcher: some View {
        HStack(spacing: 0) {
            ForEach(AuthMode.allCases) { item in
                Button {
                    withAnimation(.snappy(duration: 0.24)) {
                        mode = item
                    }
                } label: {
                    Text(item.switchTitle)
                        .font(MFTypography.label)
                        .foregroundStyle(mode == item ? .white : MFColor.textSecondary)
                        .frame(maxWidth: .infinity)
                        .frame(height: 40)
                        .background {
                            if mode == item {
                                Capsule(style: .continuous)
                                    .fill(MFColor.primaryButtonGradient)
                            }
                        }
                        .contentShape(Capsule())
                }
                .buttonStyle(.plain)
                .accessibilityAddTraits(mode == item ? .isSelected : [])
            }
        }
        .padding(4)
        .background {
            Capsule(style: .continuous)
                .fill(MFColor.surface)
        }
        .overlay {
            Capsule(style: .continuous)
                .strokeBorder(MFColor.border, lineWidth: 1)
        }
        .accessibilityElement(children: .contain)
        .accessibilityLabel("Authentication mode")
    }

    // MARK: Form

    @ViewBuilder
    private var form: some View {
        VStack(spacing: MFSpace.md) {
            field(
                .email,
                label: "Email",
                systemImage: "envelope",
                error: fieldError(.email)
            ) {
                TextField("you@example.com", text: $email)
                    .textContentType(.username)
                    .keyboardType(.emailAddress)
                    .textInputAutocapitalization(.never)
                    .autocorrectionDisabled()
                    .focused($focusedField, equals: .email)
                    .submitLabel(.next)
                    .onSubmit {
                        focusedField = mode == .register ? .displayName : .password
                    }
            }

            if mode == .register {
                field(
                    .displayName,
                    label: "Display Name",
                    systemImage: "person",
                    error: fieldError(.displayName)
                ) {
                    TextField("How should we call you?", text: $displayName)
                        .textContentType(.name)
                        .textInputAutocapitalization(.words)
                        .submitLabel(.next)
                        .focused($focusedField, equals: .displayName)
                        .onSubmit { focusedField = .password }
                }
            }

            field(
                .password,
                label: "Password",
                systemImage: "lock",
                error: fieldError(.password),
                isSecure: true
            ) {
                secureInput(
                    placeholder: mode == .register ? "At least 8 characters" : "Your password",
                    text: $password,
                    contentType: mode == .register ? .newPassword : .password,
                    field: .password
                )
                .submitLabel(mode == .register ? .next : .go)
                .onSubmit {
                    if mode == .register {
                        focusedField = .confirmPassword
                    } else {
                        submit()
                    }
                }
            }

            if mode == .register {
                field(
                    .confirmPassword,
                    label: "Confirm Password",
                    systemImage: "lock.rotation",
                    error: fieldError(.confirmPassword),
                    isSecure: true
                ) {
                    secureInput(
                        placeholder: "Repeat your password",
                        text: $confirmPassword,
                        contentType: .newPassword,
                        field: .confirmPassword
                    )
                    .submitLabel(.go)
                    .onSubmit { submit() }
                }
            }
        }
    }

    private func field<Content: View>(
        _ field: AuthField,
        label: String,
        systemImage: String,
        error: String?,
        isSecure: Bool = false,
        @ViewBuilder content: () -> Content
    ) -> some View {
        let isFocused = focusedField == field
        let hasError = error != nil

        return VStack(alignment: .leading, spacing: 6) {
            HStack(alignment: .center, spacing: MFSpace.sm) {
                Image(systemName: systemImage)
                    .font(.system(size: 15, weight: .medium))
                    .foregroundStyle(isFocused ? MFColor.violet : MFColor.textTertiary)
                    .frame(width: 20)
                    .accessibilityHidden(true)

                VStack(alignment: .leading, spacing: 2) {
                    Text(label)
                        .font(.system(size: 11, weight: .semibold))
                        .textCase(.uppercase)
                        .tracking(0.6)
                        .foregroundStyle(MFColor.textTertiary)

                    content()
                        .mfText(.body)
                        .foregroundStyle(MFColor.textPrimary)
                }

                if isSecure {
                    Button {
                        if revealedFields.contains(field) {
                            revealedFields.remove(field)
                        } else {
                            revealedFields.insert(field)
                        }
                    } label: {
                        Image(systemName: revealedFields.contains(field) ? "eye.slash" : "eye")
                            .font(.system(size: 14, weight: .semibold))
                            .foregroundStyle(MFColor.textTertiary)
                            .frame(width: 32, height: 32)
                            .contentShape(Rectangle())
                    }
                    .buttonStyle(.plain)
                    .accessibilityLabel(revealedFields.contains(field) ? "Hide password" : "Show password")
                }
            }
            .padding(.horizontal, MFSpace.md)
            .padding(.vertical, MFSpace.sm)
            .background {
                RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                    .fill(MFColor.surface)
            }
            .overlay {
                RoundedRectangle(cornerRadius: MFRadius.md, style: .continuous)
                    .strokeBorder(
                        hasError
                            ? MFColor.danger.opacity(0.75)
                            : (isFocused ? MFColor.violet.opacity(0.8) : MFColor.border),
                        lineWidth: isFocused || hasError ? 1.4 : 1
                    )
            }
            .animation(.easeOut(duration: 0.16), value: isFocused)

            if let error {
                Text(error)
                    .mfText(.caption)
                    .foregroundStyle(MFColor.danger)
                    .fixedSize(horizontal: false, vertical: true)
                    .accessibilityLabel("Error: \(error)")
            }
        }
    }

    @ViewBuilder
    private func secureInput(
        placeholder: String,
        text: Binding<String>,
        contentType: UITextContentType,
        field: AuthField
    ) -> some View {
        Group {
            if revealedFields.contains(field) {
                TextField(placeholder, text: text)
            } else {
                SecureField(placeholder, text: text)
            }
        }
        .textContentType(contentType)
        .textInputAutocapitalization(.never)
        .autocorrectionDisabled()
        .focused($focusedField, equals: field)
    }

    // MARK: Error banner

    @ViewBuilder
    private var serverErrorBanner: some View {
        if let message = session.errorMessage {
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
            .transition(.opacity)
        }
    }

    // MARK: Submit

    private var submitButton: some View {
        VStack(spacing: MFSpace.sm) {
            MFButton(
                title: mode.submitTitle,
                systemImage: mode == .signIn ? "arrow.right" : "person.badge.plus",
                role: .primary,
                isLoading: session.isSubmitting,
                isFullWidth: true
            ) {
                submit()
            }
            .disabled(session.isSubmitting)

            if mode == .register {
                Text("Passwords need 8+ characters with upper and lower case, a number, and a symbol.")
                    .mfText(.caption)
                    .foregroundStyle(MFColor.textTertiary)
                    .multilineTextAlignment(.center)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
    }

    private var legalFooter: some View {
        Text("By continuing you agree to keep your account secure and use MovieFlix for personal enjoyment.")
            .mfText(.caption)
            .foregroundStyle(MFColor.textTertiary)
            .multilineTextAlignment(.center)
            .frame(maxWidth: .infinity)
            .padding(.top, MFSpace.xs)
    }

    // MARK: Chrome

    private var closeButton: some View {
        Button {
            dismiss()
        } label: {
            Image(systemName: "xmark")
                .font(.system(size: 13, weight: .bold))
                .foregroundStyle(MFColor.textSecondary)
                .frame(width: 36, height: 36)
                .background(Circle().fill(MFColor.surface))
                .overlay {
                    Circle().strokeBorder(MFColor.border, lineWidth: 1)
                }
                .contentShape(Circle())
        }
        .buttonStyle(.plain)
        .padding(MFSpace.md)
        .accessibilityLabel("Close")
    }

    // MARK: Actions

    private func submit() {
        session.clearError()
        guard validate() else {
            focusFirstError()
            return
        }

        focusedField = nil

        Task {
            let succeeded: Bool
            switch mode {
            case .signIn:
                succeeded = await session.signIn(email: email, password: password)
            case .register:
                succeeded = await session.register(
                    email: email,
                    displayName: displayName,
                    password: password
                )
            }

            if succeeded {
                dismiss()
            } else {
                focusFirstServerError()
            }
        }
    }

    private func validate() -> Bool {
        var errors: [AuthField: String] = [:]
        let trimmedEmail = email.trimmingCharacters(in: .whitespacesAndNewlines)

        if trimmedEmail.isEmpty {
            errors[.email] = "Enter your email address."
        } else if !Self.isValidEmail(trimmedEmail) {
            errors[.email] = "Enter a valid email address."
        }

        if mode == .register {
            let trimmedName = displayName.trimmingCharacters(in: .whitespacesAndNewlines)
            if trimmedName.isEmpty {
                errors[.displayName] = "Enter a display name."
            } else if trimmedName.count > 100 {
                errors[.displayName] = "Keep your display name under 100 characters."
            }

            if password.count < 8 {
                errors[.password] = "Use at least 8 characters."
            } else if password.count > 100 {
                errors[.password] = "Passwords can't be longer than 100 characters."
            } else if !Self.isStrongPassword(password) {
                errors[.password] = "Include upper and lower case, a number, and a symbol."
            }

            if confirmPassword != password {
                errors[.confirmPassword] = "Passwords don't match."
            }
        } else if password.isEmpty {
            errors[.password] = "Enter your password."
        }

        fieldErrors = errors
        return errors.isEmpty
    }

    private func fieldError(_ field: AuthField) -> String? {
        if let local = fieldErrors[field] {
            return local
        }
        for key in field.serverKeys {
            if let message = session.fieldErrors.first(where: {
                $0.key.caseInsensitiveCompare(key) == .orderedSame
            })?.value.first {
                return message
            }
        }
        return nil
    }

    private func clearErrors() {
        if !fieldErrors.isEmpty {
            fieldErrors = [:]
        }
        if session.errorMessage != nil {
            session.clearError()
        }
    }

    private func focusFirstError() {
        if let field = AuthField.allCases.first(where: { fieldErrors[$0] != nil }) {
            focusedField = field
        }
    }

    private func focusFirstServerError() {
        if let field = AuthField.allCases.first(where: { fieldError($0) != nil }) {
            focusedField = field
        }
    }

    // MARK: Validation

    private static func isValidEmail(_ value: String) -> Bool {
        value.range(
            of: #"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$"#,
            options: .regularExpression
        ) != nil
    }

    private static func isStrongPassword(_ value: String) -> Bool {
        value.contains(where: \.isUppercase)
            && value.contains(where: \.isLowercase)
            && value.contains(where: \.isNumber)
            && value.contains { !$0.isLetter && !$0.isNumber && !$0.isWhitespace }
    }
}

// MARK: - Fields

private enum AuthField: Hashable, CaseIterable {
    case email
    case displayName
    case password
    case confirmPassword

    var serverKeys: [String] {
        switch self {
        case .email: ["Email"]
        case .displayName: ["DisplayName", "Display Name"]
        case .password: ["Password"]
        case .confirmPassword: ["ConfirmPassword", "Confirm Password"]
        }
    }
}

#Preview {
    let configuration = AppConfiguration(
        apiBaseURL: URL(string: "https://192.168.24.60:7014")!,
        imageBaseURL: URL(string: "https://image.tmdb.org/t/p")!,
        environment: .preview,
        appVersion: "1.0",
        buildNumber: "1",
        userAgent: "MovieFlix Preview"
    )
    AuthFlowView(container: AppContainer(configuration: configuration))
}
