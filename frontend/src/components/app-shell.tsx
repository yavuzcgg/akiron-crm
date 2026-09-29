"use client";

import { useQueryClient } from "@tanstack/react-query";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, type ReactNode } from "react";
import { toast } from "sonner";
import { AppSidebar } from "@/components/app-sidebar";
import { Skeleton } from "@/components/ui/skeleton";
import { Separator } from "@/components/ui/separator";
import { SidebarInset, SidebarProvider, SidebarTrigger } from "@/components/ui/sidebar";
import { UserMenu } from "@/components/user-menu";
import { useSession } from "@/features/identity/session";
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
        <header className="bg-background/80 sticky top-0 z-10 flex h-14 items-center gap-2 border-b px-4 backdrop-blur">
          <SidebarTrigger className="-ml-1" />
          <Separator orientation="vertical" className="mr-2 h-4" />
          <div className="flex-1" />
          <NotificationBell />
          <UserMenu session={session.data} />
        </header>
        <main className="flex-1 p-4 md:p-6">{children}</main>
      </SidebarInset>
    </SidebarProvider>
  );
}
