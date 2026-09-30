"use client";

import { ArrowDown, ArrowUp, Plus, Trash2 } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { ApiError } from "@/lib/api/errors";
import { useI18n } from "@/lib/i18n";
import { useStageMutations, useStages, type Stage } from "./jobs-api";
import { categoryLabels, stageLabel } from "./labels";

const categories = ["open", "active", "done"] as const;

const selectClass =
  "border-input bg-card focus-visible:border-ring focus-visible:ring-ring/25 h-9 rounded-lg border px-2 text-sm outline-none focus-visible:ring-3";

function StageRow({ stage, index, count, onMove }: { stage: Stage; index: number; count: number; onMove: (to: number) => void }) {
  const { t, tError } = useI18n();
  const { update, remove } = useStageMutations();
  const [name, setName] = useState(stage.name ?? "");
  const [category, setCategory] = useState(stage.category);
  const dirty = name.trim() !== (stage.name ?? "") || category !== stage.category;
  const fail = (error: unknown) => toast.error(tError(error instanceof ApiError ? error.code : "common.network"));

  const save = () =>
    update.mutate(
      // An untouched built-in stage keeps its translated label; saving writes it as the tenant's own name.
      { id: stage.id, name: name.trim() || stageLabel(stage, t), category },
      { onSuccess: () => toast.success(t("jobs.stages.saved")), onError: fail },
    );

  return (
    <li className="flex flex-wrap items-center gap-2 py-2">
      <div className="flex flex-col">
        <Button variant="ghost" size="icon-xs" disabled={index === 0} onClick={() => onMove(index - 1)} aria-label={t("jobs.stages.up", { stage: stageLabel(stage, t) })}>
          <ArrowUp />
        </Button>
        <Button variant="ghost" size="icon-xs" disabled={index === count - 1} onClick={() => onMove(index + 1)} aria-label={t("jobs.stages.down", { stage: stageLabel(stage, t) })}>
          <ArrowDown />
        </Button>
      </div>
      <Input
        className="h-9 min-w-40 flex-1"
        value={name}
        placeholder={stageLabel(stage, t)}
        onChange={(event) => setName(event.target.value)}
        maxLength={60}
        aria-label={t("jobs.stages.name")}
      />
      <select className={selectClass} value={category} onChange={(event) => setCategory(event.target.value)} aria-label={t("jobs.stages.category")}>
        {categories.map((option) => (
          <option key={option} value={option}>
            {t(categoryLabels[option]!)}
          </option>
        ))}
      </select>
      {dirty ? (
        <Button size="sm" onClick={save} disabled={update.isPending}>
          {t("common.save")}
        </Button>
      ) : null}
      <Button
        variant="ghost"
        size="icon-sm"
        onClick={() => {
          if (window.confirm(t("jobs.stages.removeConfirm", { stage: stageLabel(stage, t) }))) remove.mutate(stage.id, { onError: fail });
        }}
        disabled={remove.isPending}
        aria-label={t("jobs.stages.remove", { stage: stageLabel(stage, t) })}
      >
        <Trash2 />
      </Button>
    </li>
  );
}

/** The tenant's workflow: rename, recategorise, reorder, add and remove board stages. */
export function StageManager() {
  const { t, tError } = useI18n();
  const stages = useStages();
  const { create, reorder } = useStageMutations();
  const [name, setName] = useState("");
  const [category, setCategory] = useState<string>("active");

  if (stages.isPending) return <Skeleton className="h-48 w-full" />;
  const list = stages.data ?? [];

  const moveStage = (from: number, to: number) => {
    const ids = list.map((stage) => stage.id);
    const [moved] = ids.splice(from, 1);
    ids.splice(to, 0, moved!);
    reorder.mutate(ids, { onError: (error) => toast.error(tError(error instanceof ApiError ? error.code : "common.network")) });
  };

  return (
    <div className="grid gap-4">
      <ul className="divide-border divide-y">
        {list.map((stage, index) => (
          <StageRow key={`${stage.id}:${stage.name ?? ""}:${stage.category}`} stage={stage} index={index} count={list.length} onMove={(to) => moveStage(index, to)} />
        ))}
      </ul>
      <form
        className="bg-muted/50 flex flex-wrap items-center gap-2 rounded-lg p-3"
        onSubmit={(event) => {
          event.preventDefault();
          if (!name.trim()) return;
          create.mutate(
            { name: name.trim(), category },
            {
              onSuccess: () => setName(""),
              onError: (error) => toast.error(tError(error instanceof ApiError ? error.code : "common.network")),
            },
          );
        }}
      >
        <Input className="h-9 min-w-40 flex-1" value={name} onChange={(event) => setName(event.target.value)} placeholder={t("jobs.stages.newPlaceholder")} maxLength={60} aria-label={t("jobs.stages.name")} />
        <select className={selectClass} value={category} onChange={(event) => setCategory(event.target.value)} aria-label={t("jobs.stages.category")}>
          {categories.map((option) => (
            <option key={option} value={option}>
              {t(categoryLabels[option]!)}
            </option>
          ))}
        </select>
        <Button type="submit" size="sm" disabled={create.isPending || !name.trim()}>
          <Plus /> {t("jobs.stages.add")}
        </Button>
      </form>
      <p className="text-muted-foreground text-[13px]">{t("jobs.stages.hint")}</p>
    </div>
  );
}
