# Entegrasyonlar

Her dış servis bir sağlayıcı arayüzünün arkasındadır ([ADR-0005](adr/0005-integrations-behind-providers.md)). Tenant'a ait anahtarlar şifreli saklanır; ilk sağlayıcı sandbox ile geliştirilir.

## Sağlayıcı planı

| Entegrasyon | Arayüz | İlk sağlayıcı | Ön koşul / bekleme | Faz |
| --- | --- | --- | --- | --- |
| E-posta gönderim | `IEmailSender` | Resend veya SES (SMTP yedek) | Domain doğrulama, 1 gün | 1 |
| Döviz kuru | `IExchangeRateSource` | TCMB günlük XML | yok | 1 |
| Dosya deposu | S3 API | MinIO (lokal), Cloudflare R2 / S3 (prod) | yok | 1 |
| Ödeme | `IPaymentProvider` | PayTR (ödeme linki + iframe + callback) | Test mağazası hemen; gerçek başvuru 1–2 hafta | 4 |
| e-Fatura / e-Arşiv | `IEInvoiceProvider` | Nilvera (alternatif Uyumsoft, Logo e-Fatura) | Test hesabı; canlıda sözleşme + mali mühür | 4 |
| WhatsApp | `IMessageChannel` | Meta WhatsApp Cloud API | Business doğrulama 1–3 hafta; şablon onayı gün bazında | 3–4 (şablon) / 5 (gelen kutusu) |
| SMS | `IMessageChannel` | Netgsm (alternatif İleti Merkezi) | Hesap, aynı gün | 5 |
| Sosyal yayın | `ISocialPublisher` | Meta Graph API (Instagram/Facebook) | App review (`instagram_content_publish`, `pages_manage_posts`), haftalar | 6 |
| Reklam raporu | `IAdsReportSource` | Meta Marketing API, Google Ads API | Google developer token onayı | 6 |
| Muhasebe aktarımı | `IAccountingExporter` | Excel/XML paket → Logo REST → Bay.t | Müşteri lisansı, test ortamı müşteriden | 4 / 8 |
| Kargo | `IShippingProvider` | Yurtiçi / Aras / MNG | Müşteri sözleşmesi | 8 |
| GPS | `IVehicleTelemetry` | Arvento / Mobiliz | Müşteri sözleşmesi | 7+ |

## Sağlayıcı notları

**PayTR.** İki akış: "Link ile ödeme" (teklif kaporası ve fatura için; WhatsApp/e-posta ile gönderilir) ve iframe (portal). Callback isteği hash ile doğrulanır; tenant, callback URL'deki public link token'ından çözülür; aynı `merchant_oid` için ikinci callback yok sayılır (idempotent). Başarılı callback tahsilat kaydı açar ve faturaya dağıtır.

**WhatsApp Cloud API.** Müşteriye ilk mesaj yalnızca onaylı şablonla gider; müşteri yazdıktan sonra 24 saat serbest mesaj penceresi açılır. Gelen webhook imzası (`X-Hub-Signature-256`) doğrulanır; tenant `phone_number_id` ile çözülür. Konuşma başına ücretlendirme olduğu için kota defteri kullanılır.

**e-Fatura / e-Arşiv.** Gönderimden önce alıcının VKN/TCKN'si entegratörden sorgulanır: e-Fatura mükellefiyse UBL-TR ile posta kutusuna, değilse e-Arşiv olarak (e-posta ile PDF). Fatura numarası (seri + yıl + sıra, boşluksuz) entegratör tarafından atanır; sistemde iç taslak numarası ve GİB numarası/ETTN ayrı tutulur. İptal ve itiraz süreçleri entegratör API'sinden yürür. Fatura modülü entegratörsüz de "taslak" seviyesinde çalışır.

**TCMB.** `today.xml` günlük çekilir, tarih bazlı saklanır; her dövizli belge oluşturulurken o günün kuru belgeye yazılır ve sonradan değişmez.

**Meta yayınlama.** Instagram Business hesabı bir Facebook sayfasına bağlı olmalı; uzun ömürlü token 60 günde yenilenir. App review tamamlanana kadar içerik takvimi "yayınlandı olarak işaretle" ile manuel çalışır.

**Logo / Bay.t.** Her müşterinin lisansı ve versiyonu farklıdır; önce Excel/XML aktarım paketi (muhasebecinin gerçek ihtiyacı), sonra API senkronu. Eşleme tabloları (cari kodu, stok kodu, KDV kodu) tenant ayarında tutulur.

## İdari checklist

Kod değil ama bekleme süresi uzun; Faz 1'de başlatılır.

- [ ] Meta Business doğrulaması + WhatsApp Cloud API numarası + ilk mesaj şablonları
- [ ] Meta Developer app: Instagram/Facebook publishing izinleri için app review hazırlığı (gizlilik politikası, demo video)
- [ ] PayTR test mağazası; sonra gerçek başvuru
- [ ] e-Fatura entegratörü test hesabı (Nilvera / Uyumsoft)
- [ ] Netgsm SMS hesabı
- [ ] Resend veya SES: gönderen domain doğrulaması (SPF/DKIM/DMARC)
- [ ] Google Ads API developer token
- [ ] Cloudflare R2 veya S3 bucket (prod dosya deposu)
- [ ] Logo/Bay.t: ilk gerçek müşteri geldiğinde, müşterinin lisansıyla test ortamı
