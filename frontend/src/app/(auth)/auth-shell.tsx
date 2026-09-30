"use client";

import { Check, FileUp, MailPlus, StickyNote, UserPlus, type LucideIcon } from "lucide-react";
import type { ReactNode } from "react";
import { Logo, LogoMark } from "@/components/brand/logo";
import { useI18n, type TranslationKey } from "@/lib/i18n";

const points: TranslationKey[] = ["auth.brand.point.timeline", "auth.brand.point.team", "auth.brand.point.local"];

/** Only things the product does today; the panel is a promise customers will check. */
const sample: { icon: LucideIcon; text: TranslationKey; time: string; tone: string }[] = [
  { icon: StickyNote, text: "auth.brand.sample.note", time: "now", tone: "bg-amber-400/15 text-amber-200" },
  { icon: FileUp, text: "auth.brand.sample.file", time: "14:32", tone: "bg-violet-400/15 text-violet-200" },
  { icon: UserPlus, text: "auth.brand.sample.joined", time: "11:05", tone: "bg-emerald-400/15 text-emerald-200" },
  { icon: MailPlus, text: "auth.brand.sample.invite", time: "09:48", tone: "bg-sky-400/15 text-sky-200" },
];

/**
 * Split layout for sign-in, registration and invitations (design system: brand panel ≥1024px,
 * form column 400px). Below 1024px the form stands alone with the logo.
 */
export function AuthShell({ children }: { children: ReactNode }) {
  const { t } = useI18n();

  return (
    <div className="grid min-h-dvh lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)] xl:grid-cols-[minmax(0,1.1fr)_minmax(0,1fr)]">
      <aside className="relative hidden overflow-hidden bg-[#1b1650] text-white lg:flex lg:flex-col">
        <div className="bg-dots absolute inset-0 text-white/40" aria-hidden />
        <div
          className="absolute -top-40 -right-32 size-[520px] rounded-full bg-[radial-gradient(closest-side,#6366f1_0%,transparent_100%)] opacity-60"
          aria-hidden
        />
        <div
          className="absolute -bottom-48 -left-40 size-[520px] rounded-full bg-[radial-gradient(closest-side,#059669_0%,transparent_100%)] opacity-25"
          aria-hidden
        />

        <div className="relative flex flex-1 flex-col justify-between gap-12 p-10 xl:p-14">
          <div className="flex items-center gap-2.5">
            <LogoMark className="size-9" />
            <span className="text-lg font-bold tracking-tight">akiron</span>
          </div>

          <div className="grid max-w-lg gap-8">
            <div className="grid gap-4">
              <h2 className="text-[34px] leading-[1.15] font-bold tracking-[-0.01em]">{t("auth.brand.headline")}</h2>
              <p className="text-base leading-relaxed text-indigo-100/80">{t("auth.brand.subline")}</p>
            </div>
            <ul className="grid gap-3">
              {points.map((point) => (
                <li key={point} className="flex items-start gap-3 text-[15px] text-indigo-50/90">
                  <span className="mt-0.5 flex size-5 shrink-0 items-center justify-center rounded-full bg-emerald-400/20 text-emerald-300">
                    <Check className="size-3.5" aria-hidden />
                  </span>
                  {t(point)}
                </li>
              ))}
            </ul>

            <figure className="rounded-2xl border border-white/10 bg-white/[0.06] p-5 shadow-2xl shadow-indigo-950/40">
              <figcaption className="mb-4 text-xs font-medium tracking-wide text-indigo-200/70 uppercase">
                {t("auth.brand.sample.title")}
              </figcaption>
              <ol className="grid gap-3.5">
                {sample.map((entry) => (
                  <li key={entry.text} className="flex items-center gap-3">
                    <span className={`flex size-7 shrink-0 items-center justify-center rounded-full ${entry.tone}`}>
                      <entry.icon className="size-3.5" aria-hidden />
                    </span>
                    <span className="flex-1 text-sm text-indigo-50/90">{t(entry.text)}</span>
                    <time className="tabular text-xs text-indigo-200/60">
                      {entry.time === "now" ? t("auth.brand.sample.now") : entry.time}
                    </time>
                  </li>
                ))}
              </ol>
            </figure>
          </div>

          <p className="text-xs text-indigo-200/60">{t("auth.brand.footer")}</p>
        </div>
      </aside>

      <main className="flex flex-col px-4 py-8 sm:px-8">
        <div className="lg:hidden">
          <Logo />
        </div>
        <div className="flex flex-1 items-center justify-center py-10">
          <div className="w-full max-w-[400px]">{children}</div>
        </div>
      </main>
    </div>
  );
}
