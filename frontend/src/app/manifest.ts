import type { MetadataRoute } from "next";

export default function manifest(): MetadataRoute.Manifest {
  return {
    name: "Vokasia — PKL SMK",
    short_name: "Vokasia",
    description: "Ruang kerja PKL untuk siswa, mentor, dan sekolah.",
    id: "/",
    start_url: "/login",
    scope: "/",
    display: "standalone",
    background_color: "#fffdf6",
    theme_color: "#197b9c",
    icons: [
      {
        src: "/icon.svg",
        sizes: "any",
        type: "image/svg+xml",
        purpose: "any",
      },
      {
        src: "/icon-192.png",
        sizes: "192x192",
        type: "image/png",
        purpose: "any",
      },
      {
        src: "/icon-512.png",
        sizes: "512x512",
        type: "image/png",
        purpose: "any",
      },
      {
        src: "/icon-maskable-512.png",
        sizes: "512x512",
        type: "image/png",
        purpose: "maskable",
      },
    ],
  };
}
