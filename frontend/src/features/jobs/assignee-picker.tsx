"use client";

import { Skeleton } from "@/components/ui/skeleton";
import { initials } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { useAssignableMembers } from "./jobs-api";

interface AssigneePickerProps {
  value: string[];
  onChange: (userIds: string[]) => void;
}

/** A checkbox per teammate; small teams fit without a search box. */
export function AssigneePicker({ value, onChange }: AssigneePickerProps) {
  const { t } = useI18n();
  const members = useAssignableMembers();

  const toggle = (userId: string, checked: boolean) =>
    onChange(checked ? [...value, userId] : value.filter((candidate) => candidate !== userId));

  return (
    <fieldset className="grid gap-1.5">
      <legend className="mb-1.5 text-[13px] font-medium">{t("jobs.field.assignees")}</legend>
      {members.isPending ? (
        <Skeleton className="h-20 w-full" />
      ) : (
        <div className="border-input grid max-h-44 gap-0.5 overflow-y-auto rounded-lg border p-1">
          {(members.data ?? []).map((member) => (
            <label key={member.userId} className="hover:bg-muted/60 flex cursor-pointer items-center gap-2.5 rounded-md px-2 py-1.5 text-sm">
              <input
                type="checkbox"
                className="accent-primary size-4"
                checked={value.includes(member.userId)}
                onChange={(event) => toggle(member.userId, event.target.checked)}
              />
              <span className="bg-primary-soft text-primary-strong flex size-6 items-center justify-center rounded-full text-[10px] font-semibold">
                {initials(member.fullName)}
              </span>
              <span className="truncate">{member.fullName}</span>
            </label>
          ))}
        </div>
      )}
    </fieldset>
  );
}
