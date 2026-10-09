"use client";

import {
  closestCorners,
  DndContext,
  DragOverlay,
  KeyboardSensor,
  PointerSensor,
  useDroppable,
  useSensor,
  useSensors,
  type DragEndEvent,
  type DragOverEvent,
  type DragStartEvent,
} from "@dnd-kit/core";
import { arrayMove, SortableContext, sortableKeyboardCoordinates, useSortable, verticalListSortingStrategy } from "@dnd-kit/sortable";
import { CSS } from "@dnd-kit/utilities";
import { ArrowRightLeft, CalendarClock, MoreHorizontal, Plus, Trophy, XCircle } from "lucide-react";
import Link from "next/link";
import { useMemo, useState } from "react";
import { toast } from "sonner";
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
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { ApiError } from "@/lib/api/errors";
import { dayKey, formatCalendarDate, formatMoney, initials } from "@/lib/format";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { stageLabels, stageTone, sourceLabels } from "./labels";
import { openStages, pipelineTotals, useLeadMutations, type Lead } from "./leads-api";

type Columns = Record<string, string[]>;

export function LeadCardBody({ lead }: { lead: Lead }) {
  const { t } = useI18n();
  const [today] = useState(() => dayKey(new Date()));
  const late = lead.nextStepDate !== null && lead.nextStepDate !== undefined && lead.nextStepDate <= today;
  const who = lead.partyName ?? lead.companyName ?? lead.contactName;
  return (
    <div className="grid gap-1.5">
      <p className="pr-6 text-sm leading-snug font-medium">{lead.title}</p>
      {who ? <p className="text-muted-foreground truncate text-xs">{who}</p> : null}
      <div className="flex flex-wrap items-center gap-x-2 gap-y-1 text-xs">
        {lead.estimatedValue ? <span className="font-medium tabular-nums">{formatMoney(lead.estimatedValue, lead.currency)}</span> : null}
        <span className="text-muted-foreground">{t(sourceLabels[lead.source] ?? "leads.source.other")}</span>
        {lead.ownerName ? (
          <span className="bg-primary-soft text-primary-strong ml-auto flex size-5 items-center justify-center rounded-full text-[9px] font-semibold" title={lead.ownerName}>
            {initials(lead.ownerName)}
          </span>
        ) : null}
      </div>
      {lead.nextStepDate ? (
        <p className={cn("flex items-center gap-1 text-xs", late ? "text-destructive font-medium" : "text-muted-foreground")}>
          <CalendarClock className="size-3.5" aria-hidden />
          {formatCalendarDate(lead.nextStepDate)}
          {lead.nextStep ? ` · ${lead.nextStep}` : ""}
        </p>
      ) : null}
    </div>
  );
}

function Card({ lead, canWrite, onMove, onClose }: { lead: Lead; canWrite: boolean; onMove: (stage: string) => void; onClose: (won: boolean) => void }) {
  const { t } = useI18n();
  const { setNodeRef, transform, transition, isDragging, attributes, listeners } = useSortable({ id: lead.id, disabled: !canWrite });

  return (
    <li
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform), transition }}
      className={cn("group bg-card relative rounded-lg border p-3 shadow-xs transition-colors", "hover:border-primary/40 focus-within:border-primary/40", isDragging && "opacity-40")}
      {...attributes}
      {...listeners}
    >
      <Link
        href={`/crm/leads/${lead.id}`}
        className="focus-visible:ring-ring/40 absolute inset-0 rounded-lg focus-visible:ring-2 focus-visible:outline-none"
        aria-label={t("leads.card.open", { title: lead.title })}
      />
      <div className="pointer-events-none relative">
        <LeadCardBody lead={lead} />
      </div>
      {canWrite ? (
        <DropdownMenu>
          <DropdownMenuTrigger
            render={
              <Button
                variant="ghost"
                size="icon-xs"
                className="bg-card absolute top-2 right-2 opacity-0 group-hover:opacity-100 focus-visible:opacity-100 data-popup-open:opacity-100"
                aria-label={t("leads.card.actions", { title: lead.title })}
                onPointerDown={(event) => event.stopPropagation()}
                onKeyDown={(event) => event.stopPropagation()}
              >
                <MoreHorizontal />
              </Button>
            }
          />
          <DropdownMenuContent align="end" className="w-52">
            <DropdownMenuGroup>
              <DropdownMenuLabel className="flex items-center gap-2 text-xs">
                <ArrowRightLeft className="size-3.5" aria-hidden /> {t("leads.card.moveTo")}
              </DropdownMenuLabel>
              <DropdownMenuRadioGroup value={lead.stage} onValueChange={(value) => onMove(String(value))}>
                {openStages.map((stage) => (
                  <DropdownMenuRadioItem key={stage} value={stage}>
                    {t(stageLabels[stage]!)}
                  </DropdownMenuRadioItem>
                ))}
              </DropdownMenuRadioGroup>
            </DropdownMenuGroup>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => onClose(true)}>
              <Trophy /> {t("leads.action.won")}
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => onClose(false)}>
              <XCircle /> {t("leads.action.lost")}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      ) : null}
    </li>
  );
}

