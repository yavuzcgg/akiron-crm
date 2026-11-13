import type { MetadataRoute } from "next";

/** Lets "Add to home screen" show the Akiron mark and open the app without browser chrome. */
export default function manifest(): MetadataRoute.Manifest {
  return {
    name: "Akiron CRM",
    short_name: "Akiron",
    start_url: "/",
    display: "standalone",
    background_color: "#F8FAFC",
    theme_color: "#4F46E5",
    icons: [
      { src: "/icon-192.png", sizes: "192x192", type: "image/png" },
      { src: "/icon-512.png", sizes: "512x512", type: "image/png" },
      { src: "/icon-maskable-512.png", sizes: "512x512", type: "image/png", purpose: "maskable" },
    ],
  };
}
