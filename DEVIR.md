# PadPilot — Devir Notu

Son güncelleme: 12 Kasım 2026

## Amaç
PS4 (DualShock 4) ve PS5 (DualSense) pad'lerini PC'de tek uygulamadan, sıfır ayarla, mümkün olan tüm
özellikleriyle kullanmak. Yalnızca kişisel kullanım; dağıtım yok.

Araştırma raporu: https://claude.ai/artifact/RZDm93RS23GBNv1M9r5AWS

## Ortam (12 Kasım 2026'da doğrulandı)
- Windows 11 Pro 10.0.26200, .NET SDK 10.0.401, Visual Studio Community 2026, git 2.56
- ViGEmBus ve HidHide 1.2.98 kurulu ve çalışıyor
- Bluetooth adaptörü: "Generic Bluetooth Radio", USB VID_0A12 PID_0001
- Kurulu ama eski: DS4Windows 3.3.3, DualSenseX 1.4.9 (test sırasında kapalı olmalı)

## Onaylanan kararlar
- Kişisel kullanım, hedef "mümkün olanın en iyisi" (Ali, 12 Kasım 2026)
- C# / .NET 10, WPF, doğrudan Windows HID API'si
- Çekirdek sürücü yazılmayacak; ViGEmBus + HidHide kullanılacak

## Yapı
- `src/PadPilot.Core` — protokol (Protocol.cs), HID erişimi (Hid/HidNative.cs), pad (PadDevice.cs),
  otomatik bulma (PadManager.cs), Xbox 360 emülasyonu (XboxBridge.cs)
- `src/PadPilot.App` — WPF arayüz
- `tests/PadPilot.Tests` — protokol birim testleri

Derleme: `dotnet build src\PadPilot.App\PadPilot.App.csproj -c Release`
Test: `dotnet test tests\PadPilot.Tests\PadPilot.Tests.csproj`
Çalıştırma: `src\PadPilot.App\bin\Release\net10.0-windows\PadPilot.exe`

## Durum
Aşama 1 kodu yazıldı. Derleme temiz, 11/11 birim testi geçiyor, uygulama pad'siz açılıp kapanıyor.

**Gerçek pad ile HİÇ test edilmedi.** Doğrulanması gerekenler:
1. DS4 ve DualSense Bluetooth'tan tanınıyor mu, "basit mod" uyarısı çıkıyor mu
2. Tuşlar, analoglar, tetikler, pil yüzdesi doğru mu
3. Çıkış satırında hangi yazma yöntemi çalışıyor (veya hata kodu)
4. Işık rengi ve titreşim testi çalışıyor mu
5. Bir oyunda Xbox pad'i olarak görünüyor mu, oyun titreşimi geliyor mu

## Bilinen eksikler / riskler
- HidHide entegrasyonu yok: oyun hem gerçek pad'i hem sanal Xbox pad'ini görebilir (çift giriş)
- DualSense titreşimi eski bayrakla gönderiliyor (valid_flag0); yeni bellenimlerde güç ölçeği farklı olabilir
- Adaptif tetikler, jiroskop kullanımı, profiller, tepsi simgesi yok (Aşama 2)
- Sanal DualSense ve Bluetooth haptik yok (Aşama 3)

## Sıradaki adımlar
1. Ali'nin pad testi sonuçlarına göre düzeltmeler
2. HidHide entegrasyonu (çift girişi önlemek)
3. Aşama 2: adaptif tetikler, ışık/LED ayarları, profiller
