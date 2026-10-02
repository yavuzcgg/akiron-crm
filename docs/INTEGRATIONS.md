# Entegrasyonlar

Her dış servis bir sağlayıcı arayüzünün arkasındadır ([ADR-0005](adr/0005-integrations-behind-providers.md)). Tenant'a ait anahtarlar şifreli saklanır; ilk sağlayıcı sandbox ile geliştirilir; her yetenek önce manuel/null adaptörle çalışır.

## Sağlayıcı planı

| Entegrasyon | Arayüz | İlk sağlayıcı | Ön koşul / bekleme | Faz |
| --- | --- | --- | --- | --- |
| E-posta gönderim | `IEmailSender` | Resend veya SES (SMTP yedek) | Domain doğrulama (SPF/DKIM/DMARC), 1 gün | 1 |
| Döviz kuru | `IExchangeRateSource` | TCMB günlük XML | yok | 1 |
| Dosya deposu | S3 API | MinIO (lokal), Cloudflare R2 / S3 (prod) | yok | 1 |
| WhatsApp — şablon | `IMessageChannel` | Meta WhatsApp Cloud API | Business doğrulama 1–3 hafta; şablon onayı gün bazında | 3 |
| Ödeme | `IPaymentProvider` | PayTR **Link API** (+ iframe portal için) | Test mağazası hemen; gerçek başvuru 1–2 hafta | 3 |
| WhatsApp — gelen kutusu | `IMessageChannel` | Meta WhatsApp Cloud API (webhook) | Aynı hesap; webhook imzası | 4 |
| E-posta — gelen | `IInboundEmail` | Resend/Postmark/SES inbound webhook; tenant'a özel adres | MX kaydı | 4 |
| SMS | `IMessageChannel` | Netgsm (alternatif İleti Merkezi) | Hesap, aynı gün | 4 |
| e-Fatura / e-Arşiv | `IEInvoiceProvider` | Nilvera (alternatif Uyumsoft, Logo e-Fatura) | Test hesabı; canlıda sözleşme + mali mühür | 5 |
| Muhasebeci aktarımı | `IAccountingExporter` | Excel/XML paket | yok | 5 |
| Sosyal yayın | `ISocialPublisher` | Meta Graph API (Instagram/Facebook) | App review (`instagram_content_publish`, `pages_manage_posts`), haftalar | 6 |
| Reklam raporu | `IAdsReportSource` | Meta Marketing API, Google Ads API | Google developer token onayı | 6 |
| GPS | `IVehicleTelemetry` | Arvento / Mobiliz | Müşteri sözleşmesi | 7+ |
| ERP | `IErpProvider` | Logo (Cloud/REST) → Bay.t → Paraşüt → Mikro | Logo iş ortaklığı süreci (ticari); Bay.t API/partner modeli doğrulanacak; müşteri lisansı | 8 |
| E-posta senkron | `IMailboxSync` | Microsoft Graph (Outlook) → Gmail API | Gmail restricted scope → yıllık CASA güvenlik denetimi (ücretli); Outlook publisher doğrulaması | 8 |
| AI | `ILlmProvider` | akiron-seo BYOK altyapısı (Anthropic/OpenAI, tenant anahtarı) | KVKK: rıza + anonimleştirme | 8 (ucuz kazanımlar 5+) |
| Kargo | `IShippingProvider` | Yurtiçi / Aras / MNG | Müşteri sözleşmesi | 8 |

## Sağlayıcı notları

**PayTR.** İlk entegrasyon **Link API**: personel "ödeme talebi oluştur" der, sistem link üretir, WhatsApp/e-posta ile gider. Portalda iframe. Kart verisi hiçbir zaman bize uğramaz. Kesin ödeme durumu yalnızca **callback**'ten alınır (hash doğrulanır); tenant, callback URL'deki public link token'ından çözülür; aynı `merchant_oid` için ikinci callback yok sayılır. Başarılı callback: tahsilat kaydı → cari hareket → timeline → teklif "kazanıldı" → iş emri açılır → sorumluya görev (otomasyon kuralı).

