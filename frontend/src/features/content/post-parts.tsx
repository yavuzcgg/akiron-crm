"use client";

import { useQuery } from "@tanstack/react-query";
import { AlertCircle, FileText, ImageOff } from "lucide-react";
import { useState } from "react";
import { toast } from "sonner";
import { FormField } from "@/components/form-field";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Textarea } from "@/components/ui/textarea";
import { selectClass } from "@/features/sales/catalog";
import { PartyPicker, type PickedParty } from "@/features/sales/party-picker";
import { api } from "@/lib/api/client";
import { ApiError, unwrap } from "@/lib/api/errors";
import { portalApi } from "@/lib/api/portal-client";
import { useI18n, type TranslationKey } from "@/lib/i18n";
import { cn } from "@/lib/utils";
import { formatLabels, formats, isImage, platformNames, platforms, shortTime, statusStyles, useContentMutations, type ContentPost } from "./content-api";

/** The same states read differently to the client: "with the client" is "waiting for you". */
const clientLabels: Record<string, TranslationKey> = {
  client_review: "portal.content.status.waiting",
  changes_requested: "portal.content.status.changes",
};

export function ContentStatusBadge({ status, audience = "staff" }: { status: string; audience?: "staff" | "client" }) {
  const { t } = useI18n();
  const style = statusStyles[status] ?? statusStyles.draft!;
  return <Badge variant={style.badge}>{t((audience === "client" ? clientLabels[status] : undefined) ?? style.label)}</Badge>;
}

export function PlatformChips({ codes }: { codes: readonly string[] }) {
  if (codes.length === 0) return null;
  return (
    <span className="flex flex-wrap gap-1">
      {codes.map((code) => (
        <span key={code} className="bg-muted text-muted-foreground rounded-md px-1.5 py-0.5 text-[11px] font-medium">
          {platformNames[code] ?? code}
        </span>
      ))}
    </span>
  );
}

/**
 * A post's media as the audience will see them: pictures as pictures, anything else as a named
 * file. Staff and clients read through their own endpoints (`audience`).
 */
export function PostMedia({ postId, audience, className }: { postId: string; audience: "staff" | "client"; className?: string }) {
  const { t } = useI18n();
  const files = useQuery({
    queryKey: audience === "staff" ? ["files", "content_post", postId] : ["portal", "files", "content_post", postId],
    queryFn: async () =>
      audience === "staff"
        ? unwrap(await api.GET("/api/v1/files", { params: { query: { subjectType: "content_post", subjectId: postId } } }))
        : unwrap(await portalApi.GET("/api/v1/files/portal", { params: { query: { subjectType: "content_post", subjectId: postId } } })),
  });
  const href = (id: string) => (audience === "staff" ? `/api/v1/files/${id}/content` : `/api/v1/files/portal/${id}/content`);

  if (files.isPending) return <Skeleton className={cn("aspect-square w-full rounded-lg", className)} />;
  const items = files.data ?? [];
  if (items.length === 0) {
    return (
      <div className={cn("bg-muted/60 text-muted-foreground flex aspect-[4/3] w-full flex-col items-center justify-center gap-2 rounded-lg text-sm", className)}>
        <ImageOff className="size-5" aria-hidden />
        {t("content.media.empty")}
      </div>
    );
  }

  const pictures = items.filter((file) => isImage(file.contentType));
  const others = items.filter((file) => !isImage(file.contentType));
  return (
    <div className={cn("grid gap-2", className)}>
      {pictures.length > 0 ? (
        <div className={cn("grid gap-2", pictures.length > 1 && "grid-cols-2")}>
          {pictures.map((file) => (
            <a key={file.id} href={href(file.id)} className="bg-muted block overflow-hidden rounded-lg" aria-label={file.fileName}>
              {/* Served by the API from object storage: not a build-time asset next/image could optimise. */}
              {/* eslint-disable-next-line @next/next/no-img-element */}
              <img src={href(file.id)} alt={file.fileName} loading="lazy" className="aspect-square w-full object-cover" />
            </a>
          ))}
        </div>
      ) : null}
      {others.map((file) => (
        <a key={file.id} href={href(file.id)} className="hover:bg-muted/60 flex items-center gap-2 rounded-lg border px-3 py-2 text-sm">
          <FileText className="text-muted-foreground size-4 shrink-0" aria-hidden />
          <span className="truncate">{file.fileName}</span>
        </a>
      ))}
    </div>
  );
}

/** The text as it will be posted: caption, then hashtags. */
export function PostText({ caption, hashtags }: { caption?: string | null; hashtags?: string | null }) {
  const { t } = useI18n();
  if (!caption && !hashtags) return <p className="text-muted-foreground text-sm">{t("content.noCaption")}</p>;
  return (
    <div className="grid gap-2 text-sm">
      {caption ? <p className="whitespace-pre-wrap">{caption}</p> : null}
      {hashtags ? <p className="text-primary break-words">{hashtags}</p> : null}
    </div>
  );
}

interface PostDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  post?: ContentPost;
  /** For a new post: the day clicked on the calendar and the client the calendar is filtered to. */
  date?: string;
  party?: PickedParty | null;
  onSaved?: (post: ContentPost) => void;
}