function Column({
  stage,
  leads,
  canWrite,
  onAdd,
  onMove,
  onClose,
}: {
  stage: string;
  leads: Lead[];
  canWrite: boolean;
  onAdd: () => void;
  onMove: (lead: Lead, stage: string) => void;
  onClose: (lead: Lead, won: boolean) => void;
}) {
  const { t } = useI18n();
  const { setNodeRef, isOver } = useDroppable({ id: stage });
  const label = t(stageLabels[stage]!);
  const totals = pipelineTotals(leads);

  return (
    <section aria-label={label} className={cn("bg-muted/50 flex w-[272px] shrink-0 flex-col rounded-xl border border-transparent", isOver && "border-primary/30 bg-primary/5")}>
      <header className="grid gap-0.5 px-3 pt-3 pb-2">
        <div className="flex items-center gap-2">
          <span className={cn("size-2 rounded-full", stageTone[stage])} aria-hidden />
          <h2 className="truncate text-sm font-semibold">{label}</h2>
          <span className="text-muted-foreground text-xs tabular-nums">{leads.length}</span>
          {canWrite && stage === "new" ? (
            <Button variant="ghost" size="icon-xs" className="ml-auto" onClick={onAdd} aria-label={t("leads.new")}>
              <Plus />
            </Button>
          ) : null}
        </div>
        {totals.length > 0 ? <p className="text-muted-foreground text-xs tabular-nums">{totals.map((total) => formatMoney(total.amount, total.currency)).join(" · ")}</p> : null}
      </header>
      <SortableContext items={leads.map((lead) => lead.id)} strategy={verticalListSortingStrategy}>
        <ul ref={setNodeRef} className="flex min-h-24 flex-1 flex-col gap-2 px-2 pb-3">
          {leads.map((lead) => (
            <Card key={lead.id} lead={lead} canWrite={canWrite} onMove={(target) => onMove(lead, target)} onClose={(won) => onClose(lead, won)} />
          ))}
          {leads.length === 0 ? <li className="text-muted-foreground rounded-lg border border-dashed px-3 py-6 text-center text-xs">{t("leads.board.emptyColumn")}</li> : null}
        </ul>
      </SortableContext>
    </section>
  );
}

/** Asks why a lead was lost; null when the person cancels. */
export function askLostReason(t: (key: "leads.lostPrompt", params: { title: string }) => string, title: string): string | null {
  const reason = window.prompt(t("leads.lostPrompt", { title }));
  return reason?.trim() ? reason.trim() : null;
}

/**
 * The funnel: four open columns, cards drag between and within them (mouse, touch or keyboard);
 * each card's menu moves it too and closes it as won or lost, so dragging is never the only way.
 */
