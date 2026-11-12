# PadPilot — Devir Notu

Son güncelleme: 12 Kasım 2026 (Aşama 2)

## Amaç
PS4 (DualShock 4) ve PS5 (DualSense) pad'lerini PC'de tek uygulamadan, sıfır ayarla, mümkün olan tüm
özellikleriyle kullanmak. DS4Windows + DSX/DualSenseY + Steam Input'un en iyi özelliklerini tek, sade bir
arayüzde toplamak. Yalnızca kişisel kullanım; dağıtım yok.

## Ortam (12 Kasım 2026'da doğrulandı)
- Windows 11 Pro 10.0.26200, .NET SDK 10.0.401, Visual Studio Community 2026, git 2.56
- ViGEmBus ve HidHide 1.2.98 kurulu ve çalışıyor
- Kurulu ama eski: DS4Windows 3.3.3, DualSenseX 1.4.9 (test sırasında kapalı olmalı)

## Onaylanan kararlar
- Kişisel kullanım, hedef "mümkün olanın en iyisi" (Ali, 12 Kasım 2026)
- C# / .NET 10, WPF (Fluent koyu tema), doğrudan Windows HID API'si
- Çekirdek sürücü yazılmayacak; ViGEmBus + HidHide kullanılacak
- Arayüz: kaliteli, sade ama her şey içinde; rakiplerin en iyileri + fazlası

## Yapı
- `src/PadPilot.Core`
  - `Protocol.cs` rapor ayrıştırma/oluşturma (adaptif tetik dahil), `TriggerEffects.cs` Sony tetik efektleri
  - `PadDevice.cs` HID okuma/yazma, MAC okuma, Bluetooth kapatma; `PadManager.cs` otomatik bulma
  - `Settings.cs` profil/ayar modeli + JSON (%AppData%\PadPilot\settings.json)
  - `Targets.cs` tuş eşleme hedefleri; `PadRuntime.cs` işleme hattı (ölü bölge, eğri, eşleme, uzun basış,
    turbo, gyro, touchpad, flick stick); `VirtualPads.cs` ViGEm Xbox 360 / DS4 çıkışı
  - `Engine.cs` hepsini bağlar: profiller, otomatik profil, ışık, pil uyarısı, boşta kapatma, HidHide
  - `Native.cs` SendInput, Bluetooth kapatma, ön plan exe, Windows başlangıcı, HidHideCLI
- `src/PadPilot.App` WPF: `MainWindow` (kenar menü + tepsi), `Pages/*` (9 sayfa), `Controls.cs`
  (PadVisual canlı pad çizimi, StickPreview, TriggerPreview, SettingRow, SliderRow)
- `tests/PadPilot.Tests` — 17 birim testi
- `.dev/shot.ps1` — tüm sayfaların ekran görüntüsünü alır (git dışı)

Derleme: `dotnet build src\PadPilot.App\PadPilot.App.csproj -c Release`
Test: `dotnet test tests\PadPilot.Tests\PadPilot.Tests.csproj`
Çalıştırma: `src\PadPilot.App\bin\Release\net10.0-windows\PadPilot.exe`

## Özellikler (Aşama 2 sonunda)
- Sayfalar: Genel bakış, Tuşlar, Analoglar, Tetikler, Hareket (gyro), Dokunmatik, Işık ve titreşim, Profiller, Ayarlar
- Tuş eşleme: pad tuşu / klavye / fare / eylem; uzun basış ikinci eylem; turbo; canlı basış noktası
- Analog: ölü bölge, dış bölge, anti ölü bölge, 4 eğri, hassasiyet, ters çevirme; mod: analog, fare,
  flick stick, WASD, oklar; canlı önizleme (ham + işlenmiş)
- Tetik: ölü bölge, saç tetik; DualSense adaptif tetik (8 efekt + 9 hazır ayar), canlı bölge önizleme
- Gyro: fare veya sağ analog; her zaman / basılıyken / basılı değilken / aç-kapa; otomatik + elle kalibrasyon;
  titreme yumuşatma
- Touchpad: fare, dokun-tıkla, iki parmak kaydırma, sağ yarı sağ tık
- Işık: sabit / pile göre / gökkuşağı / kapalı, parlaklık, düşük pilde kırmızı yanıp sönme, oyuncu LED'leri
- Titreşim gücü %0–200, motor testleri
- Profiller: yeni, kopyala, yeniden adlandır, sıfırla, sil; oyuna göre otomatik profil (son açılan programlardan tek tıkla)
- Ayarlar: Xbox 360 / DS4 / yok çıkışı, HidHide ile çift giriş önleme (UAC), Windows'la başlat, tepside çalış,
  düşük pil uyarısı, boşta Bluetooth kapatma, PS+Options ile kapatma
- Sistem tepsisi simgesi ve bildirimler; tek kopya çalışma

## Durum
Aşama 2 kodu yazıldı. Derleme temiz, 17/17 test geçiyor, tüm sayfaların ekran görüntüsü kontrol edildi.

**Gerçek pad ile HİÇ test edilmedi.** Doğrulanması gerekenler:
1. DS4 ve DualSense USB/Bluetooth tanınıyor mu, pil ve Hz doğru mu
2. Tuşlar ve analoglar Genel bakış çiziminde doğru yanıyor mu
3. Oyunda Xbox pad'i görünüyor mu, titreşim geliyor mu; DS4 çıkışı çalışıyor mu
4. Adaptif tetik hazır ayarları hissediliyor mu (DualSense)
5. Gyro yönleri doğru mu (ters ise ters çevir anahtarları; varsayılan işaretler tahmini)
6. Touchpad fare, PS+Options kapatma, HidHide gizleme

## Bilinen eksikler / sonraki adımlar
- DSU/cemuhook sunucusu (Cemu, Yuzu/Ryujinx, Dolphin'de gyro) — rakiplerde nadir, büyük artı olur
- Bluetooth üzerinden ses-haptik (DSX'in ücretli özelliği), DualSense hoparlör/mikrofon
- Makro kaydı, mod değiştirme (tuşa basılıyken başka eşleme katmanı)
- Mikrofon tuşu = Windows mikrofonunu sustur (Core Audio)
- DualSense yeni bellenim titreşim bayrağı (valid_flag2 bit 2) kontrolü
