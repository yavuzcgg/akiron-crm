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
import { AlertCircle, ArrowRightLeft, MoreHorizontal, Plus } from "lucide-react";
import Link from "next/link";
import { useMemo, useState } from "react";
import { toast } from "sonner";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Skeleton } from "@/components/ui/skeleton";
import { ApiError } from "@/lib/api/errors";
import { useI18n } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { useBoard, useMoveWorkOrder, type BoardFilter, type Stage, type WorkOrderCard } from "./jobs-api";
import { stageLabel } from "./labels";
import { WorkOrderCardBody } from "./work-order-card";

type Columns = Record<string, string[]>;

interface BoardProps {
  filter: BoardFilter;
  canWrite: boolean;
  onAdd: (stageId: string) => void;
}

function Card({ card, stages, canWrite, onMove }: { card: WorkOrderCard; stages: Stage[]; canWrite: boolean; onMove: (stageId: string) => void }) {
  const { t } = useI18n();
  const { setNodeRef, transform, transition, isDragging, attributes, listeners } = useSortable({ id: card.id, disabled: !canWrite });

  return (
    <li
      ref={setNodeRef}
      style={{ transform: CSS.Transform.toString(transform), transition }}
      className={cn(
        "group bg-card relative rounded-lg border p-3 shadow-xs transition-colors",
        "hover:border-primary/40 focus-within:border-primary/40",
        isDragging && "opacity-40",
      )}
      {...attributes}
      {...listeners}
    >
      {/* The link covers the card; the menu sits above it. */}
      <Link
        href={`/jobs/${card.id}`}
        className="focus-visible:ring-ring/40 absolute inset-0 rounded-lg focus-visible:ring-2 focus-visible:outline-none"
        aria-label={t("jobs.card.open", { number: card.number, title: card.title })}
      />
      <div className="pointer-events-none relative">
        <WorkOrderCardBody card={card} />
      </div>
      {canWrite ? (
        <DropdownMenu>
          <DropdownMenuTrigger
            render={
              <Button
                variant="ghost"
                size="icon-xs"
                className="bg-card absolute top-2 right-2 opacity-0 group-hover:opacity-100 focus-visible:opacity-100 data-popup-open:opacity-100"
                aria-label={t("jobs.card.moveTo", { number: card.number })}
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
                <ArrowRightLeft className="size-3.5" aria-hidden /> {t("jobs.card.moveToLabel")}
              </DropdownMenuLabel>
              <DropdownMenuRadioGroup value={card.stageId} onValueChange={(value) => onMove(String(value))}>
                {stages.map((stage) => (
                  <DropdownMenuRadioItem key={stage.id} value={stage.id}>
                    {stageLabel(stage, t)}
                  </DropdownMenuRadioItem>
                ))}
              </DropdownMenuRadioGroup>
            </DropdownMenuGroup>
          </DropdownMenuContent>
        </DropdownMenu>
      ) : null}
    </li>
  );
}

function Column({
  stage,
  cards,
  stages,
  canWrite,
  onAdd,
  onMove,
}: {
  stage: Stage;
  cards: WorkOrderCard[];
  stages: Stage[];
  canWrite: boolean;
  onAdd: () => void;
  onMove: (card: WorkOrderCard, stageId: string) => void;
}) {
  const { t } = useI18n();
  const { setNodeRef, isOver } = useDroppable({ id: stage.id });
  const label = stageLabel(stage, t);

  return (
    <section
      aria-label={label}
      className={cn(
        "bg-muted/50 flex w-[264px] shrink-0 flex-col rounded-xl border border-transparent",
        isOver && "border-primary/30 bg-primary/5",
      )}
    >
      <header className="flex items-center gap-2 px-3 pt-3 pb-2">
        <span
          className={cn(
            "size-2 rounded-full",
            stage.category === "done" ? "bg-success" : stage.category === "active" ? "bg-info" : "bg-muted-foreground/50",
          )}
          aria-hidden
        />
        <h2 className="truncate text-sm font-semibold">{label}</h2>
        <span className="text-muted-foreground text-xs tabular-nums">{cards.length}</span>
        {canWrite ? (
          <Button variant="ghost" size="icon-xs" className="ml-auto" onClick={onAdd} aria-label={t("jobs.board.addTo", { stage: label })}>
            <Plus />
          </Button>
        ) : null}
      </header>
      <SortableContext items={cards.map((card) => card.id)} strategy={verticalListSortingStrategy}>
        <ul ref={setNodeRef} className="flex min-h-24 flex-1 flex-col gap-2 px-2 pb-3">
          {cards.map((card) => (
            <Card key={card.id} card={card} stages={stages} canWrite={canWrite} onMove={(stageId) => onMove(card, stageId)} />
          ))}
          {cards.length === 0 ? (
            <li className="text-muted-foreground rounded-lg border border-dashed px-3 py-6 text-center text-xs">{t("jobs.board.emptyColumn")}</li>
          ) : null}
        </ul>
      </SortableContext>
    </section>
  );
}

/**
 * The work order board. Cards drag between and within stages (mouse, touch or keyboard: space to
 * pick up, arrows to move); every card also has a "move to" menu, so dragging is never the only way.
 */
export function WorkOrderBoard({ filter, canWrite, onAdd }: BoardProps) {
  const { t, tError } = useI18n();
  const board = useBoard(filter);
  const move = useMoveWorkOrder();
  const [activeId, setActiveId] = useState<string | null>(null);
  // Local order while dragging and until the next fetch; tied to the data it started from.
  const [draft, setDraft] = useState<{ basedOn: number; columns: Columns } | null>(null);

  const sensors = useSensors(
    useSensor(PointerSensor, { activationConstraint: { distance: 6 } }),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  );

  const cards = useMemo(() => new Map((board.data?.workOrders ?? []).map((card) => [card.id, card])), [board.data]);
  const serverColumns = useMemo(() => {
    const columns: Columns = {};
    for (const stage of board.data?.stages ?? []) columns[stage.id] = [];
    for (const card of board.data?.workOrders ?? []) columns[card.stageId]?.push(card.id);
    return columns;
  }, [board.data]);
  const columns = draft && draft.basedOn === board.dataUpdatedAt ? draft.columns : serverColumns;

  const containerOf = (id: string, within: Columns) => (id in within ? id : Object.keys(within).find((stageId) => within[stageId]!.includes(id)));

  const commitMove = (cardId: string, stageId: string, index: number) => {
    move.mutate(
      { id: cardId, stageId, index },
      { onError: (error) => toast.error(tError(error instanceof ApiError ? error.code : "common.network")) },
    );
  };

  const onDragStart = ({ active }: DragStartEvent) => {
    setActiveId(String(active.id));
    setDraft({ basedOn: board.dataUpdatedAt, columns: structuredClone(columns) });
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
    const stageId = containerOf(String(over.id), draft.columns);
    if (!stageId) return;

    let column = draft.columns[stageId]!;
    const oldIndex = column.indexOf(id);
    const newIndex = column.indexOf(String(over.id));
    if (oldIndex >= 0 && newIndex >= 0 && oldIndex !== newIndex) {
      column = arrayMove(column, oldIndex, newIndex);
      setDraft({ ...draft, columns: { ...draft.columns, [stageId]: column } });
    }

    const card = cards.get(id);
    const index = column.indexOf(id);
    const serverIndex = serverColumns[stageId]?.indexOf(id);
    if (card && (card.stageId !== stageId || serverIndex !== index)) commitMove(id, stageId, index);
  };

  if (board.isPending) {
    return (
      <div className="flex gap-4 overflow-hidden">
        {Array.from({ length: 4 }, (_, index) => (
          <Skeleton key={index} className="h-80 w-[264px] shrink-0 rounded-xl" />
        ))}
      </div>
    );
  }

  if (board.isError) {
    const error = board.error;
    return (
      <Alert variant="destructive">
        <AlertCircle />
        <AlertDescription>
          {error instanceof ApiError && error.status === 403 ? t("jobs.noPermission") : tError(error instanceof ApiError ? error.code : "common.network")}
        </AlertDescription>
      </Alert>
    );
  }

  const stages = board.data.stages;
  const activeCard = activeId ? cards.get(activeId) : undefined;

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
      accessibility={{
        screenReaderInstructions: { draggable: t("jobs.board.dragInstructions") },
      }}
    >
      <div className="-mx-4 flex gap-4 overflow-x-auto px-4 pb-4 md:-mx-8 md:px-8">
        {stages.map((stage) => (
          <Column
            key={stage.id}
            stage={stage}
            stages={stages}
            cards={(columns[stage.id] ?? []).map((id) => cards.get(id)).filter((card): card is WorkOrderCard => !!card)}
            canWrite={canWrite}
            onAdd={() => onAdd(stage.id)}
            onMove={(card, stageId) => {
              if (stageId !== card.stageId) commitMove(card.id, stageId, serverColumns[stageId]?.length ?? 0);
            }}
          />
        ))}
      </div>
      <DragOverlay>
        {activeCard ? (
          <div className="bg-card w-[248px] rotate-1 rounded-lg border p-3 shadow-lg">
            <WorkOrderCardBody card={activeCard} />
          </div>
        ) : null}
      </DragOverlay>
    </DndContext>
  );
}
