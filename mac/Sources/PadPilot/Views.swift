import AppKit
import GameController
import SwiftUI

enum Page: String, CaseIterable, Identifiable {
    case overview, buttons, sticks, triggers, motion, touch, lights, profiles, settings
    var id: String { rawValue }
    var key: String {
        switch self {
        case .overview: return "PgOverview"
        case .buttons: return "PgButtons"
        case .sticks: return "PgSticks"
        case .triggers: return "PgTriggers"
        case .motion: return "PgMotion"
        case .touch: return "PgTouch"
        case .lights: return "PgLights"
        case .profiles: return "PgProfiles"
        case .settings: return "PgSettings"
        }
    }
    var icon: String {
        switch self {
        case .overview: return "house"
        case .buttons: return "keyboard"
        case .sticks: return "l.joystick"
        case .triggers: return "bolt"
        case .motion: return "gyroscope"
        case .touch: return "hand.point.up.left"
        case .lights: return "lightbulb"
        case .profiles: return "person.2"
        case .settings: return "gearshape"
        }
    }
}

struct ContentView: View {
    @EnvironmentObject var engine: PadEngine
    @State private var page: Page? = .overview

    var body: some View {
        NavigationSplitView {
            VStack(spacing: 0) {
                HStack(spacing: 10) {
                    Image(nsImage: NSApp.applicationIconImage).resizable().frame(width: 34, height: 34)
                    VStack(alignment: .leading, spacing: 1) {
                        Text("PadPilot").font(.headline)
                        Text(L10n.t("Tagline")).font(.caption).foregroundStyle(.secondary)
                    }
                    Spacer()
                }
                .padding(.horizontal, 14).padding(.vertical, 12)
                List(Page.allCases, selection: $page) { p in
                    Label(L10n.t(p.key), systemImage: p.icon).tag(p)
                }
                VStack(alignment: .leading, spacing: 6) {
                    Text(L10n.t("ActiveProfile")).font(.caption).foregroundStyle(.secondary)
                    Picker("", selection: Binding(get: { engine.settings.activeProfile ?? UUID() }, set: { engine.select($0) })) {
                        ForEach(engine.settings.profiles) { Text($0.name).tag($0.id) }
                    }
                    .labelsHidden()
                }
                .padding(14)
            }
            .navigationSplitViewColumnWidth(min: 210, ideal: 230)
        } detail: {
            Group {
                switch page ?? .overview {
                case .overview: OverviewPage()
                case .buttons: ButtonsPage()
                case .sticks: SticksPage()
                case .triggers: TriggersPage()
                case .motion: MotionPage()
                case .touch: TouchPage()
                case .lights: LightsPage()
                case .profiles: ProfilesPage()
                case .settings: SettingsPage()
                }
            }
            .navigationTitle(L10n.t((page ?? .overview).key))
        }
    }
}

// MARK: Yardimcilar

struct SliderRow: View {
    let title: String
    @Binding var value: Double
    let range: ClosedRange<Double>
    var percent = true
    var step: Double? = nil

    var body: some View {
        LabeledContent(title) {
            HStack {
                if let step { Slider(value: $value, in: range, step: step) } else { Slider(value: $value, in: range) }
                Text(percent ? "\(Int((value * 100).rounded()))%" : String(format: value >= 100 ? "%.0f" : "%.1f", value))
                    .monospacedDigit().foregroundStyle(.secondary).frame(width: 52, alignment: .trailing)
            }
            .frame(maxWidth: 360)
        }
    }
}

struct AccessBanner: View {
    @EnvironmentObject var engine: PadEngine
    var body: some View {
        if !engine.trusted {
            HStack(alignment: .top) {
                Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.orange)
                Text(L10n.t("AccessNeeded")).fixedSize(horizontal: false, vertical: true)
                Spacer()
                Button(L10n.t("GrantAccess")) { engine.requestAccess() }
            }
        }
    }
}

