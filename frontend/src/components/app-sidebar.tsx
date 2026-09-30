"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import { Logo } from "@/components/brand/logo";
import { navigation, pageLabel } from "@/components/navigation";
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarRail,
} from "@/components/ui/sidebar";
import { UserMenu } from "@/components/user-menu";
import type { Session } from "@/features/identity/session";
import { useI18n } from "@/lib/i18n";
import { hasPermission } from "@/lib/permissions";

export function AppSidebar({ session }: { session: Session }) {
  const { t } = useI18n();
  const pathname = usePathname();
  // One active item: the longest matching link (/jobs/time is not also /jobs).
  const activeLabel = pageLabel(pathname);

  return (
    <Sidebar collapsible="icon">
      <SidebarHeader className="h-14 justify-center border-b px-3">
        <Link href="/dashboard" className="rounded-lg outline-none" aria-label={t("app.name")}>
          <Logo subtitle={session.tenantName} />
        </Link>
      </SidebarHeader>

      <SidebarContent className="gap-0 py-2">
        {navigation.map((section) => {
          const items = section.items.filter((item) => !item.permission || hasPermission(session.permissions, item.permission));
          if (items.length === 0) return null;

          return (
            <SidebarGroup key={section.label} className="py-1.5">
              <SidebarGroupLabel className="text-muted-foreground px-2.5 text-[11px] font-semibold tracking-wider uppercase">
                {t(section.label)}
              </SidebarGroupLabel>
              <SidebarGroupContent>
                <SidebarMenu className="gap-0.5">
                  {items.map((item) => (
                    <SidebarMenuItem key={item.label}>
                      {item.href ? (
                        <SidebarMenuButton
                          isActive={activeLabel === item.label}
                          tooltip={t(item.label)}
                          render={<Link href={item.href} />}
                        >
                          <item.icon />
                          <span>{t(item.label)}</span>
                        </SidebarMenuButton>
                      ) : (
                        // Still readable at full contrast: the "soon" tag says why it cannot be opened.
                        <SidebarMenuButton
                          aria-disabled
                          aria-description={t("nav.soonHint")}
                          className="text-muted-foreground aria-disabled:opacity-100"
                        >
                          <item.icon />
                          <span className="flex-1">{t(item.label)}</span>
                          <span className="border-border text-muted-foreground rounded-md border px-1.5 py-px text-[10px] font-medium group-data-[collapsible=icon]:hidden">
                            {t("common.soon")}
                          </span>
                        </SidebarMenuButton>
                      )}
                    </SidebarMenuItem>
                  ))}
                </SidebarMenu>
              </SidebarGroupContent>
            </SidebarGroup>
          );
        })}
      </SidebarContent>

      <SidebarFooter className="border-t p-2">
        <UserMenu session={session} />
      </SidebarFooter>
      <SidebarRail />
    </Sidebar>
  );
}
