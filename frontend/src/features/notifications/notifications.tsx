"use client";

import { HubConnectionBuilder, HubConnectionState, LogLevel } from "@microsoft/signalr";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { AtSign, Bell, BriefcaseBusiness, CalendarCheck2, Palmtree, UserPlus, type LucideIcon } from "lucide-react";
import Link from "next/link";
import { useEffect } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { roleLabel } from "@/features/identity/role-name";
import { api, type Schemas } from "@/lib/api/client";
import { unwrap } from "@/lib/api/errors";
import { formatCalendarDate, formatDateTime, formatNumber } from "@/lib/format";
import { useI18n, type TranslationKey } from "@/lib/i18n";
import type { TranslationParams } from "@/lib/i18n/translate";
import { cn } from "@/lib/utils";

type NotificationItem = Schemas["NotificationResponse"];
type Translate = (key: TranslationKey, params?: TranslationParams) => string;

const notificationsKey = ["notifications"] as const;

const text = (value: unknown) => (typeof value === "string" ? value : "");

/** How each notification type reads; unknown types (a newer server) fall back to a generic line. */
interface NotificationType {
  icon: LucideIcon;
  message: (payload: Record<string, unknown>, t: Translate) => string;
  /** The page the notification is about, when it has one. */
  href?: (payload: Record<string, unknown>) => string | undefined;
}

const notificationTypes: Record<string, NotificationType> = {
  "identity.invitation.accepted": {
    icon: UserPlus,
    message: (payload, t) =>
      t("notifications.invitationAccepted", { member: text(payload.memberName), role: roleLabel(text(payload.role), t) }),
    href: () => "/settings/team",
  },
  "jobs.work_order.assigned": {
    icon: BriefcaseBusiness,
    message: (payload, t) =>
      t("notifications.workOrderAssigned", {
        actor: text(payload.assignedByName) || t("timeline.actor.system"),
        number: text(payload.number),
        title: text(payload.title),
      }),
    href: (payload) => (typeof payload.workOrderId === "string" ? `/jobs/${payload.workOrderId}` : undefined),
  },
  "people.leave.requested": {
    icon: Palmtree,
    message: (payload, t) =>
      t("notifications.leaveRequested", { name: text(payload.userName), days: formatNumber(Number(payload.days ?? 0)), start: calendar(payload.startDate) }),
    href: () => "/people/leave",
  },
  "people.leave.decided": {
    icon: CalendarCheck2,
    message: (payload, t) =>
      t(payload.approved ? "notifications.leaveApproved" : "notifications.leaveRejected", {
        actor: text(payload.decidedByName),
        start: calendar(payload.startDate),
      }),
    href: () => "/people/leave",
  },
  "timeline.note.mentioned": {
    icon: AtSign,
    message: (payload, t) => t("notifications.mentioned", { actor: text(payload.authorName), excerpt: text(payload.excerpt) }),
    href: (payload) => subjectHref(text(payload.subjectType), text(payload.subjectId)),
  },
};

const calendar = (value: unknown) => (typeof value === "string" && value ? formatCalendarDate(value) : "");

/** The page of the record a note was written on. */
function subjectHref(subjectType: string, subjectId: string): string | undefined {
  if (subjectType === "work_order") return `/jobs/${subjectId}`;
  if (subjectType === "party") return `/crm/parties/${subjectId}`;
  if (subjectType === "workspace") return "/dashboard";
  return undefined;
}

function describe(item: NotificationItem, t: Translate) {
  const type = notificationTypes[item.type];
  const payload = (item.payload ?? {}) as Record<string, unknown>;
  return {
    icon: type?.icon ?? Bell,
    message: type ? type.message(payload, t) : t("notifications.unknown"),
    href: type?.href?.(payload),
  };
}

export function useNotifications() {
  return useQuery({
    queryKey: notificationsKey,
    queryFn: async () => unwrap(await api.GET("/api/v1/notifications")),
    // SignalR pushes new ones; this only catches up after a lost connection.
    refetchInterval: 120_000,
  });
}

