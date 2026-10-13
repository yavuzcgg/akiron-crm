"use client";

import { useQueryClient } from "@tanstack/react-query";
import { BriefcaseBusiness, CalendarDays, FileText, House, LogOut, Wallet, type LucideIcon } from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { portalSessionExpiredEvent } from "@/lib/api/portal-client";
import { useI18n, type TranslationKey } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useLeavePortal, usePortalMe } from "./portal-api";

const destinations: { href: string; label: TranslationKey; icon: LucideIcon }[] = [
  { href: "/portal", label: "portal.nav.home", icon: House },
  { href: "/portal/jobs", label: "portal.nav.jobs", icon: BriefcaseBusiness },
  { href: "/portal/content", label: "portal.nav.content", icon: CalendarDays },
  { href: "/portal/quotes", label: "portal.nav.quotes", icon: FileText },
  { href: "/portal/account", label: "portal.nav.account", icon: Wallet },
];

const isActive = (pathname: string, href: string) => (href === "/portal" ? pathname === "/portal" : pathname.startsWith(href));

/**
 * The client's frame: the agency's name, four destinations, sign-out. A top bar on wide screens,
 * a bottom tab bar on phones (design-system/akiron-crm/pages/portal.md). No staff chrome.
 */
export function PortalShell({ children }: { children: ReactNode }) {
  const { t } = useI18n();
  const pathname = usePathname();
  const router = useRouter();
  const me = usePortalMe();
  const leave = useLeavePortal();
  const queryClient = useQueryClient();

  useEffect(() => {
    const toLogin = () => {
      // The hint only says "a session probably exists"; it no longer does, and the route guard
      // would otherwise send the client straight back here.
      document.cookie = "akiron_portal_session=; Max-Age=0; path=/";
      queryClient.removeQueries({ queryKey: ["portal"] });
      router.replace("/portal/login");
    };
    window.addEventListener(portalSessionExpiredEvent, toLogin);
    return () => window.removeEventListener(portalSessionExpiredEvent, toLogin);
  }, [router, queryClient]);

  return (
    <div className="bg-background flex min-h-dvh flex-col">
      <header className="bg-card/90 sticky top-0 z-20 border-b backdrop-blur">
        <div className="mx-auto flex h-14 max-w-[960px] items-center gap-6 px-4">
          <Link href="/portal" className="min-w-0 truncate font-semibold tracking-tight">
            {me.data?.workspaceName ?? <Skeleton className="h-5 w-32" />}
          </Link>
          <nav aria-label={t("portal.nav.label")} className="hidden items-center gap-1 md:flex">
            {destinations.map((item) => (
              <Link
                key={item.href}
                href={item.href}
                aria-current={isActive(pathname, item.href) ? "page" : undefined}
                className={cn(
                  "focus-visible:ring-ring/40 rounded-lg px-3 py-1.5 text-sm transition-colors focus-visible:ring-2 focus-visible:outline-none",
                  isActive(pathname, item.href) ? "bg-primary-soft text-primary-strong font-medium" : "text-muted-foreground hover:text-foreground",
                )}
              >
                {t(item.label)}
              </Link>
            ))}
          </nav>
          <div className="ml-auto flex items-center gap-2">
            <span className="text-muted-foreground hidden max-w-40 truncate text-sm sm:block">{me.data?.name}</span>
            <Button
              variant="ghost"
              size="sm"
              disabled={leave.isPending}
              onClick={() => leave.mutate(undefined, { onSettled: () => router.replace("/portal/login") })}
            >
              <LogOut /> <span className="hidden sm:inline">{t("portal.signOut")}</span>
              <span className="sr-only sm:hidden">{t("portal.signOut")}</span>
            </Button>
          </div>
        </div>
      </header>

      <main className="mx-auto w-full max-w-[960px] flex-1 px-4 py-6 pb-24 md:py-8 md:pb-10">{children}</main>

      <footer className="text-muted-foreground hidden pb-6 text-center text-xs md:block">{t("portal.poweredBy")}</footer>

      <nav aria-label={t("portal.nav.label")} className="bg-card fixed inset-x-0 bottom-0 z-20 grid grid-cols-5 border-t md:hidden">
        {destinations.map((item) => (
          <Link
            key={item.href}
            href={item.href}
            aria-current={isActive(pathname, item.href) ? "page" : undefined}
            className={cn(
              "flex h-14 flex-col items-center justify-center gap-0.5 text-[11px] focus-visible:outline-none",
              isActive(pathname, item.href) ? "text-primary font-medium" : "text-muted-foreground",
            )}
          >
            <item.icon className="size-5" aria-hidden />
            {t(item.label)}
          </Link>
        ))}
      </nav>
    </div>
  );
}
