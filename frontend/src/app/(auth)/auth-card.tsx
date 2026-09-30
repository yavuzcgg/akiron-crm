"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { useI18n, type TranslationKey } from "@/lib/i18n";

interface AuthCardProps {
  title: TranslationKey;
  description: TranslationKey;
  switchPrompt: TranslationKey;
  switchLabel: TranslationKey;
  switchHref: string;
  children: ReactNode;
}

/** The form column of the auth split layout: heading, form, and the way to the other auth page. */
export function AuthCard({ title, description, switchPrompt, switchLabel, switchHref, children }: AuthCardProps) {
  const { t } = useI18n();

  return (
    <div className="grid gap-8">
      <div className="grid gap-2">
        <h1 className="text-[28px] leading-tight font-bold tracking-[-0.01em]">{t(title)}</h1>
        <p className="text-muted-foreground text-[15px]">{t(description)}</p>
      </div>
      {children}
      <p className="text-muted-foreground text-center">
        {t(switchPrompt)}{" "}
        <Link href={switchHref} className="text-primary font-semibold underline-offset-4 hover:underline">
          {t(switchLabel)}
        </Link>
      </p>
    </div>
  );
}
