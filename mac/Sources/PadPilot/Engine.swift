import AppKit
import CoreHaptics
import Foundation
import GameController
import ServiceManagement
import SwiftUI

/// Arayuz icin secili pad'in anlik durumu.
struct LiveState {
    var buttons: Set<PadButton> = []
    var lx = 0.0, ly = 0.0, rx = 0.0, ry = 0.0, l2 = 0.0, r2 = 0.0
    var yaw = 0.0, pitch = 0.0
    var gyroActive = false, calibrating = false
    var touch: CGPoint?
}

/// Bir pad'in calisma durumu.
final class PadSlot {
    let controller: GCController
    var held: Set<String> = []
    var prevButtons: Set<PadButton> = []
    var remX = 0.0, remY = 0.0, scrollRem = 0.0
    var gyroEnabled = true, gyroToggled = false
    var bias = (0.0, 0.0, 0.0), ema = (0.0, 0.0, 0.0), still = 0.0
    var calibUntil: Date?, calSum = (0.0, 0.0, 0.0), calN = 0
    var touchLast: CGPoint?, touchChanged = Date.distantPast, touchStart = Date.distantPast, touchMoved = 0.0, touching = false
    var touch2Last: CGPoint?
    var lastLight: [Float] = []
    var applied: (TriggerSettings, TriggerSettings)?
    var haptic: CHHapticEngine?

    init(_ c: GCController) { controller = c }
}

final class PadEngine: ObservableObject {
    @Published var settings: AppSettings { didSet { scheduleSave() } }
    @Published private(set) var pads: [GCController] = []
    @Published var selected: GCController?
    @Published private(set) var live = LiveState()
    @Published private(set) var trusted = Output.trusted
    @Published private(set) var frontApp: (name: String, bundle: String)?
    @Published private(set) var autoProfileID: UUID?

    private var slots: [ObjectIdentifier: PadSlot] = [:]
    private var timer: Timer?
    private var tick = 0
    private var lastFrame = Date()
    private var saveItem: DispatchWorkItem?

    init() {
        settings = SettingsStore.load()
        GCController.shouldMonitorBackgroundEvents = true
        let nc = NotificationCenter.default
        nc.addObserver(forName: .GCControllerDidConnect, object: nil, queue: .main) { [weak self] n in
            if let c = n.object as? GCController { self?.attach(c) }
        }
        nc.addObserver(forName: .GCControllerDidDisconnect, object: nil, queue: .main) { [weak self] n in
            if let c = n.object as? GCController { self?.detach(c) }
        }
        GCController.controllers().forEach { attach($0) }
        let t = Timer(timeInterval: 1.0 / 120.0, repeats: true) { [weak self] _ in self?.step() }
        RunLoop.main.add(t, forMode: .common)
        timer = t
    }

    // MARK: Profiller

    var activeIndex: Int {
        if let a = autoProfileID, let i = settings.profiles.firstIndex(where: { $0.id == a }) { return i }
        return settings.profiles.firstIndex(where: { $0.id == settings.activeProfile }) ?? 0
    }

    var profile: Profile {
        get { settings.profiles[activeIndex] }
        set { settings.profiles[activeIndex] = newValue }
    }

    var profileBinding: Binding<Profile> { Binding(get: { self.profile }, set: { self.profile = $0 }) }

    func select(_ id: UUID) { settings.activeProfile = id }

    func cycleProfile(_ step: Int) {
        guard let i = settings.profiles.firstIndex(where: { $0.id == settings.activeProfile }) else { return }
        let n = settings.profiles.count
        settings.activeProfile = settings.profiles[(i + step + n) % n].id
    }

    func newProfile() {
        let p = Profile(name: uniqueName(L10n.t("New")))
        settings.profiles.append(p); settings.activeProfile = p.id
    }

    func duplicateProfile() {
        var p = profile
        p.id = UUID(); p.name = uniqueName(p.name + " 2"); p.apps = ""
        settings.profiles.append(p); settings.activeProfile = p.id
    }

    func deleteProfile() {
        guard settings.profiles.count > 1 else { return }
        let i = activeIndex
        settings.profiles.remove(at: i)
        settings.activeProfile = settings.profiles[max(0, i - 1)].id
        autoProfileID = nil
    }

    private func uniqueName(_ base: String) -> String {
        var n = base, i = 2
        while settings.profiles.contains(where: { $0.name == n }) { n = "\(base) \(i)"; i += 1 }
        return n
    }

