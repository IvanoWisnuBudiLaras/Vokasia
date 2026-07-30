import type { MetadataRoute } from "next";

export default function sitemap(): MetadataRoute.Sitemap {
  const appUrl = (process.env.NEXT_PUBLIC_APP_URL ?? "http://localhost:3000").replace(/\/$/, "");
  const now = new Date();
  return [
    { url: `${appUrl}/`, lastModified: now, changeFrequency: "weekly", priority: 1 },
    { url: `${appUrl}/verify`, lastModified: now, changeFrequency: "monthly", priority: 0.5 },
  ];
}
