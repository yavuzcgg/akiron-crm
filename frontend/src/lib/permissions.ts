/** Mirrors the API's permission check: an exact grant or `*` (owner). Only for showing or hiding UI; the API enforces. */
export function hasPermission(granted: readonly string[] | undefined, permission: string): boolean {
  return !!granted && (granted.includes("*") || granted.includes(permission));
}

export const permissions = {
  identity: {
    membersRead: "identity.members.read",
    membersManage: "identity.members.manage",
    tenantManage: "identity.tenant.manage",
  },
  files: {
    read: "files.read",
    write: "files.write",
  },
  sales: {
    quotesRead: "sales.quotes.read",
    quotesWrite: "sales.quotes.write",
    catalogManage: "sales.catalog.manage",
  },
  people: {
    read: "people.read",
    manage: "people.manage",
    costsRead: "people.costs.read",
    leaveRequest: "people.leave.request",
    leaveApprove: "people.leave.approve",
  },
  jobs: {
    workOrdersRead: "jobs.work_orders.read",
    workOrdersWrite: "jobs.work_orders.write",
    stagesManage: "jobs.stages.manage",
    timeWrite: "jobs.time.write",
    timeReadAll: "jobs.time.read_all",
    financials: "jobs.financials",
    templatesManage: "jobs.templates.manage",
  },
  crm: {
    partiesRead: "crm.parties.read",
    partiesWrite: "crm.parties.write",
    customFieldsManage: "crm.custom_fields.manage",
  },
  timeline: {
    read: "timeline.read",
    notesWrite: "timeline.notes.write",
  },
} as const;
