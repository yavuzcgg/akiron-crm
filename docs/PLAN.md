# Akiron CRM — Proje Planı

## Hedef
Küçük ve orta ölçekli ekipler için sade, hızlı, çok kiracılı (multi-tenant) bir CRM:
müşteri, kişi, lead, fırsat ve aktivite yönetimi; satış pipeline'ı ve raporlama.

## Teknoloji
| Katman | Teknoloji |
|---|---|
| Backend | ASP.NET Core 10 Web API, EF Core + Npgsql |
| Frontend | Next.js (App Router, TypeScript, Tailwind CSS), shadcn/ui, TanStack Query |
| Veritabanı | PostgreSQL 17 (esnek/özel alanlar için `jsonb`) |
| Kimlik | ASP.NET Identity + JWT (access + refresh token) |
| Altyapı | Docker Compose, GitHub Actions |

**Neden Postgres?** CRM verisi büyük ölçüde ilişkisel (Şirket → Kişi → Fırsat → Aktivite);
join, raporlama ve transaction ihtiyaçları için ilişkisel DB doğal seçim. Kullanıcı tanımlı
özel alanlar `jsonb` ile karşılanır, MongoDB'ye gerek kalmaz.

## Backend Mimarisi (Clean Architecture)
```
backend/
├─ src/
│  ├─ Akiron.Domain          # Entity'ler, value object'ler, enum'lar, domain kuralları
│  ├─ Akiron.Application     # Use-case'ler, DTO'lar, validation, arayüzler
│  ├─ Akiron.Infrastructure  # EF Core DbContext, migration'lar, dış servisler
│  └─ Akiron.Api             # HTTP endpoint'leri, auth, OpenAPI
└─ tests/
   └─ Akiron.Tests
```
Bağımlılık yönü: `Api → Infrastructure → Application → Domain`.

## Çekirdek Veri Modeli
Tüm entity'ler `BaseEntity`'den türer: `Id (Guid v7)`, `TenantId`, `OwnerId`, `CreatedAt`, `UpdatedAt`.

| Entity | Açıklama |
|---|---|
| `Tenant` | Firma/çalışma alanı |
| `User`, `Role` | Kullanıcılar ve yetkiler |
| `Account` | Müşteri şirket |
| `Contact` | Kişi (bir Account'a bağlı olabilir) |
| `Lead` | Henüz nitelendirilmemiş potansiyel müşteri; Account/Contact/Deal'e dönüştürülebilir |
| `Pipeline`, `Stage` | Satış süreçleri ve aşamaları |
| `Deal` | Fırsat: tutar, para birimi, aşama, beklenen kapanış tarihi |
| `Activity` | Görev, arama, toplantı, e-posta (Account/Contact/Deal ile ilişkili) |
| `Note`, `Tag`, `Attachment` | Notlar, etiketler, dosyalar |
| `AuditLog` | Kim, neyi, ne zaman değiştirdi |

## Yol Haritası

### Faz 0 — Temel ✅
- Repo, solution iskeleti, Next.js uygulaması
- Docker Compose (Postgres), CI pipeline
- Plan dokümanı

### Faz 1 — MVP
- Kayıt/giriş, JWT, rol bazlı yetki
- Tenant yapısı ve global query filter
- Accounts, Contacts, Leads CRUD (liste, filtre, arama, sayfalama)
- Frontend: layout, sidebar, auth sayfaları, liste/detay/form ekranları
- Basit dashboard (sayılar, son aktiviteler)

### Faz 2 — Satış
- Pipeline & Stage yönetimi
- Deals + Kanban board (sürükle-bırak)
- Activities: görev/arama/toplantı, takvim görünümü, hatırlatmalar
- Lead → Account/Contact/Deal dönüşümü

### Faz 3 — Operasyon
- Ürün kataloğu ve teklif (PDF)
- Dosya ekleri (S3 uyumlu storage)
- E-posta entegrasyonu (SMTP / Gmail / Outlook)
- Audit log, raporlar (satış hunisi, kazanma oranı, gelir tahmini)

### Faz 4 — Ölçek
- Çoklu tenant SaaS: plan/abonelik
- Gerçek zamanlı bildirimler (SignalR)
- Public API + webhook'lar
- CSV/Excel import-export, özel alanlar (`jsonb`)

## Geliştirme Prensipleri
- Her özellik: migration + endpoint + test + UI aynı PR'da
- API sözleşmesi OpenAPI'den türetilir; frontend tipleri buradan üretilir
- `main` her zaman yeşil CI ile deploy edilebilir durumda
