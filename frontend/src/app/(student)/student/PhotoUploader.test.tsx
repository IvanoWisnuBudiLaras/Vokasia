import { describe, expect, test } from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import { PhotoUploader, type PendingPhoto } from "./PhotoUploader";

describe("PhotoUploader", () => {
  test("picker memakai ikon SVG konsisten, bukan emoji", () => {
    const html = renderToStaticMarkup(
      <PhotoUploader max={3} photos={[]} setPhotos={() => undefined} />
    );

    expect(html).toContain("<svg");
    expect(html).toContain("Tambah foto");
    expect(html).not.toContain("📷");
  });

  test("tombol hapus memiliki target sentuh minimum 44px", () => {
    const photos: PendingPhoto[] = [
      {
        localId: "photo-1",
        file: new File(["photo"], "photo.jpg", { type: "image/jpeg" }),
        previewUrl: "blob:photo-1",
        status: "uploaded",
        objectKey: "journal/photo-1.jpg",
      },
    ];

    const html = renderToStaticMarkup(
      <PhotoUploader max={3} photos={photos} setPhotos={() => undefined} />
    );

    expect(html).toContain("aria-label=\"Hapus foto ini\"");
    expect(html).toContain("min-h-[var(--tap-min)]");
    expect(html).toContain("min-w-[var(--tap-min)]");
    expect(html).not.toContain("✕");
  });
});