struct StickDot: View {
    let x: Double, y: Double
    var body: some View {
        ZStack {
            Circle().fill(Color.primary.opacity(0.06))
            Circle().stroke(Color.primary.opacity(0.2))
            Path { p in p.move(to: CGPoint(x: 0, y: 50)); p.addLine(to: CGPoint(x: 100, y: 50)); p.move(to: CGPoint(x: 50, y: 0)); p.addLine(to: CGPoint(x: 50, y: 100)) }
                .stroke(Color.primary.opacity(0.12))
            Circle().fill(Color.accentColor).frame(width: 12, height: 12).offset(x: x * 44, y: -y * 44)
        }
        .frame(width: 100, height: 100)
    }
}

// MARK: Sayfalar

struct OverviewPage: View {
    @EnvironmentObject var engine: PadEngine

    var body: some View {
        Form {
            Section { AccessBanner() }
            if let c = engine.selected {
                let l = engine.live
                Section {
                    LabeledContent(PadEngine.name(c)) {
                        if let b = c.battery {
                            Label("\(Int(b.batteryLevel * 100))%\(b.batteryState == .charging ? " · " + L10n.t("Charging") : "")",
                                  systemImage: b.batteryState == .charging ? "battery.100.bolt" : "battery.75")
                        }
                    }
                    if engine.pads.count > 1 {
                        Picker("", selection: Binding(get: { engine.selected ?? c }, set: { engine.selected = $0 })) {
                            ForEach(engine.pads, id: \.self) { Text(PadEngine.name($0)).tag($0) }
                        }
                        .pickerStyle(.segmented)
                    }
                }
                Section {
                    HStack(spacing: 36) {
                        VStack { StickDot(x: l.lx, y: l.ly); Text(L10n.t("LeftStick")).font(.caption) }
                        VStack { StickDot(x: l.rx, y: l.ry); Text(L10n.t("RightStick")).font(.caption) }
                        VStack(alignment: .leading, spacing: 10) {
                            Text("L2"); ProgressView(value: l.l2).frame(width: 160)
                            Text("R2"); ProgressView(value: l.r2).frame(width: 160)
                        }
                    }
                    .frame(maxWidth: .infinity)
                    .padding(.vertical, 6)
                    LabeledContent(L10n.t("Pressed")) {
                        Text(l.buttons.isEmpty ? L10n.t("NoButtons") : PadButton.allCases.filter { l.buttons.contains($0) }.map { L10n.t("b." + $0.rawValue) }.joined(separator: "  "))
                    }
                    LabeledContent("Gyro") {
                        Text(String(format: "Yaw %6.1f°/s   Pitch %6.1f°/s", l.yaw, l.pitch)).monospacedDigit()
                    }
                }
                Section {
                    HStack {
                        Button(L10n.t("RumbleTest")) { engine.rumbleTest() }
                        Button(L10n.t("Calibrate")) { engine.calibrate() }
                    }
                }
                Section { Text(L10n.t("MacNote")).font(.callout).foregroundStyle(.secondary) }
            } else {
                Section {
                    VStack(spacing: 12) {
                        Image(systemName: "gamecontroller").font(.system(size: 54)).foregroundStyle(.secondary)
                        Text(L10n.t("Waiting")).font(.title2.bold())
                        Text(L10n.t("WaitingText")).multilineTextAlignment(.center).foregroundStyle(.secondary)
                        Text(L10n.t("Pairing")).font(.callout).multilineTextAlignment(.center).foregroundStyle(.secondary)
                        Button(L10n.t("OpenBluetooth")) {
                            NSWorkspace.shared.open(URL(string: "x-apple.systempreferences:com.apple.BluetoothSettings")!)
                        }
                    }
                    .frame(maxWidth: .infinity).padding(24)
                }
            }
        }
        .formStyle(.grouped)
    }
}

struct ButtonsPage: View {
    @EnvironmentObject var engine: PadEngine

    var body: some View {
        Form {
            Section { AccessBanner(); Text(L10n.t("MacNote")).font(.callout).foregroundStyle(.secondary) }
            Section {
                ForEach(PadButton.allCases) { b in
                    Picker(selection: Binding(get: { engine.profile.mappings[b.rawValue] ?? "" },
                                              set: { engine.profile.mappings[b.rawValue] = $0 })) {
                        ForEach(Target.all) { Text($0.label).tag($0.code) }
                    } label: {
                        HStack {
                            Circle().fill(engine.live.buttons.contains(b) ? Color.accentColor : Color.secondary.opacity(0.25)).frame(width: 8, height: 8)
                            Text(L10n.t("b." + b.rawValue))
                        }
                    }
                }
            } header: {
                HStack { Text(L10n.t("Mapping")); Spacer(); Button(L10n.t("ResetAll")) { engine.profile.mappings = [:] } }
            }
        }
        .formStyle(.grouped)
    }
}

