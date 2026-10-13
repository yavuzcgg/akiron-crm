"use client";

import { use } from "react";
import { PostDetail } from "@/features/content/post-detail";

export default function ContentPostRoute({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  return <PostDetail id={id} />;
}
