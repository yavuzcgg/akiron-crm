"use client";

import Link from "next/link";
import type { ReactNode } from "react";
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from "@/components/ui/card";
import { useI18n, type TranslationKey } from "@/lib/i18n";

interface AuthCardProps {
  title: TranslationKey;
  description: TranslationKey;
  switchPrompt: TranslationKey;
  switchLabel: TranslationKey;
  switchHref: string;
  children: ReactNode;
}

export function AuthCard({ title, description, switchPrompt, switchLabel, switchHref, children }: AuthCardProps) {
  const { t } = useI18n();

  return (
    <div className="grid gap-6">
      <div className="grid gap-1 text-center">
        <div className="bg-primary text-primary-foreground mx-auto flex size-10 items-center justify-center rounded-xl text-lg font-semibold">
          A
        </div>
        <p className="text-sm font-semibold">{t("app.name")}</p>
        <p className="text-muted-foreground text-xs">{t("app.tagline")}</p>
      </div>
      <Card>
        <CardHeader>
          <CardTitle className="text-xl">{t(title)}</CardTitle>
          <CardDescription>{t(description)}</CardDescription>
        </CardHeader>
        <CardContent>{children}</CardContent>
        <CardFooter className="text-muted-foreground justify-center text-sm">
          {t(switchPrompt)}&nbsp;
          <Link href={switchHref} className="text-foreground font-medium underline-offset-4 hover:underline">
            {t(switchLabel)}
          </Link>
        </CardFooter>
      </Card>
    </div>
  );
}