/**
 * Keeps one SignalR connection open while the app shell is mounted. New notifications refresh the
 * bell and show a toast; if the connection cannot be made, polling above still works.
 */
export function useRealtimeNotifications(enabled: boolean) {
  const queryClient = useQueryClient();
  const { t } = useI18n();

  useEffect(() => {
    if (!enabled) return;

    const connection = new HubConnectionBuilder()
      .withUrl("/api/v1/notifications/hub", { withCredentials: true })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on("notification", (item: NotificationItem) => {
      void queryClient.invalidateQueries({ queryKey: notificationsKey });
      void queryClient.invalidateQueries({ queryKey: ["timeline"] });
      toast.info(describe(item, t).message);
    });

    const starting = connection.start().catch(() => {
      // Offline or blocked (proxies that drop long polling); the bell keeps polling.
    });

    // Stop only after start settles: stopping mid-negotiation (React re-running effects in
    // development, fast navigation) makes SignalR log an error for a normal teardown.
    return () => {
      void starting.then(() => {
        if (connection.state !== HubConnectionState.Disconnected) return connection.stop();
      });
    };
    // The translator only formats the toast; reconnecting when the language changes is not needed.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [enabled, queryClient]);
}

export function NotificationBell() {
  const { t } = useI18n();
  const queryClient = useQueryClient();
  const notifications = useNotifications();
  const unread = notifications.data?.unreadCount ?? 0;

  const markAllRead = useMutation({
    mutationFn: async () => {
      unwrap(await api.POST("/api/v1/notifications/read-all"));
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: notificationsKey }),
  });

  const markRead = useMutation({
    mutationFn: async (id: string) => {
      unwrap(await api.POST("/api/v1/notifications/{id}/read", { params: { path: { id } } }));
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: notificationsKey }),
  });

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button variant="ghost" size="icon" className="relative" aria-label={t("notifications.open")}>
            <Bell />
            {unread > 0 ? (
              <span className="bg-destructive absolute -top-0.5 -right-0.5 flex min-w-4 items-center justify-center rounded-full px-1 text-[10px] font-semibold text-white">
                {unread > 9 ? "9+" : unread}
              </span>
            ) : null}
          </Button>
        }
      />
      <DropdownMenuContent align="end" className="w-80">
        <DropdownMenuGroup>
          <DropdownMenuLabel className="flex items-center justify-between">
            <span className="text-foreground font-medium">{t("notifications.title")}</span>
            {unread > 0 ? (
              <Button variant="link" size="xs" onClick={() => markAllRead.mutate()} disabled={markAllRead.isPending}>
                {t("notifications.markAllRead")}
              </Button>
            ) : null}
          </DropdownMenuLabel>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        {notifications.data?.items.length ? (
          <ul className="max-h-96 overflow-y-auto">
            {notifications.data.items.map((item) => {
              const { icon: Icon, message, href } = describe(item, t);
              return (
                <li key={item.id} className={cn("hover:bg-muted/60 relative flex gap-3 rounded-md px-2 py-2.5 text-sm", !item.readAt && "bg-primary/5")}>
                  <Icon className="text-muted-foreground mt-0.5 size-4 shrink-0" />
                  <div className="grid gap-0.5">
                    {href ? (
                      <Link href={href} className="after:absolute after:inset-0" onClick={() => !item.readAt && markRead.mutate(item.id)}>
                        {message}
                      </Link>
                    ) : (
                      <p>{message}</p>
                    )}
                    <time className="text-muted-foreground text-xs" dateTime={item.createdAt}>
                      {formatDateTime(item.createdAt)}
                    </time>
                  </div>
                </li>
              );
            })}
          </ul>
        ) : (
          <p className="text-muted-foreground px-2 py-6 text-center text-sm">{t("notifications.empty")}</p>
        )}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
