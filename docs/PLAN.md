# Akiron CRM — Ürün Planı ve Yol Haritası

> Güncelleme: 2026-09-29 · Durum: Faz 0 tamamlandı, Faz 1 başlıyor
> İlgili belgeler: [Modül kataloğu](MODULES.md) · [Entegrasyonlar](INTEGRATIONS.md) · [Mimari kararlar](adr/README.md) · [Geliştirme kuralları](../AGENTS.md)

## Ne yapıyoruz

**Müşteriden işe, işten tahsilata kadar şirket operasyonlarını tek yerde yöneten platform.**

Müşteri → Teklif → Sözleşme → İş Emri → Zaman/Revizyon → Fatura; tahsilat bu zincirin bir adımı değil, her aşamada (kapora, taksit, fatura ödemesi) alınıp belgelere eşleştirilir. Üstüne WhatsApp/e-posta iletişimi, müşteri portalı ve içerik takvimi. Klasik CRM değil; Türkiye'ye göre tasarlanmış, **modül modül satılan** bir iş yönetimi platformu. Önce ajanslar, sonra aynı çekirdek üzerine sektör paketleri.

## Hedef kitle ve dağıtım

| Konu | Karar |
| --- | --- |
| İlk müşteri | Dijital/reklam ajansları (kendi ajansımız dahil — dogfooding) |
| Sonraki | Yazılım/danışmanlık, teknik servis, ticaret firmaları (sektör paketleriyle) |
| Dağıtım | SaaS (multi-tenant). Tek firma kurulumu (on-prem, `docker compose up`) mimaride açık |
| Satış birimi | Modül. Tenant hangi modülleri aldıysa o menüler ve uçlar açılır |
| Dil | Arayüz TR (EN sözlük baştan), kod ve commit İngilizce |
| Ekip | Tek geliştirici + Claude; 2 haftalık sprint |

## Sektör paketleri

Aynı çekirdek, farklı modül setleri. Paket = önceden tanımlı modül listesi; tenant sonradan modül ekleyebilir.

| Paket | Modüller |
| --- | --- |
| Ajans | Core + CRM + Satış + İş + Portal + SMM |
| Yazılım / danışmanlık | Core + CRM + Satış + İş + Portal + Destek |
| Teknik servis | Core + CRM + İş Emri + Stok + Araç |
| Ticaret | Core + CRM + Satış + Stok + Finans + Muhasebe entegrasyonu |

Mekanizma: `Entitlement` (tenant × modül × koltuk × bitiş) + `RequireModule("smm")` endpoint filtresi + menü görünürlüğü. SaaS'ta bu abonelik kaydıdır, şifreleme gerekmez. On-prem için imzalı lisans dosyası (modüller, koltuk, bitiş; Ed25519 imza, günlük kontrol, tolerans süresi) ilk gerçek on-prem müşteride yazılır; şimdi yalnızca `Entitlement` soyutlaması doğru kurulur.

## Tasarım ilkeleri