/** Plan or edit a post: for whom, what it is called, when and where it goes out, and its text. */
export function PostDialog({ open, onOpenChange, post, date, party: presetParty, onSaved }: PostDialogProps) {
  const { t, tError } = useI18n();
  const { save } = useContentMutations();
  const [party, setParty] = useState<PickedParty | null>(post ? { id: post.partyId, name: post.partyName } : (presetParty ?? null));
  const [title, setTitle] = useState(post?.title ?? "");
  const [plannedDate, setPlannedDate] = useState(post?.plannedDate ?? date ?? "");
  const [plannedTime, setPlannedTime] = useState(shortTime(post?.plannedTime));
  const [format, setFormat] = useState(post?.format ?? "post");
  const [selected, setSelected] = useState<string[]>(post ? [...post.platforms] : ["instagram"]);
  const [caption, setCaption] = useState(post?.caption ?? "");
  const [hashtags, setHashtags] = useState(post?.hashtags ?? "");
  const [error, setError] = useState<string | null>(null);

  const submit = async () => {
    if (!party) {
      setError(t("sales.quote.clientRequired"));
      return;
    }
    if (!title.trim() || !plannedDate) {
      setError(t("content.required"));
      return;
    }
    setError(null);
    try {
      const saved = await save.mutateAsync({
        id: post?.id,
        partyId: party.id,
        title: title.trim(),
        plannedDate,
        plannedTime: plannedTime ? `${plannedTime}:00` : null,
        format,
        platforms: selected,
        caption: caption.trim() || null,
        hashtags: hashtags.trim() || null,
      });
      toast.success(t(post ? "content.saved" : "content.created"));
      onSaved?.(saved);
      onOpenChange(false);
    } catch (failure) {
      setError(tError(failure instanceof ApiError ? failure.code : "common.network"));
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-h-[90dvh] overflow-y-auto sm:max-w-xl" closeLabel={t("common.cancel")}>
        <DialogHeader>
          <DialogTitle>{t(post ? "content.edit" : "content.new")}</DialogTitle>
          <DialogDescription>{t("content.dialogHint")}</DialogDescription>
        </DialogHeader>
        <div className="grid gap-4">
          {error ? (
            <Alert variant="destructive">
              <AlertCircle />
              <AlertDescription>{error}</AlertDescription>
            </Alert>
          ) : null}
          {post ? null : <PartyPicker id="post-party" value={party} onChange={setParty} />}
          <FormField id="post-title" label={t("content.field.title")} placeholder={t("content.field.titlePlaceholder")} value={title} onChange={(event) => setTitle(event.target.value)} maxLength={200} />
          <div className="grid gap-3 sm:grid-cols-3">
            <FormField id="post-date" type="date" label={t("content.field.date")} value={plannedDate} onChange={(event) => setPlannedDate(event.target.value)} />
            <FormField id="post-time" type="time" label={t("content.field.time")} value={plannedTime} onChange={(event) => setPlannedTime(event.target.value)} />
            <div className="grid gap-1.5">
              <Label htmlFor="post-format" className="text-[13px] font-medium">
                {t("content.field.format")}
              </Label>
              <select id="post-format" className={selectClass} value={format} onChange={(event) => setFormat(event.target.value)}>
                {formats.map((option) => (
                  <option key={option} value={option}>
                    {t(formatLabels[option]!)}
                  </option>
                ))}
              </select>
            </div>
          </div>
          <fieldset className="grid gap-1.5">
            <legend className="mb-1.5 text-[13px] font-medium">{t("content.field.platforms")}</legend>
            <div className="flex flex-wrap gap-2">
              {platforms.map((code) => {
                const on = selected.includes(code);
                return (
                  <label
                    key={code}
                    className={cn(
                      "flex cursor-pointer items-center gap-2 rounded-lg border px-3 py-1.5 text-sm transition-colors",
                      on ? "border-primary bg-primary-soft text-primary-strong font-medium" : "border-input hover:bg-muted/60",
                    )}
                  >
                    <input type="checkbox" className="accent-primary size-4" checked={on} onChange={(event) => setSelected(event.target.checked ? [...selected, code] : selected.filter((item) => item !== code))} />
                    {platformNames[code]}
                  </label>
                );
              })}
            </div>
          </fieldset>
          <div className="grid gap-1.5">
            <Label htmlFor="post-caption" className="text-[13px] font-medium">
              {t("content.field.caption")}
            </Label>
            <Textarea id="post-caption" rows={5} maxLength={5000} value={caption} onChange={(event) => setCaption(event.target.value)} placeholder={t("content.field.captionPlaceholder")} />
            <p className="text-muted-foreground text-right text-xs tabular-nums">{caption.length} / 5000</p>
          </div>
          <FormField id="post-hashtags" label={t("content.field.hashtags")} placeholder="#kahve #yenisezon" value={hashtags} onChange={(event) => setHashtags(event.target.value)} maxLength={1000} />
          {post ? null : <p className="text-muted-foreground text-[13px]">{t("content.mediaHint")}</p>}
          <div className="flex justify-end gap-2">
            <Button variant="outline" onClick={() => onOpenChange(false)}>
              {t("common.cancel")}
            </Button>
            <Button onClick={() => void submit()} disabled={save.isPending}>
              {save.isPending ? t("common.saving") : t("common.save")}
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
