import SwiftUI

nonisolated enum MFMotion {
    static let standard = Animation.smooth(duration: 0.34)
    static let quick = Animation.snappy(duration: 0.22, extraBounce: 0.03)
    static let gentle = Animation.easeInOut(duration: 0.5)
    static let press = Animation.snappy(duration: 0.16, extraBounce: 0.02)
}

private struct MFAnimationModifier<Value: Equatable>: ViewModifier {
    let value: Value
    let animation: Animation
    @Environment(\.accessibilityReduceMotion) private var reduceMotion

    func body(content: Content) -> some View {
        content.animation(reduceMotion ? nil : animation, value: value)
    }
}

extension View {
    func mfAnimation<Value: Equatable>(
        _ value: Value,
        _ animation: Animation = MFMotion.standard
    ) -> some View {
        modifier(MFAnimationModifier(value: value, animation: animation))
    }

    func mfTransition(reduceMotion: Bool) -> AnyTransition {
        reduceMotion ? .opacity : .opacity.combined(with: .scale(scale: 0.98))
    }
}
