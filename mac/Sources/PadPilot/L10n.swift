import Foundation

/// Turkce / Ingilizce metinler. "auto" = macOS dili.
enum L10n {
    static var english: Bool = !(Locale.preferredLanguages.first ?? "en").hasPrefix("tr")

    static func apply(_ setting: String) {
        switch setting {
        case "tr": english = false
        case "en": english = true
        default: english = !(Locale.preferredLanguages.first ?? "en").hasPrefix("tr")
        }
    }

    static func t(_ key: String) -> String {
        guard let v = map[key] else { return key }
        return english ? v.1 : v.0
    }

    static let map: [String: (String, String)] = [
        "Tagline": ("PS4 · PS5 pad merkezi", "PS4 · PS5 controller hub"),
        "DefaultProfile": ("Varsayılan", "Default"),
        "PgOverview": ("Genel bakış", "Overview"), "PgButtons": ("Tuşlar", "Buttons"), "PgSticks": ("Analoglar", "Sticks"),
        "PgTriggers": ("Tetikler", "Triggers"), "PgMotion": ("Hareket (gyro)", "Motion (gyro)"), "PgTouch": ("Dokunmatik", "Touchpad"),
        "PgLights": ("Işık ve titreşim", "Light & rumble"), "PgProfiles": ("Profiller", "Profiles"), "PgSettings": ("Ayarlar", "Settings"),
        "ActiveProfile": ("Etkin profil", "Active profile"),
        "Waiting": ("Pad bekleniyor", "Waiting for a controller"),
        "WaitingText": ("PS4 veya PS5 pad'ini USB kabloyla tak ya da Bluetooth'tan bağla. macOS pad'i oyunlara zaten kendisi tanıtır; PadPilot ışık, tetik, eşleme ve gyro gibi ekstraları ekler.",
                        "Connect a PS4 or PS5 controller via USB or Bluetooth. macOS already exposes it to games; PadPilot adds lightbar, triggers, mapping, gyro and more."),
        "Pairing": ("Bluetooth eşleştirme: PS5'te Create + PS, PS4'te Share + PS tuşlarına ışık yanıp sönene kadar bas, sonra Sistem Ayarları → Bluetooth.",
                    "Bluetooth pairing: hold Create + PS (PS5) or Share + PS (PS4) until the light blinks, then System Settings → Bluetooth."),
        "OpenBluetooth": ("Bluetooth ayarlarını aç", "Open Bluetooth settings"),
        "Battery": ("Pil", "Battery"), "Charging": ("şarj oluyor", "charging"), "Player": ("Oyuncu", "Player"),
        "Pressed": ("Basılı tuşlar", "Pressed buttons"), "NoButtons": ("Basılı tuş yok", "No buttons pressed"),
        "RumbleTest": ("Titreşim testi", "Rumble test"), "Calibrate": ("Gyro kalibre et", "Calibrate gyro"),
        "AccessNeeded": ("Klavye/fare çıkışı için PadPilot'a Erişilebilirlik izni ver (Sistem Ayarları → Gizlilik ve Güvenlik → Erişilebilirlik).",
                         "To send keyboard/mouse input, allow PadPilot in System Settings → Privacy & Security → Accessibility."),
        "GrantAccess": ("İzin ver", "Grant access"),
        "MacNote": ("Not: macOS'ta oyunlar pad'i doğrudan okur. Buradaki eşlemeler ve analog/gyro/touchpad modları, pad'in yanında klavye ve fare olayları üretir.",
                    "Note: on macOS games read the controller directly. Mappings and stick/gyro/touchpad modes here generate keyboard and mouse events alongside it."),
        "Mapping": ("Tuş eşleme", "Button mapping"), "Button": ("Pad tuşu", "Controller button"), "Action": ("Eylem", "Action"),
        "ResetAll": ("Hepsini temizle", "Clear all"),
        "None": ("— Yok —", "— None —"), "MouseLeft": ("Fare sol tık", "Mouse left click"), "MouseRight": ("Fare sağ tık", "Mouse right click"),
        "MouseMiddle": ("Fare orta tık", "Mouse middle click"), "ScrollUp": ("Tekerlek yukarı", "Scroll up"), "ScrollDown": ("Tekerlek aşağı", "Scroll down"),
        "NextProfile": ("Sonraki profil", "Next profile"), "PrevProfile": ("Önceki profil", "Previous profile"), "ToggleGyro": ("Gyro aç/kapat", "Toggle gyro"),
        "LeftStick": ("Sol analog", "Left stick"), "RightStick": ("Sağ analog", "Right stick"), "Mode": ("Mod", "Mode"),
        "Curve": ("Tepki eğrisi", "Response curve"), "Deadzone": ("Ölü bölge", "Deadzone"), "Outer": ("Dış bölge", "Outer zone"),
        "Speed": ("Hız", "Speed"), "InvertY": ("Y eksenini ters çevir", "Invert Y axis"),
        "sm.none": ("Kapalı (yalnızca oyun)", "Off (game only)"), "sm.mouse": ("Fare imleci", "Mouse cursor"), "sm.scroll": ("Kaydırma", "Scrolling"),
        "sm.wasd": ("WASD tuşları", "WASD keys"), "sm.arrows": ("Ok tuşları", "Arrow keys"),
        "sc.linear": ("Doğrusal", "Linear"), "sc.precise": ("Hassas", "Precise"), "sc.smooth": ("Yumuşak", "Smooth"), "sc.aggressive": ("Çevik", "Aggressive"),
        "L2Title": ("L2 (sol tetik)", "L2 (left trigger)"), "R2Title": ("R2 (sağ tetik)", "R2 (right trigger)"),
        "Adaptive": ("Adaptif tetik (DualSense)", "Adaptive trigger (DualSense)"), "Effect": ("Efekt", "Effect"),
        "Start": ("Başlangıç", "Start"), "End": ("Bitiş", "End"), "Strength": ("Güç", "Strength"), "Frequency": ("Frekans", "Frequency"),
        "te.off": ("Kapalı", "Off"), "te.resistance": ("Sabit direnç", "Constant resistance"), "te.slope": ("Artan direnç", "Increasing resistance"),
        "te.weapon": ("Silah (sonda tık)", "Weapon (click)"), "te.vibration": ("Titreşim / otomatik silah", "Vibration / automatic gun"),
        "Presets": ("Hazır ayarlar", "Presets"), "PrPistol": ("Tabanca", "Pistol"), "PrAuto": ("Otomatik tüfek", "Automatic rifle"),
        "PrBrake": ("Fren pedalı", "Brake pedal"), "PrGas": ("Gaz pedalı", "Throttle"), "PrHard": ("Sert direnç", "Hard resistance"),
        "GyroAim": ("Gyro ile nişan", "Gyro aiming"), "gm.off": ("Kapalı", "Off"), "gm.mouse": ("Fare", "Mouse"),
        "When": ("Ne zaman", "When"), "ga.always": ("Her zaman", "Always"), "ga.whileHeld": ("Tuş basılıyken", "While held"),
        "ga.whileNotHeld": ("Tuş basılı değilken", "While not held"), "ga.toggle": ("Tuşla aç/kapat", "Toggle"),
        "ActivationButton": ("Etkinleştirme tuşu", "Activation button"), "SensX": ("Yatay hassasiyet", "Horizontal sensitivity"),
        "SensY": ("Dikey hassasiyet", "Vertical sensitivity"), "Smoothing": ("Titreme yumuşatma", "Jitter smoothing"),
        "InvertH": ("Yatayı ters çevir", "Invert horizontal"), "InvertV": ("Dikeyi ters çevir", "Invert vertical"),
        "tm.off": ("Kapalı", "Off"), "tm.mouse": ("Fare / touchpad", "Mouse / touchpad"),
        "TapClick": ("Dokunarak tıkla", "Tap to click"), "TwoFinger": ("İki parmakla kaydır", "Two-finger scroll"),
        "Lightbar": ("Işık çubuğu", "Lightbar"), "lm.solid": ("Sabit renk", "Solid color"), "lm.battery": ("Pil seviyesine göre", "By battery"),
        "lm.rainbow": ("Gökkuşağı", "Rainbow"), "lm.off": ("Kapalı", "Off"), "Color": ("Renk", "Color"), "Brightness": ("Parlaklık", "Brightness"),
        "LowFlash": ("Pil azalınca kırmızı yanıp sön", "Flash red on low battery"), "PlayerLeds": ("Oyuncu numarası LED'leri", "Player number LEDs"),
        "Rumble": ("Titreşim", "Rumble"),
        "Profiles": ("Profiller", "Profiles"), "New": ("Yeni", "New"), "Duplicate": ("Kopyala", "Duplicate"), "Delete": ("Sil", "Delete"),
        "Name": ("Ad", "Name"), "AutoProfile": ("Uygulamaya göre otomatik profil", "Automatic profile per app"),
        "AppsHint": ("Uygulama adı veya paket kimliği, noktalı virgülle ayır (ör. Minecraft; com.valvesoftware.steam)", "App names or bundle IDs separated by semicolons (e.g. Minecraft; com.valvesoftware.steam)"),
        "FrontApp": ("Öndeki uygulama", "Front app"), "AddFront": ("Ekle", "Add"),
        "Language": ("Dil", "Language"), "LangAuto": ("Otomatik (macOS dili)", "Automatic (macOS language)"),
        "LowBattery": ("Düşük pil uyarısı (%)", "Low battery warning (%)"), "StartAtLogin": ("Oturum açılınca başlat", "Open at login"),
        "Feedback": ("Geri bildirim", "Feedback"), "FeedbackDesc": ("Hata, öneri ya da istek… Mesajın doğrudan geliştiriciye e-posta olarak gider.", "Bug, idea or request… Your message is emailed straight to the developer."),
        "YourEmail": ("E-posta adresin (isteğe bağlı)", "Your email (optional)"), "Send": ("Gönder", "Send"), "Sending": ("Gönderiliyor…", "Sending…"),
        "Sent": ("Teşekkürler! Mesajın gönderildi.", "Thanks! Your message has been sent."), "SendFailed": ("Gönderilemedi: ", "Couldn't send: "),
        "About": ("Hakkında", "About"), "Version": ("Sürüm", "Version"), "Developer": ("Geliştirici", "Developer"),
        "GitHub": ("GitHub sayfası", "GitHub page"), "Releases": ("Yeni sürümler", "Releases"),
        "License": ("Açık kaynak, MIT lisansı. Sony veya Microsoft ile bağlantılı değildir.", "Open source, MIT license. Not affiliated with Sony or Microsoft."),
        "ShowWindow": ("PadPilot'u göster", "Show PadPilot"), "Quit": ("Çıkış", "Quit"),
        "b.cross": ("✕ Çarpı", "✕ Cross"), "b.circle": ("○ Daire", "○ Circle"), "b.square": ("□ Kare", "□ Square"), "b.triangle": ("△ Üçgen", "△ Triangle"),
        "b.l1": ("L1", "L1"), "b.r1": ("R1", "R1"), "b.l2": ("L2", "L2"), "b.r2": ("R2", "R2"), "b.l3": ("L3", "L3"), "b.r3": ("R3", "R3"),
        "b.up": ("Yön ↑", "D-pad ↑"), "b.down": ("Yön ↓", "D-pad ↓"), "b.left": ("Yön ←", "D-pad ←"), "b.right": ("Yön →", "D-pad →"),
        "b.options": ("Options", "Options"), "b.share": ("Share / Create", "Share / Create"), "b.ps": ("PS tuşu", "PS button"), "b.touchpad": ("Touchpad tıklama", "Touchpad click"),
    ]
}
