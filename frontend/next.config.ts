import type { NextConfig } from "next";

const nextConfig: NextConfig = {
  output: "standalone", // dibutuhkan Dockerfile multi-stage (frontend/Dockerfile)
};

export default nextConfig;
