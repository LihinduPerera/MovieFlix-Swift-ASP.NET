import Observation
import SwiftUI

nonisolated enum ToastStyle: Sendable, Equatable {
    case info
    case success
    case error
    case warning

    var systemImage: String {
        switch self {
        case .info: "info.circle.fill"
        case .success: "checkmark.circle.fill"
        case .error: "exclamationmark.octagon.fill"
        case .warning: "exclamationmark.triangle.fill"
        }
    }

    var tint: Color {
        switch self {
        case .info: MFColor.info
        case .success: MFColor.success
        case .error: MFColor.danger
        case .warning: MFColor.warning
        }
    }
}

struct Toast: Identifiable, Equatable, Sendable {
    let id: UUID
    let message: String
    let style: ToastStyle

    init(id: UUID = UUID(), message: String, style: ToastStyle) {
        self.id = id
        self.message = message
        self.style = style
    }
}

@MainActor
@Observable
final class ToastCenter {
    private(set) var toast: Toast?
    @ObservationIgnored private var dismissalTask: Task<Void, Never>?

    func show(_ message: String, style: ToastStyle = .info, duration: Duration = .seconds(3)) {
        let toast = Toast(message: message, style: style)
        dismissalTask?.cancel()
        self.toast = toast

        AccessibilityNotification.Announcement(message).post()

        dismissalTask = Task { [weak self] in
            try? await Task.sleep(for: duration)
            guard !Task.isCancelled else { return }
            self?.dismiss(id: toast.id)
        }
    }

    func success(_ message: String) {
        show(message, style: .success)
    }

    func error(_ message: String) {
        show(message, style: .error, duration: .seconds(5))
    }

    func info(_ message: String) {
        show(message, style: .info)
    }

    func dismiss(id: UUID? = nil) {
        guard let current = toast else { return }
        if let id, current.id != id { return }
        dismissalTask?.cancel()
        dismissalTask = nil
        toast = nil
    }
}

struct ToastHost: View {
    let center: ToastCenter

    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    var body: some View {
        ZStack(alignment: .bottom) {
            if let toast = center.toast {
                HStack(spacing: MFSpace.sm) {
                    Image(systemName: toast.style.systemImage)
                        .font(.system(size: 18, weight: .semibold))
                        .foregroundStyle(toast.style.tint)
                        .accessibilityHidden(true)

                    Text(toast.message)
                        .mfText(.callout)
                        .foregroundStyle(MFColor.textPrimary)
                        .fixedSize(horizontal: false, vertical: true)

                    Spacer(minLength: 0)

                    Button {
                        center.dismiss()
                    } label: {
                        Image(systemName: "xmark")
                            .font(.system(size: 12, weight: .bold))
                            .foregroundStyle(MFColor.textTertiary)
                            .frame(width: 28, height: 28)
                            .contentShape(Rectangle())
                    }
                    .buttonStyle(.plain)
                    .accessibilityLabel("Dismiss")
                }
                .padding(.horizontal, MFSpace.md)
                .padding(.vertical, MFSpace.sm)
                .frame(maxWidth: 520)
                .mfGlass(cornerRadius: MFRadius.md, tint: toast.style.tint.opacity(0.12))
                .mfShadow(MFShadow.floating)
                .padding(.horizontal, MFSpace.md)
                .padding(.bottom, MFSpace.xs)
                .transition(
                    reduceMotion
                        ? .opacity
                        : .move(edge: .bottom).combined(with: .opacity)
                )
                .accessibilityElement(children: .contain)
            }
        }
        .animation(reduceMotion ? nil : MFMotion.quick, value: center.toast)
        .allowsHitTesting(center.toast != nil)
    }
}

extension View {
    func mfToastOverlay(_ center: ToastCenter, bottomPadding: CGFloat = 64) -> some View {
        overlay(alignment: .bottom) {
            ToastHost(center: center)
                .padding(.bottom, bottomPadding)
        }
    }
}