export function LeadBoard({ leads, updatedAt, canWrite, onAdd }: { leads: Lead[]; updatedAt: number; canWrite: boolean; onAdd: () => void }) {
  const { t, tError } = useI18n();
  const { move } = useLeadMutations();
  const [activeId, setActiveId] = useState<string | null>(null);
  const [draft, setDraft] = useState<{ basedOn: number; columns: Columns } | null>(null);

  const sensors = useSensors(useSensor(PointerSensor, { activationConstraint: { distance: 6 } }), useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }));

  const byId = useMemo(() => new Map(leads.map((lead) => [lead.id, lead])), [leads]);
  const serverColumns = useMemo(() => {
    const columns: Columns = Object.fromEntries(openStages.map((stage) => [stage, [] as string[]]));
    for (const lead of leads) columns[lead.stage]?.push(lead.id);
    return columns;
  }, [leads]);
  const columns = draft && draft.basedOn === updatedAt ? draft.columns : serverColumns;
  const containerOf = (id: string, within: Columns) => (id in within ? id : Object.keys(within).find((stage) => within[stage]!.includes(id)));
  const fail = (error: unknown) => toast.error(tError(error instanceof ApiError ? error.code : "common.network"));

  const commit = (id: string, stage: string, index?: number, lostReason?: string) => move.mutate({ id, stage, index, lostReason }, { onError: fail });

  const close = (lead: Lead, won: boolean) => {
    if (won) {
      commit(lead.id, "won");
      toast.success(t("leads.wonToast", { title: lead.title }));
      return;
    }
    const reason = askLostReason(t, lead.title);
    if (reason) commit(lead.id, "lost", undefined, reason);
  };

  const onDragStart = ({ active }: DragStartEvent) => {
    setActiveId(String(active.id));
    setDraft({ basedOn: updatedAt, columns: structuredClone(columns) });
  };

  const onDragOver = ({ active, over }: DragOverEvent) => {
    if (!over || !draft) return;
    const from = containerOf(String(active.id), draft.columns);
    const to = containerOf(String(over.id), draft.columns);
    if (!from || !to || from === to) return;
    const next = structuredClone(draft.columns);
    next[from] = next[from]!.filter((id) => id !== active.id);
    const overIndex = next[to]!.indexOf(String(over.id));
    next[to]!.splice(overIndex < 0 ? next[to]!.length : overIndex, 0, String(active.id));
    setDraft({ ...draft, columns: next });
  };

  const onDragEnd = ({ active, over }: DragEndEvent) => {
    setActiveId(null);
    if (!over || !draft) {
      setDraft(null);
      return;
    }
    const id = String(active.id);
    const stage = containerOf(String(over.id), draft.columns);
    if (!stage) return;
    let column = draft.columns[stage]!;
    const oldIndex = column.indexOf(id);
    const newIndex = column.indexOf(String(over.id));
    if (oldIndex >= 0 && newIndex >= 0 && oldIndex !== newIndex) {
      column = arrayMove(column, oldIndex, newIndex);
      setDraft({ ...draft, columns: { ...draft.columns, [stage]: column } });
    }
    const lead = byId.get(id);
    const index = column.indexOf(id);
    if (lead && (lead.stage !== stage || serverColumns[stage]?.indexOf(id) !== index)) commit(id, stage, index);
  };

  const active = activeId ? byId.get(activeId) : undefined;

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCorners}
      onDragStart={onDragStart}
      onDragOver={onDragOver}
      onDragEnd={onDragEnd}
      onDragCancel={() => {
        setActiveId(null);
        setDraft(null);
      }}
      accessibility={{ screenReaderInstructions: { draggable: t("jobs.board.dragInstructions") } }}
    >
      <div className="-mx-4 flex gap-4 overflow-x-auto px-4 pb-4 md:-mx-8 md:px-8">
        {openStages.map((stage) => (
          <Column
            key={stage}
            stage={stage}
            leads={(columns[stage] ?? []).map((id) => byId.get(id)).filter((lead): lead is Lead => !!lead)}
            canWrite={canWrite}
            onAdd={onAdd}
            onMove={(lead, target) => {
              if (target !== lead.stage) commit(lead.id, target);
            }}
            onClose={close}
          />
        ))}
      </div>
      <DragOverlay>
        {active ? (
          <div className="bg-card w-[256px] rotate-1 rounded-lg border p-3 shadow-lg">
            <LeadCardBody lead={active} />
          </div>
        ) : null}
      </DragOverlay>
    </DndContext>
  );
}