struct StickSection: View {
    let title: String
    @Binding var s: StickSettings

    var body: some View {
        Section(title) {
            Picker(L10n.t("Mode"), selection: $s.mode) { ForEach(StickMode.allCases) { Text(L10n.t("sm." + $0.rawValue)).tag($0) } }
            Picker(L10n.t("Curve"), selection: $s.curve) { ForEach(StickCurve.allCases) { Text(L10n.t("sc." + $0.rawValue)).tag($0) } }
            SliderRow(title: L10n.t("Deadzone"), value: $s.deadzone, range: 0...0.4)
            SliderRow(title: L10n.t("Outer"), value: $s.outer, range: 0.6...1)
            SliderRow(title: L10n.t("Speed"), value: $s.speed, range: 200...5000, percent: false, step: 50)
            Toggle(L10n.t("InvertY"), isOn: $s.invertY)
        }
    }
}

struct SticksPage: View {
    @EnvironmentObject var engine: PadEngine
    var body: some View {
        Form {
            Section { AccessBanner(); Text(L10n.t("MacNote")).font(.callout).foregroundStyle(.secondary) }
            StickSection(title: L10n.t("LeftStick"), s: engine.profileBinding.leftStick)
            StickSection(title: L10n.t("RightStick"), s: engine.profileBinding.rightStick)
        }
        .formStyle(.grouped)
    }
}

struct TriggerSection: View {
    let title: String
    let value: Double
    @Binding var t: TriggerSettings

    var body: some View {
        Section {
            ProgressView(value: value)
            Picker(L10n.t("Effect"), selection: $t.effect) { ForEach(TriggerEffect.allCases) { Text(L10n.t("te." + $0.rawValue)).tag($0) } }
            SliderRow(title: L10n.t("Start"), value: $t.start, range: 0...0.9)
            SliderRow(title: L10n.t("End"), value: $t.end, range: 0.1...1)
            SliderRow(title: L10n.t("Strength"), value: $t.strength, range: 0...1)
            SliderRow(title: L10n.t("Frequency"), value: $t.frequency, range: 0...1)
            LabeledContent(L10n.t("Presets")) {
                HStack {
                    Button(L10n.t("te.off")) { t.effect = .off }
                    Button(L10n.t("PrPistol")) { t = TriggerSettings(effect: .weapon, start: 0.3, end: 0.6, strength: 0.9, frequency: t.frequency) }
                    Button(L10n.t("PrAuto")) { t = TriggerSettings(effect: .vibration, start: 0.3, end: 1, strength: 0.8, frequency: 0.35) }
                    Button(L10n.t("PrBrake")) { t = TriggerSettings(effect: .slope, start: 0.1, end: 0.9, strength: 1, frequency: t.frequency) }
                    Button(L10n.t("PrGas")) { t = TriggerSettings(effect: .resistance, start: 0.2, end: 1, strength: 0.35, frequency: t.frequency) }
                    Button(L10n.t("PrHard")) { t = TriggerSettings(effect: .resistance, start: 0, end: 1, strength: 1, frequency: t.frequency) }
                }
            }
        } header: {
            HStack { Text(title); Spacer(); Text(L10n.t("Adaptive")).foregroundStyle(.secondary) }
        }
    }
}

struct TriggersPage: View {
    @EnvironmentObject var engine: PadEngine
    var body: some View {
        Form {
            TriggerSection(title: L10n.t("L2Title"), value: engine.live.l2, t: engine.profileBinding.l2)
            TriggerSection(title: L10n.t("R2Title"), value: engine.live.r2, t: engine.profileBinding.r2)
        }
        .formStyle(.grouped)
    }
}

