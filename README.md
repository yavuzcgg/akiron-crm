# Akiron CRM

Küçük ve orta ölçekli ekipler için modern, açık kaynak CRM.

**Stack:** ASP.NET Core 10 · Next.js · PostgreSQL

> Proje erken aşamada. Mimari ve yol haritası için: [docs/PLAN.md](docs/PLAN.md)

## Yapı
```
backend/    ASP.NET Core Web API (Clean Architecture)
frontend/   Next.js (App Router, TypeScript, Tailwind)
docs/       Plan ve dokümantasyon
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
