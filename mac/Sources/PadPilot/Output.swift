import AppKit
import ApplicationServices
import CoreGraphics

/// Klavye ve fare olaylari (Erisilebilirlik izni gerekir).
enum Output {
    static var trusted: Bool { AXIsProcessTrusted() }

    static func requestAccess() {
        let key = kAXTrustedCheckOptionPrompt.takeUnretainedValue() as String
        _ = AXIsProcessTrustedWithOptions([key: true] as CFDictionary)
    }

    static var heldMouse: Set<String> = []

    static func key(_ code: Int, down: Bool) {
        CGEvent(keyboardEventSource: nil, virtualKey: CGKeyCode(code), keyDown: down)?.post(tap: .cghidEventTap)
    }

    static func mouseButton(_ b: String, down: Bool) {
        let loc = CGEvent(source: nil)?.location ?? .zero
        let (type, button): (CGEventType, CGMouseButton) = {
            switch b {
            case "R": return (down ? .rightMouseDown : .rightMouseUp, .right)
            case "M": return (down ? .otherMouseDown : .otherMouseUp, .center)
            default: return (down ? .leftMouseDown : .leftMouseUp, .left)
            }
        }()
        if down { heldMouse.insert(b) } else { heldMouse.remove(b) }
        CGEvent(mouseEventSource: nil, mouseType: type, mouseCursorPosition: loc, mouseButton: button)?.post(tap: .cghidEventTap)
    }

    static func click() { mouseButton("L", down: true); mouseButton("L", down: false) }

    static func move(dx: Int, dy: Int) {
        guard dx != 0 || dy != 0 else { return }
        let cur = CGEvent(source: nil)?.location ?? .zero
        // Imleci ekran sinirlari icinde tut
        let union = NSScreen.screens.reduce(CGRect.null) { $0.union(flipped($1.frame)) }
        var p = CGPoint(x: cur.x + CGFloat(dx), y: cur.y + CGFloat(dy))
        if !union.isNull {
            p.x = min(max(p.x, union.minX), union.maxX - 1)
            p.y = min(max(p.y, union.minY), union.maxY - 1)
        }
        let type: CGEventType = heldMouse.contains("L") ? .leftMouseDragged : heldMouse.contains("R") ? .rightMouseDragged : .mouseMoved
        let button: CGMouseButton = heldMouse.contains("R") && !heldMouse.contains("L") ? .right : .left
        guard let e = CGEvent(mouseEventSource: nil, mouseType: type, mouseCursorPosition: p, mouseButton: button) else { return }
        e.setIntegerValueField(.mouseEventDeltaX, value: Int64(dx))
        e.setIntegerValueField(.mouseEventDeltaY, value: Int64(dy))
        e.post(tap: .cghidEventTap)
    }

    static func scroll(_ lines: Int32) {
        guard lines != 0 else { return }
        CGEvent(scrollWheelEvent2Source: nil, units: .pixel, wheelCount: 1, wheel1: lines, wheel2: 0, wheel3: 0)?.post(tap: .cghidEventTap)
    }

    /// NSScreen (sol alt koken) -> CoreGraphics (sol ust koken)
    private static func flipped(_ r: CGRect) -> CGRect {
        let h = NSScreen.screens.first?.frame.height ?? r.maxY
        return CGRect(x: r.minX, y: h - r.maxY, width: r.width, height: r.height)
    }
}