**WhatsApp Cloud API.** Yalnızca resmî Cloud API (on-premises API kapatıldı; WhatsApp Web tabanlı çözümler yasak). Müşteriye ilk mesaj yalnızca onaylı şablonla gider; müşteri yazınca 24 saat serbest pencere açılır. Webhook imzası (`X-Hub-Signature-256`) doğrulanır; tenant `phone_number_id` ile çözülür. Konuşma başına ücret → kota defteri.

**E-posta gelen (V1 yolu).** Her tenant'a `<slug>@in.<domain>` adresi; kullanıcı müşteriye yazarken BCC'ler veya gelen e-postayı forward eder; sistem gönderen/alıcı adresinden cariyi bulur, timeline'a ve ortak gelen kutusuna düşürür (HubSpot/Pipedrive BCC modeli). Gmail tam senkronu Google'ın restricted scope politikası yüzünden (CASA denetimi) Faz 8.

**e-Fatura / e-Arşiv.** Gönderimden önce alıcının VKN/TCKN'si entegratörden sorgulanır: e-Fatura mükellefiyse UBL-TR ile posta kutusuna, değilse e-Arşiv olarak (e-posta ile PDF). Fatura numarasını (seri + yıl + sıra, boşluksuz) entegratör atar; iç taslak numarası ve GİB numarası/ETTN ayrı tutulur. V1'de fatura modülü yok: "fatura kesilecekler" listesi muhasebeciye Excel ile gider.

**TCMB.** `today.xml` günlük çekilir, tarih bazlı saklanır; dövizli belge oluşturulurken o günün kuru belgeye yazılır ve değişmez.

**Meta yayınlama.** Instagram Business hesabı bir Facebook sayfasına bağlı olmalı; uzun ömürlü token 60 günde yenilenir. App review tamamlanana kadar içerik takvimi "yayınlandı olarak işaretle" ile manuel.

**ERP (Logo / Bay.t / Paraşüt / Mikro).** Kendi ERP'mizi yazmıyoruz: sistem "bu cariye 20.000 TL satış yaptık, şu tahsil edildi" der, ERP muhasebe kaydını tutar. Logo tarafında teknik adaptörün yanında **iş ortaklığı/ticari süreç** var; Bay.t için güncel API/partner modeli geliştirmeye başlamadan Bay.t ile doğrulanacak. Eşleme tabloları (cari kodu, stok kodu, KDV kodu) tenant ayarında. Önce Excel/XML paket (muhasebecinin gerçek ihtiyacı), sonra API.

## İdari checklist

Kod değil ama bekleme süresi uzun; Faz 1'de başlatılır.

- [ ] Meta Business doğrulaması + WhatsApp Cloud API (beklemede: şirket hesabı yok; her ajans kendi numarasını bağlayacak, geliştirmede Meta test numarası + sahte sağlayıcı)
- [ ] Ödeme: her ajans kendi PayTR/iyzico mağaza bilgisini girer; geliştirmede sahte sağlayıcı + iyzico sandbox (şirket gerekmez). Akiron'un kendi mağazası Faz 8 (SaaS faturalama)
- [x] SMTP gönderim altyapısı (MailKit); yerelde Mailpit. Prod için SMTP relay seçimi bekliyor (Faz 1 sonu)
- [ ] Resend veya SES: gönderen domain doğrulaması + gelen e-posta için MX (Faz 1 / Faz 4)
- [ ] Netgsm SMS hesabı (Faz 4)
- [ ] Meta Developer app: Instagram/Facebook publishing izinleri için app review hazırlığı — gizlilik politikası, demo video (Faz 6)
- [ ] Google Ads API developer token (Faz 6)
- [ ] e-Fatura entegratörü test hesabı: Nilvera / Uyumsoft (Faz 5)
- [x] Dosya deposu kodu S3 API ile hazır; yerelde MinIO (Chainguard imajı)
- [ ] Cloudflare R2 veya S3 bucket (prod dosya deposu)
- [x] TCMB kur senkronu canlı (`today` yerine tarihli arşiv URL'leri, saatlik)
- [ ] Logo iş ortaklığı başvurusu; Bay.t ile API/partner görüşmesi (Faz 8 öncesi)
- [ ] Microsoft Entra app kaydı + publisher doğrulaması (Outlook senkronu, Faz 8)
