# Akiron CRM — Ürün Planı ve Yol Haritası

> Güncelleme: 2026-09-29 · Durum: Faz 0 tamamlandı, Faz 1 başlıyor
> İlgili belgeler: [Modül kataloğu](MODULES.md) · [Entegrasyonlar](INTEGRATIONS.md) · [Mimari kararlar](adr/README.md) · [Geliştirme kuralları](../AGENTS.md)

## Ne yapıyoruz

Bir ajans, müşteriyi kazandığı andan parayı tahsil ettiği ana kadar her şeyi tek yerden yürütür:

**Müşteri → Teklif → İş Emri → Zaman/Revizyon → Fatura → Tahsilat**

Üstüne WhatsApp/e-posta iletişimi, müşteri portalı ve içerik takvimi. Klasik CRM değil; Türkiye'ye göre tasarlanmış **ajans odaklı iş yönetimi + ön muhasebe** platformu (Paraşüt + Bitrix24 + Trello kesişimi). Ajanslardan sonra genel KOBİ'ye açılır (stok, satın alma, araç/demirbaş, Logo/Bay.t).

## Hedef kitle ve dağıtım

| Konu | Karar |
| --- | --- |
| İlk müşteri | Dijital/reklam ajansları (kendi ajansımız dahil — dogfooding) |
| Sonraki | Hizmet KOBİ'leri, ardından ürün satan firmalar |
| Dağıtım | SaaS (multi-tenant). Tek firma kurulumu (on-prem, `docker compose up`) mimaride açık |
| Dil | Arayüz TR (EN sözlük baştan), kod ve commit İngilizce |
| Ekip | Tek geliştirici + Claude; 2 haftalık sprint |

## Tasarım ilkeleri

1. **Cari hesap omurgadır.** Müşteri ve tedarikçi tek "Cari" kavramıdır; her fatura, tahsilat, çek, gider bir cariye bağlanır.
2. **Belge zinciri kopyalanmaz, türetilir.** Ajans zinciri: Teklif → İş Emri → Fatura → Tahsilat. Ürün zinciri (Faz 7): Teklif → Sipariş → İrsaliye → Fatura. Her belge kaynağını bilir.
3. **Modüler monolit.** Her modül kendi Postgres şemasında; modüller arası iletişim olaylarla; tenant başına modül aç/kapa. Ayrıntı: [ADR-0001](adr/0001-modular-monolith.md).
4. **Her dış servis bir arayüzün arkasında.** Ödeme, mesajlaşma, e-Fatura, muhasebe aktarımı, sosyal yayın — sağlayıcı değişir, modül değişmez. [ADR-0005](adr/0005-integrations-behind-providers.md).
5. **Yasal sınırlar baştan.** Türkiye'de fatura e-Fatura/e-Arşiv'dir ve yalnızca GİB entegratörü üzerinden kesilir; biz taslak üretir, entegratöre göndeririz. Finansal kayıt silinmez (iptal/ters kayıt + denetim izi). KVKK: rıza, dışa aktarma, silme.
6. **TR-first ama i18n baştan.** Metinler sözlükten; çoklu para birimi baştan (TCMB kuru belge anında sabitlenir).
7. **Önce elle, sonra otomatik.** Onay bekleyen entegrasyonlar (WhatsApp gelen kutusu, sosyal yayın, banka eşleştirme) önce manuel akışla çalışır; entegrasyon gelince arayüz değişmez.

## Yol haritası

Sprint = 2 hafta. Her faz "kullanılabilir bir şey" ile kapanır; sonraki faz gerçek kullanımdan beslenir. Bir faz bitmeden sonrakinin modülü açılmaz.

| Faz | Ad | Sprint | Sonunda ne olur |
| --- | --- | --- | --- |
| 0 | İskelet ✅ | – | Repo, CI, boş API/Next.js |
| 1 | Platform çekirdeği | 3 | Giriş, tenant/rol/izin, dosya yükleme, bildirim, e-posta, arka plan işleri. Henüz "iş" yok. |
| 2 | Müşteri & İş Takibi | 3 | **Kendi ajansımız kullanmaya başlar:** cari kartlar, iş emirleri (Kanban), personel, zaman takibi, iş maliyeti. |
| 3 | Satış | 2–3 | Lead hunisi, katalog (KDV dahil/hariç, tevkifat kodları), teklif PDF + proforma + public onay linki, tekliften iş emri, WhatsApp şablon bildirimi (hesap hazırsa). |
| 4 | Finans I | 4 | **Satılabilir v1.0:** fatura (taslak → e-Fatura/e-Arşiv), tahsilat, kasa/banka, çek/senet, gider ve freelancer belgeleri, PayTR ödeme linki, retainer faturası, ekstre/yaşlandırma, açılış bakiyeleri, muhasebeci Excel paketi. |
| 5 | İletişim & Portal | 3 | WhatsApp ortak gelen kutusu, SMS, müşteri portalı (onay, revizyon, ödeme), destek talebi, otomasyon kuralları v1, sözleşme. |
| 6 | Ajans modülleri | 3 | İçerik takvimi + müşteri onayı, Meta yayınlama, sosyal/reklam raporları, aylık müşteri raporu, kreatif kütüphane/proofing. |
| 7 | Stok & Varlıklar | 3 | Sipariş, irsaliye, stok, satın alma, demirbaş & araç. Genel KOBİ'ye satılabilir. |
| 8 | Entegrasyon & Ölçek | sürekli | Logo, Bay.t, banka eşleştirme, public API/webhook, PWA, KVKK araçları, SaaS faturalama, kargo. |

**Kilometre taşları:** Faz 2 sonu = dogfooding · Faz 4 sonu = ilk dış müşteri · Faz 6 sonu = ajanslara pazarlanabilir · Faz 7 sonu = genel KOBİ.

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

### Sprint 2
Dosya deposu (MinIO/S3), denetim `changes` tablosu, domain event + outbox + Hangfire, bildirim merkezi (SignalR + e-posta), belge numaralama, para birimi + TCMB kur işi, kullanıcı davet akışı, ayarlar sayfası.

### Sprint 3
Tenant modül bayrakları + lisans, docker-compose prod profili (api, web, postgres, minio), yedek/geri yükleme scripti, Playwright kritik akış, güvenlik temelleri (rate limit, CORS, secrets doğrulama), Faz 2 veri modeli ADR'leri.

## Paralel idari işler

Kod değil ama bekleme süreleri uzun; Faz 1'de başlatılır. Liste ve takip: [INTEGRATIONS.md](INTEGRATIONS.md#idari-checklist).

## Riskler ve önlemler

| Risk | Önlem |
| --- | --- |
| Kapsam patlaması | Faz kapıları; her faz kullanılabilir çıktıyla kapanır |
| Meta onay süreleri (WhatsApp, Instagram yayın) | Manuel akışlar önce; entegrasyon gelince arayüz aynı |
| e-Fatura yasal karmaşası | Kendi GİB entegrasyonu yazılmaz; entegratör API'si. Fatura modülü entegratörsüz de taslak seviyesinde çalışır |
| Tek geliştirici | Her modül aynı kalıp; BuildingBlocks'ta test ve CI disiplini |
| KVKK | Kişisel veri alanları işaretlenir; dışa aktarma/silme API'si Faz 1'den planlanır |