1. **Cari hesap omurgadır.** Müşteri ve tedarikçi tek "Cari" kavramıdır (alıcı/satıcı bayrakları, ikisi birden olabilir); ayrı Customer/Supplier nesnesi yok. Her tahsilat, fatura, çek, gider bir cariye bağlanır. [ADR-0008](adr/0008-single-party-cari.md).
2. **Belge zinciri kopyalanmaz, türetilir; para zincirde değil.** Ticari zincir: Teklif → Sözleşme (opsiyonel) → İş Emri → Fatura; ürün zinciri (Faz 7): Teklif → Sipariş → İrsaliye → Fatura. Tahsilat/ödeme cariye kaydedilir ve herhangi bir açık kaleme (kapora, taksit, fatura) kısmen veya tamamen eşleştirilir; eşleşmeyen tahsilat carinin alacak (avans) bakiyesidir. Logo'nun cari hareket + borç kapama modeliyle aynı. [ADR-0004](adr/0004-document-chain-money-tax.md).
3. **Her şey tek zaman çizelgesine düşer.** Activity timeline ürünün merkezi ekranıdır, log değildir ([ADR-0009](adr/0009-activity-timeline.md)); her modül ona yazar ("teklif gönderildi → müşteri görüntüledi → ödendi → iş açıldı"), müşteri kartı onu okur.
4. **Modüler monolit, modül = satılabilir birim.** Her modül kendi Postgres şemasında; modüller arası iletişim olaylarla; tenant başına modül aç/kapa. [ADR-0001](adr/0001-modular-monolith.md).
5. **Her dış servis bir arayüzün arkasında.** Ödeme, mesajlaşma, e-Fatura, ERP, sosyal yayın — sağlayıcı değişir, modül değişmez. [ADR-0005](adr/0005-integrations-behind-providers.md).
6. **Yasal sınırlar baştan.** Fatura yalnızca GİB entegratörü üzerinden; finansal kayıt silinmez; KVKK (rıza, dışa aktarma, silme). [ADR-0004](adr/0004-document-chain-money-tax.md).
7. **TR-first ama i18n baştan.** Metinler sözlükten; çoklu para birimi baştan (TCMB kuru belge anında sabitlenir).
8. **Önce elle, sonra otomatik.** Onay bekleyen entegrasyonlar (WhatsApp gelen kutusu, sosyal yayın, e-posta senkronu, banka eşleştirme) önce manuel akışla çalışır; entegrasyon gelince arayüz değişmez.

## Yol haritası

Sprint = 2 hafta. Her faz "kullanılabilir bir şey" ile kapanır. Bir faz bitmeden sonrakinin modülü açılmaz.

| Sürüm | Faz | Ad | Sprint | Sonunda ne olur |
| --- | --- | --- | --- | --- |
| **V1 Ajans** | 1 | Platform çekirdeği | 3 | Giriş, tenant/rol/izin, modül bayrakları, activity timeline, onay iskeleti, dosya, bildirim, e-posta, arka plan işleri. Henüz "iş" yok. |
| | 2 | Müşteri & İş Takibi | 3 | **Kendi ajansımız kullanmaya başlar:** cari + zaman çizelgesi, iş emri/görev/checklist (Kanban), personel, zaman takibi + maliyet, dosya/revizyon, özel alanlar. |
| | 3 | Satış & Para | 3–4 | Lead hunisi, katalog (KDV dahil/hariç, tevkifat kodları), teklif PDF + public onay linki, **sözleşme/abonelik (retainer)**, **Finance Lite** (tahsilat kaydı, ödeme planı, cari bakiye), **PayTR ödeme linki**, WhatsApp şablon bildirimi. |
| | 4 | Portal, İletişim & SMM onayı | 3 | **Müşteri portalı** (iş durumu, teklif onayı, revizyon, ödeme), **içerik takvimi + onay akışı** (yayın manuel), WhatsApp gelen kutusu, SMS, e-posta gönderim + BCC eşleme, destek talebi, otomasyon v1. **V1 satışa çıkar.** |
| **V2 Finans** | 5 | Fatura & Finans | 4 | e-Fatura/e-Arşiv entegratörü, gelen belgeler (alış faturası, e-SMM, gider pusulası), gider, kasa/banka, açılış bakiyeleri + dönem kilidi, ekstre/yaşlandırma, mutabakat, retainer otomatik fatura, muhasebeci Excel paketi. |
| | 6 | Ajans raporlama | 3 | Meta yayınlama, sosyal/reklam raporları, aylık müşteri raporu, gelişmiş proofing, kreatif kütüphane. |
| **V3 KOBİ** | 7 | Ticaret paketi | 3 | Sipariş, irsaliye, stok, satın alma, çek/senet, demirbaş & araç. |
| | 8 | Entegrasyon & AI | sürekli | Logo/Bay.t/Paraşüt adaptörleri, banka eşleştirme, Gmail/Outlook senkron, AI asistan, public API/webhook, PWA, KVKK araçları, SaaS faturalama, kargo. |