    func setLanguage(_ value: String) {
        settings.language = value
        L10n.apply(value)
        objectWillChange.send()
    }

    // MARK: Pad'ler

    private func attach(_ c: GCController) {
        guard c.extendedGamepad != nil, slots[ObjectIdentifier(c)] == nil else { return }
        slots[ObjectIdentifier(c)] = PadSlot(c)
        pads.append(c)
        if let m = c.motion, m.sensorsRequireManualActivation { m.sensorsActive = true }
        assignPlayerIndices()
        if selected == nil { selected = c }
    }

    private func detach(_ c: GCController) {
        if let s = slots.removeValue(forKey: ObjectIdentifier(c)) { release(s) }
        pads.removeAll { $0 === c }
        if selected === c { selected = pads.first }
        assignPlayerIndices()
    }

    private func assignPlayerIndices() {
        let idx: [GCControllerPlayerIndex] = [.index1, .index2, .index3, .index4]
        for (i, c) in pads.enumerated() {
            c.playerIndex = profile.light.playerLeds && i < 4 ? idx[i] : .indexUnset
        }
    }

    static func name(_ c: GCController) -> String {
        if c.extendedGamepad is GCDualSenseGamepad { return "DualSense" }
        if c.extendedGamepad is GCDualShockGamepad { return "DualShock 4" }
        return c.vendorName ?? c.productCategory
    }

    func calibrate() {
        guard let c = selected, let s = slots[ObjectIdentifier(c)] else { return }
        s.calibUntil = Date().addingTimeInterval(1.5); s.calSum = (0, 0, 0); s.calN = 0
    }

    func rumbleTest() {
        guard let c = selected, let s = slots[ObjectIdentifier(c)],
              let engine = c.haptics?.createEngine(withLocality: .default) else { return }
        s.haptic = engine
        do {
            try engine.start()
            let ev = CHHapticEvent(eventType: .hapticContinuous,
                                   parameters: [CHHapticEventParameter(parameterID: .hapticIntensity, value: 1)],
                                   relativeTime: 0, duration: 0.5)
            let player = try engine.makePlayer(with: CHHapticPattern(events: [ev], parameters: []))
            try player.start(atTime: 0)
        } catch {}
        DispatchQueue.main.asyncAfter(deadline: .now() + 1) { engine.stop(completionHandler: nil) }
    }

    func requestAccess() { Output.requestAccess() }

    func setStartAtLogin(_ on: Bool) {
        do { if on { try SMAppService.mainApp.register() } else { try SMAppService.mainApp.unregister() } } catch {}
        objectWillChange.send()
    }

    var startAtLogin: Bool { SMAppService.mainApp.status == .enabled }

    // MARK: Ana dongu (120 Hz)

    private func step() {
        let now = Date()
        let dt = min(now.timeIntervalSince(lastFrame), 0.05)
        lastFrame = now
        tick += 1
        let p = profile
        for c in pads {
            guard let s = slots[ObjectIdentifier(c)], let g = c.extendedGamepad else { continue }
            process(s, g, p, dt, now)
            if tick % 6 == 0 { updateLight(s, p, now) }
        }
        if tick % 4 == 0, let c = selected, let g = c.extendedGamepad, let s = slots[ObjectIdentifier(c)] { publishLive(g, s) }
        if tick % 120 == 0 { checkFrontApp(); assignPlayerIndices(); let t = Output.trusted; if t != trusted { trusted = t } }
    }

    private func buttons(_ g: GCExtendedGamepad) -> Set<PadButton> {
        var s = Set<PadButton>()
        func add(_ b: PadButton, _ i: GCControllerButtonInput?) { if i?.isPressed == true { s.insert(b) } }
        add(.cross, g.buttonA); add(.circle, g.buttonB); add(.square, g.buttonX); add(.triangle, g.buttonY)
        add(.l1, g.leftShoulder); add(.r1, g.rightShoulder); add(.l2, g.leftTrigger); add(.r2, g.rightTrigger)
        add(.l3, g.leftThumbstickButton); add(.r3, g.rightThumbstickButton)
        add(.up, g.dpad.up); add(.down, g.dpad.down); add(.left, g.dpad.left); add(.right, g.dpad.right)
        add(.options, g.buttonMenu); add(.share, g.buttonOptions); add(.ps, g.buttonHome)
        if let ds = g as? GCDualSenseGamepad { add(.touchpad, ds.touchpadButton) }
        else if let d4 = g as? GCDualShockGamepad { add(.touchpad, d4.touchpadButton) }
        return s
    }

