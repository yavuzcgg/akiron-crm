# Modül Kataloğu

> Faz numarası yapım sırasıdır ([yol haritası](PLAN.md#yol-haritası)): Faz 1–4 = **V1 Ajans**, 5–6 = V2 Finans, 7–8 = V3 KOBİ. "Bağımlı" = önce bitmesi gereken modül.
> Kod yerleşimi: her modül `backend/src/Modules/<Modül>/` altında, kendi Postgres şemasıyla ([ADR-0001](adr/0001-modular-monolith.md)). Satış birimi modüldür; paketler için bkz. [Sektör paketleri](PLAN.md#sektör-paketleri).

## A. Platform (Core)

| Modül | Kapsam | Bağımlı | Faz |
| --- | --- | --- | --- |
| Kiracı & Entitlement | Tenant, şube (opsiyonel), **modül bayrakları** (`Entitlement`: modül × koltuk × bitiş), `RequireModule()`, ayarlar; on-prem imzalı lisans dosyası ilk on-prem müşteride | – | 1 |
| Kimlik & Yetki | Kayıt/giriş, JWT + refresh (httpOnly cookie), 2FA (sonra), **izin tabanlı** roller (Owner/Admin/Personel/özel), müşteri-portal kullanıcısı ayrı tip | Kiracı | 1 |
| **Activity timeline** | Ürünün merkezi ekranı ([ADR-0009](adr/0009-activity-timeline.md)): tipli kayıtlar (tip anahtarı, aktör, payload, görünürlük iç/portal), bir kayıt birden çok akışta (cari + fatura + iş); modüller olay yayınlar, timeline projekte eder; notlar/aramalar doğrudan yazılır | Kimlik, Outbox | 1 |
| Denetim izi | Kim, neyi, ne zaman (alan bazlı eski/yeni); finans kayıtlarında zorunlu | Kimlik | 1 |
| Dosya deposu | S3 uyumlu (MinIO lokal, R2/S3 prod), her kayda dosya ekleme, sürüm | Kiracı | 1 |
| Bildirim merkezi | Uygulama içi (SignalR) + e-posta; kanal soyutlaması (WhatsApp/SMS sonra takılır); kullanıcı tercihleri | Kimlik | 1 |
| Arka plan işleri | Hangfire (Postgres): hatırlatma, tekrarlayan işler, kuyruk, kur çekme, outbox dağıtımı | Kiracı | 1 |
| Belge numaralama | Tenant/yıl/seri bazlı: `TKL-2026-0001`, `ISE-2026-0001` (fatura numarası entegratörden gelir) | Kiracı | 1 |
| Para & Kur | Para birimi, TCMB günlük kur, belge üstünde kur sabitleme | Arka plan | 1 |
| Onay motoru | Genel `ApprovalRequest`: adımlar (personel → takım lideri → finans), onaylayan rolü, durum, hatırlatma. İskelet Faz 1; izin (2), teklif iç onayı (3), içerik onayı (4), gider (5) | Bildirim | 1 |
| Özel alanlar | Entity başına tipli alan tanımı (metin, sayı, tarih, seçim), `jsonb` değer, form/liste/filtre desteği | Kiracı | 2 |
| Public link | Anonim erişim token'ı (HMAC, süreli): teklif onayı, ödeme sayfası, mutabakat onayı; webhook'larda tenant çözümü | Kiracı | 3 |
| Otomasyon kuralları | "Olay → aksiyon" (durum değişince WhatsApp şablonu gönder, görev aç, bildir) | Bildirim, Mesajlaşma | 4 |
| Public API & Webhook | Dış sistemler için; Zapier/Make | Kimlik | 8 |

## B. Müşteri & Satış (CRM, Sales)

| Modül | Kapsam | Bağımlı | Faz |
| --- | --- | --- | --- |
| **Cari (Müşteri/Tedarikçi)** | Firma/şahıs, VKN/TCKN + vergi dairesi, adresler, kişiler, etiketler, notlar, özel alanlar; kartta **özet** (açık işler, bekleyen teklif, bakiye, son görüşme, açık talepler, sorumlu) + activity timeline | Platform | 2 |
| Lead & Satış hunisi | Kaynak (web formu, WhatsApp, Instagram DM, referans), Kanban pipeline, aktivite (arama/toplantı/görev), kayıp nedeni, Lead → Cari | Cari | 3 |
| Ürün/Hizmet kataloğu | Hizmet paketleri, fiyat listeleri (KDV dahil/hariç), birim, KDV oranı (1/10/20), **GİB tevkifat kodları**, stopaj | Para | 3 |
| Teklif | Satır bazlı oluşturucu, şablonlar, indirim/vergi, PDF ve proforma, e-posta/WhatsApp ile gönderim, **public onay linki** (kabul/ret/not), görüntülenme takibi, revizyon geçmişi, geçerlilik, kapora, iç onay (opsiyonel), Teklif → Sözleşme / İş Emri | Cari, Katalog | 3 |
| **Sözleşme & Abonelik** | Retainer/abonelik: başlangıç, bitiş, aylık ücret, kapsanan hizmetler, yenileme hatırlatması; şablon + PDF + link ile onay; Faz 5'te otomatik fatura üretir | Teklif | 3 |

## C. Operasyon (İş)

| Modül | Kapsam | Bağımlı | Faz |
| --- | --- | --- | --- |
| **İş Emri** | Müşteriye bağlı iş; tenant'a göre **özelleştirilebilir durum akışı**; atanan kişiler, öncelik, termin, alt görev/checklist, yorum + @bahsetme, dosya + **revizyon numaralı teslimat**; Kanban / liste / takvim / zaman çizelgesi; **iş şablonları**; sözleşmeden tekrarlayan iş | Cari, Dosya | 2 |
| Zaman takibi | Sayaç + haftalık timesheet, iş/görev bazlı, faturalanabilir/değil, personel saatlik maliyeti → **iş/müşteri karlılığı** | İş Emri, Personel | 2 |
| Personel | Profil, departman/ekip, unvan, çalışma saatleri, saatlik maliyet, zimmet, izin talebi (onay motoru), iş yükü/utilizasyon. Bordro yok | Kimlik | 2 |
| Takvim & Kaynak | Ortak takvim (toplantı, çekim, termin), stüdyo/ekipman **rezervasyonu**, Google/Outlook takvim senkronu (sonra) | İş Emri | 3 |
| Destek talebi | Portal/WhatsApp/e-postadan talep → iş emrine dönüştür, basit SLA, sorun geçmişi | Portal, Mesajlaşma | 4 |

## D. Finans

| Modül | Kapsam | Bağımlı | Faz |
| --- | --- | --- | --- |
| **Finance Lite** | Cari hareket defteri (borç/alacak satırları), açık kalemler ve **eşleştirme** (bir tahsilat birden çok kaleme, bir kalem birden çok tahsilata; eşleşmeyen tahsilat = avans, ADR-0004); tahsilat kaydı (nakit, havale/EFT, kart, çek/senet notu), ödeme planı (taksit/vade), cari bakiye ve basit ekstre, vade hatırlatması; **fatura kesilecekler listesi** (muhasebeciye) | Cari, Sözleşme | 3 |
| **Sanal POS (PayTR)** | Link API ile ödeme linki (teklif kaporası, ödeme planı taksiti), portaldan kartla ödeme, taksit, callback → tahsilat otomatik + timeline; ikinci sağlayıcı iyzico | Finance Lite | 3 |
| **Fatura** | Satış faturası (iş/sözleşme/tekliften; Faz 7'de siparişten); türler: satış / iade / tevkifatlı / istisna; **gelen belgeler:** alış faturası, **e-SMM**, **gider pusulası**; vergi motoru (KDV, tevkifat kodları, stopaj, istisna), döviz + kur, PDF; **e-Fatura/e-Arşiv entegratörü:** alıcı mükellef sorgusu, gönderim, durum, iptal/itiraz; **GİB numarası entegratörden** | Finance Lite, Katalog | 5 |
| Tahsilat eşleme | Tahsilatın faturalara dağıtımı (kısmi, çoklu), açık kalem takibi | Fatura | 5 |
| Kasa & Banka | Kasalar, banka hesapları, virman, günlük kasa raporu (Faz 3'te tahsilatta yalnız "hesap" alanı) | Finance Lite | 5 |
| Gider | Gider fişleri, kategoriler, fiş fotoğrafı, tekrarlayan giderler, personel masraf talebi (onay motoru), işe/müşteriye maliyet olarak bağlama | Cari, Onay | 5 |
| Tekrarlayan fatura | Sözleşmeden aylık otomatik fatura + hatırlatma + ödeme linki | Fatura, Sözleşme | 5 |
| Cari ekstre & Yaşlandırma | Borç/alacak dökümü, vade yaşlandırma (0-30-60-90) | Tahsilat eşleme | 5 |
| Açılış & Devir | Onboarding: cari/kasa/banka açılış bakiyeleri, Excel ile toplu içe aktarma; **dönem kilidi** | Cari, Kasa | 5 |
| Mutabakat | Cari mutabakat mektubu PDF + e-posta; public link ile onay/itiraz | Ekstre | 5 |
| Raporlar (Finans) | Nakit akışı, tahsilat/ödeme takvimi, KDV özeti, iş/müşteri karlılığı | Tümü | 5 |
| Çek & Senet | Portföy: alınan/verilen; portföyde → ciro → tahsile/teminata verildi → tahsil edildi / karşılıksız / iade; vade takvimi | Kasa & Banka | 7 |
| Sipariş | Satış siparişi (tekliften); ürün satan firma için | Teklif | 7 |
| İrsaliye | Sevk irsaliyesi PDF; e-İrsaliye entegratör üzerinden (Faz 8) | Sipariş | 7 |

## E. İletişim & Müşteri Portalı

| Modül | Kapsam | Bağımlı | Faz |
| --- | --- | --- | --- |
| E-posta gönderim | İşlemsel e-posta (Resend/SES/SMTP), tenant'a özel gönderen, şablonlar | Bildirim | 1 |
| WhatsApp — bildirim | Cloud API **şablon mesajları**: teklif linki, ödeme linki, iş durumu, hatırlatma | Bildirim | 3 |
| **Müşteri portalı** | Müşteri girişi: iş durumu, teslimat/revizyon geri bildirimi (görsel üstüne not), teklif/sözleşme onayı, içerik onayı, ödeme planı & **kartla ödeme**, dosya yükleme, talep açma | Teklif, İş Emri, PayTR | 4 |
| WhatsApp — gelen kutusu | Webhook ile gelen mesaj, **ortak gelen kutusu**, personele atama, cari/işe bağlama, 24 saat penceresi, hızlı yanıtlar; müşteri kartında konuşma geçmişi | Cari | 4 |
| E-posta — gelen (BCC/forward) | Tenant'a özel gelen adres (`abc@in.…`), BCC/forward edilen e-posta cari ile eşleşir ve timeline'a düşer; ortak gelen kutusu | Cari | 4 |
| SMS | Netgsm/İleti Merkezi: OTP, hatırlatma; WhatsApp yedeği | Bildirim | 4 |
| E-posta senkron | Gmail/Outlook gelen kutusu tam senkron (Gmail restricted scope → CASA denetimi) | E-posta gelen | 8 |

## F. Ajans modülleri (SMM)

| Modül | Kapsam | Bağımlı | Faz |
| --- | --- | --- | --- |
| **İçerik takvimi & onay** | Müşteri bazlı gönderi planı (platform, tarih, görsel, metin, hashtag seti); akış: taslak → iç onay → müşteriye gönder → revizyon istendi → revize → onaylandı → planlandı → yayınlandı; **portaldan onayla / revizyon iste / yorum**; yayın Faz 4'te manuel ("yayınlandı olarak işaretle") | İş Emri, Portal, Onay | 4 |
| Proofing (temel) | Teslimat sürümleri, görsel üstüne işaretleme, onay — portalda | Dosya, Portal | 4 |
| Sosyal yayınlama | Meta Graph API (Instagram/Facebook) zamanlanmış yayın; sonra LinkedIn, X, TikTok; hesap bağlama (OAuth), token yenileme | İçerik takvimi | 6 |
| Sosyal & reklam raporları | Erişim/etkileşim/takipçi metrikleri; Meta Ads + Google Ads harcama/sonuç; **aylık müşteri raporu** (PDF + portal); SEO verisi akiron-seo'dan | Yayınlama | 6 |
| Kreatif kütüphane | Marka varlıkları (logo, font, renk), gelişmiş proofing, sürüm karşılaştırma | Proofing | 6 |

## G. Stok, Satın alma, Varlıklar

| Modül | Kapsam | Bağımlı | Faz |
| --- | --- | --- | --- |
| Stok | Ürün/varyant/birim, depolar, hareketler (giriş/çıkış/transfer/sayım), barkod/QR, min stok uyarısı, ağırlıklı ortalama maliyet, rezervasyon, sipariş/irsaliye/fatura ile bağ | Katalog | 7 |
| Satın alma & Tedarikçi | Satın alma talebi (onay motoru), satın alma siparişi, mal kabul → stok, alış faturası eşleme, tedarikçi/freelancer performansı | Stok, Fatura | 7 |
| Demirbaş & Araç | Varlık kaydı, **zimmet**, bakım planı; araç: km/yakıt, **muayene/sigorta/kasko hatırlatması**, sürücü ataması, görev/yol kaydı; GPS sağlayıcı entegrasyonu opsiyonel | Personel | 7 |
| Kargo | Yurtiçi/Aras/MNG etiket + takip | Sipariş | 8 |

## H. Entegrasyon, AI & Ölçek

| Modül | Kapsam | Bağımlı | Faz |
| --- | --- | --- | --- |
| Muhasebeci aktarımı | Aylık Excel/XML paket (cari, fatura, tahsilat, gider) | Finans | 5 |
| ERP adaptörleri | `IErpProvider`: Logo (Cloud/REST), Bay.t, Paraşüt, Mikro; cari, fatura, stok, tahsilat senkronu; eşleme tabloları tenant ayarında | Finans, Stok | 8 |
| Banka eşleştirme | Ekstre içe aktarma (Excel/MT940), otomatik tahsilat önerisi; açık bankacılık sonra | Kasa & Banka | 8 |
| Kur farkı & Vade farkı | Dövizli faturada kur farkı faturası, geç ödemede vade farkı | Fatura | 8 |
| **AI asistan** | WhatsApp/e-posta özeti ve niyet tespiti (→ talep/görev), brief'ten teklif taslağı, mesaj taslağı, toplantı notundan görev çıkarma, müşteri risk sinyali; BYOK altyapısı akiron-seo'dan; KVKK: rıza + anonimleştirme | Timeline, Mesajlaşma | 8 (ucuz kazanımlar Faz 5+) |
| Mobil | Önce PWA (saha/personel: zaman, masraf, araç km); native sonra | – | 8 |
| KVKK & Güvenlik | Veri dışa aktarma/silme, rıza kayıtları, 2FA, SSO, IP kısıtı, yedek indirme | Kimlik | 8 (temelleri 1) |
| SaaS faturalama | Planlar/paketler, modül aboneliği, kendi aboneliğimizi PayTR/iyzico ile tahsil | Entitlement, PayTR | 8 |

## Bilinçli olarak kapsam dışı

Genel muhasebe (yevmiye/e-Defter), bordro (Logo Bordro'ya bırakılır), üretim/MRP, amortisman, GPS donanımı, native mobil (PWA yeter), pazaryeri entegrasyonları (akiron-commerce'in işi; sipariş akışı webhook ile bağlanır), WhatsApp Web tabanlı gayriresmî entegrasyonlar.
