import SwiftUI

struct PlaceholderFeatureView: View {
    let tab: AppTab
    let systemImage: String
    let title: String
    let message: String

    var body: some View {
        NavigationStack {
            ScrollView {
                VStack(spacing: MFSpace.xxl) {
                    HStack {
                        MFLogo(showsMark: true)
                        Spacer()
                    }
                    .padding(.horizontal, MFSpace.lg)
                    .padding(.top, MFSpace.xs)

                    MFEmptyStateView(
                        systemImage: systemImage,
                        title: title,
                        message: message
                    )
                    .padding(.top, MFSpace.huge)
                }
                .frame(maxWidth: .infinity)
            }
            .scrollIndicators(.hidden)
            .background(MFColor.canvas)
            .navigationTitle(tab.title)
            .navigationBarTitleDisplayMode(.inline)
        }
    }
}

struct ConfigurationErrorView: View {
    let error: Error

    var body: some View {
        ZStack {
            MFColor.canvas.ignoresSafeArea()

            ScrollView {
                VStack(spacing: MFSpace.xl) {
                    MFLogo(size: 26, showsMark: true)

                    ZStack {
                        Circle()
                            .fill(MFColor.danger.opacity(0.12))
                            .frame(width: 96, height: 96)
                        Image(systemName: "gearshape.badge.xmark")
                            .font(.system(size: 36, weight: .medium))
                            .foregroundStyle(MFColor.danger)
                    }

                    VStack(spacing: MFSpace.xs) {
                        Text("Configuration Needed")
                            .mfText(.title)
                            .multilineTextAlignment(.center)

                        Text(error.localizedDescription)
                            .mfText(.body)
                            .foregroundStyle(MFColor.textSecondary)
                            .multilineTextAlignment(.center)
                            .fixedSize(horizontal: false, vertical: true)
                    }

                    MFGlassPanel(cornerRadius: MFRadius.md) {
                        VStack(alignment: .leading, spacing: MFSpace.xs) {
                            Text("How to fix")
                                .mfText(.label)
                            Text("Open Config/Debug.xcconfig and set API_BASE_URL to your backend, for example https://192.168.24.60:7014. Then rebuild the app.")
                                .mfText(.callout)
                                .foregroundStyle(MFColor.textSecondary)
                                .fixedSize(horizontal: false, vertical: true)
                        }
                        .frame(maxWidth: .infinity, alignment: .leading)
                    }
                }
                .frame(maxWidth: 520)
                .padding(MFSpace.xl)
                .frame(maxWidth: .infinity)
            }
        }
    }
}
