import AthenaNativeCore
import SwiftUI

@main
struct AthenaNativeApp: App {
    @StateObject private var model = AthenaAppModel()

    var body: some Scene {
        WindowGroup("Athena") {
            RootView()
                .environmentObject(model)
                .frame(minWidth: 420, minHeight: 520)
        }
        .windowStyle(.hiddenTitleBar)

        Settings {
            SettingsView()
                .environmentObject(model)
                .frame(width: 420)
                .padding()
        }
    }
}