**Kilometre taşları:** Faz 2 sonu = dogfooding · Faz 4 sonu = **V1, ilk dış müşteri** · Faz 5 sonu = muhasebe tarafı tamam · Faz 7 sonu = genel KOBİ.

Modül bazında kapsam ve bağımlılıklar: [MODULES.md](MODULES.md).

## Faz 1 — Platform çekirdeği

### Sprint 1

| # | Görev | Kaynak | Kabul ölçütü |
| --- | --- | --- | --- |
| 1 | Solution'ı yeniden kur: `BuildingBlocks`, `Contracts`, `Modules/Identity`, `Akiron.Api` (host), `tests/{Architecture,Integration}`; `Directory.Build.props` (warnings-as-errors), `Directory.Packages.props`, `global.json` | akiron-commerce | `dotnet build` uyarısız; mimari testler yeşil |
| 2 | BuildingBlocks çekirdeği: typed GUIDv7 id'li `Entity<TId>`, `ITenantScoped/ISoftDeletable/IAuditable`, `ITenantContext`, SaveChanges interceptor'ları (tenant damgası + uyumsuzluk reddi, UpdatedAt, soft delete), `Result/Error`, `PagedResult`, `IClock`, `ModuleDbContext`, `IModule` | akiron-seo filtre döngüsü | Interceptor'lar entegrasyon testinde kanıtlı |
| 3 | Identity modülü: Tenant, User, Membership, Role, Permission, RefreshToken; register (tenant + owner), login, refresh, logout, me. İzin kataloğu + `RequirePermission()` | akiron-seo auth, lastik-depo izinler | Auth ve tenant izolasyon testleri yeşil |
| 4 | Hata sözleşmesi: ProblemDetails + `module.resource.reason` kodları; FluentValidation endpoint filtresi; DB ihlali → 409 | akiron-commerce ADR-0018 | 400/404/409 gövdeleri testli |
| 5 | Persistence: modül şeması + ayrı migration geçmişi, design-time factory, dev'de otomatik migrate, Türkçe collation | akiron-commerce | Sıfır DB'de uygulama ayağa kalkar |
| 6 | Serilog + correlation id, `/health/live`, `/health/ready` | akiron-seo | Log satırında correlation id |
| 7 | Test tabanı: Postgres fixture + WebApplicationFactory + Respawn | akiron-seo tests | CI'da Testcontainers koşuyor |
| 8 | OpenAPI: `MapOpenApi()`; frontend `npm run api:gen` → tip üretimi; `openapi-fetch` istemcisi tek eşzamanlı refresh ile | akiron-seo apiClient | `tsc` yeşil |
| 9 | Frontend kabuk: shadcn/ui, sidebar/topbar, login/register (RHF + zod), `useSession`, korumalı route, tipli TR/EN sözlük, tema token'ları | akiron-seo, lastik-depo | Login → dashboard → logout çalışır |
| 10 | CI: backend build/test/trx, frontend tsc/lint/vitest/build, `dotnet format` doğrulaması, docker buildx | akiron-seo ci.yml | İlk PR yeşil |

**Durum (29 Eyl 2026): tamamlandı.** 28 backend testi (mimari + Testcontainers üzerinde entegrasyon) ve 9 frontend testi yeşil; kayıt → oturum → ekip listesi → çıkış akışı gerçek API'ye karşı elle de doğrulandı.

Plandan bilinçli sapmalar:

- `Contracts` projesi ve birim test projesi açılmadı; ilk integration event'te / ilk veritabansız mantıkta eklenecek.
- `IClock` yerine .NET'in yerleşik `TimeProvider`'ı kullanıldı.
- Respawn alınmadı: testler her seferinde yeni tenant ve e-posta ile izole, paralel koşuyor.
- Türkçe collation, ilk Türkçe ad sıralaması gereken tabloda (Faz 2, Cari) eklenecek.
- TRX raporu ve docker buildx Sprint 3'e (prod compose ile birlikte) kaydı.
- Yerel portlar diğer Akiron projeleriyle çakışmasın diye: Postgres 5434, API 5080, web 3100.

