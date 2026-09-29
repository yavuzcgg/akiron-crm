# Akiron CRM

Ajanslar için iş yönetimi ve ön muhasebe platformu — Türkiye'ye göre.

**Müşteri → Teklif → Sözleşme → İş Emri → Fatura**, her aşamada tahsilat (kapora, taksit, fatura ödemesi) ve hepsini tek akışta gösteren müşteri zaman çizelgesi; üstüne WhatsApp/e-posta iletişimi, müşteri portalı ve içerik takvimi. Önce dijital/reklam ajansları, sonra genel KOBİ (stok, satın alma, araç/demirbaş, Logo/Bay.t entegrasyonu).

**Stack:** ASP.NET Core 10 (modüler monolit) · Next.js · PostgreSQL

> Durum: Faz 1 / Sprint 1–2 tamamlandı — modüler iskelet, çok kiracılı veri katmanı, kimlik doğrulama ve davet, activity timeline, denetim izi, dosya deposu, bildirimler (anlık), e-posta, TCMB kurları. Üretime hazır değildir.

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
backend/    ASP.NET Core Web API — modüler monolit (BuildingBlocks, Modules/*, Akiron.Api)
frontend/   Next.js (App Router, TypeScript, Tailwind, shadcn/ui)
docs/       Plan, modül kataloğu, entegrasyonlar, ADR'ler
```

## Başlangıç

Gereksinimler: .NET SDK 10, Node.js 22+, Docker

```bash
# Postgres (5434), MinIO dosya deposu (9010, konsol 9011), Mailpit e-posta kutusu (http://localhost:8026)
docker compose up -d

# Backend (http://localhost:5080 — API belgesi /scalar); ilk açılışta migration'lar uygulanır
dotnet run --project backend/src/Akiron.Api

# Frontend (http://localhost:3100)
cd frontend
npm install
npm run dev
```

`http://localhost:3100/register` adresinden bir ajans oluşturup giriş yapabilirsiniz.

## Lisans

[Business Source License 1.1](LICENSE). Kaynak kod görünürdür; kişisel, eğitim ve değerlendirme amaçlı kullanım serbesttir. Barındırılan hizmet olarak sunmak veya ticari olarak dağıtmak için lisans gerekir. Her sürüm 4 yıl sonra Apache 2.0 lisansına geçer.