    private func shape(_ x: Double, _ y: Double, _ st: StickSettings) -> (Double, Double) {
        let y = st.invertY ? -y : y
        let mag = (x * x + y * y).squareRoot()
        let dz = min(max(st.deadzone, 0), 0.9)
        guard mag > dz, mag > 1e-5 else { return (0, 0) }
        let outer = max(st.outer, dz + 0.05)
        var t = min(max((mag - dz) / (outer - dz), 0), 1)
        switch st.curve {
        case .precise: t = pow(t, 1.8)
        case .smooth: t = t * t * (3 - 2 * t)
        case .aggressive: t = t.squareRoot()
        case .linear: break
        }
        return (x / mag * t, y / mag * t)
    }

    private func process(_ s: PadSlot, _ g: GCExtendedGamepad, _ p: Profile, _ dt: Double, _ now: Date) {
        let pressed = buttons(g)
        var wanted = Set<String>()
        var mx = 0.0, my = 0.0
        let rising = pressed.subtracting(s.prevButtons)
        let touchMouse = p.touch.mode == .mouse

        // Tus eslemeleri
        for b in pressed {
            if b == .touchpad && touchMouse { continue }
            guard let code = p.mappings[b.rawValue], !code.isEmpty else { continue }
            if code.hasPrefix("a:") || code.hasPrefix("s:") {
                if rising.contains(b) { fire(code, s) }
            } else { wanted.insert(code) }
        }

        // Analoglar
        for (st, stick) in [(p.leftStick, g.leftThumbstick), (p.rightStick, g.rightThumbstick)] where st.mode != .none {
            let (x, y) = shape(Double(stick.xAxis.value), Double(stick.yAxis.value), st)
            switch st.mode {
            case .mouse: mx += x * st.speed * dt; my -= y * st.speed * dt
            case .scroll:
                s.scrollRem += y * st.speed * dt * 0.5
                let n = Int32(s.scrollRem); s.scrollRem -= Double(n); Output.scroll(n)
            case .wasd, .arrows:
                let w = st.mode == .wasd
                if y > 0.5 { wanted.insert(w ? "k:13" : "k:126") }
                if y < -0.5 { wanted.insert(w ? "k:1" : "k:125") }
                if x < -0.5 { wanted.insert(w ? "k:0" : "k:123") }
                if x > 0.5 { wanted.insert(w ? "k:2" : "k:124") }
            case .none: break
            }
        }

        // Gyro
        gyro(s, pressed, p.gyro, dt, now, &mx, &my)

        // Touchpad
        if touchMouse { touch(s, g, p.touch, pressed, now, &mx, &my, &wanted) }

        // Adaptif tetikler (yalnizca degistiginde gonderilir)
        if let ds = g as? GCDualSenseGamepad, s.applied.map({ $0.0 != p.l2 || $0.1 != p.r2 }) ?? true {
            apply(ds.leftTrigger, p.l2); apply(ds.rightTrigger, p.r2)
            s.applied = (p.l2, p.r2)
        }

        // Ciktilar
        s.remX += mx; s.remY += my
        let ix = Int(s.remX), iy = Int(s.remY)
        s.remX -= Double(ix); s.remY -= Double(iy)
        if trusted {
            Output.move(dx: ix, dy: iy)
            for c in s.held.subtracting(wanted) { emit(c, false) }
            for c in wanted.subtracting(s.held) { emit(c, true) }
            s.held = wanted
        }
        s.prevButtons = pressed
    }

    private func emit(_ code: String, _ down: Bool) {
        let v = String(code.dropFirst(2))
        if code.hasPrefix("k:"), let k = Int(v) { Output.key(k, down: down) }
        else if code.hasPrefix("m:") { Output.mouseButton(v, down: down) }
    }

    private func fire(_ code: String, _ s: PadSlot) {
        switch code {
        case "a:next": cycleProfile(1)
        case "a:prev": cycleProfile(-1)
        case "a:gyro": s.gyroEnabled.toggle()
        case "s:up": if trusted { Output.scroll(60) }
        case "s:down": if trusted { Output.scroll(-60) }
        default: break
        }
    }

    private func release(_ s: PadSlot) {
        for c in s.held { emit(c, false) }
        s.held = []
    }