### Sprint 2
Domain event + outbox + Hangfire (timeline bunun üstüne kurulur), activity timeline (ADR-0009: `timeline` şeması, projektörler, notlar, keyset sayfalama; her modülün "bitti" tanımına timeline projektörü eklenir), onay motoru iskeleti (`ApprovalRequest`: adımlar, onaylayan rolü, durum), özel alan altyapısı (entity başına tipli tanım + `jsonb` değer + filtre), dosya deposu (MinIO/S3), denetim `changes` tablosu, bildirim merkezi (SignalR + e-posta), belge numaralama, para birimi + TCMB kur işi, kullanıcı davet akışı.

### Sprint 3
`Entitlement` + modül bayrakları + `RequireModule()`, ayarlar sayfası, docker-compose prod profili (api, web, postgres, minio), yedek/geri yükleme scripti, Playwright kritik akış, güvenlik temelleri (rate limit, CORS, secrets doğrulama), Faz 2 veri modeli ADR'leri.

## Paralel idari işler

Kod değil ama bekleme süreleri uzun; Faz 1'de başlatılır. Liste ve takip: [INTEGRATIONS.md](INTEGRATIONS.md#idari-checklist).

## Açık kararlar

| # | Karar | Neden şimdi | Öneri |
| --- | --- | --- | --- |
| 1 | **Repo lisansı.** Repo public ve MIT; MIT'te herkes tüm modülleri bedava kurar, bayrakları siler. Modül satışıyla çelişir. | MIT ile yayımlanan her commit MIT kalır; kod gelmeden karar verilmeli. | Tek repo + source-available lisans (BSL 1.1 veya FSL): kaynak görünür, rakip olarak üretimde kullanılamaz, süre sonunda açık kaynak olur. Alternatif: AGPL-3.0 + ticari lisans (dual). Open-core (çekirdek MIT, premium modüller private repo) tek geliştirici için fazla yük. |
| 2 | **Ortak platform paketi** (`akiron-platform`: BuildingBlocks + auth + test kiti + frontend istemci/i18n/token). | Üç üründe aynı altyapı üç kez yazılıyor. | Faz 1 sonunda BuildingBlocks stabilleşince NuGet/npm paketi olarak çıkar; önce CRM tüketir, sonra Commerce'in henüz yazılmamış Identity servisi, en son Seo. Faz 1'den önce çıkarma: ihtiyaç daha netleşmedi. |
| 3 | **B2B projesi kodu** (USD bayi hesapları, Logo terimleri). | akiron-commerce devlog'u kur/cari tasarımının oradan geleceğini yazıyor. | Varsa Faz 5'te cari/Logo için kullanılır. |

## Riskler ve önlemler

| Risk | Önlem |
| --- | --- |
| Kapsam patlaması | Faz kapıları; her faz kullanılabilir çıktıyla kapanır |
| Meta onay süreleri (WhatsApp, Instagram yayın) | Manuel akışlar önce; entegrasyon gelince arayüz aynı |
| Gmail okuma izinleri (restricted scope → yıllık CASA denetimi) | V1'de BCC/forward ile e-posta eşleme; gerçek senkron Faz 8 |
| e-Fatura yasal karmaşası | Kendi GİB entegrasyonu yazılmaz; entegratör API'si; V1'de fatura yok, "fatura kesilecekler" listesi muhasebeciye |
| Tek geliştirici | Her modül aynı kalıp; BuildingBlocks'ta test ve CI disiplini |
| KVKK (özellikle AI'ya veri gönderimi) | Kişisel veri alanları işaretlenir; dışa aktarma/silme API'si Faz 1'den; AI için rıza + anonimleştirme |
