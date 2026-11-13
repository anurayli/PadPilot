import Foundation

/// Geri bildirimi FormSubmit uzerinden gelistiricinin e-postasina gonderir.
enum Feedback {
    static let endpoint = URL(string: "https://formsubmit.co/ajax/alinuraylii@gmail.com")!

    static func send(message: String, email: String, version: String) async -> (Bool, String?) {
        var body: [String: String] = [
            "_subject": "PadPilot geri bildirim (macOS \(version))",
            "_template": "table",
            "_captcha": "false",
            "Mesaj": message,
            "Platform": "macOS",
            "Surum": version,
            "Dil": L10n.english ? "en" : "tr",
            "Sistem": ProcessInfo.processInfo.operatingSystemVersionString,
        ]
        let mail = email.trimmingCharacters(in: .whitespaces)
        if !mail.isEmpty { body["email"] = mail; body["_replyto"] = mail }

        var req = URLRequest(url: endpoint)
        req.httpMethod = "POST"
        req.setValue("application/json", forHTTPHeaderField: "Content-Type")
        req.setValue("application/json", forHTTPHeaderField: "Accept")
        req.setValue("https://github.com/anurayli/PadPilot", forHTTPHeaderField: "Referer")
        req.timeoutInterval = 20
        do {
            req.httpBody = try JSONSerialization.data(withJSONObject: body)
            let (data, _) = try await URLSession.shared.data(for: req)
            let json = (try? JSONSerialization.jsonObject(with: data)) as? [String: Any]
            let success = json?["success"]
            let ok = (success as? Bool) == true || (success as? String) == "true"
            return ok ? (true, nil) : (false, json?["message"] as? String ?? String(data: data, encoding: .utf8))
        } catch {
            return (false, error.localizedDescription)
        }
    }
}