    private func gyro(_ s: PadSlot, _ pressed: Set<PadButton>, _ g: GyroSettings, _ dt: Double, _ now: Date, _ mx: inout Double, _ my: inout Double) {
        guard let m = s.controller.motion, m.hasRotationRate else { return }
        let deg = 180.0 / Double.pi
        let r = m.rotationRate
        let (x, y, z) = (r.x * deg, r.y * deg, r.z * deg)

        if let until = s.calibUntil {
            if now < until { s.calSum.0 += x; s.calSum.1 += y; s.calSum.2 += z; s.calN += 1 }
            else {
                if s.calN > 10 { let n = Double(s.calN); s.bias = (s.calSum.0 / n, s.calSum.1 / n, s.calSum.2 / n) }
                s.calibUntil = nil
            }
        }
        // Hareketsizken sapmayi yavasca ogren
        s.ema.0 += (x - s.ema.0) * 0.05; s.ema.1 += (y - s.ema.1) * 0.05; s.ema.2 += (z - s.ema.2) * 0.05
        let dev = max(abs(x - s.ema.0), abs(y - s.ema.1), abs(z - s.ema.2))
        if dev < 1.5 {
            s.still += dt
            if s.still > 1.2 { s.bias.0 += (s.ema.0 - s.bias.0) * 0.02; s.bias.1 += (s.ema.1 - s.bias.1) * 0.02; s.bias.2 += (s.ema.2 - s.bias.2) * 0.02 }
        } else { s.still = 0 }

        var pitch = x - s.bias.0, yaw = y - s.bias.1
        let held = pressed.contains(g.button)
        if g.activation == .toggle && held && !s.prevButtons.contains(g.button) { s.gyroToggled.toggle() }
        let on: Bool
        switch g.activation {
        case .always: on = true
        case .whileHeld: on = held
        case .whileNotHeld: on = !held
        case .toggle: on = s.gyroToggled
        }
        guard g.mode == .mouse, on, s.gyroEnabled, s.calibUntil == nil else { return }
        let speed = (yaw * yaw + pitch * pitch).squareRoot(), thr = g.smoothing * 3
        if thr > 0 && speed < thr { let k = speed / thr; yaw *= k; pitch *= k }
        mx += (g.invertX ? 1 : -1) * yaw * dt * g.sensX
        my += (g.invertY ? 1 : -1) * pitch * dt * g.sensY
    }

    private func touch(_ s: PadSlot, _ g: GCExtendedGamepad, _ t: TouchSettings, _ pressed: Set<PadButton>, _ now: Date,
                       _ mx: inout Double, _ my: inout Double, _ wanted: inout Set<String>) {
        let pads: (GCControllerDirectionPad, GCControllerDirectionPad)?
        if let ds = g as? GCDualSenseGamepad { pads = (ds.touchpadPrimary, ds.touchpadSecondary) }
        else if let d4 = g as? GCDualShockGamepad { pads = (d4.touchpadPrimary, d4.touchpadSecondary) }
        else { pads = nil }
        guard let pair = pads else { return }
        let (p1, p2) = pair
        let pt = CGPoint(x: CGFloat(p1.xAxis.value), y: CGFloat(-p1.yAxis.value))
        let pt2 = CGPoint(x: CGFloat(p2.xAxis.value), y: CGFloat(-p2.yAxis.value))
        let twoFingers = s.touch2Last.map { $0 != pt2 } ?? false
        defer { s.touch2Last = pt2 }

        if let last = s.touchLast, last != pt {
            if !s.touching { s.touching = true; s.touchStart = now; s.touchMoved = 0 }
            else {
                let dx = Double(pt.x - last.x), dy = Double(pt.y - last.y)
                s.touchMoved += abs(dx) + abs(dy)
                if twoFingers && t.twoFingerScroll { if trusted { Output.scroll(Int32(-dy * 400 * t.speed)) } }
                else { mx += dx * 900 * t.speed; my += dy * 900 * t.speed }
            }
            s.touchChanged = now
        } else if s.touching && now.timeIntervalSince(s.touchChanged) > 0.12 {
            // Parmak kalkti: kisa dokunus = tik
            if t.tapToClick && s.touchChanged.timeIntervalSince(s.touchStart) < 0.2 && s.touchMoved < 0.08 && !pressed.contains(.touchpad), trusted { Output.click() }
            s.touching = false
        }
        s.touchLast = pt
        if pressed.contains(.touchpad) { wanted.insert("m:L") }
    }

