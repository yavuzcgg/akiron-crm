"use client";

import { Languages, LogOut, Monitor, Moon, Sun } from "lucide-react";
import { useTheme } from "next-themes";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
} from "@/components/ui/dropdown-menu";
import { DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { RoleName } from "@/features/identity/role-name";
import { useLogout, type Session } from "@/features/identity/session";
import { initials } from "@/lib/format";
import { locales, useI18n, type Locale } from "@/lib/i18n";

const localeNames: Record<Locale, string> = { tr: "Türkçe", en: "English" };

export function UserMenu({ session }: { session: Session }) {
  const { t, locale, setLocale } = useI18n();
  const { theme, setTheme } = useTheme();
  const logout = useLogout();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger
        render={
          <Button variant="ghost" className="h-9 gap-2 px-2" aria-label={session.fullName}>
            <Avatar className="size-7">
              <AvatarFallback className="text-xs">{initials(session.fullName)}</AvatarFallback>
            </Avatar>
            <span className="hidden text-sm font-medium sm:inline">{session.fullName}</span>
          </Button>
        }
      />
      <DropdownMenuContent align="end" className="w-60">
        <DropdownMenuGroup>
          <DropdownMenuLabel>
            <div className="grid gap-0.5">
              <span className="text-foreground truncate font-medium">{session.fullName}</span>
              <span className="text-muted-foreground truncate text-xs font-normal">{session.email}</span>
              <span className="text-muted-foreground text-xs font-normal">
                <RoleName role={session.role} /> · {session.tenantName}
              </span>
            </div>
          </DropdownMenuLabel>
        </DropdownMenuGroup>
        <DropdownMenuSeparator />
        <DropdownMenuGroup>
          <DropdownMenuLabel className="flex items-center gap-2">
            <Sun className="size-3.5" /> {t("user.menu.theme")}
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
          <DropdownMenuLabel className="flex items-center gap-2">
            <Languages className="size-3.5" /> {t("user.menu.language")}
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
        <DropdownMenuItem onClick={() => logout.mutate()} disabled={logout.isPending}>
          <LogOut /> {t("auth.logout")}
        </DropdownMenuItem>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
