"use client";

import { useQueryClient } from "@tanstack/react-query";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { toast } from "sonner";
import { AppSidebar } from "@/components/app-sidebar";
import { pageLabel } from "@/components/navigation";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { SidebarInset, SidebarProvider, SidebarTrigger } from "@/components/ui/sidebar";
import { useSession } from "@/features/identity/session";
import { TimerChip } from "@/features/jobs/timer";
import { NotificationBell, useRealtimeNotifications } from "@/features/notifications/notifications";
import { sessionExpiredEvent } from "@/lib/api/client";
import { useI18n } from "@/lib/i18n";

/**
 * The signed-in frame: sidebar, top bar and the session guard. The proxy only guesses from a
 * cookie; this is where an invalid session is actually detected and the user sent to sign in.
 */
export function AppShell({ children }: { children: ReactNode }) {
  const { t } = useI18n();
  const router = useRouter();
  const pathname = usePathname();
  const queryClient = useQueryClient();
  const session = useSession();
  useRealtimeNotifications(!!session.data);
  const currentPage = pageLabel(pathname);

  useEffect(() => {
    const onExpired = () => {
      queryClient.clear();
      toast.info(t("auth.sessionExpired"));
      router.replace(`/login?next=${encodeURIComponent(pathname)}`);
    };

    window.addEventListener(sessionExpiredEvent, onExpired);
    return () => window.removeEventListener(sessionExpiredEvent, onExpired);
  }, [pathname, queryClient, router, t]);

  useEffect(() => {
    if (session.isSuccess && session.data === null) {
      router.replace(`/login?next=${encodeURIComponent(pathname)}`);
    }
  }, [pathname, router, session.data, session.isSuccess]);

  if (!session.data) {
    return (
      <div className="flex min-h-svh">
        <Skeleton className="hidden w-64 md:block" />
        <div className="flex-1 space-y-4 p-6">
          <Skeleton className="h-8 w-48" />
          <Skeleton className="h-32 w-full" />
        </div>
      </div>
    );
  }

  return (
    <SidebarProvider>
      <AppSidebar session={session.data} />
      <SidebarInset>
        <header className="bg-background/85 sticky top-0 z-20 flex h-14 items-center gap-3 border-b px-4 backdrop-blur-sm md:px-6">
          <SidebarTrigger className="-ml-1.5" />
          <Separator orientation="vertical" className="h-5" />
          <p className="text-sm font-semibold">{currentPage ? t(currentPage) : session.data.tenantName}</p>
          <div className="flex-1" />
          <TimerChip />
          <NotificationBell />
        </header>
        <main id="main" className="flex-1 px-4 py-6 md:px-8 md:py-8">
          <div className="mx-auto w-full max-w-[1280px]">{children}</div>
        </main>
      </SidebarInset>
    </SidebarProvider>
  );
}
