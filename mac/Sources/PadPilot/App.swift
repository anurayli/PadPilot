import AppKit
import SwiftUI

@main
struct PadPilotApp: App {
    @StateObject private var engine = PadEngine()

    var body: some Scene {
        Window("PadPilot", id: "main") {
            ContentView()
                .environmentObject(engine)
                .frame(minWidth: 920, minHeight: 620)
        }
        .defaultSize(width: 1100, height: 760)

        MenuBarExtra {
            MenuContent().environmentObject(engine)
        } label: {
            Image(systemName: "gamecontroller.fill")
        }
    }
}

struct MenuContent: View {
    @EnvironmentObject var engine: PadEngine
    @Environment(\.openWindow) private var openWindow

    var body: some View {
        Button(L10n.t("ShowWindow")) {
            openWindow(id: "main")
            NSApp.activate(ignoringOtherApps: true)
        }
        Divider()
        ForEach(engine.pads, id: \.self) { c in
            let level = Int((c.battery?.batteryLevel ?? 0) * 100)
            Text("\(PadEngine.name(c)) · \(L10n.t("Battery")) %\(level)")
        }
        if !engine.pads.isEmpty { Divider() }
        Picker(L10n.t("ActiveProfile"), selection: Binding(get: { engine.settings.activeProfile ?? UUID() }, set: { engine.select($0) })) {
            ForEach(engine.settings.profiles) { Text($0.name).tag($0.id) }
        }
        Divider()
        Button(L10n.t("Quit")) { NSApp.terminate(nil) }
    }
}