struct MotionPage: View {
    @EnvironmentObject var engine: PadEngine
    var body: some View {
        let g = engine.profileBinding.gyro
        Form {
            Section { AccessBanner() }
            Section(L10n.t("GyroAim")) {
                Picker(L10n.t("Mode"), selection: g.mode) { ForEach(GyroMode.allCases) { Text(L10n.t("gm." + $0.rawValue)).tag($0) } }
                Picker(L10n.t("When"), selection: g.activation) { ForEach(GyroActivation.allCases) { Text(L10n.t("ga." + $0.rawValue)).tag($0) } }
                Picker(L10n.t("ActivationButton"), selection: g.button) { ForEach(PadButton.allCases) { Text(L10n.t("b." + $0.rawValue)).tag($0) } }
                SliderRow(title: L10n.t("SensX"), value: g.sensX, range: 1...40, percent: false)
                SliderRow(title: L10n.t("SensY"), value: g.sensY, range: 1...40, percent: false)
                SliderRow(title: L10n.t("Smoothing"), value: g.smoothing, range: 0...3, percent: false)
                Toggle(L10n.t("InvertH"), isOn: g.invertX)
                Toggle(L10n.t("InvertV"), isOn: g.invertY)
            }
            Section {
                LabeledContent(engine.live.calibrating ? "…" : "Gyro") {
                    Text(String(format: "Yaw %6.1f°/s   Pitch %6.1f°/s", engine.live.yaw, engine.live.pitch)).monospacedDigit()
                }
                Button(L10n.t("Calibrate")) { engine.calibrate() }
            }
        }
        .formStyle(.grouped)
    }
}

struct TouchPage: View {
    @EnvironmentObject var engine: PadEngine
    var body: some View {
        let t = engine.profileBinding.touch
        Form {
            Section { AccessBanner() }
            Section(L10n.t("PgTouch")) {
                Picker(L10n.t("Mode"), selection: t.mode) { ForEach(TouchMode.allCases) { Text(L10n.t("tm." + $0.rawValue)).tag($0) } }
                SliderRow(title: L10n.t("Speed"), value: t.speed, range: 0.2...3, percent: false)
                Toggle(L10n.t("TapClick"), isOn: t.tapToClick)
                Toggle(L10n.t("TwoFinger"), isOn: t.twoFingerScroll)
            }
        }
        .formStyle(.grouped)
    }
}

struct LightsPage: View {
    @EnvironmentObject var engine: PadEngine
    var body: some View {
        let l = engine.profileBinding.light
        Form {
            Section(L10n.t("Lightbar")) {
                Picker(L10n.t("Mode"), selection: l.mode) { ForEach(LightMode.allCases) { Text(L10n.t("lm." + $0.rawValue)).tag($0) } }
                ColorPicker(L10n.t("Color"), selection: Binding(
                    get: { let c = PadEngine.hex(l.wrappedValue.color); return Color(red: c.0, green: c.1, blue: c.2) },
                    set: { new in
                        if let c = NSColor(new).usingColorSpace(.sRGB) {
                            l.wrappedValue.color = String(format: "#%02X%02X%02X", Int(c.redComponent * 255), Int(c.greenComponent * 255), Int(c.blueComponent * 255))
                            if l.wrappedValue.mode == .off { l.wrappedValue.mode = .solid }
                        }
                    }), supportsOpacity: false)
                SliderRow(title: L10n.t("Brightness"), value: l.brightness, range: 0.05...1)
                Toggle(L10n.t("LowFlash"), isOn: l.lowBatteryFlash)
                Toggle(L10n.t("PlayerLeds"), isOn: l.playerLeds)
            }
            Section(L10n.t("Rumble")) {
                Button(L10n.t("RumbleTest")) { engine.rumbleTest() }
            }
        }
        .formStyle(.grouped)
    }
}

