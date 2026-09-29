"use client";

import {
  BriefcaseBusiness,
  CalendarDays,
  FileText,
  LayoutDashboard,
  Users,
  UsersRound,
  Wallet,
  type LucideIcon,
} from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import {
  Sidebar,
  SidebarContent,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
} from "@/components/ui/sidebar";
import type { Session } from "@/features/identity/session";
import { useI18n, type TranslationKey } from "@/lib/i18n";
import { hasPermission, permissions } from "@/lib/permissions";

interface NavItem {
  label: TranslationKey;
  icon: LucideIcon;
  href?: string;
  permission?: string;
}

/** Modules without an href are on the roadmap; they are shown so the product's shape is visible from day one. */
const workItems: NavItem[] = [
  { label: "nav.dashboard", icon: LayoutDashboard, href: "/dashboard" },
  { label: "nav.customers", icon: UsersRound },
  { label: "nav.jobs", icon: BriefcaseBusiness },
  { label: "nav.quotes", icon: FileText },
  { label: "nav.finance", icon: Wallet },
  { label: "nav.content", icon: CalendarDays },
];

const settingsItems: NavItem[] = [
  { label: "nav.team", icon: Users, href: "/settings/team", permission: permissions.identity.membersRead },
];

export function AppSidebar({ session }: { session: Session }) {
  const { t } = useI18n();
  const pathname = usePathname();

  const renderItems = (items: NavItem[]) =>
    items
      .filter((item) => !item.permission || hasPermission(session.permissions, item.permission))
      .map((item) => (
        <SidebarMenuItem key={item.label}>
          {item.href ? (
            <SidebarMenuButton isActive={pathname.startsWith(item.href)} tooltip={t(item.label)} render={<Link href={item.href} />}>
              <item.icon />
              <span>{t(item.label)}</span>
            </SidebarMenuButton>
          ) : (
            <SidebarMenuButton disabled tooltip={t(item.label)} className="opacity-60">
              <item.icon />
              <span>{t(item.label)}</span>
              <Badge variant="outline" className="ml-auto text-[10px]">
                {t("common.soon")}
              </Badge>
            </SidebarMenuButton>
          )}
        </SidebarMenuItem>
      ));

  return (
    <Sidebar collapsible="icon">
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton size="lg" render={<Link href="/dashboard" />}>
              <div className="bg-primary text-primary-foreground flex aspect-square size-8 items-center justify-center rounded-lg font-semibold">
                A
              </div>
              <div className="grid flex-1 text-left text-sm leading-tight">
                <span className="truncate font-semibold">{session.tenantName}</span>
                <span className="text-muted-foreground truncate text-xs">{t("app.name")}</span>
              </div>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>
      <SidebarContent>
        <SidebarGroup>
          <SidebarGroupLabel>{t("nav.section.work")}</SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu>{renderItems(workItems)}</SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
        <SidebarGroup>
          <SidebarGroupLabel>{t("nav.section.settings")}</SidebarGroupLabel>
          <SidebarGroupContent>
            <SidebarMenu>{renderItems(settingsItems)}</SidebarMenu>
          </SidebarGroupContent>
        </SidebarGroup>
      </SidebarContent>
    </Sidebar>
  );
}