    private func apply(_ trig: GCDualSenseAdaptiveTrigger, _ t: TriggerSettings) {
        let start = Float(min(max(t.start, 0), 0.9)), end = Float(min(max(t.end, t.start + 0.1), 1))
        let str = Float(min(max(t.strength, 0), 1))
        switch t.effect {
        case .off: trig.setModeOff()
        case .resistance: trig.setModeFeedbackWithStartPosition(start, resistiveStrength: str)
        case .weapon: trig.setModeWeaponWithStartPosition(min(start, 0.7), endPosition: max(end, min(start, 0.7) + 0.1), resistiveStrength: str)
        case .vibration: trig.setModeVibrationWithStartPosition(start, amplitude: str, frequency: Float(min(max(t.frequency, 0), 1)))
        case .slope: trig.setModeSlopeFeedback(startPosition: start, endPosition: end, startStrength: 0.1, endStrength: str)
        }
    }

    private func updateLight(_ s: PadSlot, _ p: Profile, _ now: Date) {
        guard let light = s.controller.light else { return }
        let l = p.light
        var rgb: (Double, Double, Double)
        switch l.mode {
        case .off: rgb = (0, 0, 0)
        case .rainbow: rgb = Self.hsv((now.timeIntervalSince1970 * 45).truncatingRemainder(dividingBy: 360))
        case .battery: rgb = Self.hsv(Double(s.controller.battery?.batteryLevel ?? 1) * 120)
        case .solid: rgb = Self.hex(l.color)
        }
        if let b = s.controller.battery, b.batteryState == .discharging, Double(b.batteryLevel) * 100 <= Double(settings.lowBattery), l.lowBatteryFlash {
            rgb = now.timeIntervalSince1970.truncatingRemainder(dividingBy: 1.2) < 0.6 ? (1, 0, 0) : (0, 0, 0)
        }
        let k = l.mode == .off ? 0 : min(max(l.brightness, 0.05), 1)
        let v = [Float(rgb.0 * k), Float(rgb.1 * k), Float(rgb.2 * k)]
        if v != s.lastLight {
            light.color = GCColor(red: v[0], green: v[1], blue: v[2])
            s.lastLight = v
        }
    }

    static func hsv(_ h: Double) -> (Double, Double, Double) {
        let x = 1 - abs((h / 60).truncatingRemainder(dividingBy: 2) - 1)
        switch Int(h / 60) {
        case 0: return (1, x, 0)
        case 1: return (x, 1, 0)
        case 2: return (0, 1, x)
        case 3: return (0, x, 1)
        case 4: return (x, 0, 1)
        default: return (1, 0, x)
        }
    }

    static func hex(_ s: String) -> (Double, Double, Double) {
        let h = s.trimmingCharacters(in: CharacterSet(charactersIn: "# "))
        guard h.count == 6, let v = UInt32(h, radix: 16) else { return (0.18, 0.42, 1) }
        return (Double((v >> 16) & 0xFF) / 255, Double((v >> 8) & 0xFF) / 255, Double(v & 0xFF) / 255)
    }

    private func publishLive(_ g: GCExtendedGamepad, _ s: PadSlot) {
        var l = LiveState()
        l.buttons = buttons(g)
        l.lx = Double(g.leftThumbstick.xAxis.value); l.ly = Double(g.leftThumbstick.yAxis.value)
        l.rx = Double(g.rightThumbstick.xAxis.value); l.ry = Double(g.rightThumbstick.yAxis.value)
        l.l2 = Double(g.leftTrigger.value); l.r2 = Double(g.rightTrigger.value)
        if let m = s.controller.motion, m.hasRotationRate {
            l.yaw = m.rotationRate.y * 180 / .pi - s.bias.1
            l.pitch = m.rotationRate.x * 180 / .pi - s.bias.0
        }
        l.calibrating = s.calibUntil != nil
        l.touch = s.touching ? s.touchLast : nil
        live = l
    }

    private func checkFrontApp() {
        guard let app = NSWorkspace.shared.frontmostApplication, app.bundleIdentifier != Bundle.main.bundleIdentifier else { return }
        let name = app.localizedName ?? "", bundle = app.bundleIdentifier ?? ""
        if frontApp?.bundle != bundle { frontApp = (name, bundle) }
        let match = settings.autoProfile ? settings.profiles.first(where: { $0.matches(app: name, bundle: bundle) })?.id : nil
        if match != autoProfileID { autoProfileID = match }
    }

    private func scheduleSave() {
        saveItem?.cancel()
        let item = DispatchWorkItem { [weak self] in if let s = self?.settings { SettingsStore.save(s) } }
        saveItem = item
        DispatchQueue.main.asyncAfter(deadline: .now() + 0.8, execute: item)
    }
}
