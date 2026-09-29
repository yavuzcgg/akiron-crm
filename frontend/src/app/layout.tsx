import type { Metadata } from "next";
import type { ReactNode } from "react";
import { Geist, Geist_Mono } from "next/font/google";
import { Providers } from "@/components/providers";
import "./globals.css";

const geistSans = Geist({
  variable: "--font-sans",
  subsets: ["latin", "latin-ext"],
});

const geistMono = Geist_Mono({
  variable: "--font-geist-mono",
  subsets: ["latin", "latin-ext"],
});

export const metadata: Metadata = {
  title: { default: "Akiron CRM", template: "%s · Akiron CRM" },
  description: "Akiron CRM — müşteriden işe, işten tahsilata.",
};

export default function RootLayout({ children }: { children: ReactNode }) {
  return (
    // next-themes sets the theme class before hydration; the mismatch on <html> is expected.
    <html lang="tr" className={`${geistSans.variable} ${geistMono.variable} h-full antialiased`} suppressHydrationWarning>
      <body className="bg-background text-foreground min-h-full">
        <Providers>{children}</Providers>
      </body>
    </html>
  );
}
