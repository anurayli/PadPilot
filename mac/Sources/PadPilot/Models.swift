import Foundation

enum StickMode: String, Codable, CaseIterable, Identifiable { case none, mouse, scroll, wasd, arrows; var id: String { rawValue } }
enum StickCurve: String, Codable, CaseIterable, Identifiable { case linear, precise, smooth, aggressive; var id: String { rawValue } }
enum GyroMode: String, Codable, CaseIterable, Identifiable { case off, mouse; var id: String { rawValue } }
enum GyroActivation: String, Codable, CaseIterable, Identifiable { case always, whileHeld, whileNotHeld, toggle; var id: String { rawValue } }
enum TouchMode: String, Codable, CaseIterable, Identifiable { case off, mouse; var id: String { rawValue } }
enum LightMode: String, Codable, CaseIterable, Identifiable { case solid, battery, rainbow, off; var id: String { rawValue } }
enum TriggerEffect: String, Codable, CaseIterable, Identifiable { case off, resistance, slope, weapon, vibration; var id: String { rawValue } }

enum PadButton: String, Codable, CaseIterable, Identifiable {
    case cross, circle, square, triangle, l1, r1, l2, r2, l3, r3, up, down, left, right, options, share, ps, touchpad
    var id: String { rawValue }
}

struct StickSettings: Codable, Equatable {
    var mode: StickMode = .none
    var deadzone = 0.08
    var outer = 0.97
    var curve: StickCurve = .linear
    var speed = 1600.0
    var invertY = false
}

/// Konumlar ve guc 0...1 (GameController olcegi).
struct TriggerSettings: Codable, Equatable {
    var effect: TriggerEffect = .off
    var start = 0.2
    var end = 0.6
    var strength = 0.6
    var frequency = 0.3
}

struct GyroSettings: Codable, Equatable {
    var mode: GyroMode = .off
    var activation: GyroActivation = .whileHeld
    var button: PadButton = .l2
    var sensX = 12.0
    var sensY = 10.0
    var smoothing = 0.6
    var invertX = false
    var invertY = false
}

struct TouchSettings: Codable, Equatable {
    var mode: TouchMode = .off
    var speed = 1.0
    var tapToClick = true
    var twoFingerScroll = true
}

struct LightSettings: Codable, Equatable {
    var mode: LightMode = .solid
    var color = "#2F6BFF"
    var brightness = 1.0
    var lowBatteryFlash = true
    var playerLeds = true
}

struct Profile: Codable, Identifiable, Equatable {
    var id = UUID()
    var name: String
    /// Bu profili otomatik acan uygulamalar (ad veya paket kimligi, ";" ile ayrilmis).
    var apps = ""
    /// PadButton.rawValue -> hedef kodu ("" = yok). Bkz. Targets.
    var mappings: [String: String] = [:]
    var leftStick = StickSettings()
    var rightStick = StickSettings()
    var l2 = TriggerSettings()
    var r2 = TriggerSettings()
    var gyro = GyroSettings()
    var touch = TouchSettings()
    var light = LightSettings()

    func matches(app name: String?, bundle: String?) -> Bool {
        let parts = apps.split(whereSeparator: { $0 == ";" || $0 == "," }).map { $0.trimmingCharacters(in: .whitespaces).lowercased() }.filter { !$0.isEmpty }
        return parts.contains { $0 == name?.lowercased() || $0 == bundle?.lowercased() }
    }
}

struct AppSettings: Codable {
    var language = "auto"
    var activeProfile: UUID?
    var autoProfile = true
    var lowBattery = 20
    var profiles: [Profile] = []
}

enum SettingsStore {
    static var folder: URL {
        let base = FileManager.default.urls(for: .applicationSupportDirectory, in: .userDomainMask)[0]
        return base.appendingPathComponent("PadPilot", isDirectory: true)
    }
    static var file: URL { folder.appendingPathComponent("settings.json") }

    static func load() -> AppSettings {
        var s = AppSettings()
        if let data = try? Data(contentsOf: file) {
            if let decoded = try? JSONDecoder().decode(AppSettings.self, from: data) { s = decoded }
            else { try? data.write(to: folder.appendingPathComponent("settings.bad.json")) }
        }
        L10n.apply(s.language)
        if s.profiles.isEmpty { s.profiles = [Profile(name: L10n.t("DefaultProfile"))] }
        if s.activeProfile == nil || !s.profiles.contains(where: { $0.id == s.activeProfile }) { s.activeProfile = s.profiles[0].id }
        return s
    }

    static func save(_ s: AppSettings) {
        try? FileManager.default.createDirectory(at: folder, withIntermediateDirectories: true)
        let enc = JSONEncoder()
        enc.outputFormatting = [.prettyPrinted, .sortedKeys]
        if let data = try? enc.encode(s) { try? data.write(to: file, options: .atomic) }
    }
}

/// Esleme hedefleri: "k:<macOS tus kodu>", "m:L|R|M", "s:up|down", "a:next|prev|gyro".
struct Target: Identifiable, Hashable {
    let code: String
    let label: String
    var id: String { code }

    static var all: [Target] {
        var l = [Target(code: "", label: L10n.t("None"))]
        l += [Target(code: "m:L", label: L10n.t("MouseLeft")), Target(code: "m:R", label: L10n.t("MouseRight")),
              Target(code: "m:M", label: L10n.t("MouseMiddle")), Target(code: "s:up", label: L10n.t("ScrollUp")),
              Target(code: "s:down", label: L10n.t("ScrollDown"))]
        l += [Target(code: "a:next", label: L10n.t("NextProfile")), Target(code: "a:prev", label: L10n.t("PrevProfile")),
              Target(code: "a:gyro", label: L10n.t("ToggleGyro"))]
        l += keys.map { Target(code: "k:\($0.code)", label: $0.name) }
        return l
    }

    static let keys: [(code: Int, name: String)] = {
        var k: [(Int, String)] = [(49, "Space"), (36, "Return"), (53, "Esc"), (48, "Tab"), (51, "Delete"),
                                  (56, "Shift"), (59, "Control"), (58, "Option"), (55, "Command"),
                                  (126, "↑"), (125, "↓"), (123, "←"), (124, "→"), (116, "Page Up"), (121, "Page Down"), (115, "Home"), (119, "End")]
        let letters: [(String, Int)] = [("A", 0), ("B", 11), ("C", 8), ("D", 2), ("E", 14), ("F", 3), ("G", 5), ("H", 4), ("I", 34), ("J", 38),
                                        ("K", 40), ("L", 37), ("M", 46), ("N", 45), ("O", 31), ("P", 35), ("Q", 12), ("R", 15), ("S", 1), ("T", 17),
                                        ("U", 32), ("V", 9), ("W", 13), ("X", 7), ("Y", 16), ("Z", 6)]
        k += letters.map { ($0.1, $0.0) }
        let digits: [(String, Int)] = [("1", 18), ("2", 19), ("3", 20), ("4", 21), ("5", 23), ("6", 22), ("7", 26), ("8", 28), ("9", 25), ("0", 29)]
        k += digits.map { ($0.1, $0.0) }
        let fkeys = [122, 120, 99, 118, 96, 97, 98, 100, 101, 109, 103, 111]
        for (i, c) in fkeys.enumerated() { k.append((c, "F\(i + 1)")) }
        return k
    }()
}
