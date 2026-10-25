import SwiftUI
import UIKit

/// Semantic, adaptive color tokens. Dark is the primary design target (true
/// OLED black); light mode is a first-class adaptation, not an afterthought.
nonisolated enum MFColor {
    // Canonical surfaces
    static let canvas = dynamic(light: 0xF6F5FA, dark: 0x000000)
    static let canvasElevated = dynamic(light: 0xFFFFFF, dark: 0x08080C)
    static let surface = dynamic(light: 0xFFFFFF, dark: 0x101017)
    static let surfaceElevated = dynamic(light: 0xF0EFF6, dark: 0x17171F)
    static let surfaceSunken = dynamic(light: 0xEAE9F1, dark: 0x050508)

    // Text
    static let textPrimary = dynamic(light: 0x0B0B10, dark: 0xFFFFFF)
    static let textSecondary = dynamicAlpha(light: (0x0B0B10, 0.64), dark: (0xFFFFFF, 0.72))
    static let textTertiary = dynamicAlpha(light: (0x0B0B10, 0.45), dark: (0xFFFFFF, 0.46))
    static let textInverse = dynamic(light: 0xFFFFFF, dark: 0x09090D)

    // Lines and separation
    static let border = dynamicAlpha(light: (0x000000, 0.08), dark: (0xFFFFFF, 0.10))
    static let borderStrong = dynamicAlpha(light: (0x000000, 0.16), dark: (0xFFFFFF, 0.20))
    static let separator = dynamicAlpha(light: (0x000000, 0.06), dark: (0xFFFFFF, 0.08))

    // Brand
    static let violet = dynamic(light: 0x6D28D9, dark: 0x9F7AFF)
    static let magenta = dynamic(light: 0xBE185D, dark: 0xFF5FB8)
    static let amber = dynamic(light: 0xB45309, dark: 0xFFC45C)
    static let pink = dynamic(light: 0xDB2777, dark: 0xFF4D9A)

    // Semantic
    static let rating = dynamic(light: 0xB45309, dark: 0xFFD166)
    static let favorite = dynamic(light: 0xD7004A, dark: 0xFF4D7D)
    static let success = dynamic(light: 0x248A3D, dark: 0x30D158)
    static let warning = dynamic(light: 0x9A5B00, dark: 0xFFD60A)
    static let danger = dynamic(light: 0xC70015, dark: 0xFF696E)
    static let info = dynamic(light: 0x0066CC, dark: 0x64B5FF)

    // Skeleton and overlays
    static let skeletonBase = dynamic(light: 0xE8E7EF, dark: 0x15151D)
    static let skeletonHighlight = dynamic(light: 0xF8F7FC, dark: 0x262632)
    static let scrim = Color.black.opacity(0.62)
    static let shadow = Color.black.opacity(0.42)

    // Gradients
    static let brandGradient = LinearGradient(
        colors: [violet, magenta, amber],
        startPoint: .topLeading,
        endPoint: .bottomTrailing
    )

    static let primaryButtonGradient = LinearGradient(
        colors: [violet, magenta],
        startPoint: .topLeading,
        endPoint: .bottomTrailing
    )

    static let subtleGradient = LinearGradient(
        colors: [violet.opacity(0.20), magenta.opacity(0.10), .clear],
        startPoint: .topLeading,
        endPoint: .bottomTrailing
    )

    static let heroScrim = LinearGradient(
        stops: [
            .init(color: .clear, location: 0.30),
            .init(color: .black.opacity(0.30), location: 0.56),
            .init(color: .black.opacity(0.78), location: 0.80),
            .init(color: canvas, location: 1.0)
        ],
        startPoint: .top,
        endPoint: .bottom
    )

    static let posterScrim = LinearGradient(
        stops: [
            .init(color: .clear, location: 0.55),
            .init(color: .black.opacity(0.16), location: 0.74),
            .init(color: .black.opacity(0.72), location: 1.0)
        ],
        startPoint: .top,
        endPoint: .bottom
    )

    static let ratingScrim = LinearGradient(
        colors: [.black.opacity(0.70), .black.opacity(0.18)],
        startPoint: .top,
        endPoint: .bottom
    )

    private static func dynamic(light: UInt32, dark: UInt32) -> Color {
        Color(uiColor: UIColor { traits in
            UIColor(hex: traits.userInterfaceStyle == .dark ? dark : light)
        })
    }

    private static func dynamicAlpha(
        light: (UInt32, CGFloat),
        dark: (UInt32, CGFloat)
    ) -> Color {
        Color(uiColor: UIColor { traits in
            let value = traits.userInterfaceStyle == .dark ? dark : light
            return UIColor(hex: value.0, alpha: value.1)
        })
    }
}

nonisolated extension UIColor {
    convenience init(hex: UInt32, alpha: CGFloat = 1) {
        self.init(
            red: CGFloat((hex >> 16) & 0xFF) / 255,
            green: CGFloat((hex >> 8) & 0xFF) / 255,
            blue: CGFloat(hex & 0xFF) / 255,
            alpha: alpha
        )
    }
}
