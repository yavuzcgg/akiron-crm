import {
  BriefcaseBusiness,
  Building2,
  CalendarDays,
  FileText,
  LayoutDashboard,
  UserRound,
  Users,
  UsersRound,
  Wallet,
  type LucideIcon,
} from "lucide-react";
import type { TranslationKey } from "@/lib/i18n";
import { permissions } from "@/lib/permissions";

export interface NavItem {
  label: TranslationKey;
  icon: LucideIcon;
  /** Missing while the module is on the roadmap; the item shows as "soon". */
  href?: string;
  permission?: string;
}

export interface NavSection {
  label: TranslationKey;
  items: NavItem[];
}

/**
 * The product's shape, in the order an agency works: workspace, selling, delivering, getting paid.
 * Roadmap modules stay visible (disabled) so the product's scope is clear from day one.
 */
export const navigation: NavSection[] = [
  {
    label: "nav.section.work",
    items: [{ label: "nav.dashboard", icon: LayoutDashboard, href: "/dashboard" }],
  },
  {
    label: "nav.section.sales",
    items: [
      { label: "nav.customers", icon: UsersRound, href: "/crm/parties", permission: permissions.crm.partiesRead },
      { label: "nav.quotes", icon: FileText },
    ],
  },
  {
    label: "nav.section.operations",
    items: [
      { label: "nav.jobs", icon: BriefcaseBusiness },
      { label: "nav.content", icon: CalendarDays },
    ],
  },
  {
    label: "nav.section.finance",
    items: [{ label: "nav.finance", icon: Wallet }],
  },
  {
    label: "nav.section.settings",
    items: [
      { label: "nav.account", icon: UserRound, href: "/settings/account" },
      { label: "nav.workspace", icon: Building2, href: "/settings/workspace", permission: permissions.identity.tenantManage },
      { label: "nav.team", icon: Users, href: "/settings/team", permission: permissions.identity.membersRead },
    ],
  },
];

/** The nav label of the page at <paramref name="pathname"/>, for the header. */
export function pageLabel(pathname: string): TranslationKey | null {
  for (const section of navigation) {
    for (const item of section.items) {
      if (item.href && pathname.startsWith(item.href)) return item.label;
    }
  }
  return null;
}
