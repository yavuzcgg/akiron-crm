/** Mirrors the API's permission check: an exact grant or `*` (owner). Only for showing or hiding UI; the API enforces. */
export function hasPermission(granted: readonly string[] | undefined, permission: string): boolean {
  return !!granted && (granted.includes("*") || granted.includes(permission));
}

export const permissions = {
  identity: {
    membersRead: "identity.members.read",
    membersManage: "identity.members.manage",
  },
} as const;
