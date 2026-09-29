# Modül Kataloğu

> Faz numarası yapım sırasıdır ([yol haritası](PLAN.md#yol-haritası)). "Bağımlı" = önce bitmesi gereken modül.
> Kod yerleşimi: her modül `backend/src/Modules/<Modül>/` altında, kendi Postgres şemasıyla ([ADR-0001](adr/0001-modular-monolith.md)).

## A. Platform

| Modül | Kapsam | Bağımlı | Faz |
| --- | --- | --- | --- |
| Kiracı & Lisans | Tenant, şube (opsiyonel), plan/modül bayrakları, ayarlar | – | 1 |
| Kimlik & Yetki | Kayıt/giriş, JWT + refresh (httpOnly cookie), 2FA (sonra), **izin tabanlı** roller (Owner/Admin/Personel/özel), müşteri-portal kullanıcısı ayrı tip | Kiracı | 1 |
| Denetim izi | Kim, neyi, ne zaman; finans kayıtlarında zorunlu | Kimlik | 1 |
| Dosya deposu | S3 uyumlu (MinIO lokal, R2/S3 prod), her kayda dosya ekleme, sürüm | Kiracı | 1 |
| Bildirim merkezi | Uygulama içi (SignalR) + e-posta; kanal soyutlaması (WhatsApp/SMS sonra takılır); kullanıcı tercihleri | Kimlik | 1 |
| Arka plan işleri | Hangfire (Postgres): hatırlatma, tekrarlayan fatura, kuyruk, kur çekme, outbox dağıtımı | Kiracı | 1 |
| Belge numaralama | Tenant/yıl/seri bazlı: `TKL-2026-0001`, `ISE-2026-0001` (fatura numarası entegratörden gelir) | Kiracı | 1 |
| Para & Kur | Para birimi, TCMB günlük kur, belge üstünde kur sabitleme | Arka plan | 1 |
| Public link | Anonim erişim token'ı (HMAC, süreli): teklif onayı, ödeme sayfası, mutabakat onayı; webhook'larda tenant çözümü | Kiracı | 3 |
| Onay motoru | Genel: gider, izin, içerik onayı aynı motoru kullanır (izin Faz 2'de basit onay/ret ile başlar) | Bildirim | 4 |
| Otomasyon kuralları | "Olay → aksiyon" (durum değişince WhatsApp şablonu gönder vb.) | Bildirim, Mesajlaşma | 5 |
| Public API & Webhook | Dış sistemler için; Zapier/Make | Kimlik | 8 |

## B. Müşteri & Satış

| Modül | Kapsam | Bağımlı | Faz |
| --- | --- | --- | --- |
| **Cari (Müşteri/Tedarikçi)** | Firma/şahıs, VKN/TCKN + vergi dairesi, adresler, kişiler, etiketler, notlar, **zaman çizelgesi** (her temas/belge tek akışta), bakiye özeti | Platform | 2 |
| Lead & Satış hunisi | Kaynak (web formu, WhatsApp, Instagram DM, referans), Kanban pipeline, aktivite (arama/toplantı/görev), kayıp nedeni, Lead → Cari | Cari | 3 |
| Ürün/Hizmet kataloğu | Hizmet paketleri, fiyat listeleri (KDV dahil/hariç), birim, KDV oranı (1/10/20), **GİB tevkifat kodları** (ör. reklam hizmetlerinde kısmi tevkifat), stopaj | Para | 3 |
| Teklif | Satır bazlı oluşturucu, şablonlar, indirim/vergi, PDF ve proforma, e-posta/WhatsApp ile gönderim, **public onay linki** (kabul/ret/not), revizyon geçmişi, geçerlilik, kapora, Teklif → İş Emri | Cari, Katalog | 3 |
| Sözleşme | Şablon + değişkenler, PDF, link ile onay, yenileme hatırlatması | Teklif | 5 |

## C. Operasyon

| Modül | Kapsam | Bağımlı | Faz |
| --- | --- | --- | --- |
| **İş Emri** | Müşteriye bağlı iş; tenant'a göre **özelleştirilebilir durum akışı**; atanan kişiler, öncelik, termin, alt görev/checklist, yorum + @bahsetme, dosya + **revizyon numaralı teslimat**; Kanban / liste / takvim / zaman çizelgesi; **iş şablonları** ("Logo tasarımı" → hazır görevler); tekrarlayan iş (retainer) | Cari, Dosya | 2 |
| Zaman takibi | Sayaç + haftalık timesheet, iş/görev bazlı, faturalanabilir/değil, personel saatlik maliyeti → **iş karlılığı** | İş Emri, Personel | 2 |
| Personel | Profil, departman/ekip, unvan, çalışma saatleri, saatlik maliyet, zimmet, **izin talebi** (Faz 2 basit onay, Faz 4 onay motoru), iş yükü/utilizasyon. Bordro yok | Kimlik | 2 |
| Takvim & Kaynak | Ortak takvim (toplantı, çekim, termin), stüdyo/ekipman **rezervasyonu**, Google/Outlook senkronu (sonra) | İş Emri | 3 |
| Destek talebi | Portal/WhatsApp/e-postadan talep → iş emrine dönüştür, basit SLA | Portal, Mesajlaşma | 5 |

## D. Finans (ön muhasebe)

| Modül | Kapsam | Bağımlı | Faz |
| --- | --- | --- | --- |
| **Fatura** | Satış faturası (iş/tekliften; Faz 7'de siparişten); türler: satış / iade / tevkifatlı / istisna; **gelen belgeler:** alış faturası, **e-SMM** (freelancer, stopajlı), **gider pusulası**; vergi motoru (KDV, tevkifat kodları, stopaj, istisna kodu), döviz + kur, PDF; **e-Fatura/e-Arşiv entegratörü:** alıcı mükellef sorgusu (e-Fatura mı e-Arşiv mi), gönderim, durum, iptal/itiraz; **GİB numarası entegratörden gelir**, iç taslak numarası ayrı | Cari, Katalog | 4 |
| **Tahsilat & Ödeme** | Nakit, havale/EFT, kredi kartı, çek, senet; kısmi ödeme, çoklu faturaya dağıtım, vade takibi, **otomatik hatırlatma** (e-posta → WhatsApp/SMS) | Fatura | 4 |
| Kasa & Banka | Kasalar, banka hesapları, virman, günlük kasa raporu; ekstre içe aktarma + eşleştirme (Faz 8) | Tahsilat | 4 |
| Çek & Senet | Portföy: alınan/verilen; durumlar: portföyde → ciro → tahsile/teminata verildi → tahsil edildi / karşılıksız / iade; vade takvimi; ciro cari hareketi doğurur | Kasa & Banka | 4 |
| Gider | Gider fişleri, kategoriler, fiş fotoğrafı, tekrarlayan giderler, personel masraf talebi (onay motoru), işe/müşteriye maliyet olarak bağlama | Cari, Onay | 4 |
| **Sanal POS (PayTR)** | Ödeme linki (teklif kaporası, fatura), portaldan kartla ödeme, taksit, callback → tahsilat otomatik; ikinci sağlayıcı iyzico | Tahsilat | 4 |
| Tekrarlayan fatura | Retainer/abonelik: aylık otomatik fatura + hatırlatma + ödeme linki | Fatura, Arka plan | 4 |
| Cari ekstre & Yaşlandırma | Borç/alacak dökümü, vade yaşlandırma (0-30-60-90) | Tahsilat | 4 |
| Açılış & Devir | Onboarding: cari/kasa/banka/çek açılış bakiyeleri, Excel ile toplu içe aktarma; **dönem kilidi** | Cari, Kasa | 4 |
| Mutabakat | Cari mutabakat mektubu PDF + e-posta; public link ile onay/itiraz (Faz 5) | Ekstre | 4 |
| Raporlar (Finans) | Nakit akışı, tahsilat/ödeme takvimi, KDV özeti, iş/müşteri karlılığı | Tümü | 4 |
| Sipariş | Satış siparişi (tekliften); ürün satan firma için | Teklif | 7 |
| İrsaliye | Sevk irsaliyesi PDF; e-İrsaliye entegratör üzerinden (Faz 8) | Sipariş | 7 |

## E. İletişim & Müşteri Portalı

| Modül | Kapsam | Bağımlı | Faz |
| --- | --- | --- | --- |
| E-posta gönderim | İşlemsel e-posta (Resend/SES/SMTP), tenant'a özel gönderen, şablonlar | Bildirim | 1 |
| WhatsApp — bildirim | Cloud API **şablon mesajları**: teklif linki, ödeme linki, iş durumu, hatırlatma | Bildirim | 3–4 |
| WhatsApp — gelen kutusu | Webhook ile gelen mesaj, **ortak gelen kutusu**, personele atama, cari/işe bağlama, 24 saat penceresi, hızlı yanıtlar | Cari | 5 |
| SMS | Netgsm/İleti Merkezi: OTP, hatırlatma; WhatsApp yedeği | Bildirim | 5 |
| E-posta senkron | Gmail/Outlook gelen kutusu → cari zaman çizelgesi (opsiyonel) | Cari | 8 |
| **Müşteri portalı** | Müşteri girişi: iş durumu, teslimat/revizyon geri bildirimi (görsel üstüne not), teklif onayı, fatura & **kartla ödeme**, dosya yükleme, talep açma | Teklif, İş Emri, Fatura, PayTR | 5 |

## F. Ajans modülleri

| Modül | Kapsam | Bağımlı | Faz |
| --- | --- | --- | --- |
| **İçerik takvimi** | Müşteri bazlı gönderi planı (platform, tarih, görsel, metin, hashtag seti), üretim durumu, **portaldan müşteri onayı**, yorum | İş Emri, Portal | 6 |
| Sosyal yayınlama | Meta Graph API (Instagram/Facebook) zamanlanmış yayın; sonra LinkedIn, X, TikTok; hesap bağlama (OAuth), token yenileme | İçerik takvimi | 6 |
| Sosyal & reklam raporları | Erişim/etkileşim/takipçi metrikleri; Meta Ads + Google Ads harcama/sonuç; **aylık müşteri raporu** (PDF + portal); SEO verisi akiron-seo'dan | Yayınlama | 6 |
| Kreatif kütüphane & Proofing | Marka varlıkları (logo, font, renk), teslimat sürümleri, görsel üstüne işaretleme, onay | Dosya, Portal | 6 |

## G. Stok, Satın alma, Varlıklar

| Modül | Kapsam | Bağımlı | Faz |
| --- | --- | --- | --- |
| Stok | Ürün/varyant/birim, depolar, hareketler (giriş/çıkış/transfer/sayım), min stok uyarısı, ağırlıklı ortalama maliyet, sipariş/irsaliye/fatura ile bağ | Katalog, İrsaliye | 7 |
| Satın alma & Tedarikçi | Satın alma siparişi, mal kabul → stok, alış faturası eşleme, tedarikçi/freelancer performansı | Stok, Fatura | 7 |
| Demirbaş & Araç | Varlık kaydı, **zimmet**, bakım planı; araç: km/yakıt, **muayene/sigorta/kasko hatırlatması**, sürücü ataması, görev/yol kaydı; GPS sağlayıcı entegrasyonu opsiyonel | Personel | 7 |
| Kargo | Yurtiçi/Aras/MNG etiket + takip | Sipariş | 8 |

## H. Muhasebe entegrasyonları & Ölçek

| Modül | Kapsam | Bağımlı | Faz |
| --- | --- | --- | --- |
| Muhasebeci aktarımı | Aylık Excel/XML paket (cari, fatura, tahsilat, gider) | Finans | 4 (basit) / 8 |
| Logo entegrasyonu | Logo REST (Tiger/Go/İşbaşı): cari, fatura, stok, tahsilat senkronu | Finans, Stok | 8 |
| Bay.t entegrasyonu | Entegre Pro API; kapsam Logo ile aynı | Finans, Stok | 8 |
| Banka eşleştirme | Ekstre içe aktarma (Excel/MT940), otomatik tahsilat önerisi; açık bankacılık sonra | Kasa & Banka | 8 |
| Kur farkı & Vade farkı | Dövizli faturada kur farkı faturası, geç ödemede vade farkı | Fatura | 8 |
| Mobil | Önce PWA (saha/personel: zaman, masraf, araç km); native sonra | – | 8 |
| KVKK & Güvenlik | Veri dışa aktarma/silme, rıza kayıtları, 2FA, SSO, IP kısıtı, yedek indirme | Kimlik | 8 (temelleri 1) |
| SaaS faturalama | Planlar, modül lisansı, kendi aboneliğimizi PayTR/iyzico ile tahsil | Lisans, PayTR | 8 |

## Bilinçli olarak kapsam dışı

Genel muhasebe (yevmiye/e-Defter), bordro (Logo Bordro'ya bırakılır), GPS donanımı, native mobil (PWA yeter), pazaryeri entegrasyonları (akiron-commerce'in işi; sipariş akışı webhook ile bağlanır).