struct ProfilesPage: View {
    @EnvironmentObject var engine: PadEngine
    var body: some View {
        Form {
            Section(L10n.t("Profiles")) {
                Picker("", selection: Binding(get: { engine.settings.activeProfile ?? UUID() }, set: { engine.select($0) })) {
                    ForEach(engine.settings.profiles) { Text($0.name).tag($0.id) }
                }
                .pickerStyle(.inline).labelsHidden()
                HStack {
                    Button(L10n.t("New")) { engine.newProfile() }
                    Button(L10n.t("Duplicate")) { engine.duplicateProfile() }
                    Button(L10n.t("Delete"), role: .destructive) { engine.deleteProfile() }.disabled(engine.settings.profiles.count <= 1)
                }
            }
            Section {
                TextField(L10n.t("Name"), text: engine.profileBinding.name)
                Toggle(L10n.t("AutoProfile"), isOn: $engine.settings.autoProfile)
                TextField(L10n.t("AutoProfile"), text: engine.profileBinding.apps, prompt: Text("Minecraft; com.valvesoftware.steam"))
                Text(L10n.t("AppsHint")).font(.caption).foregroundStyle(.secondary)
                if let f = engine.frontApp {
                    LabeledContent(L10n.t("FrontApp")) {
                        HStack {
                            Text("\(f.name) (\(f.bundle))").foregroundStyle(.secondary)
                            Button(L10n.t("AddFront")) {
                                let cur = engine.profile.apps.trimmingCharacters(in: .whitespaces)
                                engine.profile.apps = cur.isEmpty ? f.name : cur + "; " + f.name
                            }
                        }
                    }
                }
            }
        }
        .formStyle(.grouped)
    }
}

struct SettingsPage: View {
    @EnvironmentObject var engine: PadEngine
    @State private var message = ""
    @State private var email = ""
    @State private var status = ""
    @State private var sending = false

    private var version: String { Bundle.main.object(forInfoDictionaryKey: "CFBundleShortVersionString") as? String ?? "dev" }

    var body: some View {
        Form {
            Section {
                Picker(L10n.t("Language"), selection: Binding(get: { engine.settings.language }, set: { engine.setLanguage($0) })) {
                    Text(L10n.t("LangAuto")).tag("auto"); Text("Türkçe").tag("tr"); Text("English").tag("en")
                }
                Stepper(value: $engine.settings.lowBattery, in: 5...50, step: 5) {
                    LabeledContent(L10n.t("LowBattery"), value: "\(engine.settings.lowBattery)")
                }
                Toggle(L10n.t("StartAtLogin"), isOn: Binding(get: { engine.startAtLogin }, set: { engine.setStartAtLogin($0) }))
                AccessBanner()
            }
            Section(L10n.t("Feedback")) {
                Text(L10n.t("FeedbackDesc")).font(.callout).foregroundStyle(.secondary)
                TextEditor(text: $message).frame(minHeight: 110).font(.body)
                TextField(L10n.t("YourEmail"), text: $email)
                HStack {
                    Text(status).foregroundStyle(.secondary)
                    Spacer()
                    Button(L10n.t("Send")) {
                        let text = message.trimmingCharacters(in: .whitespacesAndNewlines)
                        guard !text.isEmpty else { return }
                        sending = true; status = L10n.t("Sending")
                        Task {
                            let (ok, err) = await Feedback.send(message: text, email: email, version: version)
                            await MainActor.run {
                                sending = false
                                status = ok ? L10n.t("Sent") : L10n.t("SendFailed") + (err ?? "")
                                if ok { message = "" }
                            }
                        }
                    }
                    .keyboardShortcut(.defaultAction)
                    .disabled(sending || message.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty)
                }
            }
            Section(L10n.t("About")) {
                HStack(alignment: .top, spacing: 16) {
                    Image(nsImage: NSApp.applicationIconImage).resizable().frame(width: 56, height: 56)
                    VStack(alignment: .leading, spacing: 4) {
                        Text("PadPilot \(version)").font(.headline)
                        Text("\(L10n.t("Developer")): ").foregroundColor(.secondary) + Text("Ali Nuraylı").bold()
                        Text(L10n.t("License")).font(.callout).foregroundStyle(.secondary)
                        HStack {
                            Link(L10n.t("GitHub"), destination: URL(string: "https://github.com/anurayli/PadPilot")!)
                            Link(L10n.t("Releases"), destination: URL(string: "https://github.com/anurayli/PadPilot/releases")!)
                        }
                        .padding(.top, 4)
                    }
                }
            }
        }
        .formStyle(.grouped)
    }
}
