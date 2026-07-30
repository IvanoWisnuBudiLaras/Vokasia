import type { NextConfig } from "next";

import { buildSecurityHeaders } from "./src/lib/securityHeaders";

const nextConfig: NextConfig = {
  output: "standalone", // dibutuhkan Dockerfile multi-stage (frontend/Dockerfile)
  poweredByHeader: false,
  async headers() {
    return [
      {
        source: "/(.*)",
        headers: buildSecurityHeaders(process.env.NODE_ENV === "production"),
      },
      {
        source: "/sw.js",
        headers: [
          {
            key: "Cache-Control",
            value: "no-cache, no-store, must-revalidate",
          },
          {
            key: "Service-Worker-Allowed",
            value: "/",
          },
        ],
      },
    ];
  },
};

export default nextConfig;
