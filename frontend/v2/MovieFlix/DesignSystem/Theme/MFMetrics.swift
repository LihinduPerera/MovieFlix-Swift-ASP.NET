import CoreGraphics
import SwiftUI

nonisolated enum MFSpace {
    static let xxs: CGFloat = 4
    static let xs: CGFloat = 8
    static let sm: CGFloat = 12
    static let md: CGFloat = 16
    static let lg: CGFloat = 20
    static let xl: CGFloat = 24
    static let xxl: CGFloat = 32
    static let xxxl: CGFloat = 40
    static let huge: CGFloat = 56
}

nonisolated enum MFRadius {
    static let xs: CGFloat = 8
    static let sm: CGFloat = 12
    static let md: CGFloat = 16
    static let lg: CGFloat = 22
    static let xl: CGFloat = 28
    static let poster: CGFloat = 14
    static let pill: CGFloat = 999
}

nonisolated enum MFSize {
    static let posterWidth: CGFloat = 132
    static let posterWidthLarge: CGFloat = 156
    static let posterAspectRatio: CGFloat = 2.0 / 3.0
    static let railSpacing: CGFloat = 12
    static let cardMinHeight: CGFloat = 44
    static let heroHeightPhone: CGFloat = 520
    static let heroHeightPad: CGFloat = 620
    static let contentMaxWidth: CGFloat = 1_020
    static let buttonHeight: CGFloat = 50
    static let iconButton: CGFloat = 44
    static let avatar: CGFloat = 40
}

nonisolated enum MFShadow {
    static let card = Shadow(color: MFColor.shadow, radius: 18, x: 0, y: 10)
    static let floating = Shadow(color: MFColor.shadow, radius: 28, x: 0, y: 16)

    struct Shadow: Sendable {
        let color: Color
        let radius: CGFloat
        let x: CGFloat
        let y: CGFloat
    }
}

extension View {
    func mfShadow(_ shadow: MFShadow.Shadow) -> some View {
        self.shadow(color: shadow.color, radius: shadow.radius, x: shadow.x, y: shadow.y)
    }
}
