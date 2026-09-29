# Akiron CRM

Ajanslar için iş yönetimi ve ön muhasebe platformu — Türkiye'ye göre.

**Müşteri → Teklif → İş Emri → Zaman/Revizyon → Fatura → Tahsilat**, üstüne WhatsApp/e-posta iletişimi, müşteri portalı ve içerik takvimi. Önce dijital/reklam ajansları, sonra genel KOBİ (stok, satın alma, araç/demirbaş, Logo/Bay.t entegrasyonu).

**Stack:** ASP.NET Core 10 (modüler monolit) · Next.js · PostgreSQL

> Durum: Faz 0 (iskelet) tamamlandı, Faz 1 (platform çekirdeği) başlıyor. Üretime hazır değildir.

## Belgeler

| Belge | İçerik |
| --- | --- |
| [docs/PLAN.md](docs/PLAN.md) | Ürün tezi, ilkeler, 8 fazlık yol haritası, Sprint 1 görevleri |
| [docs/MODULES.md](docs/MODULES.md) | Modül kataloğu: kapsam, bağımlılık, faz |
| [docs/INTEGRATIONS.md](docs/INTEGRATIONS.md) | Sağlayıcı planı (PayTR, WhatsApp, e-Fatura, Meta, Logo…) ve idari checklist |
| [docs/adr/](docs/adr/README.md) | Mimari kararlar |
| [AGENTS.md](AGENTS.md) | Geliştirme kuralları (insan ve ajan için) |

## Yapı

```text
backend/    ASP.NET Core Web API — modüler monolit (BuildingBlocks, Contracts, Modules/*, Akiron.Api)
frontend/   Next.js (App Router, TypeScript, Tailwind, shadcn/ui)
docs/       Plan, modül kataloğu, entegrasyonlar, ADR'ler
```

## Başlangıç

Gereksinimler: .NET SDK 10, Node.js 22+, Docker

```bash
# Veritabanı
docker compose up -d postgres

# Backend  (http://localhost:5xxx/health)
dotnet run --project backend/src/Akiron.Api

# Frontend (http://localhost:3000)
cd frontend
npm install
npm run dev
```

## Lisans

[MIT](LICENSE)
