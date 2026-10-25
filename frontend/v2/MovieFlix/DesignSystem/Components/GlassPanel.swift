import SwiftUI

nonisolated private struct MFGlassModifier: ViewModifier {
    let cornerRadius: CGFloat
    let tint: Color?

    func body(content: Content) -> some View {
        let shape = RoundedRectangle(cornerRadius: cornerRadius, style: .continuous)
        let glass: Glass = tint.map { .regular.tint($0) } ?? .regular

        content
            .glassEffect(glass, in: shape)
            .overlay {
                shape.strokeBorder(MFColor.border, lineWidth: 1)
            }
    }
}

extension View {
    /// Liquid Glass is reserved for floating, functional surfaces (toasts,
    /// hero controls). Content surfaces use opaque tokens instead.
    func mfGlass(cornerRadius: CGFloat = MFRadius.lg, tint: Color? = nil) -> some View {
        modifier(MFGlassModifier(cornerRadius: cornerRadius, tint: tint))
    }

    func mfGlassCapsule(tint: Color? = nil) -> some View {
        let glass: Glass = tint.map { .regular.tint($0) } ?? .regular
        return glassEffect(glass, in: Capsule(style: .continuous))
            .overlay {
                Capsule(style: .continuous)
                    .strokeBorder(MFColor.border, lineWidth: 1)
            }
    }
}

struct MFGlassPanel<Content: View>: View {
    var cornerRadius: CGFloat = MFRadius.lg
    var tint: Color?
    @ViewBuilder let content: Content

    var body: some View {
        content
            .padding(MFSpace.md)
            .mfGlass(cornerRadius: cornerRadius, tint: tint)
    }
}
