import type { Metadata, Viewport } from "next";
import { OfflineBanner } from "@/components/ui";
import { ServiceWorkerRegistration } from "@/components/ServiceWorkerRegistration";
import "./globals.css";

export const metadata: Metadata = {
  title: "Vokasia",
  description: "Platform manajemen PKL SMK — jurnal, monitoring, penilaian, sertifikat.",
  applicationName: "Vokasia",
  manifest: "/manifest.webmanifest",
  icons: {
    icon: [
      { url: "/icon.svg", type: "image/svg+xml" },
      { url: "/icon-192.png", sizes: "192x192", type: "image/png" },
      { url: "/icon-512.png", sizes: "512x512", type: "image/png" },
    ],
    apple: [{ url: "/icon-192.png", sizes: "192x192", type: "image/png" }],
  },
};

export const viewport: Viewport = {
  colorScheme: "light",
  themeColor: "#197b9c",
  viewportFit: "cover",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html
      lang="id"
      className="h-full antialiased"
    >
      <body className="flex min-h-full flex-col">
        <ServiceWorkerRegistration />
        <OfflineBanner />
        {children}
      </body>
    </html>
  );
}
