# Akiron CRM — web

Next.js App Router, TypeScript, Tailwind v4, shadcn/ui (Base UI), TanStack Query, react-hook-form + zod.

```bash
npm install
npm run dev        # http://localhost:3100, API expected at http://localhost:5080 (override with API_URL)
npm run api:gen    # regenerate src/lib/api/schema.d.ts from the running API
npm run typecheck && npm run lint && npm test && npm run build
```

| Path | What lives there |
| --- | --- |
| `src/app/(auth)` | Sign-in and registration pages |
| `src/app/(app)` | Signed-in pages inside the app shell |
| `src/features/<module>` | Feature code: API hooks, forms, tables |
| `src/lib/api` | Typed API client, session renewal, error type |
| `src/lib/i18n` | Typed TR/EN dictionaries and error-code translation |
| `src/proxy.ts` | Optimistic redirect between signed-in and signed-out pages |

Project rules: [../AGENTS.md](../AGENTS.md).
