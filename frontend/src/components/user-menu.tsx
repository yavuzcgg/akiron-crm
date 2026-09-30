"use client";

import { ChevronsUpDown, Languages, LogOut, Monitor, Moon, Sun, UserRound } from "lucide-react";
import Link from "next/link";
import { useTheme } from "next-themes";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { SidebarMenuButton } from "@/components/ui/sidebar";
import { RoleName } from "@/features/identity/role-name";
import { useLogout, type Session } from "@/features/identity/session";
import { initials } from "@/lib/format";
import { locales, useI18n, type Locale } from "@/lib/i18n";

const localeNames: Record<Locale, string> = { tr: "Türkçe", en: "English" };

/** The signed-in person at the foot of the sidebar: preferences and sign-out live here. */
export function UserMenu({ session }: { session: Session }) {
  const { t, locale, setLocale } = useI18n();
  const { theme, setTheme } = useTheme();
  const logout = useLogout();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <SidebarMenuButton size="lg" className="data-popup-open:bg-muted h-12">
            <Avatar className="size-8 rounded-lg">
              <AvatarFallback className="bg-primary-soft text-primary-strong rounded-lg text-xs font-semibold">
                {initials(session.fullName)}
              </AvatarFallback>
            </Avatar>
            <span className="grid flex-1 text-left leading-tight">
              <span className="truncate text-sm font-semibold">{session.fullName}</span>
              <span className="text-muted-foreground truncate text-xs">
                <RoleName role={session.role} />
              </span>
            </span>
            <ChevronsUpDown className="text-muted-foreground ml-auto size-4" />
          </SidebarMenuButton>
        }
      />
      <DropdownMenuContent side="top" align="start" className="w-64">
        <DropdownMenuGroup>
          <DropdownMenuLabel>
            <div className="grid gap-0.5">
              <span className="text-foreground truncate font-semibold">{session.fullName}</span>
              <span className="text-muted-foreground truncate text-xs font-normal">{session.email}</span>
              <span className="text-muted-foreground truncate text-xs font-normal">{session.tenantName}</span>
            </div>
          </DropdownMenuLabel>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        <DropdownMenuItem render={<Link href="/settings/account" />}>
          <UserRound /> {t("nav.account")}
        </DropdownMenuItem>
        <DropdownMenuSeparator />
        <DropdownMenuGroup>
          <DropdownMenuLabel className="flex items-center gap-2 text-xs">
            <Sun className="size-3.5" aria-hidden /> {t("user.menu.theme")}
          </DropdownMenuLabel>
          <DropdownMenuRadioGroup value={theme ?? "system"} onValueChange={(value) => setTheme(String(value))}>
            <DropdownMenuRadioItem value="light">
              <Sun /> {t("user.menu.theme.light")}
            </DropdownMenuRadioItem>
            <DropdownMenuRadioItem value="dark">
              <Moon /> {t("user.menu.theme.dark")}
            </DropdownMenuRadioItem>
            <DropdownMenuRadioItem value="system">
              <Monitor /> {t("user.menu.theme.system")}
            </DropdownMenuRadioItem>
          </DropdownMenuRadioGroup>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        <DropdownMenuGroup>
          <DropdownMenuLabel className="flex items-center gap-2 text-xs">
            <Languages className="size-3.5" aria-hidden /> {t("user.menu.language")}
          </DropdownMenuLabel>
          <DropdownMenuRadioGroup value={locale} onValueChange={(value) => setLocale(value as Locale)}>
            {locales.map((option) => (
              <DropdownMenuRadioItem key={option} value={option}>
                {localeNames[option]}
              </DropdownMenuRadioItem>
            ))}
          </DropdownMenuRadioGroup>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        <DropdownMenuItem variant="destructive" onClick={() => logout.mutate()} disabled={logout.isPending}>
          <LogOut /> {t("auth.logout")}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
